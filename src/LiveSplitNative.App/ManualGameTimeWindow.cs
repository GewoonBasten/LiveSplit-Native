using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// LiveSplit.ManualGameTime ShitSplitter: the small "Enter Game Time" window open during a run.
/// Enter takes the typed time(s) as game time and splits.
/// </summary>
internal sealed class ManualGameTimeWindow : Window
{
    private readonly TextBox _box;

    public ManualGameTimeWindow(Action<string> enter)
    {
        DialogChrome.ConfigureLightDialog(this);
        Title = "Enter Game Time";
        Width = 284;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        _box = new TextBox { Width = 260, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        _box.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Return)
            {
                enter(_box.Text ?? "");
                e.Handled = true;
            }
        };
        Content = new Border { Padding = new Thickness(12), Child = _box };
        Opened += (_, _) => _box.Focus();
    }

    /// <summary>Clears the box after a split (txtGameTime.Text = "").</summary>
    public void Clear() => _box.Text = "";

    /// <summary>For the app tests: what Enter does with this text.</summary>
    internal void EnterForTests(string text)
    {
        _box.Text = text;
        _box.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyDownEvent, Key = Key.Enter });
    }
}

/// <summary>
/// ManualGameTimeComponent's timer events, while the layout has a Manual Game Time component:
/// on start the window opens (where it was last) and game time is paused at 0; on reset it
/// closes; undo split puts the previous split's game time back.
/// </summary>
internal sealed class ManualGameTimeController(
    Window owner,
    LiveSplitState state,
    Func<Layout> layout,
    Action split)
{
    private ManualGameTimeWindow? _window;
    private PixelPoint? _previousLocation;

    internal ManualGameTimeWindow? InputWindow => _window;

    private ManualGameTimeComponentSettings? Settings =>
        layout().Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.ManualGameTimePath) is { } component
            ? ManualGameTimeComponentSettings.FromInnerXml(component.SettingsInnerXml)
            : null;

    public void OnStart()
    {
        if (Settings is null)
        {
            return;
        }

        _window?.Close();
        _window = new ManualGameTimeWindow(text =>
        {
            if (Settings is { } settings && ManualGameTimeLogic.Enter(state, settings, text))
            {
                split();
                _window?.Clear();
            }
        });
        _window.Show(owner);
        if (_previousLocation is { } location)
        {
            _window.Position = location;
        }

        ManualGameTimeLogic.Start(state);
    }

    public void OnReset()
    {
        if (_window is null)
        {
            return;
        }

        _previousLocation = _window.Position;
        _window.Close();
        _window = null;
    }

    public void OnUndoSplit()
    {
        if (Settings is not null)
        {
            ManualGameTimeLogic.UndoSplit(state);
        }
    }
}
