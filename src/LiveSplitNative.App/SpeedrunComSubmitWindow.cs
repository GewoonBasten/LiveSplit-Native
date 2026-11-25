using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.App;

/// <summary>
/// Port of LiveSplit's SpeedrunComSubmitDialog (93a11b85), "Submitting to Speedrun.com": the
/// video and a comment; the date when the personal best's attempt has none; the time without
/// loads and the game time when the game uses them and the run lacks a game time (or the game
/// uses both). Submit opens the new run's page; the window answers true then.
/// </summary>
internal sealed class SpeedrunComSubmitWindow : Window
{
    private readonly Run _run;
    private readonly SpeedrunComSubmitter _submitter;
    private readonly bool _hasPersonalBestDateTime;
    private readonly TextBox _video = new() { Width = 280 };
    private readonly TextBox _comment = new()
    {
        Width = 280,
        Height = 110,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        VerticalContentAlignment = VerticalAlignment.Top,
    };

    private readonly CalendarDatePicker? _date;
    private readonly TextBox? _withoutLoads;
    private readonly TextBox? _gameTime;

    public SpeedrunComSubmitWindow(Run run, SrcRunMetadata metadata, SpeedrunComSubmitter submitter)
    {
        _run = run;
        _submitter = submitter;
        DialogChrome.ConfigureDialog(this);
        Title = "Submitting to Speedrun.com";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var commentField = new SettingsField { Label = "Comment:", Field = _comment };
        commentField.LabelBlock.VerticalAlignment = VerticalAlignment.Top;
        commentField.LabelBlock.Margin = new Thickness(0, 6, 0, 0);
        var card = new SettingsCard
        {
            Margin = new Thickness(24, 20),
            Children =
            {
                new SettingsField { Label = "Video:", Field = _video },
                commentField,
            },
        };

        _hasPersonalBestDateTime = SpeedrunComSubmitter.FindPersonalBestAttemptDate(run).HasValue;
        if (!_hasPersonalBestDateTime)
        {
            _date = new CalendarDatePicker { SelectedDate = DateTime.Today, Width = 280 };
            card.Children.Add(new SettingsField { Label = "Date:", Field = _date });
        }

        SplitTime runTime = run[^1].PersonalBestSplitTime;
        IReadOnlyList<SrcTimingMethod> timingMethods = metadata.Game!.TimingMethods;
        bool usesGameTime = timingMethods.Contains(SrcTimingMethod.GameTime);
        bool usesWithoutLoads = timingMethods.Contains(SrcTimingMethod.RealTimeWithoutLoads);
        if (!runTime.GameTime.HasValue || (usesGameTime && usesWithoutLoads))
        {
            if (usesWithoutLoads)
            {
                _withoutLoads = new TextBox { Width = 280 };
                card.Children.Add(new SettingsField { Label = "Without Loads:", Field = _withoutLoads });
            }

            if (usesGameTime)
            {
                _gameTime = new TextBox { Width = 280 };
                card.Children.Add(new SettingsField { Label = "Game Time:", Field = _gameTime });
            }
        }

        var submit = new Button { Content = "Submit", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        submit.Click += async (_, _) => await SubmitAsync();
        cancel.Click += (_, _) => Close(false);
        Content = new DockPanel { Children = { DialogChrome.Footer(cancel, submit), card } };
        SettingsLayout.SetLabelWidth(this, 110);
        Opened += (_, _) => _video.Focus();
    }

    /// <summary>The fields shown, by label (tests).</summary>
    internal IReadOnlyList<string> Fields =>
        ((SettingsCard)((DockPanel)Content!).Children[1]).Children.OfType<SettingsField>().Select(f => f.Label ?? "").ToList();

    internal TextBox VideoBox => _video;

    internal TextBox CommentBox => _comment;

    internal CalendarDatePicker? DateBox => _date;

    internal TextBox? WithoutLoadsBox => _withoutLoads;

    internal TextBox? GameTimeBox => _gameTime;

    /// <summary>The messages shown (title, text), for the tests; set to skip showing them.</summary>
    internal List<(string Title, string Text)>? MessagesForTests { get; set; }

    /// <summary>The run's page after a submission (tests: the browser is not opened then).</summary>
    internal Uri? SubmittedLink { get; private set; }

    /// <summary>btnSubmit_Click.</summary>
    internal async Task<bool> SubmitAsync()
    {
        Uri? videoUri = null;
        if (!string.IsNullOrEmpty(_video.Text))
        {
            string videoText = _video.Text;
            if (!videoText.StartsWith("http", StringComparison.Ordinal))
            {
                videoText = "http://" + videoText;
            }

            if (Uri.IsWellFormedUriString(videoText, UriKind.Absolute))
            {
                videoUri = new Uri(videoText);
            }
            else
            {
                await ShowMessageAsync("Submitting Failed", "You didn't provide a valid Video URL.");
                return false;
            }
        }

        string comment = _comment.Text ?? "";
        DateTime? date = null;
        if (!_hasPersonalBestDateTime && _date?.SelectedDate is { } picked)
        {
            date = new DateTime(picked.Year, picked.Month, picked.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        if (_gameTime is not null)
        {
            try
            {
                SpeedrunComSubmitter.PatchGameTime(_run, TimeSpanParser.ParseNullable(_gameTime.Text));
            }
            catch (Exception)
            {
                await ShowMessageAsync("Submitting Failed", "You didn't enter a valid Game Time.");
                return false;
            }
        }

        TimeSpan? withoutLoads = null;
        if (_withoutLoads is not null)
        {
            try
            {
                withoutLoads = TimeSpanParser.ParseNullable(_withoutLoads.Text);
                SpeedrunComSubmitter.PatchGameTime(_run, withoutLoads);
            }
            catch (Exception)
            {
                await ShowMessageAsync("Submitting Failed", "You didn't enter a valid Real Time without Loads.");
                return false;
            }
        }

        SrcSubmitResult result = await _submitter.SubmitRunAsync(_run, comment, videoUri, date, withoutLoads);
        if (result.Submitted)
        {
            SubmittedLink = result.WebLink;
            if (result.WebLink is { } link)
            {
                SpeedrunComSharing.OpenBrowser(link.AbsoluteUri);
            }

            Close(true);
            return true;
        }

        await ShowMessageAsync("Submitting Failed", result.ReasonForRejection ?? "");
        return false;
    }

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
