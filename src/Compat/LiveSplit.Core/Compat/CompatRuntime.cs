// The bridge between the app and Windows LiveSplit components loaded unchanged: the app sees
// components through LiveSplitNative.ComponentHost.Compat's interfaces; this side turns those
// calls into LiveSplit.Core's (IComponentFactory, IComponent, LiveSplitState, Graphics, the
// settings Control).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplitNative.ComponentHost.Compat;
using Native = LiveSplitNative.Core;

namespace LiveSplitNative.Compat
{
    /// <summary>What settings pages and components reach of the app (dialogs).</summary>
    public static class CompatServices
    {
        public static IHostDialogs Dialogs { get; set; }
    }

    /// <summary>The ICompatRuntime the app finds in this assembly.</summary>
    public sealed class CompatRuntime : ICompatRuntime
    {
        public IReadOnlyList<IHostedComponentFactory> GetFactories(Assembly componentAssembly)
        {
            // ComponentManager.LoadFactory: the assembly's ComponentFactoryAttribute names the factory.
            var factories = new List<IHostedComponentFactory>();
            foreach (ComponentFactoryAttribute attribute in componentAssembly.GetCustomAttributes<ComponentFactoryAttribute>())
            {
                if (Activator.CreateInstance(attribute.ComponentFactoryClassType) is IComponentFactory factory)
                {
                    factories.Add(new HostedFactory(System.IO.Path.GetFileName(componentAssembly.Location), factory));
                }
            }

            return factories;
        }

        public IHostedTimer CreateTimer(IHostTimerSource source)
        {
            CompatServices.Dialogs ??= source.Dialogs;
            return new HostedTimer(source);
        }
    }

    internal sealed class HostedFactory : IHostedComponentFactory
    {
        private readonly IComponentFactory _factory;

        public HostedFactory(string fileName, IComponentFactory factory)
        {
            FileName = fileName;
            _factory = factory;
        }

        public string FileName { get; }

        public string ComponentName => _factory.ComponentName;

        public string Description => _factory.Description;

        public int Category => (int)_factory.Category;

        public IHostedComponent Create(IHostedTimer timer)
        {
            var hosted = (HostedTimer)timer;
            return new HostedComponent(_factory.Create(hosted.State), hosted, FileName);
        }
    }

    /// <summary>LiveSplitState over the app's timer; the run is mirrored when it changes.</summary>
    internal sealed class HostedTimer : IHostedTimer
    {
        private readonly IHostTimerSource _source;
        private Native.Model.TimerModel _model;
        private object _runKey;

        public HostedTimer(IHostTimerSource source)
        {
            _source = source;
            State = new LiveSplitState(() => _source.State, new Run(), new LayoutSettings(() => _source.LayoutSettings));
            State.NativeModel = () => _source.Model;
            State.Settings = new HostSettings(() => _source.Settings);
            State.Layout = new HostedLayout(source, State);
            Subscribe(source.Model);
            Refresh();
        }

        public LiveSplitState State { get; }

        public void Invalidate() => _source.Invalidate();

        public LiveSplitNative.Core.UI.LayoutMode LayoutMode => _source.LayoutMode;

        public void Refresh()
        {
            InfoTextComponent.RowHeight = 25 + (_source.LayoutSettings?.ComponentSpacing ?? Native.UI.LayoutSettings.DefaultComponentSpacing);
            if (!ReferenceEquals(_model, _source.Model))
            {
                Subscribe(_source.Model);
            }

            Native.Model.Run run = _source.State.Run;
            object key = RunMirror.Key(run);
            if (!Equals(key, _runKey))
            {
                _runKey = key;
                State.Run = RunMirror.Create(run);
            }
        }

        public void Dispose() => Subscribe(null);

        private void Subscribe(Native.Model.TimerModel model)
        {
            if (_model is not null)
            {
                _model.OnSplit -= OnSplit;
                _model.OnUndoSplit -= OnUndoSplit;
                _model.OnSkipSplit -= OnSkipSplit;
                _model.OnStart -= OnStart;
                _model.OnReset -= OnReset;
                _model.OnPause -= OnPause;
                _model.OnResume -= OnResume;
                _model.OnUndoAllPauses -= OnUndoAllPauses;
                _model.OnScrollUp -= OnScrollUp;
                _model.OnScrollDown -= OnScrollDown;
                _model.OnSwitchComparisonPrevious -= OnSwitchPrevious;
                _model.OnSwitchComparisonNext -= OnSwitchNext;
            }

            _model = model;
            if (model is not null)
            {
                model.OnSplit += OnSplit;
                model.OnUndoSplit += OnUndoSplit;
                model.OnSkipSplit += OnSkipSplit;
                model.OnStart += OnStart;
                model.OnReset += OnReset;
                model.OnPause += OnPause;
                model.OnResume += OnResume;
                model.OnUndoAllPauses += OnUndoAllPauses;
                model.OnScrollUp += OnScrollUp;
                model.OnScrollDown += OnScrollDown;
                model.OnSwitchComparisonPrevious += OnSwitchPrevious;
                model.OnSwitchComparisonNext += OnSwitchNext;
            }
        }

        // The run changes with every timer event; it is mirrored before the component hears of it.
        private void Before()
        {
            _runKey = null;
            Refresh();
        }

        private void OnSplit(object sender, EventArgs e)
        {
            Before();
            State.RaiseSplit();
        }

        private void OnUndoSplit(object sender, EventArgs e)
        {
            Before();
            State.RaiseUndoSplit();
        }

        private void OnSkipSplit(object sender, EventArgs e)
        {
            Before();
            State.RaiseSkipSplit();
        }

        private void OnStart(object sender, EventArgs e)
        {
            Before();
            State.RaiseStart();
        }

        private void OnReset(object sender, Native.Timing.TimerPhase phase)
        {
            Before();
            State.RaiseReset((TimerPhase)(int)phase);
        }

        private void OnPause(object sender, EventArgs e) => State.RaisePause();

        private void OnResume(object sender, EventArgs e) => State.RaiseResume();

        private void OnUndoAllPauses(object sender, EventArgs e) => State.RaiseUndoAllPauses();

        private void OnScrollUp(object sender, EventArgs e) => State.RaiseScrollUp();

        private void OnScrollDown(object sender, EventArgs e) => State.RaiseScrollDown();

        private void OnSwitchPrevious(object sender, EventArgs e) => State.RaiseSwitchComparisonPrevious();

        private void OnSwitchNext(object sender, EventArgs e) => State.RaiseSwitchComparisonNext();
    }

    /// <summary>The app's run as LiveSplit.Model.Run.</summary>
    internal static class RunMirror
    {
        /// <summary>What a mirror depends on: a new key means the run changed.</summary>
        public static object Key(Native.Model.Run run)
        {
            var hash = new HashCode();
            hash.Add(run);
            hash.Add(run.Count);
            hash.Add(run.AttemptCount);
            hash.Add(run.AttemptHistory.Count);
            hash.Add(run.GameName);
            hash.Add(run.CategoryName);
            hash.Add(run.Offset);
            foreach (Native.Model.Segment segment in run.Segments)
            {
                hash.Add(segment.Name);
                hash.Add(segment.SegmentHistory.Count);
                hash.Add(segment.SplitTime.RealTime);
                hash.Add(segment.SplitTime.GameTime);
                hash.Add(segment.PersonalBestSplitTime.RealTime);
                hash.Add(segment.PersonalBestSplitTime.GameTime);
                hash.Add(segment.BestSegmentTime.RealTime);
                hash.Add(segment.BestSegmentTime.GameTime);
            }

            return hash.ToHashCode();
        }

        public static Run Create(Native.Model.Run run)
        {
            var mirror = new Run
            {
                GameName = run.GameName,
                CategoryName = run.CategoryName,
                Offset = run.Offset,
                AttemptCount = run.AttemptCount,
                HasChanged = run.HasChanged,
                LayoutPath = run.LayoutPath,
                AttemptHistory = run.AttemptHistory
                    .Select(a => new Attempt(
                        a.Index,
                        LiveSplitState.Convert(a.Time),
                        a.Started is { } started ? LiveSplitState.Convert(started) : null,
                        a.Ended is { } ended ? LiveSplitState.Convert(ended) : null,
                        a.PauseTime))
                    .ToList(),
                CustomComparisons = [.. run.CustomComparisons],
                GeneratedComparisons = [.. run.ComparisonGenerators.Select(g => g.Name)],
            };
            mirror.Source = run;
            foreach ((string name, string value) in run.Metadata.PermanentCustomVariables)
            {
                mirror.Metadata.CustomVariables[name] = value;
            }

            mirror.Metadata.RunID = run.Metadata.RunId;
            mirror.Metadata.PlatformName = run.Metadata.PlatformName;
            mirror.Metadata.RegionName = run.Metadata.RegionName;
            mirror.Metadata.UsesEmulator = run.Metadata.UsesEmulator;
            foreach (KeyValuePair<string, string> pair in run.Metadata.VariableValueNames)
            {
                mirror.Metadata.VariableValueNames[pair.Key] = pair.Value;
            }

            foreach (Native.Model.Segment segment in run.Segments)
            {
                var copy = new Segment(segment.Name)
                {
                    SplitTime = LiveSplitState.Convert(segment.SplitTime),
                    BestSegmentTime = LiveSplitState.Convert(segment.BestSegmentTime),
                };
                foreach (KeyValuePair<string, Native.Model.SplitTime> comparison in segment.Comparisons)
                {
                    copy.Comparisons[comparison.Key] = LiveSplitState.Convert(comparison.Value);
                }

                foreach (KeyValuePair<int, Native.Model.SplitTime> history in segment.SegmentHistory)
                {
                    copy.SegmentHistory[history.Key] = LiveSplitState.Convert(history.Value);
                }

                foreach (KeyValuePair<string, string> variable in segment.CustomVariableValues)
                {
                    copy.CustomVariableValues[variable.Key] = variable.Value;
                }

                mirror.Add(copy);
            }

            return mirror;
        }
    }

    /// <summary>
    /// LiveSplitState.Layout over the app's layout: its mode and settings, and as its components
    /// those of component DLLs and the app's Subsplits (mirrored as Windows' LiveSplit.Subsplits
    /// SplitsComponent, from compat/LiveSplit.Subsplits.dll), in the layout's order. The app's
    /// other components (Timer, Splits, …) are not IComponents and so are not among them.
    /// </summary>
    internal sealed class HostedLayout : ILayout
    {
        private const string SubsplitsPath = "LiveSplit.Subsplits.dll";
        private static Type _subsplitsType;
        private static bool _subsplitsLoaded;
        private readonly IHostTimerSource _source;
        private readonly LiveSplitState _state;
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IHostSubsplits, IComponent> _mirrors = new();

        public HostedLayout(IHostTimerSource source, LiveSplitState state)
        {
            _source = source;
            _state = state;
        }

        public LayoutMode Mode
        {
            get => (LayoutMode)(int)_source.LayoutMode;
            set
            {
                // The layout's mode is the app's (Layout Editor); a component does not change it.
            }
        }

        public int VerticalWidth { get; set; }

        public int VerticalHeight { get; set; }

        public int HorizontalWidth { get; set; }

        public int HorizontalHeight { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public bool HasChanged { get; set; }

        public string FilePath { get; set; }

        public LayoutSettings Settings
        {
            get => _state.LayoutSettings;
            set
            {
                // The layout's settings are the app's (Layout Settings).
            }
        }

        public IList<ILayoutComponent> LayoutComponents
        {
            get => [.. Entries().Select(e => (ILayoutComponent)new LayoutComponent(e.Path, e.Component))];
            set
            {
                // Which components the layout has is the app's (Layout Editor).
            }
        }

        public IEnumerable<IComponent> Components => Entries().Select(e => e.Component).ToList();

        public object Clone() => MemberwiseClone();

        private IEnumerable<(string Path, IComponent Component)> Entries()
        {
            foreach (HostLayoutEntry entry in _source.LayoutComponents)
            {
                if (entry.Component is HostedComponent hosted)
                {
                    yield return (hosted.Path, hosted.Component);
                }
                else if (entry.Subsplits is { } subsplits && Mirror(subsplits) is { } mirror)
                {
                    yield return (SubsplitsPath, mirror);
                }
            }
        }

        private IComponent Mirror(IHostSubsplits subsplits)
        {
            if (_mirrors.TryGetValue(subsplits, out IComponent mirror))
            {
                return mirror;
            }

            if (SubsplitsType() is not { } type)
            {
                return null;
            }

            mirror = (IComponent)Activator.CreateInstance(type, subsplits, _state);
            _mirrors.AddOrUpdate(subsplits, mirror);
            return mirror;
        }

        /// <summary>LiveSplit.Subsplits' SplitsComponent, from the look-alike next to this one.</summary>
        private static Type SubsplitsType()
        {
            if (!_subsplitsLoaded)
            {
                _subsplitsLoaded = true;
                try
                {
                    System.Runtime.Loader.AssemblyLoadContext context =
                        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(HostedLayout).Assembly)
                        ?? System.Runtime.Loader.AssemblyLoadContext.Default;
                    Assembly subsplits = context.LoadFromAssemblyName(new AssemblyName("LiveSplit.Subsplits"));
                    _subsplitsType = subsplits.GetType("LiveSplit.UI.Components.SplitsComponent", throwOnError: true);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }

            return _subsplitsType;
        }
    }

    /// <summary>An IComponent seen through IHostedComponent.</summary>
    internal sealed class HostedComponent : IHostedComponent, IInvalidator
    {
        private readonly IComponent _component;
        private readonly HostedTimer _timer;

        public HostedComponent(IComponent component, HostedTimer timer, string path)
        {
            _component = component;
            _timer = timer;
            Path = path;
        }

        /// <summary>The component itself (LiveSplitState.Layout.Components).</summary>
        public IComponent Component => _component;

        /// <summary>The DLL's file name, a layout component's Path.</summary>
        public string Path { get; }

        public string ComponentName => Safe(() => _component.ComponentName, "");

        public float HorizontalWidth => Safe(() => _component.HorizontalWidth, 0);

        public float MinimumHeight => Safe(() => _component.MinimumHeight, 0);

        public float VerticalHeight => Safe(() => _component.VerticalHeight, 0);

        public float MinimumWidth => Safe(() => _component.MinimumWidth, 0);

        public float PaddingTop => Safe(() => _component.PaddingTop, 0);

        public float PaddingBottom => Safe(() => _component.PaddingBottom, 0);

        public float PaddingLeft => Safe(() => _component.PaddingLeft, 0);

        public float PaddingRight => Safe(() => _component.PaddingRight, 0);

        public IReadOnlyDictionary<string, Action> ContextMenuControls =>
            Safe(() => _component.ContextMenuControls is { } controls ? new Dictionary<string, Action>(controls) : null, null);

        // IInvalidator: the component asks for a repaint.
        public System.Drawing.Drawing2D.Matrix Transform { get; set; } = new();

        public void Invalidate(float x, float y, float width, float height) => _timer.Invalidate();

        public void Update(float width, float height, Native.UI.LayoutMode mode)
        {
            _timer.Refresh();
            Run(() => _component.Update(this, _timer.State, width, height, (LayoutMode)(int)mode));
        }

        public void DrawVertical(IHostCanvas canvas, float width)
        {
            using Graphics g = Graphics.FromHostCanvas(canvas, new RectangleF(0, 0, width, VerticalHeight));
            g.TextRenderingHint = canvas.AntiAliasText ? System.Drawing.Text.TextRenderingHint.AntiAlias : System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Run(() => _component.DrawVertical(g, _timer.State, width, new Region(new RectangleF(0, 0, width, VerticalHeight))));
        }

        public void DrawHorizontal(IHostCanvas canvas, float height)
        {
            using Graphics g = Graphics.FromHostCanvas(canvas, new RectangleF(0, 0, HorizontalWidth, height));
            g.TextRenderingHint = canvas.AntiAliasText ? System.Drawing.Text.TextRenderingHint.AntiAlias : System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Run(() => _component.DrawHorizontal(g, _timer.State, height, new Region(new RectangleF(0, 0, HorizontalWidth, height))));
        }

        public string GetSettingsInnerXml()
        {
            var document = new XmlDocument();
            XmlNode node = Safe(() => _component.GetSettings(document), null);
            return node?.InnerXml ?? "";
        }

        public void SetSettingsInnerXml(string innerXml)
        {
            // Windows calls SetSettings only with a layout's saved settings, never for a new component.
            if (string.IsNullOrEmpty(innerXml))
            {
                return;
            }

            var document = new XmlDocument();
            document.LoadXml("<Settings>" + innerXml + "</Settings>");
            Run(() => _component.SetSettings(document.DocumentElement));
        }

        public IHostControl GetSettingsControl(Native.UI.LayoutMode mode) =>
            Safe(() => _component.GetSettingsControl((LayoutMode)(int)mode) as IHostControl, null);

        public void Dispose() => Run(_component.Dispose);

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private static T Safe<T>(Func<T> get, T fallback)
        {
            try
            {
                return get();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return fallback;
            }
        }
    }
}
