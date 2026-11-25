using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using SkiaSharp;

namespace LiveSplitNative.App;

/// <summary>
/// Port of LiveSplit's ShareRunDialog (93a11b85), "Run Sharer": Speedrun.com (when the timer is
/// not going and the run has a personal best), Screenshot and Excel. The social platforms
/// (X, Bluesky, Twitch, Imgur) and the post text that only they use are left out.
/// The state is a copy; a finished attempt is first reset into it as the personal best.
/// </summary>
internal sealed class ShareRunWindow : Window
{
    public const string SpeedrunComName = "Speedrun.com";
    public const string ScreenshotName = "Screenshot";
    public const string ExcelName = "Excel";

    private readonly LiveSplitState _state;
    private readonly Func<Task<byte[]?>> _screenshot;
    private readonly INativeFileDialogs _fileDialogs;
    private readonly ComboBox _platform;
    private readonly TextBlock _description;

    public ShareRunWindow(LiveSplitState state, Func<Task<byte[]?>> screenshot, INativeFileDialogs fileDialogs)
    {
        _state = state;
        _screenshot = screenshot;
        _fileDialogs = fileDialogs;
        if (_state.CurrentPhase == TimerPhase.Ended)
        {
            new TimerModel { CurrentState = _state }.ResetAndSetAttemptAsPb();
        }

        DialogChrome.ConfigureDialog(this);
        Title = "Run Sharer";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // SubmitDialog_Load: Speedrun.com only for a run that isn't going and has a PB.
        var platforms = new List<string>();
        if (_state.CurrentPhase is TimerPhase.NotRunning or TimerPhase.Ended && HasPersonalBest(Run))
        {
            platforms.Add(SpeedrunComName);
        }

        platforms.Add(ScreenshotName);
        platforms.Add(ExcelName);

        _platform = new ComboBox { ItemsSource = platforms, SelectedIndex = 0, Width = 280 };
        _platform.SelectionChanged += (_, _) => RefreshDescription();
        _description = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = DialogChrome.ControlFontSize };
        _description.Bind(TextBlock.ForegroundProperty, _description.GetResourceObservable("LsDialogHintForeground"));

        var share = new Button { Content = "Share", IsDefault = true };
        var close = new Button { Content = "Close", IsCancel = true };
        share.Click += async (_, _) => await ShareAsync();
        close.Click += (_, _) => Close();

        Content = new DockPanel
        {
            Children =
            {
                DialogChrome.Footer(close, share),
                new SettingsCard
                {
                    Margin = new Thickness(24, 20),
                    Children =
                    {
                        new SettingsField { Label = "Platform:", Field = _platform },
                        _description,
                    },
                },
            },
        };
        SettingsLayout.SetLabelWidth(this, 110);
        RefreshDescription();
    }

    /// <summary>The run being shared (the copy).</summary>
    public Run Run => _state.Run;

    /// <summary>The platforms offered, in order (tests).</summary>
    internal IReadOnlyList<string> Platforms => _platform.Items.Cast<string>().ToList();

    internal string SelectedPlatform
    {
        get => (string)_platform.SelectedItem!;
        set => _platform.SelectedItem = value;
    }

    internal string Description => _description.Text ?? "";

    /// <summary>The messages shown (title, text), for the tests; set to skip showing them.</summary>
    internal List<(string Title, string Text)>? MessagesForTests { get; set; }

    /// <summary>The Speedrun.com submit window's answer for the tests (instead of showing it).</summary>
    internal Func<SpeedrunComSubmitWindow, Task<bool>>? SubmitWindowForTests { get; set; }

    /// <summary>IRunUploadPlatform.Description of each platform.</summary>
    internal static string DescriptionOf(string platform) => platform switch
    {
        SpeedrunComName => "Speedrun.com is a site intended to provide centralized leaderboards for speedrunning.",
        ScreenshotName => "Sharing will save a screenshot of LiveSplit.",
        _ => "Export your splits as an Excel Sheet to analyze your splits.\nThis includes your whole history of all the runs you ever did.",
    };

    private static bool HasPersonalBest(Run run) => run.Count > 0 && run[^1].PersonalBestSplitTime.RealTime.HasValue;

    private void RefreshDescription() => _description.Text = UiLocalizer.T(DescriptionOf(SelectedPlatform));

    /// <summary>
    /// btnSubmit_Click: shares to the platform; "Run Shared" when it worked, otherwise (also when
    /// the platform's own dialog was cancelled) "The run could not be shared.".
    /// </summary>
    internal async Task<bool> ShareAsync()
    {
        string platform = SelectedPlatform;
        bool shared = false;
        try
        {
            shared = platform switch
            {
                SpeedrunComName => await SubmitToSpeedrunComAsync(),
                ScreenshotName => await SaveScreenshotAsync(),
                _ => await SaveExcelAsync(),
            };
        }
        catch (Exception)
        {
            shared = false;
        }

        if (shared)
        {
            await ShowMessageAsync("Run Shared", string.Format(UiLocalizer.T("Your run was successfully shared to {0}."), platform));
        }
        else
        {
            await ShowMessageAsync("Error", "The run could not be shared.");
        }

        return shared;
    }

    /// <summary>SpeedrunComRunUploadPlatform.SubmitRun: validate, then the submit window.</summary>
    private async Task<bool> SubmitToSpeedrunComAsync()
    {
        Core.Web.SpeedrunComSubmitter submitter = SpeedrunComSharing.Submitter;
        submitter.Authenticator = () => SpeedrunComSharing.AskForApiKeyAsync(this);
        (string? reason, Core.Web.SrcRunMetadata? metadata) = await submitter.ValidateRunAsync(Run);
        if (reason is not null || metadata is null)
        {
            await ShowMessageAsync("Submitting Failed", reason ?? "");
            return false;
        }

        var submit = new SpeedrunComSubmitWindow(Run, metadata, submitter);
        UiLocalizer.Apply(submit);
        if (SubmitWindowForTests is { } answer)
        {
            return await answer(submit);
        }

        return await OwnedDialog.ShowAsync<bool>(this, submit);
    }

    /// <summary>Screenshot.SubmitRun: the timer as an image, saved where the user picks.</summary>
    private async Task<bool> SaveScreenshotAsync()
    {
        byte[]? png = await _screenshot();
        if (png is null)
        {
            return false;
        }

        string? path = await PickSavePathAsync(new FilePickerSaveOptions
        {
            DefaultExtension = "png",
            SuggestedFileName = "LiveSplit.png",
            FileTypeChoices =
            [
                new FilePickerFileType("PNG (*.png)") { Patterns = ["*.png"] },
                new FilePickerFileType("JPEG (*.jpeg)") { Patterns = ["*.jpeg", "*.jpg"] },
            ],
        });
        if (path is null)
        {
            return false;
        }

        await File.WriteAllBytesAsync(path, EncodeScreenshot(png, path));
        return true;
    }

    /// <summary>
    /// The screenshot in the file's format (Image.Save picks it from the name): JPEG for .jpg and
    /// .jpeg, PNG otherwise. MakeScreenShot starts from black, as the window can't be see-through.
    /// </summary>
    internal static byte[] EncodeScreenshot(byte[] png, string path)
    {
        using SKBitmap source = SKBitmap.Decode(png);
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Black);
        surface.Canvas.DrawBitmap(source, 0, 0);
        using SKImage image = surface.Snapshot();
        string extension = Path.GetExtension(path).ToLowerInvariant();
        using SKData data = extension is ".jpg" or ".jpeg"
            ? image.Encode(SKEncodedImageFormat.Jpeg, 95)
            : image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Excel.SubmitRun: the splits and their whole history as an Excel sheet.</summary>
    private async Task<bool> SaveExcelAsync()
    {
        string? path = await PickSavePathAsync(new FilePickerSaveOptions
        {
            DefaultExtension = "xlsx",
            SuggestedFileName = string.IsNullOrWhiteSpace(Run.GetExtendedFileName()) ? "splits.xlsx" : Run.GetExtendedFileName() + ".xlsx",
            FileTypeChoices = [new FilePickerFileType("Excel Sheet (*.xlsx)") { Patterns = ["*.xlsx"] }],
        });
        if (path is null)
        {
            return false;
        }

        await using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write);
        ExcelRunSaver.Save(Run, stream);
        return true;
    }

    /// <summary>The save panel's path (null: cancelled); tests answer through <see cref="SavePathForTests"/>.</summary>
    private async Task<string?> PickSavePathAsync(FilePickerSaveOptions options)
    {
        if (SavePathForTests is { } answer)
        {
            return answer(options);
        }

        IStorageFile? file = await _fileDialogs.SaveFileAsync(this, options);
        return file?.TryGetLocalPath();
    }

    /// <summary>The save panel's answer for the tests (a path, or null for Cancel).</summary>
    internal Func<FilePickerSaveOptions, string?>? SavePathForTests { get; set; }

    private async Task ShowMessageAsync(string title, string text)
    {
        if (MessagesForTests is { } messages)
        {
            messages.Add((title, text));
            return;
        }

        var ok = new Button { Content = "OK", IsDefault = true };
        Window dialog = DialogChrome.Message(UiLocalizer.T(title), UiLocalizer.T(text), 420, ok);
        UiLocalizer.Apply(dialog);
        ok.Click += (_, _) => dialog.Close();
        await OwnedDialog.ShowAsync(this, dialog);
    }
}
