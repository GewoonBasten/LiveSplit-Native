using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// The run's Start Timer value (Run.Offset), as Windows LiveSplit uses it: the Timer shows it
/// before an attempt (Timer.GetTime), the attempt continues from it (TimerModel.Start), a reset
/// shows it again, and the .lss keeps it.
/// </summary>
public class StartTimerOffsetTests
{
    private static readonly TimeSpan BastenOffset = new TimeSpan(0, 0, 5, 40, 440);

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Nine_Digit_Offset_In_Bastens_Splits_Loads_And_Saves()
    {
        // The file says 00:05:40.440000000 (9 fractional digits, which TimeSpan.Parse rejects).
        Run run = XmlRunSerializer.LoadFile(Fixture("sms-any-gcn-no-ace-9-full.lss"));
        Assert.Equal(BastenOffset, run.Offset);

        string path = Path.Combine(Path.GetTempPath(), "lsn-offset-" + Guid.NewGuid().ToString("N") + ".lss");
        try
        {
            XmlRunSerializer.SaveFile(run, path);
            Assert.Equal(BastenOffset, XmlRunSerializer.LoadFile(path).Offset);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Timer_Shows_The_Offset_Before_During_And_After_An_Attempt()
    {
        using var clock = ManualTimeSource.Install(TimeSpan.FromHours(2));
        var run = new Run { Offset = BastenOffset };
        run.AddSegment("A");
        run.AddSegment("B");
        var state = new LiveSplitState(run);
        var model = new TimerModel { CurrentState = state };

        // Before the attempt: the Start Timer value (CurrentTime itself is 0, as upstream).
        Assert.Equal(BastenOffset, TimerComponentLogic.DisplayedTime(state, TimingMethod.RealTime));
        Assert.Equal(TimeSpan.Zero, state.CurrentTime.RealTime);

        model.Start();
        Assert.Equal(BastenOffset, TimerComponentLogic.DisplayedTime(state, TimingMethod.RealTime));
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(BastenOffset + TimeSpan.FromSeconds(10), TimerComponentLogic.DisplayedTime(state, TimingMethod.RealTime));
        model.Split();
        Assert.Equal(BastenOffset + TimeSpan.FromSeconds(10), run[0].SplitTime.RealTime);

        model.Reset();
        Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);
        Assert.Equal(BastenOffset, TimerComponentLogic.DisplayedTime(state, TimingMethod.RealTime));

        // A second attempt starts from the offset again.
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(BastenOffset + TimeSpan.FromSeconds(1), TimerComponentLogic.DisplayedTime(state, TimingMethod.RealTime));
    }

    [Fact]
    public void A_Negative_Offset_Counts_Down_And_Game_Time_Falls_Back_To_Real_Time()
    {
        using var clock = ManualTimeSource.Install(TimeSpan.FromHours(2));
        var run = new Run { Offset = TimeSpan.FromSeconds(-5) };
        run.AddSegment("A");
        var state = new LiveSplitState(run);
        var model = new TimerModel { CurrentState = state };

        Assert.Equal(TimeSpan.FromSeconds(-5), TimerComponentLogic.DisplayedTime(state, TimingMethod.GameTime));
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(-3), TimerComponentLogic.DisplayedTime(state, TimingMethod.RealTime));
        // Game Time not initialized: CurrentTime.GameTime is null, so the Timer shows Real Time.
        Assert.Equal(TimeSpan.FromSeconds(-3), TimerComponentLogic.DisplayedTime(state, TimingMethod.GameTime));
    }

    [Fact]
    public void Timing_Method_Setting_Overrides_The_Current_One()
    {
        Assert.Equal(TimingMethod.RealTime, TimerComponentLogic.ResolveTimingMethod("Real Time", TimingMethod.GameTime));
        Assert.Equal(TimingMethod.GameTime, TimerComponentLogic.ResolveTimingMethod("Game Time", TimingMethod.RealTime));
        Assert.Equal(TimingMethod.GameTime, TimerComponentLogic.ResolveTimingMethod("Current Timing Method", TimingMethod.GameTime));
    }
}
