using System.Net;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>therun.gg (LiveSplit.TheRun 0.4.5): its settings page and what the timer sends.</summary>
[Collection(MainWindowCollection.Name)]
public class TheRunAppTests
{
    private const string Key = "0123456789abcdef0123456789abcdef0123";

    private sealed class FakeTheRun : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.OriginalString;
            lock (Requests)
            {
                Requests.Add((request.Method, url));
            }

            if (url == TheRunClient.ValidateBaseUrl + Key)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"result\":{\"data\":{\"username\":\"Basten\"}}}"),
                });
            }

            if (url.StartsWith(TheRunClient.FileUploadBaseUrl, StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"url\":\"https://splits-bucket-main.s3.eu-west-1.amazonaws.com/Basten/Live-Any%25.lss?X-Amz-Signature=1\"}"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(url.StartsWith(TheRunClient.ValidateBaseUrl, StringComparison.Ordinal)
                ? HttpStatusCode.Forbidden
                : HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }

    [AvaloniaFact]
    public async Task Settings_Show_The_Account_Behind_The_Key()
    {
        TheRunClient offline = TheRunClient.Current;
        TheRunClient.Current = new TheRunClient(new FakeTheRun());
        try
        {
            TheRunUploadKey.Save(Key);
            var editor = new ComponentSettingsEditor(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.TheRunPath));
            await editor.TheRunValidation!;
            Dispatcher.UIThread.RunJobs();

            List<string> texts = editor.GetLogicalDescendants().OfType<TextBlock>().Select(LabelText.Of).ToList();
            Assert.Contains("Upload Key", texts);
            TextBox key = editor.GetLogicalDescendants().OfType<TextBox>().Single();
            Assert.Equal(Key, key.Text);
            Assert.Equal('•', key.PasswordChar);
            List<HyperlinkButton> links = editor.GetLogicalDescendants().OfType<HyperlinkButton>().ToList();
            Assert.Equal(new Uri("https://therun.gg/livesplit"), links[0].NavigateUri);
            HyperlinkButton user = links.Single(l => Equals(l.Content, "therun.gg/Basten"));
            Assert.True(user.IsVisible);
            Assert.Equal(new Uri("https://therun.gg/Basten"), user.NavigateUri);
            HyperlinkButton live = links.Single(l => Equals(l.Content, "Live"));
            Assert.True(live.IsVisible);
            Assert.Equal(new Uri("https://therun.gg/live/Basten"), live.NavigateUri);
            Button test = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Test"));
            Assert.Equal(Color.FromRgb(0, 150, 0), Assert.IsAssignableFrom<ISolidColorBrush>(test.Foreground).Color);

            // A wrong key, tested: "Invalid upload key." in red; leaving the box saves it.
            key.Text = "wrong";
            test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await editor.TheRunValidation!;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(links, l => Equals(l.Content, "Invalid upload key.") && l.IsVisible);
            Assert.False(live.IsVisible);
            Assert.Equal(Color.FromRgb(200, 0, 0), Assert.IsAssignableFrom<ISolidColorBrush>(test.Foreground).Color);
            key.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
            Assert.Equal("wrong", TheRunUploadKey.Read());

            List<CheckBox> checks = editor.GetLogicalDescendants().OfType<CheckBox>().ToList();
            Assert.Equal(["Enable Stats Sync", "Sync on Reset", "Enable Live Tracking", "Upload Layout Path"], checks.Select(c => c.Content as string));
            Assert.Equal([true, true, true, false], checks.Select(c => c.IsChecked == true));
            checks[1].IsChecked = false;
            checks[3].IsChecked = true;
            var settings = TheRunComponentSettings.FromInnerXml(editor.Apply().SettingsInnerXml);
            Assert.False(settings.IsUploadOnResetEnabled);
            Assert.True(settings.IsLayoutPathUploadEnabled);
        }
        finally
        {
            TheRunClient.Current = offline;
            TheRunUploadKey.Save("");
        }
    }

    [AvaloniaFact]
    public async Task The_Timer_Sends_Its_Run_And_The_Finished_Splits()
    {
        TheRunClient offline = TheRunClient.Current;
        var fake = new FakeTheRun();
        TheRunClient.Current = new TheRunClient(fake);
        string dir = HeadlessUi.NewTempDir();
        try
        {
            TheRunUploadKey.Save(Key);
            string splitsPath = Path.Combine(dir, "run.lss");
            var run = new Run { GameName = "Live", CategoryName = "Any%" };
            run.AddSegment("One");
            XmlRunSerializer.SaveFile(run, splitsPath);
            string settingsPath = Path.Combine(dir, "settings.cfg");
            var appSettings = new AppSettings();
            appSettings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
            XmlAppSettingsSerializer.SaveFile(appSettings, settingsPath);
            AppCli.Parse(["--settings", settingsPath]);
            var window = new MainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Layout layout = window.CurrentLayout.Clone();
            layout.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.TheRunPath));
            window.ApplyLayoutForTests(layout);

            window.Session.Execute(TimerCommand.StartOrSplit);
            await Task.Delay(30);
            window.Session.Execute(TimerCommand.StartOrSplit);
            for (int i = 0; i < 100 && fake.Requests.Count(r => r.Method == HttpMethod.Put) == 0; i++)
            {
                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
            }

            await Task.Delay(50);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(TimerPhase.Ended, window.Session.State.CurrentPhase);
            List<HttpMethod> methods;
            lock (fake.Requests)
            {
                methods = fake.Requests.Select(r => r.Method).ToList();
            }

            // Start and the last split post the live run; the last split then uploads the splits.
            Assert.Equal(HttpMethod.Post, methods[0]);
            Assert.Equal([HttpMethod.Get, HttpMethod.Put], methods.Where(m => m != HttpMethod.Post));
            Assert.True(methods.IndexOf(HttpMethod.Get) > methods.LastIndexOf(HttpMethod.Post));
            Assert.StartsWith("therun.gg (synced just now)", window.CurrentLayout.Components[^1].DisplayName);

            window.Session.State.Run.HasChanged = false;
            window.CurrentLayout.HasChanged = false;
            window.Close();
        }
        finally
        {
            TheRunClient.Current = offline;
            TheRunUploadKey.Save("");
            HeadlessUi.Delete(dir);
        }
    }
}
