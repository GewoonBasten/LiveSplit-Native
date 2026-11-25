using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.App;

/// <summary>
/// LiveSplit.TheRun CollectorSettings (0.4.5): the description, the link to the upload key, the
/// Upload Key (hidden as typed, saved to its own file when the box is left) with Test, the
/// account it belongs to (picture, profile link, Live link), and the four switches.
/// </summary>
public sealed partial class ComponentSettingsEditor
{
    private enum TheRunStatus
    {
        None,
        Validating,
        Connected,
        Error,
    }

    /// <summary>The key check in progress, for the tests.</summary>
    internal Task? TheRunValidation { get; private set; }

    private void BuildTheRunFields()
    {
        TheRunComponentSettings settings = TheRunComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        // SetSettings: txtPath.Text = GetUploadKey() (an old layout's key moves to the key file).
        string savedKey = TheRunUploadKey.Resolve(settings);

        // The settings look: the description and the link on top, then a card with the key,
        // the account it belongs to (shown once tested) and the four switches.
        _fields.Children.Add(new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(2, 0, 2, 0),
            Children =
            {
                new TextBlock
                {
                    Text = "therun.gg syncs your splits automatically and shows your live runs alongside "
                        + "thousands of other runners. Get advanced stats on your profile, race against "
                        + "others, participate in tournaments, and receive a yearly recap of your speedrunning journey.",
                    TextWrapping = TextWrapping.Wrap,
                },
                Link("Get your upload key at therun.gg/livesplit", "https://therun.gg/livesplit"),
            },
        });
        Controls rows = Rows(_fields);

        TextBox key = new()
        {
            Text = savedKey,
            PasswordChar = '•',
            VerticalAlignment = VerticalAlignment.Center,
        };
        Button test = MakeDialogButton("Test", minWidth: 54);
        test.Margin = new Thickness(8, 0, 0, 0);
        test.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(test, 1);
        rows.Add(Labeled("Upload Key", new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { key, test },
        }));

        var picture = new Image
        {
            Width = 29,
            Height = 29,
            Stretch = Stretch.Fill,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };
        HyperlinkButton username = Link("", null);
        username.IsVisible = false;
        username.VerticalAlignment = VerticalAlignment.Center;
        HyperlinkButton live = Link("Live", null);
        live.IsVisible = false;
        live.VerticalAlignment = VerticalAlignment.Center;
        SettingsField account = Labeled("", new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { picture, username, live },
        });
        // The account row is there only while it shows something (after Test).
        account[!IsVisibleProperty] = username[!IsVisibleProperty];
        rows.Add(account);

        CheckBox stats = TheRunCheck(
            "Enable Stats Sync",
            settings.IsStatsUploadingEnabled,
            "Syncs your splits file to therun.gg after each completed run, on reset, and when closing LiveSplit.\n"
                + "This powers your profile stats, personal bests, and history.");
        CheckBox onReset = TheRunCheck(
            "Sync on Reset",
            settings.IsUploadOnResetEnabled,
            "When enabled, stats are synced every time you reset the timer.\n"
                + "When disabled, stats are only synced after a completed run or when closing LiveSplit.");
        CheckBox liveTracking = TheRunCheck(
            "Enable Live Tracking",
            settings.IsLiveTrackingEnabled,
            "Sends real-time split data to therun.gg so your current run appears on the live page.\n"
                + "Viewers and other runners can follow your progress as it happens.");
        CheckBox layoutPath = TheRunCheck(
            "Upload Layout Path",
            settings.IsLayoutPathUploadEnabled,
            "Includes the layout file path in the uploaded splits file.\n"
                + "Disabled by default because the path may contain personal information such as your Windows username.\n"
                + "If disabled, you will need to reload your layout when re-downloading your splits from therun.gg.");
        foreach (CheckBox check in new[] { stats, onReset, liveTracking, layoutPath })
        {
            rows.Add(check);
        }

        string? validatedUsername = null;
        void SetStatus(TheRunStatus status)
        {
            switch (status)
            {
                case TheRunStatus.Connected:
                    test.Foreground = new SolidColorBrush(Color.FromRgb(0, 150, 0));
                    test.Content = "Test";
                    break;
                case TheRunStatus.Error:
                    test.Foreground = new SolidColorBrush(Color.FromRgb(200, 0, 0));
                    test.Content = "Test";
                    break;
                case TheRunStatus.Validating:
                    test.ClearValue(ForegroundProperty);
                    test.Content = "...";
                    break;
                default:
                    test.ClearValue(ForegroundProperty);
                    test.Content = "Test";
                    break;
            }

            live.IsVisible = status == TheRunStatus.Connected && validatedUsername is not null;
        }

        async Task ShowUserAsync(TheRunUser? user)
        {
            if (user is null)
            {
                picture.IsVisible = false;
                picture.Source = null;
                username.IsVisible = false;
                username.Content = "";
                username.NavigateUri = null;
                live.IsVisible = false;
                return;
            }

            username.Content = "therun.gg/" + user.Username;
            username.NavigateUri = new Uri("https://therun.gg/" + Uri.EscapeDataString(user.Username));
            username.IsVisible = true;
            live.NavigateUri = new Uri("https://therun.gg/live/" + Uri.EscapeDataString(user.Username));
            live.IsVisible = true;
            if (string.IsNullOrEmpty(user.Picture))
            {
                picture.IsVisible = false;
                return;
            }

            byte[]? bytes = await TheRunClient.Current.DownloadPictureAsync(user.Picture);
            try
            {
                if (bytes is not null)
                {
                    using var stream = new MemoryStream(bytes);
                    picture.Source = new Bitmap(stream);
                    picture.IsVisible = true;
                }
            }
            catch (Exception)
            {
                picture.IsVisible = false;
            }
        }

        // ValidateKeyAsync: the key's account, "Invalid upload key." or "Could not reach therun.gg.".
        async Task ValidateAsync()
        {
            string uploadKey = !string.IsNullOrEmpty(key.Text) ? key.Text : TheRunUploadKey.Read();
            if (string.IsNullOrWhiteSpace(uploadKey))
            {
                validatedUsername = null;
                SetStatus(TheRunStatus.None);
                await ShowUserAsync(null);
                return;
            }

            test.IsEnabled = false;
            test.Content = "...";
            SetStatus(TheRunStatus.Validating);
            await ShowUserAsync(null);
            TheRunKeyCheck check = await TheRunClient.Current.ValidateAsync(uploadKey);
            if (check.User is { } user)
            {
                validatedUsername = user.Username;
                SetStatus(TheRunStatus.Connected);
                await ShowUserAsync(user);
            }
            else
            {
                validatedUsername = null;
                SetStatus(TheRunStatus.Error);
                username.Content = check.Reached ? "Invalid upload key." : "Could not reach therun.gg.";
                username.NavigateUri = null;
                username.IsVisible = true;
            }

            test.IsEnabled = true;
        }

        test.Click += (_, _) => TheRunValidation = ValidateAsync();
        // txtPath_Leave: the key is saved as soon as the box is left.
        key.LostFocus += (_, _) => TheRunUploadKey.Save(key.Text ?? "");
        key.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                NotifyPreview();
            }
        };
        TheRunValidation = ValidateAsync();

        _applyMore = () =>
        {
            settings.IsStatsUploadingEnabled = stats.IsChecked == true;
            settings.IsUploadOnResetEnabled = onReset.IsChecked == true;
            settings.IsLiveTrackingEnabled = liveTracking.IsChecked == true;
            settings.IsLayoutPathUploadEnabled = layoutPath.IsChecked == true;
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    private CheckBox TheRunCheck(string label, bool value, string tip)
    {
        var box = new CheckBox { Content = label, IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
        box.IsCheckedChanged += (_, _) => NotifyPreview();
        ToolTip.SetTip(box, tip);
        return box;
    }

    /// <summary>A LinkLabel: opens its address in the browser (none: just text).</summary>
    private static HyperlinkButton Link(string text, string? url) => new()
    {
        Content = text,
        NavigateUri = url is null ? null : new Uri(url),
        Padding = new Thickness(0),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Left,
    };
}
