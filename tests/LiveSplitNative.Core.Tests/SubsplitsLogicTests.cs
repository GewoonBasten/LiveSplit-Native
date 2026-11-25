using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// LiveSplit.Subsplits SplitsComponent.Update / SectionList and SplitComponent.UpdateAll
/// (93a11b85). The expected rows are worked out by hand from the Windows code for each case.
/// </summary>
public class SubsplitsLogicTests
{
    // Sections (SectionList): [0-2] Bianco Hills, [3-4] Ricco Harbor, [5], [6-7] Sirena Beach, [8].
    private static readonly string[] Names =
    [
        "-Windmill",
        "-Hillside Cave",
        "{Bianco Hills}Shadow Mario",
        "-Red Dawgs",
        "{Ricco Harbor}Shadow Mario",
        "Pianta Village",
        "-Gooper Blooper",
        "{Sirena Beach}Shadow Mario",
        "Corona Mountain",
    ];

    private const int Min = int.MinValue;

    private static Run SmsRun()
    {
        var run = new Run();
        foreach (string name in Names)
        {
            run.AddSegment(name);
        }

        // Personal Best: one minute per split.
        for (int i = 0; i < run.Count; i++)
        {
            run[i].PersonalBestSplitTime = new SplitTime(TimeSpan.FromMinutes(i + 1));
        }

        return run;
    }

    /// <summary>The split each row shows as Windows' visibleSplits list: a header is -(section + 1), a blank int.MinValue.</summary>
    private static int[] Visible(SubsplitsFrame frame) =>
        frame.Rows.Select(r => r.Kind switch
        {
            SubsplitsRowKind.Blank => Min,
            SubsplitsRowKind.Header => -(new SubsplitsSectionList(Names.Select(n => new Segment(n)).ToList()).GetSection(r.SplitIndex) + 1),
            _ => r.SplitIndex,
        }).ToArray();

    [Fact]
    public void SectionList_Groups_The_Subsplits_Before_A_Split_With_It()
    {
        var sections = new SubsplitsSectionList(Names.Select(n => new Segment(n)).ToList());
        Assert.Equal(
            [new(0, 2), new(3, 4), new(5, 5), new(6, 7), new(8, 8)],
            sections.Sections.ToArray());
        Assert.Equal(1, sections.GetSection(3));
        Assert.Equal(-1, sections.GetSection(9));
        Assert.False(sections.IsMajorSplit(1));
        Assert.True(sections.IsMajorSplit(2));
        Assert.Equal(4, sections.GetMajorSplit(3));
        Assert.Equal([0, 0, 0, 1, 1, 2, 3, 3, 4], SubsplitsLogic.WindowsSections(Names.Select(n => new Segment(n)).ToList()));
    }

    [Fact]
    public void Not_Running_Shows_The_First_Section_With_Its_Header_And_Collapses_The_Others()
    {
        // CurrentSplitIndex -1 is clamped to 0, so section 0 is the current one.
        var state = new LiveSplitState(SmsRun());
        SubsplitsFrame frame = new SubsplitsLogic().Update(state, new SubsplitsComponentSettings());

        Assert.Equal([-1, 0, 1, 2, 4, 5, 7, 8], Visible(frame));
        Assert.Equal(
            [false, false, false, false, true, false, true, false],
            frame.Rows.Select(r => r.CollapsedSplit).ToArray());
        Assert.Equal(0, frame.Rows[0].TopSplit);
        Assert.Equal(3, frame.Rows[4].TopSplit);
        // Indent Last Split Of Section: the current section's last split.
        Assert.Equal([3], frame.Rows.Select((r, i) => (r, i)).Where(x => x.r.ForceIndent).Select(x => x.i).ToArray());
        // Alternating by section: sections 0, 2, 4 take Background Color.
        Assert.Equal(
            [true, true, true, true, false, true, false, true],
            frame.Rows.Select(r => r.OddSplit).ToArray());
        // The split before the last one is right above it: no thick separator.
        Assert.Equal(0f, frame.LastSeparatorSize);
        Assert.False(frame.LastSeparatorUsesSeparatorColor);
        Assert.Equal(-1, frame.HighlightSplit);

        IReadOnlyList<SubsplitsRowView> views = Views(state, frame, new SubsplitsComponentSettings());
        Assert.Equal(
            ["Bianco Hills", "Windmill", "Hillside Cave", "Shadow Mario", "Ricco Harbor", "Pianta Village", "Sirena Beach", "Corona Mountain"],
            views.Select(v => v.Name).ToArray());
        Assert.Equal(
            [false, true, true, true, false, false, false, false],
            views.Select(v => v.Indent).ToArray());
        Assert.DoesNotContain(views, v => v.IsActive);
    }

    [Fact]
    public void Running_In_A_Section_Expands_It_Under_Its_Header()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, _) = RunTo(clock, splits: 3, extra: TimeSpan.FromSeconds(30));
        var settings = new SubsplitsComponentSettings();
        SubsplitsFrame frame = new SubsplitsLogic().Update(state, settings);

        // Header of section 1 after the end of section 0; one blank row; the last split.
        Assert.Equal([2, -2, 3, 4, 5, 7, Min, 8], Visible(frame));
        Assert.Equal(
            [true, false, false, false, false, true, false, false],
            frame.Rows.Select(r => r.CollapsedSplit).ToArray());
        Assert.True(frame.Rows[3].ForceIndent);
        Assert.Equal(0f, frame.LastSeparatorSize); // blank rows above the last split

        IReadOnlyList<SubsplitsRowView> views = Views(state, frame, settings);
        Assert.Equal("Bianco Hills", views[0].Name);
        Assert.Equal("Ricco Harbor", views[1].Name);
        Assert.Equal(SubsplitsRowKind.Header, views[1].Kind);
        Assert.Equal("Red Dawgs", views[2].Name);
        Assert.True(views[2].IsActive);
        Assert.Equal("Shadow Mario", views[3].Name);
        Assert.Equal("Sirena Beach", views[5].Name);

        // Header: the section has run for 0:30 (split 2 at 3:30, now 4:00) against 2:00 in the
        // PB: not behind, so the PB's section length shows, and the section timer runs.
        Assert.Equal("2:00.0", views[1].HeaderTime);
        Assert.Equal("0:30.0", views[1].SectionTimer);
        Assert.True(views[1].SectionTimerGradient);

        // Collapsed Bianco Hills: split at 3:30 against 3:00 in the PB (Delta and Time columns).
        Assert.Equal("+30.0", views[0].Columns[0].Text);
        Assert.Equal("3:30", views[0].Columns[1].Text);
    }

    [Fact]
    public void Header_Shows_The_Section_Delta_Once_Behind()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, _) = RunTo(clock, splits: 3, extra: TimeSpan.FromSeconds(150));
        var settings = new SubsplitsComponentSettings();
        SubsplitsFrame frame = new SubsplitsLogic().Update(state, settings);
        SubsplitsRowView header = Views(state, frame, settings)[1];
        // 2:30 in the section (3:30 to 6:00) against 2:00: +30.0, behind and losing.
        Assert.Equal("+30.0", header.HeaderTime);
        Assert.Equal(LayoutSettings.CreateStandard().BehindLosingTimeColor, header.HeaderTimeColor);
        Assert.Equal("2:30.0", header.SectionTimer);
    }

    [Fact]
    public void Scrolling_Shows_Another_Section_Highlights_It_And_Keeps_The_Running_Section_Active()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, _) = RunTo(clock, splits: 3, extra: TimeSpan.FromSeconds(30));
        var settings = new SubsplitsComponentSettings();
        var logic = new SubsplitsLogic();
        logic.ScrollDown();
        logic.ScrollDown();
        logic.ScrollDown();
        SubsplitsFrame frame = logic.Update(state, settings);

        Assert.Equal([2, 4, 5, -4, 6, 7, Min, 8], Visible(frame));
        Assert.Equal(6, frame.HighlightSplit);
        Assert.Equal(4, frame.SectionSplit);
        IReadOnlyList<SubsplitsRowView> views = Views(state, frame, settings);
        Assert.Equal("Ricco Harbor", views[1].Name); // collapsed, the running section
        Assert.True(views[1].IsActive);
        Assert.True(views[4].IsHighlight);
        Assert.Equal("Gooper Blooper", views[4].Name);

        // The offset is clamped to the run: far past the end it stops at the last split.
        for (int i = 0; i < 20; i++)
        {
            logic.ScrollDown();
        }

        logic.Update(state, settings);
        Assert.Equal(5, logic.ScrollOffset);
        logic.ResetScroll();
        Assert.Equal(0, logic.ScrollOffset);
    }

    [Fact]
    public void Always_Hide_Subsplits_Collapses_Every_Section_And_Activates_The_Running_One()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, _) = RunTo(clock, splits: 3, extra: TimeSpan.FromSeconds(30));
        var settings = new SubsplitsComponentSettings { Visibility = SubsplitsComponentSettings.SubsplitVisibility.AlwaysHide };
        SubsplitsFrame frame = new SubsplitsLogic().Update(state, settings);

        Assert.Equal([2, -2, 4, 5, 7, Min, Min, 8], Visible(frame));
        Assert.Equal(4, frame.SectionSplit);
        Assert.DoesNotContain(frame.Rows, r => r.ForceIndent);
        IReadOnlyList<SubsplitsRowView> views = Views(state, frame, settings);
        Assert.True(views[2].IsActive);
        Assert.Equal("Ricco Harbor", views[2].Name);
        Assert.True(frame.Rows[2].CollapsedSplit);
    }

    [Fact]
    public void Always_Show_Subsplits_Lists_Every_Split_With_The_Thick_Separator()
    {
        var state = new LiveSplitState(SmsRun());
        var settings = new SubsplitsComponentSettings { Visibility = SubsplitsComponentSettings.SubsplitVisibility.AlwaysShow };
        SubsplitsFrame frame = new SubsplitsLogic().Update(state, settings);

        Assert.Equal([-1, 0, 1, 2, 3, 4, 5, 8], Visible(frame));
        Assert.DoesNotContain(frame.Rows, r => r.CollapsedSplit || r.ForceIndent);
        Assert.Equal(2f, frame.LastSeparatorSize);
        Assert.True(frame.LastSeparatorUsesSeparatorColor);
    }

    [Fact]
    public void Minimum_Sections_Before_Current_And_Current_Section_Only()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, _) = RunTo(clock, splits: 6, extra: TimeSpan.FromSeconds(10));

        // Current split 6 (section 3). Two rows only for earlier sections: the preview and the
        // rows below keep MinimumMajorSplits free, the rows above take the major splits.
        var settings = new SubsplitsComponentSettings { VisualSplitCount = 5, MinimumMajorSplits = 2 };
        Assert.Equal([4, 5, -4, 6, 8], Visible(new SubsplitsLogic().Update(state, settings)));

        settings = new SubsplitsComponentSettings { OnlyCurrentSection = true };
        Assert.Equal([-4, 6, 7, Min, Min, Min, Min, 8], Visible(new SubsplitsLogic().Update(state, settings)));
    }

    [Fact]
    public void Names_Lose_The_Dash_And_Show_The_Section_Name_When_It_Stands_For_The_Section()
    {
        Assert.Equal("Windmill", SubsplitsRowLogic.DisplayName("-Windmill", isSubsplit: true, sectionName: false));
        Assert.Equal("Shadow Mario", SubsplitsRowLogic.DisplayName("{Bianco Hills} Shadow Mario", false, false));
        Assert.Equal("Bianco Hills", SubsplitsRowLogic.DisplayName("{Bianco Hills} Shadow Mario", false, true));
        Assert.Equal("Plain", SubsplitsRowLogic.DisplayName("Plain", false, true));
        // The run's last split is never a subsplit: its dash stays.
        var state = new LiveSplitState(new Run());
        state.Run.AddSegment("A");
        state.Run.AddSegment("-B");
        SubsplitsFrame frame = new SubsplitsLogic().Update(state, new SubsplitsComponentSettings { VisualSplitCount = 2 });
        Assert.Equal("-B", Views(state, frame, new SubsplitsComponentSettings())[^1].Name);
    }

    [Fact]
    public void CreateBuiltin_Subsplits_Has_Windows_Defaults()
    {
        LayoutComponent c = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.SubsplitsPath);
        Assert.Equal("Subsplits", c.DisplayName);
        SubsplitsComponentSettings settings = SubsplitsComponentSettings.FromInnerXml(c.SettingsInnerXml);
        Assert.Equal(SubsplitsComponentSettings.SubsplitVisibility.Contextual, settings.Visibility);
        Assert.True(settings.IndentSubsplits);
        Assert.Equal(8, settings.VisualSplitCount);
        Assert.False(settings.ShowThinSeparators);
        Assert.Equal(ArgbColor.FromArgb(0x2B, 0xFF, 0xFF, 0xFF), settings.HeaderTopColor);
        Assert.Equal(ArgbColor.FromArgb(0xD8, 0x00, 0x00, 0x00), settings.HeaderBottomColor);
        Assert.Equal(ArgbColor.FromArgb(255, 0x77, 0x77, 0x77), settings.SectionTimerColor);
        Assert.Contains(
            StandardLayoutFactory.BuiltinCatalog,
            info => info.Path == StandardLayoutFactory.SubsplitsPath
                && info.Category == StandardLayoutFactory.ComponentCategory.List
                && info.NativeRendered);
    }

    [Fact]
    public void SubsplitsSettings_RoundTrip_And_Windows_Elements()
    {
        var settings = new SubsplitsComponentSettings
        {
            Visibility = SubsplitsComponentSettings.SubsplitVisibility.AlwaysShow,
            IndentSubsplits = false,
            OnlyCurrentSection = true,
            VisualSplitCount = 9,
        };
        string xml = settings.ToInnerXml();
        Assert.StartsWith("<Version>1.7</Version>", xml);
        Assert.Contains("<HideSubsplits>False</HideSubsplits><ShowSubsplits>True</ShowSubsplits><CurrentSectionOnly>True</CurrentSectionOnly>", xml);
        Assert.DoesNotContain("SubsplitVisibility", xml);
        Assert.DoesNotContain("OnlyCurrentSection", xml);
        Assert.DoesNotContain("ShowSectionHeader", xml);
        SubsplitsComponentSettings again = SubsplitsComponentSettings.FromInnerXml(xml);
        Assert.Equal(SubsplitsComponentSettings.SubsplitVisibility.AlwaysShow, again.Visibility);
        Assert.False(again.IndentSubsplits);
        Assert.True(again.OnlyCurrentSection);
        Assert.Equal(9, again.VisualSplitCount);
    }

    [Fact]
    public void Windows_Layout_HideSubsplits_And_ShowSubsplits_Are_Read()
    {
        // These used to be ignored (a missing SubsplitVisibility read as Contextual).
        Assert.Equal(
            SubsplitsComponentSettings.SubsplitVisibility.AlwaysShow,
            SubsplitsComponentSettings.FromInnerXml("<Version>1.7</Version><HideSubsplits>False</HideSubsplits><ShowSubsplits>True</ShowSubsplits><Columns />").Visibility);
        Assert.Equal(
            SubsplitsComponentSettings.SubsplitVisibility.AlwaysHide,
            SubsplitsComponentSettings.FromInnerXml("<Version>1.7</Version><HideSubsplits>True</HideSubsplits><ShowSubsplits>False</ShowSubsplits><Columns />").Visibility);
        // Earlier builds of this app wrote SubsplitVisibility only.
        Assert.Equal(
            SubsplitsComponentSettings.SubsplitVisibility.AlwaysHide,
            SubsplitsComponentSettings.FromInnerXml("<Version>1.8</Version><SubsplitVisibility>AlwaysHide</SubsplitVisibility>").Visibility);
        // Before 1.7: Comparison is the header comparison; no ShowSplitTimes gives one +/- column.
        SubsplitsComponentSettings old = SubsplitsComponentSettings.FromInnerXml("<Version>1.6</Version><Comparison>Best Segments</Comparison>");
        Assert.Equal("Best Segments", old.HeaderComparison);
        Assert.Single(old.Columns);
        Assert.Equal(ColumnType.DeltaorSplitTime, old.Columns[0].Type);
    }

    /// <summary>Starts a run, splits <paramref name="splits"/> times, each 0:10 behind the PB, then waits <paramref name="extra"/>.</summary>
    private static (LiveSplitState State, TimerModel Model) RunTo(ManualTimeSourceScope clock, int splits, TimeSpan extra)
    {
        var state = new LiveSplitState(SmsRun());
        var model = new TimerModel { CurrentState = state };
        model.Start();
        for (int i = 0; i < splits; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(70));
            model.Split();
        }

        clock.Advance(extra);
        return (state, model);
    }

    private static IReadOnlyList<SubsplitsRowView> Views(LiveSplitState state, SubsplitsFrame frame, SubsplitsComponentSettings settings)
    {
        IReadOnlyList<ColumnData> columns = ColumnData.CreateDefault();
        return SubsplitsRowLogic.Views(
            state,
            frame,
            settings,
            LayoutSettings.CreateStandard(),
            columns,
            SplitColumnLogic.TimeFormatter(settings.SplitTimesAccuracy),
            SplitColumnLogic.DeltaFormatter(settings.DeltasAccuracy, settings.DropDecimals));
    }
}
