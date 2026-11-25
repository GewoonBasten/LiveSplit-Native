using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using LiveSplitNative.Core.Imaging;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Web;
using SkiaSharp;
using InlineCollection = Avalonia.Controls.Documents.InlineCollection;
using InlineRun = Avalonia.Controls.Documents.Run;
using TextElement = Avalonia.Controls.Documents.TextElement;

namespace LiveSplitNative.App;

/// <summary>
/// RunEditorDialog's Additional Info tab (MetadataControl, 93a11b85) and its speedrun.com tools:
/// the category's rules, the game's platforms and regions, the emulator and the variables (read
/// from speedrun.com when the tab is shown and when the game or category changes), Submit Run...,
/// Associate with Speedrun.com... / Show on Speedrun.com..., and the game icon's menu (Set Icon...,
/// Download Box Art, Download Icon, Open from URL..., Remove Icon).
/// </summary>
public partial class SplitsEditorWindow
{
    private const int MetadataTabIndex = 2;
    private static readonly Regex UrlPattern = new(@"(?:https?://|www\.)[^\s\]\)<>""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly List<Control> _variableFields = [];
    private readonly List<(int Start, int End, string Url)> _ruleLinks = [];
    private string? _runId;
    private string _platformName = "";
    private string _regionName = "";
    private bool _usesEmulator;
    private string _committedGame = "";
    private string _committedCategory = "";
    private int _metadataVersion;
    private bool _populatingMetadata;

    /// <summary>What speedrun.com said about the game and category last (null before the first look).</summary>
    internal SrcRunMetadata? LoadedMetadata { get; private set; }

    /// <summary>The last Additional Info refresh (tests wait for it).</summary>
    internal Task MetadataRefreshed { get; private set; } = Task.CompletedTask;

    /// <summary>The Speedrun.com submit window's answer for the tests (instead of showing it).</summary>
    internal Func<SpeedrunComSubmitWindow, Task<bool>>? SubmitWindowForTests { get; set; }

    /// <summary>The run's speedrun.com ID as the editor has it now (tests).</summary>
    internal string? RunIdForTests => CurrentRunId;

    internal string PlatformNameForTests => _platformName;

    internal string RegionNameForTests => _regionName;

    internal IReadOnlyDictionary<string, string> VariablesForTests => _variables;

    /// <summary>The variables' rows (label → its box), tests.</summary>
    internal IReadOnlyList<SettingsField> VariableFieldsForTests => [.. _variableFields.OfType<SettingsField>()];

    /// <summary>
    /// RunMetadata.RunID as it will be saved: a game or category typed since the last look also
    /// clears it, as the GameName / CategoryName setters do.
    /// </summary>
    private string? CurrentRunId =>
        (GameNameBox.Text?.Trim() ?? "") == _committedGame && (CategoryBox.Text?.Trim() ?? "") == _committedCategory
            ? _runId
            : null;

    private bool IsMetadataTab => EditorTabs.SelectedIndex == MetadataTabIndex;

    private void InitializeMetadata(Run run)
    {
        _runId = string.IsNullOrEmpty(run.Metadata.RunId) ? null : run.Metadata.RunId;
        _platformName = run.Metadata.PlatformName ?? "";
        _regionName = run.Metadata.RegionName ?? "";
        _usesEmulator = run.Metadata.UsesEmulator;
        _committedGame = run.GameName.Trim();
        _committedCategory = run.CategoryName.Trim();
    }

    private void SetUpMetadata()
    {
        PlatformBox.SelectionChanged += (_, _) =>
        {
            if (!_populatingMetadata && PlatformBox.SelectedItem is string platform && platform != _platformName)
            {
                _platformName = platform;
                ClearRunId();
            }
        };
        RegionBox.SelectionChanged += (_, _) =>
        {
            if (!_populatingMetadata && RegionBox.SelectedItem is string region && region != _regionName)
            {
                _regionName = region;
                ClearRunId();
            }
        };
        EmulatorBox.IsCheckedChanged += (_, _) =>
        {
            if (!_populatingMetadata && (EmulatorBox.IsChecked == true) != _usesEmulator)
            {
                _usesEmulator = EmulatorBox.IsChecked == true;
                ClearRunId();
            }
        };
        EditorTabs.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Source, EditorTabs) && IsMetadataTab)
            {
                RefreshMetadata();
            }
        };
        // GameName / CategoryName are set when the boxes are left (validation), which clears the
        // run's ID and refreshes the tab when it is shown.
        GameNameBox.LostFocus += (_, _) => CommitGameAndCategory();
        CategoryBox.LostFocus += (_, _) => CommitGameAndCategory();
        CategoryList.SelectionChanged += (_, _) => CommitGameAndCategory();
        RulesText.AddHandler(PointerReleasedEvent, OnRulesPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        RulesText.PointerMoved += (_, e) => RulesText.Cursor = LinkAt(e.GetPosition(RulesText)) is null ? Cursor.Default : new Cursor(StandardCursorType.Hand);
        ShowMetadata(null);
        RefreshAssociateButton();
    }

    private void CommitGameAndCategory()
    {
        string game = GameNameBox.Text?.Trim() ?? "";
        string category = CategoryBox.Text?.Trim() ?? "";
        if (game == _committedGame && category == _committedCategory)
        {
            return;
        }

        _committedGame = game;
        _committedCategory = category;
        ClearRunId();
        if (IsMetadataTab)
        {
            RefreshMetadata();
        }
    }

    /// <summary>MetadataChangedEventArgs(ClearRunID: true): an edited detail is no longer the run on speedrun.com.</summary>
    private void ClearRunId()
    {
        _runId = null;
        RefreshAssociateButton();
    }

    /// <summary>RefreshAssociateButton: Submit Run... only for a run that isn't on speedrun.com yet.</summary>
    private void RefreshAssociateButton()
    {
        bool linked = !string.IsNullOrEmpty(CurrentRunId);
        SubmitRunButton.IsEnabled = !linked;
        AssociateButton.Content = UiLocalizer.T(linked ? "Show on Speedrun.com..." : "Associate with Speedrun.com...");
    }

    /// <summary>MetadataControl.RefreshInformation, after speedrun.com was asked about the game.</summary>
    internal void RefreshMetadata() => MetadataRefreshed = RefreshMetadataAsync();

    private async Task RefreshMetadataAsync()
    {
        int version = ++_metadataVersion;
        string game = GameNameBox.Text?.Trim() ?? "";
        string category = CategoryBox.Text?.Trim() ?? "";
        SrcRunMetadata metadata;
        try
        {
            metadata = await SpeedrunComSharing.Submitter.LoadMetadataAsync(
                game, category, _platformName, _regionName, new Dictionary<string, string>(_variables, StringComparer.Ordinal));
        }
        catch (Exception)
        {
            metadata = new SrcRunMetadata(null, null, null, null, [], LookupFailed: true);
        }

        if (version != _metadataVersion)
        {
            return;
        }

        // RunMetadata.Refresh: once speedrun.com has answered, a platform or region the game
        // doesn't have is cleared, and so are variables it doesn't have for this category (all of
        // them for a game it doesn't know).
        if (!metadata.LookupFailed && !string.IsNullOrEmpty(game))
        {
            SrcGame? found = metadata.Game;
            if (found is null || !found.Platforms.Any(p => p.Name == _platformName))
            {
                SetPruned(ref _platformName);
            }

            if (found is null || !found.Regions.Any(r => r.Name == _regionName))
            {
                SetPruned(ref _regionName);
            }

            List<SrcVariable> variables = [.. metadata.VariableValues.Select(x => x.Variable)];
            foreach ((string name, string value) in _variables.ToList())
            {
                SrcVariable? variable = variables.FirstOrDefault(v => v.Name == name);
                if (found is null || variable is null || (!variable.Values.Values.Contains(value) && !variable.IsUserDefined))
                {
                    _variables.Remove(name);
                }
            }
        }

        ShowMetadata(metadata);
    }

    private void SetPruned(ref string name)
    {
        if (name.Length > 0)
        {
            name = "";
            ClearRunId();
        }
    }

    /// <summary>The tab's boxes for what speedrun.com said (null: not asked yet).</summary>
    private void ShowMetadata(SrcRunMetadata? metadata)
    {
        LoadedMetadata = metadata;
        SrcGame? game = metadata?.Game;
        _populatingMetadata = true;
        try
        {
            // Without the game, the boxes show what the run has, but can't be changed.
            List<string> platforms = game is null ? Known(_platformName) : ["", .. game.Platforms.Select(p => p.Name)];
            List<string> regions = game is null ? Known(_regionName) : ["", .. game.Regions.Select(r => r.Name)];
            PlatformBox.ItemsSource = platforms;
            PlatformBox.SelectedItem = platforms.Contains(_platformName) ? _platformName : null;
            PlatformBox.IsEnabled = game is not null && platforms.Count > 1;
            RegionBox.ItemsSource = regions;
            RegionBox.SelectedItem = regions.Contains(_regionName) ? _regionName : null;
            RegionBox.IsEnabled = game is not null && regions.Count > 1;
            EmulatorBox.IsVisible = game?.EmulatorsAllowed == true;
            EmulatorBox.IsChecked = _usesEmulator;

            foreach (Control field in _variableFields)
            {
                MetadataCard.Children.Remove(field);
            }

            _variableFields.Clear();
            if (game is not null)
            {
                int index = MetadataCard.Children.IndexOf(EmulatorBox) + 1;
                foreach ((SrcVariable variable, _) in metadata!.VariableValues)
                {
                    var field = new SettingsField { Label = variable.Name + ":", Field = VariableBox(variable) };
                    MetadataCard.Children.Insert(index++, field);
                    _variableFields.Add(field);
                }
            }

            ShowRules(metadata);
        }
        finally
        {
            _populatingMetadata = false;
        }
    }

    private static List<string> Known(string name) => name.Length > 0 ? [name] : [];

    /// <summary>
    /// A variable's box: its values (and none) to pick from; a user-defined variable also takes
    /// any text (VariableBinding).
    /// </summary>
    private Control VariableBox(SrcVariable variable)
    {
        List<string> values = ["", .. variable.Values.Values];
        string current = _variables.TryGetValue(variable.Name, out string? value) ? value : "";
        if (variable.IsUserDefined)
        {
            var box = new AutoCompleteBox
            {
                ItemsSource = values.Skip(1).ToList(),
                Text = current,
                Width = 260,
                FilterMode = AutoCompleteFilterMode.StartsWith,
                MinimumPrefixLength = 0,
            };
            box.Classes.Add("runEditorField");
            box.LostFocus += (_, _) => SetVariable(variable, box.Text ?? "");
            box.SelectionChanged += (_, _) =>
            {
                if (box.SelectedItem is string picked)
                {
                    SetVariable(variable, picked);
                }
            };
            return box;
        }

        var combo = new ComboBox { ItemsSource = values, SelectedItem = values.Contains(current) ? current : "", Width = 260 };
        combo.Classes.Add("settingsCombo");
        combo.SelectionChanged += (_, _) =>
        {
            if (!_populatingMetadata && combo.SelectedItem is string picked)
            {
                SetVariable(variable, picked);
            }
        };
        return combo;
    }

    /// <summary>
    /// VariableBinding.Value: one of the variable's values, or (user-defined) any text; nothing
    /// removes it. Any change clears the run's ID.
    /// </summary>
    private void SetVariable(SrcVariable variable, string value)
    {
        if (_populatingMetadata)
        {
            return;
        }

        string? variableValue = variable.Values.Values.Contains(value) ? value : variable.IsUserDefined ? value : null;
        if (string.IsNullOrEmpty(variableValue))
        {
            variableValue = null;
        }

        _variables.TryGetValue(variable.Name, out string? old);
        if (variableValue is null)
        {
            _variables.Remove(variable.Name);
        }
        else
        {
            _variables[variable.Name] = variableValue;
        }

        if (old != variableValue)
        {
            ClearRunId();
        }
    }

    /// <summary>refreshRules: the bold sentence about the game's runs, then the category's rules.</summary>
    private void ShowRules(SrcRunMetadata? metadata)
    {
        InlineCollection inlines = RulesText.Inlines ??= new InlineCollection();
        inlines.Clear();
        _ruleLinks.Clear();
        int length = 0;
        if (metadata?.Game is not { } game)
        {
            return;
        }

        if (SpeedrunComRules.Intro(game) is { } intro)
        {
            string text = intro + Environment.NewLine + Environment.NewLine;
            inlines.Add(new InlineRun(text) { FontWeight = FontWeight.Bold });
            length += text.Length;
        }

        if (metadata.Category is not { } category)
        {
            return;
        }

        foreach (BBCodeSpan span in SpeedrunComRules.ParseBBCode(category.Rules ?? ""))
        {
            int last = 0;
            foreach (Match link in UrlPattern.Matches(span.Text))
            {
                AddSpan(span, span.Text[last..link.Index], null);
                AddSpan(span, link.Value, link.Value);
                last = link.Index + link.Length;
            }

            AddSpan(span, span.Text[last..], null);
        }

        void AddSpan(BBCodeSpan span, string text, string? url)
        {
            if (text.Length == 0)
            {
                return;
            }

            var run = new InlineRun(text)
            {
                FontWeight = span.Bold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = span.Italic ? FontStyle.Italic : FontStyle.Normal,
                FontSize = RulesText.FontSize * span.Size / BBCodeSpan.NormalSize,
            };
            var decorations = new TextDecorationCollection();
            if (span.Underline || url is not null)
            {
                decorations.AddRange(TextDecorations.Underline);
            }

            if (span.Strikeout)
            {
                decorations.AddRange(TextDecorations.Strikethrough);
            }

            if (decorations.Count > 0)
            {
                run.TextDecorations = decorations;
            }

            if (url is not null)
            {
                run.Bind(TextElement.ForegroundProperty, RulesText.GetResourceObservable("LsDialogAccentAltBrush"));
                _ruleLinks.Add((length, length + text.Length, url));
            }
            else if (span.Hidden)
            {
                // [spoiler]: text on its own color until it is selected.
                run.Bind(TextElement.ForegroundProperty, RulesText.GetResourceObservable("LsDialogForeground"));
                run.Bind(TextElement.BackgroundProperty, RulesText.GetResourceObservable("LsDialogForeground"));
            }
            else if (span.Shaded)
            {
                run.Bind(TextElement.BackgroundProperty, RulesText.GetResourceObservable("LsDialogRowSeparatorBrush"));
            }

            inlines.Add(run);
            length += text.Length;
        }
    }

    /// <summary>The rules' link under a point of the text, if any.</summary>
    private string? LinkAt(Point point)
    {
        if (_ruleLinks.Count == 0)
        {
            return null;
        }

        TextHitTestResult hit = RulesText.TextLayout.HitTestPoint(point);
        if (!hit.IsInside)
        {
            return null;
        }

        int index = hit.TextPosition;
        return _ruleLinks.FirstOrDefault(l => index >= l.Start && index < l.End).Url;
    }

    /// <summary>tbxRules_LinkClicked: a click on a link (not a selection) opens it.</summary>
    private void OnRulesPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left
            && string.IsNullOrEmpty(RulesText.SelectedText)
            && LinkAt(e.GetPosition(RulesText)) is { } url)
        {
            string target = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "http://" + url : url;
            SpeedrunComSharing.OpenBrowser(target);
        }
    }

    private async void OnAssociate(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CurrentRunId))
        {
            await AssociateRunAsync();
        }
        else
        {
            await ShowRunOnSpeedrunComAsync();
        }
    }

    /// <summary>
    /// associateRun: the run's speedrun.com page; its game, category, platform, region, emulator,
    /// variables and ID go into the editor (RunMetadata.PatchRun).
    /// </summary>
    internal async Task AssociateRunAsync()
    {
        string? url = await DialogChrome.PromptAsync(this, "Enter Speedrun.com URL", "Speedrun.com Run URL:", "", 320, labelWidth: 160);
        if (url is null)
        {
            return;
        }

        string? id = SpeedrunComSubmitter.GetRunIdFromSiteUri(url.Trim());
        if (id is null)
        {
            await ShowMessageAsync(UiLocalizer.T("Invalid URL"), UiLocalizer.T("The URL provided is not a valid speedrun.com Run URL."));
            return;
        }

        SrcRunInfo info;
        try
        {
            info = await SpeedrunComSharing.Submitter.GetRunAsync(id);
        }
        catch (Exception)
        {
            await ShowMessageAsync(UiLocalizer.T("Error"), UiLocalizer.T("The run could not be associated."));
            return;
        }

        GameNameBox.Text = info.GameName;
        CategoryBox.Text = info.CategoryName;
        _committedGame = info.GameName.Trim();
        _committedCategory = info.CategoryName.Trim();
        _platformName = info.PlatformName;
        _regionName = info.RegionName;
        _usesEmulator = info.Emulated;
        _variables.Clear();
        foreach ((string name, string value) in info.VariableValueNames)
        {
            _variables[name] = value;
        }

        _runId = info.Id;
        RefreshAssociateButton();
        RefreshCategorySuggestions();
        RefreshMetadata();
    }

    /// <summary>showRunOnSpeedrunCom: the run's page in the browser (nothing when it can't be read).</summary>
    private async Task ShowRunOnSpeedrunComAsync()
    {
        try
        {
            if (await SpeedrunComSharing.Submitter.GetRunWebLinkAsync(CurrentRunId!) is { } link)
            {
                SpeedrunComSharing.OpenBrowser(link.AbsoluteUri);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnSubmitRun(object? sender, RoutedEventArgs e) => await SubmitRunAsync();

    /// <summary>
    /// btnSubmit_Click: the run as edited, checked (SpeedrunCom.ValidateRun), then the submit
    /// window. A submitted run keeps its new ID; a game time entered there stays in the splits.
    /// </summary>
    internal async Task SubmitRunAsync()
    {
        Run run;
        try
        {
            run = BuildRun(preserveHistory: true);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            return;
        }

        SpeedrunComSubmitter submitter = SpeedrunComSharing.Submitter;
        submitter.Authenticator = () => SpeedrunComSharing.AskForApiKeyAsync(this);
        (string? reason, SrcRunMetadata? metadata) = await submitter.ValidateRunAsync(run);
        if (reason is not null || metadata is null)
        {
            await ShowMessageAsync(UiLocalizer.T("Submitting Failed"), UiLocalizer.T(reason ?? ""));
            return;
        }

        var submit = new SpeedrunComSubmitWindow(run, metadata, submitter);
        UiLocalizer.Apply(submit);
        bool submitted = SubmitWindowForTests is { } answer
            ? await answer(submit)
            : await OwnedDialog.ShowAsync<bool>(this, submit);
        KeepPatchedGameTime(run);
        if (submitted)
        {
            _runId = run.Metadata.RunId;
            RefreshAssociateButton();
        }
    }

    /// <summary>patchGameTime edits the editor's run on Windows: the PB's game time and its attempt.</summary>
    private void KeepPatchedGameTime(Run run)
    {
        if (run.Count == 0 || _rows.Count == 0)
        {
            return;
        }

        int pb = _comparisons.IndexOf(Run.PersonalBestComparisonName);
        TimeSpan? gameTime = run[^1].PersonalBestSplitTime.GameTime;
        SegmentEditRow last = _rows[^1];
        if (pb >= 0 && pb < last.GameComparisonTexts.Count
            && TimeSpanParser.ParseNullable(last.GameComparisonTexts[pb]) != gameTime)
        {
            last.GameComparisonTexts[pb] = FormatOptional(gameTime);
            RebuildGridColumns();
        }

        foreach (Attempt patched in run.AttemptHistory)
        {
            int index = _attemptHistory.FindIndex(a => a.Index == patched.Index);
            if (index >= 0 && !ReferenceEquals(_attemptHistory[index], patched))
            {
                _attemptHistory[index].Time = patched.Time;
            }
        }
    }

    private void SetUpGameIconMenu()
    {
        GameIconBorder.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right)
            {
                PixelPoint screen = GameIconBorder.PointToScreen(e.GetPosition(GameIconBorder));
                Platform.NativeContextMenuFactory.Current.Show(this, screen, UiLocalizer.Apply(GameIconMenuModel()));
                e.Handled = true;
            }
        };
    }

    /// <summary>RemoveIconMenu: Set Icon..., Download Box Art, Download Icon, Open from URL..., Remove Icon.</summary>
    internal Platform.NativeMenuModel GameIconMenuModel() => new()
    {
        Items =
        [
            new Platform.NativeMenuAction { Header = "Set Icon...", OnClickAsync = SetGameIconFromFileAsync },
            new Platform.NativeMenuAction { Header = "Download Box Art", OnClickAsync = DownloadBoxArtAsync },
            new Platform.NativeMenuAction { Header = "Download Icon", OnClickAsync = DownloadIconAsync },
            new Platform.NativeMenuAction { Header = "Open from URL...", OnClickAsync = OpenGameIconFromUrlAsync },
            new Platform.NativeMenuAction { Header = "Remove Icon", IsEnabled = _gameIconXml is not null, OnClick = RemoveGameIcon },
        ],
    };

    /// <summary>The game icon as the splits keep it (tests).</summary>
    internal string? GameIconXmlForTests => _gameIconXml;

    private void RemoveGameIcon()
    {
        _gameIconXml = null;
        RefreshGameIconImage();
    }

    /// <summary>downloadIconToolStripMenuItem_Click: the game's icon on speedrun.com.</summary>
    internal Task DownloadIconAsync() =>
        DownloadGameAssetAsync(game => game.IconUri, "Could not download the icon of the game!");

    /// <summary>
    /// downloadBoxartToolStripMenuItem_Click: the game's box art on speedrun.com. (Windows then
    /// tries Twitch, which needs LiveSplit's Twitch login; Share no longer has Twitch.)
    /// </summary>
    internal Task DownloadBoxArtAsync() =>
        DownloadGameAssetAsync(game => game.CoverUri, "Could not download the box art of the game!");

    private async Task DownloadGameAssetAsync(Func<SrcGame, Uri?> asset, string failure)
    {
        try
        {
            string game = GameNameBox.Text?.Trim() ?? "";
            SrcRunMetadata metadata = await SpeedrunComSharing.Submitter.LoadMetadataAsync(
                game, "", "", "", new Dictionary<string, string>(StringComparer.Ordinal));
            if (metadata.Game is { } found && asset(found) is { } uri)
            {
                byte[] image = await SpeedrunComSharing.Submitter.DownloadAsync(uri);
                if (IsImage(image))
                {
                    _gameIconXml = IconBlobDecoder.ToPortableIconXml(image);
                    RefreshGameIconImage();
                    return;
                }
            }
        }
        catch (Exception)
        {
        }

        await ShowMessageAsync(UiLocalizer.T("Error"), UiLocalizer.T(failure));
    }

    /// <summary>openFromURLMenuItem_Click: an image from an address as the game icon.</summary>
    internal async Task OpenGameIconFromUrlAsync()
    {
        string? url = await DialogChrome.PromptAsync(this, "Open Game Icon from URL", "URL:", "", 320);
        if (url is null)
        {
            return;
        }

        byte[] image;
        try
        {
            image = await MainWindow.DownloadFromUrl(new Uri(url.Trim()), CancellationToken.None);
        }
        catch (Exception)
        {
            await ShowMessageAsync(UiLocalizer.T("Error"), UiLocalizer.T("The Game Icon couldn't be downloaded."));
            return;
        }

        if (!IsImage(image))
        {
            await ShowMessageAsync(UiLocalizer.T("Error"), UiLocalizer.T("The URL was not recognized as an image."));
            return;
        }

        _gameIconXml = IconBlobDecoder.ToPortableIconXml(image);
        RefreshGameIconImage();
    }

    private static bool IsImage(byte[] data)
    {
        try
        {
            using SKBitmap? bitmap = SKBitmap.Decode(data);
            return bitmap is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
