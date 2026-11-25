using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Racing;

namespace LiveSplitNative.App;

/// <summary>
/// LiveSplit.Racetime's ChannelForm: the race room. It connects the channel (the timer follows
/// the race), shows "Loading..." until racetime.gg's LiveSplit page for the race
/// (https://racetime.gg/&lt;race&gt;/livesplit) is loaded with the account's token, and on closing
/// offers to forfeit while racing, removes the "[Race]" comparisons and leaves the race.
/// </summary>
internal sealed class RacetimeChannelWindow : Window
{
    private readonly TextBlock _loadMessage;
    private readonly MacWebView? _chatBox;
    private readonly DispatcherTimer _sourceWatch;
    private bool _closeConfirmed;
    private int _retries;
    private DateTime _lastRetry = DateTime.Now;
    private string? _requested;
    private bool _wasLoading;
    private bool _openedInBrowser;

    public RacetimeChannelWindow(RacetimeChannel channel, string channelId, bool alwaysOnTop = true)
    {
        Channel = channel;
        Channel.Disconnected += Channel_Disconnected;
        Channel.RaceChanged += Channel_RaceChanged;
        Channel.Authorized += Channel_Authorized;

        Width = 1145;
        Height = 619;
        Topmost = alwaysOnTop;
        Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x41));
        _loadMessage = new TextBlock
        {
            Text = "Loading...",
            FontSize = 50 * 96 / 72.0,
            Foreground = Brushes.Gainsboro,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var content = new Grid();
        content.Children.Add(_loadMessage);
        if (MacWebView.IsSupported)
        {
            // A native view is drawn over everything in the window, so until the race's page is
            // there (Windows hides the WebView2) it is kept to one pixel in the corner.
            _chatBox = new MacWebView();
            ShowChatBox(false);
            content.Children.Add(_chatBox);
        }

        Content = content;
        Title = "Connecting to " + channelId[(channelId.IndexOf('/') + 1)..];
        _sourceWatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _sourceWatch.Tick += (_, _) => WatchSource();
        Closing += OnClosing;
        Closed += (_, _) => _sourceWatch.Stop();
    }

    public RacetimeChannel Channel { get; }

    /// <summary>The page the window asks for: FullWebRoot + race id + "/livesplit".</summary>
    public string? PageAddress => Channel.Race is { } race ? Channel.FullWebRoot + race.Id + "/livesplit" : null;

    /// <summary>The page shown in the web view (null when none or no web view).</summary>
    public string? ShownAddress => _chatBox?.CurrentUrl;

    public bool IsPageShown { get; private set; }

    /// <summary>The web view's page title (null when none or no web view).</summary>
    public string? PageTitle => _chatBox?.PageTitle;

    private void ShowChatBox(bool show)
    {
        if (_chatBox is null)
        {
            return;
        }

        IsPageShown = show;
        _chatBox.Width = show ? double.NaN : 1;
        _chatBox.Height = show ? double.NaN : 1;
        _chatBox.HorizontalAlignment = show ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        _chatBox.VerticalAlignment = show ? VerticalAlignment.Stretch : VerticalAlignment.Top;
    }

    /// <summary>Shows the window and joins the race.</summary>
    public void Start(string channelId)
    {
        Show();
        _ = Channel.ConnectAsync(channelId);
    }

    // OnSourceChanged: once loading stops, the race's page shows; anything else reloads it (up to 5
    // tries within 10 s each), then "Error loading page.".
    private void WatchSource()
    {
        if (_chatBox is null || _requested is null)
        {
            return;
        }

        bool loading = _chatBox.IsLoading;
        if (loading || !_wasLoading)
        {
            _wasLoading = loading;
            return;
        }

        _wasLoading = false;
        if (!string.Equals(ShownAddress, PageAddress, StringComparison.Ordinal))
        {
            if (_retries >= 5)
            {
                _loadMessage.Text = "Error loading page.";
                ShowChatBox(false);
                _requested = null;
            }
            else
            {
                Channel_RaceChanged(null, EventArgs.Empty);
            }

            if (_lastRetry.AddSeconds(10) >= DateTime.Now)
            {
                _lastRetry = DateTime.Now;
                _retries++;
            }
        }
        else
        {
            ShowChatBox(true);
            _retries = 0;
        }
    }

    private async void Channel_RaceChanged(object? sender, EventArgs e)
    {
        try
        {
            if (Channel.Race is not { } race || !IsVisible)
            {
                return;
            }

            Title = $"{race.Goal} [{race.GameName}] - {race.ChannelName}";
            if (_chatBox is null && !_openedInBrowser)
            {
                // No web view here (Linux): the race's page, with its chat, opens in the browser;
                // the race itself keeps going through this window.
                _openedInBrowser = true;
                _loadMessage.Text = "The race chat is open in your browser.\nKeep this window open: the timer follows the race through it.";
                _loadMessage.FontSize = 20;
                _loadMessage.TextAlignment = TextAlignment.Center;
                RacetimeController.OpenBrowser(Channel.FullWebRoot + race.Id);
            }

            if (_chatBox is not null && _retries <= 5 && (_requested is null || sender is null))
            {
                await Task.Delay(3000);
                if (Channel.Token is { } token && PageAddress is { } address && IsVisible)
                {
                    _requested = address;
                    _wasLoading = true;
                    _chatBox.Load(address, Channel.FullWebRoot, token.AccessToken);
                    _sourceWatch.Start();
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private void Channel_Authorized(object? sender, EventArgs e) => Activate();

    private void Channel_Disconnected(object? sender, EventArgs e)
    {
        if (IsVisible)
        {
            Title = "Disconnected";
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_closeConfirmed && Channel.Race?.State == RaceState.Started && Channel.PersonalStatus == UserStatus.Racing)
        {
            e.Cancel = true;
            _ = AskForfeitAsync();
            return;
        }

        Channel.RemoveRaceComparisons();
        Channel.Authorized -= Channel_Authorized;
        Channel.RaceChanged -= Channel_RaceChanged;
        Channel.Disconnect();
    }

    // "Do you want to FORFEIT before closing the window?" Yes forfeits, No closes, Cancel stays.
    private async Task AskForfeitAsync()
    {
        string answer = "Cancel";
        var yes = DialogChrome.Style(new Button { Content = "Yes", IsDefault = true });
        var no = DialogChrome.Style(new Button { Content = "No" });
        var cancel = DialogChrome.Style(new Button { Content = "Cancel", IsCancel = true });
        var dialog = DialogChrome.Message(
            "", "Do you want to FORFEIT before closing the window?", 420, cancel, no, yes);
        yes.Click += (_, _) => { answer = "Yes"; dialog.Close(); };
        no.Click += (_, _) => { answer = "No"; dialog.Close(); };
        cancel.Click += (_, _) => { answer = "Cancel"; dialog.Close(); };
        await OwnedDialog.ShowAsync(this, dialog);
        if (answer == "Cancel")
        {
            return;
        }

        if (answer == "Yes")
        {
            Channel.Forfeit();
        }

        _closeConfirmed = true;
        Close();
    }
}
