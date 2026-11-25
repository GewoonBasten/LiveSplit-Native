using System.Reflection;
using System.Runtime.InteropServices;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App.Platform;

/// <summary>What an auto splitter asks of the timer (asr-capi's CTimer callbacks).</summary>
internal interface IAsrTimer
{
    /// <summary>0 not running, 1 running, 2 paused, 3 ended.</summary>
    int State { get; }

    int CurrentSplitIndex { get; }

    /// <summary>1 when the split at the index (before the current one) has a time, 0 when skipped, -1 otherwise.</summary>
    int SegmentSplitted(int index);

    void Start();

    void Split();

    void SkipSplit();

    void UndoSplit();

    void Reset();

    void SetGameTime(TimeSpan time);

    void PauseGameTime();

    void ResumeGameTime();

    void SetCustomVariable(string name, string value);

    void Log(string message);
}

/// <summary>One of an auto splitter's settings widgets (Widgets_get_*).</summary>
internal sealed record AsrWidget(
    string Kind,
    string Key,
    string Description,
    string? Tooltip,
    int HeadingLevel,
    bool Bool,
    IReadOnlyList<(string Key, string Description)> Choices,
    int ChoiceIndex,
    string FileFilter);

/// <summary>
/// Port of LiveSplit.AutoSplittingRuntime's ASR.cs over asr-capi (libasr_capi.dylib beside the
/// app): a WebAssembly auto splitter compiled and run by livesplit-core's runtime, its tick rate,
/// its settings map and its settings widgets.
/// </summary>
internal sealed class AsrRuntime : IDisposable
{
    private const string Library = "asr_capi";
    private static bool? _available;

    // The callbacks are kept alive as long as the runtime (the native side holds their pointers).
    private readonly Callbacks _callbacks;
    private nint _ptr;

    private AsrRuntime(nint ptr, Callbacks callbacks)
    {
        _ptr = ptr;
        _callbacks = callbacks;
    }

    /// <summary>Whether libasr_capi could be loaded (ASRLoader.LoadASR).</summary>
    public static bool IsAvailable
    {
        get
        {
            if (_available is { } known)
            {
                return known;
            }

            try
            {
                _available = NativeLibrary.TryLoad(Library, Assembly.GetExecutingAssembly(), null, out _);
            }
            catch (Exception)
            {
                _available = false;
            }

            return _available.Value;
        }
    }

    /// <summary>Runtime(path, settingsMap, …): null when the module cannot be loaded (the reason is logged).</summary>
    public static AsrRuntime? Load(string path, IReadOnlyList<KeyValuePair<string, AsrSettingValue>>? settings, IAsrTimer timer)
    {
        if (!IsAvailable)
        {
            timer.Log("The Auto Splitting Runtime library (libasr_capi) is missing.");
            return null;
        }

        var callbacks = new Callbacks(timer);
        nint map = settings is null ? 0 : ToNativeMap(settings);
        nint ptr = Runtime_new(
            path,
            map,
            callbacks.State,
            callbacks.Index,
            callbacks.SegmentSplitted,
            callbacks.Start,
            callbacks.Split,
            callbacks.SkipSplit,
            callbacks.UndoSplit,
            callbacks.Reset,
            callbacks.SetGameTime,
            callbacks.PauseGameTime,
            callbacks.ResumeGameTime,
            callbacks.SetCustomVariable,
            callbacks.Log);
        return ptr == 0 ? null : new AsrRuntime(ptr, callbacks);
    }

    /// <summary>Runs the auto splitter once; false when it failed (logged).</summary>
    public bool Step() => _ptr != 0 && Runtime_step(_ptr) != 0;

    /// <summary>How often the auto splitter wants to run.</summary>
    public TimeSpan TickRate => _ptr == 0 ? TimeSpan.Zero : new TimeSpan((long)Runtime_tick_rate(_ptr));

    /// <summary>The settings map as it is now.</summary>
    public IReadOnlyList<KeyValuePair<string, AsrSettingValue>> GetSettings()
    {
        if (_ptr == 0)
        {
            return [];
        }

        nint map = Runtime_get_settings_map(_ptr);
        try
        {
            return FromNativeMap(map);
        }
        finally
        {
            SettingsMap_drop(map);
        }
    }

    public void SetSettings(IReadOnlyList<KeyValuePair<string, AsrSettingValue>> settings)
    {
        if (_ptr != 0)
        {
            Runtime_set_settings_map(_ptr, ToNativeMap(settings));
        }
    }

    public void SetBool(string key, bool value)
    {
        if (_ptr != 0)
        {
            Runtime_settings_map_set_bool(_ptr, key, value ? (byte)1 : (byte)0);
        }
    }

    public void SetString(string key, string value)
    {
        if (_ptr != 0)
        {
            Runtime_settings_map_set_string(_ptr, key, value);
        }
    }

    /// <summary>The settings widgets with their current values (Runtime_get_settings_widgets).</summary>
    public IReadOnlyList<AsrWidget> GetWidgets()
    {
        if (_ptr == 0)
        {
            return [];
        }

        nint widgets = Runtime_get_settings_widgets(_ptr);
        nint map = Runtime_get_settings_map(_ptr);
        try
        {
            var list = new List<AsrWidget>();
            ulong length = (ulong)Widgets_len(widgets);
            for (nuint i = 0; i < (nuint)length; i++)
            {
                string kind = (ulong)Widgets_get_type(widgets, i) switch
                {
                    1 => "bool",
                    2 => "title",
                    3 => "choice",
                    4 => "file-select",
                    _ => "",
                };
                var choices = new List<(string, string)>();
                int choiceIndex = 0;
                if (kind == "choice")
                {
                    ulong count = (ulong)Widgets_get_choice_options_len(widgets, i);
                    for (nuint c = 0; c < (nuint)count; c++)
                    {
                        string key = Str(Widgets_get_choice_option_key(widgets, i, c));
                        choices.Add((key, Str(Widgets_get_choice_option_description(widgets, i, c))));
                    }

                    choiceIndex = (int)Widgets_get_choice_current_index(widgets, i, map);
                }

                list.Add(new AsrWidget(
                    kind,
                    Str(Widgets_get_key(widgets, i)),
                    Str(Widgets_get_description(widgets, i)),
                    Widgets_get_tooltip(widgets, i) is var tip and not 0 ? Str(tip) : null,
                    kind == "title" ? (int)Widgets_get_heading_level(widgets, i) : 0,
                    kind == "bool" && Widgets_get_bool(widgets, i, map) != 0,
                    choices,
                    choiceIndex,
                    kind == "file-select" ? Str(Widgets_get_file_select_filter(widgets, i)) : ""));
            }

            return list;
        }
        finally
        {
            SettingsMap_drop(map);
            Widgets_drop(widgets);
        }
    }

    /// <summary>path_to_wasi: a path as the auto splitter sees it (/mnt/…).</summary>
    public static string PathToWasi(string path) => IsAvailable ? Str(path_to_wasi(path)) : path;

    /// <summary>wasi_to_path: an auto splitter's path as a path on this computer.</summary>
    public static string WasiToPath(string path) => IsAvailable ? Str(wasi_to_path(path)) : path;

    public void Dispose()
    {
        if (_ptr != 0)
        {
            Runtime_drop(_ptr);
            _ptr = 0;
        }

        GC.KeepAlive(_callbacks);
    }

    private static string Str(nint ptr) => ptr == 0 ? "" : Marshal.PtrToStringUTF8(ptr) ?? "";

    private static string Str(nint ptr, nuint length) =>
        ptr == 0 || length > int.MaxValue ? "" : Marshal.PtrToStringUTF8(ptr, (int)length) ?? "";

    private static nint ToNativeMap(IEnumerable<KeyValuePair<string, AsrSettingValue>> settings)
    {
        nint map = SettingsMap_new();
        foreach ((string key, AsrSettingValue value) in settings)
        {
            SettingsMap_insert(map, key, ToNativeValue(value));
        }

        return map;
    }

    private static nint ToNativeValue(AsrSettingValue value) => value.Kind switch
    {
        AsrValueKind.Map => SettingValue_new_map(ToNativeMap(value.Map)),
        AsrValueKind.List => SettingValue_new_list(ToNativeList(value.List)),
        AsrValueKind.Bool => SettingValue_new_bool(value.Bool ? (byte)1 : (byte)0),
        AsrValueKind.I64 => SettingValue_new_i64(value.I64),
        AsrValueKind.F64 => SettingValue_new_f64(value.F64),
        _ => SettingValue_new_string(value.String),
    };

    private static nint ToNativeList(IEnumerable<AsrSettingValue> values)
    {
        nint list = SettingsList_new();
        foreach (AsrSettingValue value in values)
        {
            SettingsList_push(list, ToNativeValue(value));
        }

        return list;
    }

    private static List<KeyValuePair<string, AsrSettingValue>> FromNativeMap(nint map)
    {
        var values = new List<KeyValuePair<string, AsrSettingValue>>();
        ulong length = (ulong)SettingsMap_len(map);
        for (nuint i = 0; i < (nuint)length; i++)
        {
            string key = Str(SettingsMap_get_key(map, i));
            if (FromNativeValue(SettingsMap_get_value(map, i)) is { } value)
            {
                values.Add(new KeyValuePair<string, AsrSettingValue>(key, value));
            }
        }

        return values;
    }

    private static AsrSettingValue? FromNativeValue(nint value)
    {
        switch ((ulong)SettingValue_get_type(value))
        {
            case 1:
                return AsrSettingValue.FromMap(FromNativeMap(SettingValue_get_map(value)));
            case 2:
            {
                nint list = SettingValue_get_list(value);
                var items = new List<AsrSettingValue>();
                ulong length = (ulong)SettingsList_len(list);
                for (nuint i = 0; i < (nuint)length; i++)
                {
                    if (FromNativeValue(SettingsList_get(list, i)) is { } item)
                    {
                        items.Add(item);
                    }
                }

                return AsrSettingValue.FromList(items);
            }

            case 3:
                return AsrSettingValue.FromBool(SettingValue_get_bool(value) != 0);
            case 4:
                return AsrSettingValue.FromI64(SettingValue_get_i64(value));
            case 5:
                return AsrSettingValue.FromF64(SettingValue_get_f64(value));
            case 6:
                return AsrSettingValue.FromString(Str(SettingValue_get_string(value)));
            default:
                return null;
        }
    }

    /// <summary>The CTimer callbacks as delegates the native side can call.</summary>
    private sealed class Callbacks
    {
        public Callbacks(IAsrTimer timer)
        {
            State = () => Guard(() => timer.State);
            Index = () => Guard(() => timer.CurrentSplitIndex);
            SegmentSplitted = index => Guard(() => timer.SegmentSplitted(index));
            Start = () => Guard(timer.Start);
            Split = () => Guard(timer.Split);
            SkipSplit = () => Guard(timer.SkipSplit);
            UndoSplit = () => Guard(timer.UndoSplit);
            Reset = () => Guard(timer.Reset);
            SetGameTime = ticks => Guard(() => timer.SetGameTime(new TimeSpan(ticks)));
            PauseGameTime = () => Guard(timer.PauseGameTime);
            ResumeGameTime = () => Guard(timer.ResumeGameTime);
            SetCustomVariable = (name, nameLength, value, valueLength) =>
                Guard(() => timer.SetCustomVariable(Str(name, nameLength), Str(value, valueLength)));
            Log = (message, length) => Guard(() => timer.Log(Str(message, length)));
        }

        public IntDelegate State { get; }

        public IntDelegate Index { get; }

        public SegmentSplittedDelegate SegmentSplitted { get; }

        public VoidDelegate Start { get; }

        public VoidDelegate Split { get; }

        public VoidDelegate SkipSplit { get; }

        public VoidDelegate UndoSplit { get; }

        public VoidDelegate Reset { get; }

        public SetGameTimeDelegate SetGameTime { get; }

        public VoidDelegate PauseGameTime { get; }

        public VoidDelegate ResumeGameTime { get; }

        public SetCustomVariableDelegate SetCustomVariable { get; }

        public LogDelegate Log { get; }

        // An exception must never cross into the native runtime.
        private static int Guard(Func<int> call)
        {
            try
            {
                return call();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static void Guard(Action call)
        {
            try
            {
                call();
            }
            catch (Exception)
            {
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int IntDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SegmentSplittedDelegate(int index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VoidDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetGameTimeDelegate(long ticks);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetCustomVariableDelegate(nint name, nuint nameLength, nint value, nuint valueLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LogDelegate(nint message, nuint length);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Runtime_new(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        nint settingsMap,
        IntDelegate state,
        IntDelegate index,
        SegmentSplittedDelegate segmentSplitted,
        VoidDelegate start,
        VoidDelegate split,
        VoidDelegate skipSplit,
        VoidDelegate undoSplit,
        VoidDelegate reset,
        SetGameTimeDelegate setGameTime,
        VoidDelegate pauseGameTime,
        VoidDelegate resumeGameTime,
        SetCustomVariableDelegate setCustomVariable,
        LogDelegate log);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Runtime_drop(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte Runtime_step(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong Runtime_tick_rate(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Runtime_get_settings_widgets(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Runtime_settings_map_set_bool(nint self, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, byte value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Runtime_settings_map_set_string(
        nint self, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Runtime_get_settings_map(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Runtime_set_settings_map(nint self, nint settingsMap);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingsMap_new();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SettingsMap_drop(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SettingsMap_insert(nint self, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, nint value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint SettingsMap_len(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingsMap_get_key(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingsMap_get_value(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingsList_new();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SettingsList_push(nint self, nint value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint SettingsList_len(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingsList_get(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingValue_new_map(nint value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingValue_new_list(nint value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingValue_new_bool(byte value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingValue_new_i64(long value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingValue_new_f64(double value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingValue_new_string([MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint SettingValue_get_type(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingValue_get_map(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingValue_get_list(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte SettingValue_get_bool(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern long SettingValue_get_i64(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern double SettingValue_get_f64(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SettingValue_get_string(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Widgets_drop(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint Widgets_len(nint self);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Widgets_get_key(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Widgets_get_description(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Widgets_get_tooltip(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint Widgets_get_heading_level(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint Widgets_get_type(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte Widgets_get_bool(nint self, nuint index, nint settingsMap);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint Widgets_get_choice_current_index(nint self, nuint index, nint settingsMap);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint Widgets_get_choice_options_len(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Widgets_get_choice_option_key(nint self, nuint index, nuint optionIndex);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Widgets_get_choice_option_description(nint self, nuint index, nuint optionIndex);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Widgets_get_file_select_filter(nint self, nuint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint path_to_wasi([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint wasi_to_path([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
}
