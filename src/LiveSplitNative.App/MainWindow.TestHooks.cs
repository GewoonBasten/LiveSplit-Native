using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>What the app tests (tests/LiveSplitNative.App.Tests) read from and do with the timer window.</summary>
public partial class MainWindow
{
    private int _diagPressCount;
    private int _diagMenusShown;

    /// <summary>How many times the context menu was shown, for tests.</summary>
    internal int ContextMenusShown => _diagMenusShown;

    /// <summary>How many pointer presses reached the timer window, for tests.</summary>
    internal int PointerPresses => _diagPressCount;

    /// <summary>ComponentRenderer.MinimumWidth / MinimumHeight now, for the size-rule tests.</summary>
    internal (double Width, double Height) ComponentsMinimumSize => TimerSurface.MinimumContentSize();

    /// <summary>ComponentRenderer.OverallSize now, for the size-rule tests.</summary>
    internal double OverallSize => _naturalContentSize;

    /// <summary>Opens Edit Layout as the context menu does, for the live-preview tests.</summary>
    internal Task EditLayoutForTestsAsync() => EditLayoutAsync();

    /// <summary>"Name=Value" of each information component and Counter on the timer, for the app tests.</summary>
    internal IReadOnlyList<string> TimerInfoTextsForTests => TimerSurface.InfoTexts();

    /// <summary>The Contextual Slideshow's shown component as "Name=Value", for the app tests.</summary>
    internal string? SlideshowShownForTests => TimerSurface.SlideshowShown();

    /// <summary>Takes a layout as Open Layout does (SetLayout), for the tests.</summary>
    internal void ApplyLayoutForTests(Layout layout) => SetLayout(layout, null);

    /// <summary>One frame's value update (the refresh timer's work), for the app tests.</summary>
    internal void RefreshTimerForTests() => TimerSurface.UpdateValues();

    /// <summary>Manual Game Time's "Enter Game Time" window while it is open, for the app tests.</summary>
    internal ManualGameTimeWindow? ManualGameTimeWindowForTests => _manualGameTime.InputWindow;

    /// <summary>The Timer component's digits as shown (big + small part), for the timer tests.</summary>
    internal string TimerText =>
        TimerSurface.TimerDigits is { } digits ? digits.BigText + digits.SmallText : "";

    /// <summary>Whether the Timer component's digits are drawn with Show Gradient, for the timer tests.</summary>
    internal bool TimerDigitsGradient => TimerSurface.TimerDigits?.DigitGradient is not null;

    /// <summary>The scale the timer is drawn at, for the size-rule tests.</summary>
    internal double DrawScale => TimerSurface.Snapshot?.Scale ?? 0;

    /// <summary>Where the timer drew each component (tests sample the drawn pixels there).</summary>
    internal TimerLayoutSnapshot? LayoutSnapshotForTests => TimerSurface.Snapshot;
}
