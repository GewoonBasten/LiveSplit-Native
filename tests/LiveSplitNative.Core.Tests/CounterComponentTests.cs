using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>LiveSplit.Counter: the Counter, the component's counter state and its hotkeys.</summary>
public class CounterComponentTests
{
    [Fact]
    public void Counts_By_The_Increment_And_Stops_At_The_Int_Limits()
    {
        var counter = new CounterValue(3, 2);
        Assert.True(counter.Increment());
        Assert.True(counter.Increment());
        Assert.Equal(7, counter.Count);
        Assert.True(counter.Decrement());
        Assert.Equal(5, counter.Count);
        counter.Reset();
        Assert.Equal(3, counter.Count);

        var high = new CounterValue(int.MaxValue - 1, 5);
        Assert.False(high.Increment());
        Assert.Equal(int.MaxValue, high.Count);
        Assert.False(high.Increment());

        var low = new CounterValue(int.MinValue + 1, 5);
        Assert.False(low.Decrement());
        Assert.Equal(int.MinValue, low.Count);
    }

    [Fact]
    public void The_Count_Stays_With_The_Component_Until_The_Initial_Value_Changes()
    {
        var component = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.CounterPath);
        var settings = CounterComponentSettings.FromInnerXml(component.SettingsInnerXml);
        CounterValue counter = CounterLogic.For(component, settings);
        counter.Increment();
        counter.Increment();

        // The Layout Editor works on clones: they count on the same counter.
        LayoutComponent clone = component.Clone();
        Assert.Same(counter, CounterLogic.For(clone, settings));

        // A new Increment Value applies to the running count (IncrementUpdateRequired).
        settings.Increment = 10;
        Assert.Same(counter, CounterLogic.For(component, settings));
        counter.Increment();
        Assert.Equal(12, counter.Count);

        // A new Initial Value starts a new counter (CounterReinitialiseRequired).
        settings.InitialValue = 100;
        CounterValue restarted = CounterLogic.For(component, settings);
        Assert.NotSame(counter, restarted);
        Assert.Equal(100, restarted.Count);
    }

    [Fact]
    public void Hotkeys_Count_Every_Counter_They_Belong_To()
    {
        Layout layout = StandardLayoutFactory.Create();
        LayoutComponent first = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.CounterPath);
        var custom = new CounterComponentSettings { IncrementKey = "D1, Control", InitialValue = 5 };
        LayoutComponent second = new(StandardLayoutFactory.CounterPath, custom.ToInnerXml());
        layout.Components.Add(first);
        layout.Components.Add(second);
        int Count(LayoutComponent c) =>
            CounterLogic.For(c, CounterComponentSettings.FromInnerXml(c.SettingsInnerXml)).Count;

        Assert.True(CounterLogic.HandleHotkey(layout, "Add"));
        Assert.True(CounterLogic.HandleHotkey(layout, "Add"));
        Assert.True(CounterLogic.HandleHotkey(layout, "Ctrl+D1"));
        Assert.Equal((2, 6), (Count(first), Count(second)));

        // Subtract and NumPad0 are both counters' keys.
        Assert.True(CounterLogic.HandleHotkey(layout, "Subtract"));
        Assert.Equal((1, 5), (Count(first), Count(second)));
        Assert.True(CounterLogic.HandleHotkey(layout, "NumPad0"));
        Assert.Equal((0, 5), (Count(first), Count(second)));

        // A key with other modifiers is another key.
        Assert.False(CounterLogic.HandleHotkey(layout, "Shift+Add"));
        Assert.False(CounterLogic.HandleHotkey(layout, "D1"));
        Assert.False(CounterLogic.HandleHotkey(layout, ""));
    }

    [Theory]
    [InlineData("Add", "Add")]
    [InlineData("Ctrl+D1", "D1, Control")]
    [InlineData("Ctrl+Alt+Shift+F5", "F5, Shift, Control, Alt")]
    [InlineData("Shift+NumPad0", "NumPad0, Shift")]
    public void Chords_Are_Written_As_Windows_Keys(string chord, string windowsKeys)
    {
        Assert.Equal(windowsKeys, WindowsKeys.FromChord(chord));
        Assert.Equal(chord, WindowsKeys.ToChord(windowsKeys));
        Assert.True(WindowsKeys.Matches(chord, windowsKeys));
    }

    [Fact]
    public void Command_Has_No_Windows_Key_And_None_Matches_Nothing()
    {
        Assert.Null(WindowsKeys.FromChord("Meta+A"));
        Assert.Equal("", WindowsKeys.ToChord(""));
        Assert.Equal("", WindowsKeys.ToChord(null));
        Assert.False(WindowsKeys.Matches("A", ""));
        // Windows parses key names without case.
        Assert.True(WindowsKeys.Matches("OemPlus", "Oemplus"));
    }
}
