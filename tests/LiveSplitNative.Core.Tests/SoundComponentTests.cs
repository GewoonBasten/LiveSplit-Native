using System.Xml;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>LiveSplit.Sound (93a11b85): its settings and which sound each timer event plays.</summary>
public class SoundComponentTests
{
    [Fact]
    public void New_Settings_Are_Written_In_Windows_Order()
    {
        var root = ComponentSettingsXml.LoadRoot(new SoundComponentSettings().ToInnerXml());
        List<string> names = root.ChildNodes.OfType<XmlElement>().Select(e => e.Name).ToList();
        var expected = new List<string> { "Version", "OutputDevice", "GeneralVolume", "IsClearAddDragDrop" };
        foreach (SoundEventType type in SoundEventTypes.All)
        {
            expected.Add(type.ToString());
            expected.Add(type + "Volume");
        }

        Assert.Equal(expected, names);
        Assert.Equal("1.6", root["Version"]!.InnerText);
        Assert.Equal("0", root["OutputDevice"]!.InnerText);
        Assert.Equal("100", root["GeneralVolume"]!.InnerText);
        Assert.Equal("True", root["IsClearAddDragDrop"]!.InnerText);
        Assert.True(root["Split"]!.IsEmpty);
        Assert.Equal("100", root["StartTimerVolume"]!.InnerText);
    }

    [Fact]
    public void Settings_Round_Trip_With_Several_Files()
    {
        var settings = new SoundComponentSettings { OutputDevice = 2, GeneralVolume = 80, IsClearAddDragDrop = false };
        settings.Sounds[SoundEventType.BestSegment].FilePaths = ["/sounds/gold.wav", "/sounds/gold 2.mp3"];
        settings.Sounds[SoundEventType.BestSegment].Volume = 45;

        string xml = settings.ToInnerXml();
        Assert.Contains("<BestSegment><path>/sounds/gold.wav</path><path>/sounds/gold 2.mp3</path></BestSegment>", xml);
        SoundComponentSettings loaded = SoundComponentSettings.FromInnerXml(xml);

        Assert.Equal(2, loaded.OutputDevice);
        Assert.Equal(80, loaded.GeneralVolume);
        Assert.False(loaded.IsClearAddDragDrop);
        Assert.Equal(["/sounds/gold.wav", "/sounds/gold 2.mp3"], loaded.Sounds[SoundEventType.BestSegment].FilePaths);
        Assert.Equal(45, loaded.Sounds[SoundEventType.BestSegment].Volume);
        Assert.Empty(loaded.Sounds[SoundEventType.Split].FilePaths);
        Assert.Equal(100, loaded.Sounds[SoundEventType.Split].Volume);
    }

    [Fact]
    public void Old_Single_Path_Setting_Reads_And_A_Missing_Volume_Is_Zero()
    {
        SoundComponentSettings loaded = SoundComponentSettings.FromInnerXml(
            @"<Version>1.5</Version><Split>C:\sounds\split.wav</Split><GeneralVolume>70</GeneralVolume>");

        Assert.Equal([@"C:\sounds\split.wav"], loaded.Sounds[SoundEventType.Split].FilePaths);
        Assert.Equal(0, loaded.Sounds[SoundEventType.Split].Volume);
        Assert.Equal(70, loaded.GeneralVolume);
        Assert.True(loaded.IsClearAddDragDrop);
    }

    /// <summary>Three splits, PB 0:10 / 0:20 / 0:30, best segments 0:10 / 0:08 / 0:10, every situation with a file.</summary>
    private static (LiveSplitState State, SoundComponentSettings Settings) Run(params double[] splits)
    {
        var run = new Run();
        foreach ((string name, double pb, double best) in new[] { ("A", 10.0, 10.0), ("B", 20.0, 8.0), ("C", 30.0, 10.0) })
        {
            run.AddSegment(name);
            run[^1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(pb));
            run[^1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(best));
        }

        var state = new LiveSplitState(run) { CurrentPhase = TimerPhase.Running };
        for (int i = 0; i < splits.Length; i++)
        {
            run[i].SplitTime = new SplitTime(TimeSpan.FromSeconds(splits[i]));
        }

        state.CurrentSplitIndex = splits.Length;
        if (splits.Length == run.Count)
        {
            state.CurrentPhase = TimerPhase.Ended;
        }

        var settings = new SoundComponentSettings();
        foreach (SoundEventType type in SoundEventTypes.All)
        {
            settings.Sounds[type].FilePaths = [type + ".wav"];
        }

        return (state, settings);
    }

    [Theory]
    [InlineData(new[] { 11.0 }, SoundEventType.SplitBehindLosing)]
    [InlineData(new[] { 9.0 }, SoundEventType.BestSegment)]
    [InlineData(new[] { 8.0, 19.0 }, SoundEventType.SplitAheadLosing)]
    [InlineData(new[] { 12.0, 18.0 }, SoundEventType.BestSegment)]
    [InlineData(new[] { 12.0, 21.0 }, SoundEventType.SplitBehindGaining)]
    [InlineData(new[] { 10.5, 19.0 }, SoundEventType.SplitAheadGaining)]
    [InlineData(new[] { 10.0, 20.0, 29.0 }, SoundEventType.PersonalBest)]
    [InlineData(new[] { 10.0, 20.0, 31.0 }, SoundEventType.NotAPersonalBest)]
    public void Each_Split_Plays_Its_Situation(double[] splits, SoundEventType expected)
    {
        (LiveSplitState state, SoundComponentSettings settings) = Run(splits);
        Assert.Equal(expected, SoundLogic.SplitEvent(state, settings));
    }

    [Fact]
    public void A_Situation_Without_Files_Plays_The_Split_Sound()
    {
        (LiveSplitState state, SoundComponentSettings settings) = Run(11.0);
        settings.Sounds[SoundEventType.SplitBehindLosing].FilePaths = [];
        Assert.Equal(SoundEventType.Split, SoundLogic.SplitEvent(state, settings));

        // The last split has no fallback.
        (state, settings) = Run(10.0, 20.0, 29.0);
        settings.Sounds[SoundEventType.PersonalBest].FilePaths = [];
        Assert.Equal(SoundEventType.PersonalBest, SoundLogic.SplitEvent(state, settings));
    }

    [Fact]
    public void A_Run_Without_A_Personal_Best_Ends_In_A_Personal_Best()
    {
        (LiveSplitState state, SoundComponentSettings settings) = Run(10.0, 20.0, 31.0);
        state.Run[^1].PersonalBestSplitTime = default;
        Assert.Equal(SoundEventType.PersonalBest, SoundLogic.SplitEvent(state, settings));
    }

    private sealed class RecordingPlayer : ISoundPlayer
    {
        public List<string> Calls { get; } = [];

        public void Stop() => Calls.Add("stop");

        public void Play(string path, float volume, int outputDevice) =>
            Calls.Add(FormattableString.Invariant($"play {path} {volume:0.00} on {outputDevice}"));
    }

    [Fact]
    public void Play_Stops_First_Then_Plays_An_Existing_File_At_Both_Volumes()
    {
        var settings = new SoundComponentSettings { GeneralVolume = 80, OutputDevice = 1 };
        settings.Sounds[SoundEventType.Pause].FilePaths = ["missing.wav", "pause.wav"];
        settings.Sounds[SoundEventType.Pause].Volume = 50;
        var player = new RecordingPlayer();

        SoundLogic.Play(player, settings, SoundEventType.Pause, path => path == "pause.wav", new Random(1));
        SoundLogic.Play(player, settings, SoundEventType.Resume, _ => true, new Random(1));

        Assert.Equal(["stop", "play pause.wav 0.40 on 1", "stop"], player.Calls);
    }
}
