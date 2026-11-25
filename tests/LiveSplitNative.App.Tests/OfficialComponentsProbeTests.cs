using System.Drawing;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.ComponentHost.Compat;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Investigation, not a requirement: how far LiveSplit's own built-in component DLLs from the
/// official Windows release (fetched into .checks/components/LiveSplit_LiveSplit) get through the
/// look-alikes, unchanged. Each is loaded in a loader of its own (not the app's, so no other test
/// sees them), created, updated, drawn, its settings page built and its settings saved and set
/// back. The steps each one passes go to .checks/official-components.txt. Never fails; runs only
/// when .checks/official-probe exists.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class OfficialComponentsProbeTests
{
    [AvaloniaFact]
    public void Official_Built_In_Components_Through_The_Look_Alikes()
    {
        string? components = null;
        string? checks = null;
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string root = Path.Combine(dir.FullName, ".checks", "components", "LiveSplit_LiveSplit");
            if (Directory.Exists(root))
            {
                checks = Path.Combine(dir.FullName, ".checks");
                components = Directory.EnumerateDirectories(root, "Components", SearchOption.AllDirectories).FirstOrDefault();
                break;
            }
        }

        // Opt-in (create .checks/official-probe): a component that fails badly (Title) stops the
        // whole test process.
        if (components is null || checks is null || !File.Exists(Path.Combine(checks, "official-probe")))
        {
            return;
        }

        string reportPath = Path.Combine(checks, "official-components.txt");
        var report = new StringBuilder();
        void Flush(string extra = "") => File.WriteAllText(reportPath, report.ToString() + extra);
        // A component that takes the whole process down: what it threw, if it was an exception.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            File.AppendAllText(reportPath, $"UNHANDLED (terminating={e.IsTerminating}) on thread {Environment.CurrentManagedThreadId}: {e.ExceptionObject}{Environment.NewLine}");
        report.AppendLine($"== official components {DateTime.Now:yyyy-MM-dd HH:mm:ss} ({components})");
        var loader = new CompatComponentLoader(CompatComponentLoader.DefaultCompatDirectory);
        ComponentLoadReport loaded = loader.LoadDirectory(components);
        foreach ((string path, string error) in loaded.Failures)
        {
            report.AppendLine($"LOAD-FAIL {Path.GetFileName(path)}: {error}");
        }

        string dir2 = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            IHostTimerSource source = ExternalComponents.Source ?? throw new InvalidOperationException("no timer source");
            using IHostedTimer timer = loader.Runtime.CreateTimer(source);
            window.Session.Execute(TimerCommand.StartOrSplit);
            Dispatcher.UIThread.RunJobs();
            timer.Refresh();
            foreach (IHostedComponentFactory factory in loaded.Factories
                // Title stopped the whole test process: last, so the others are reported.
                .OrderBy(f => f.FileName == "LiveSplit.Title.dll")
                .ThenBy(f => f.FileName, StringComparer.Ordinal))
            {
                report.AppendLine($"-- {factory.FileName}");
                Flush();
                var steps = new List<string>();
                void Step(string step)
                {
                    steps.Add(step);
                    Flush("   reached: " + string.Join(" > ", steps) + Environment.NewLine);
                }

                string? failure = null;
                IHostedComponent? component = null;
                var canvas = new Canvas();
                try
                {
                    component = factory.Create(timer);
                    Step("create");
                    component.Update(300, 400, LayoutMode.Vertical);
                    Step("update");
                    component.DrawVertical(canvas, 300);
                    Step($"draw({canvas.Calls} calls, h={component.VerticalHeight:0})");
                    component.Update(400, 60, LayoutMode.Horizontal);
                    component.DrawHorizontal(canvas, 60);
                    Step("horizontal");
                    string xml = component.GetSettingsInnerXml();
                    Step($"settings-xml({xml.Length})");
                    component.SetSettingsInnerXml(xml);
                    Step("set-settings");
                    IHostControl? page = component.GetSettingsControl(LayoutMode.Vertical);
                    Step("got-settings-control");
                    if (page is not null)
                    {
                        var view = new HostedSettingsView(page);
                        Step($"settings-page({Count(page)} controls, {view.Width:0}x{view.Height:0})");
                    }
                    else
                    {
                        Step("no-settings-page");
                    }
                }
                catch (Exception ex)
                {
                    Exception inner = ex.GetBaseException();
                    failure = $"{inner.GetType().Name}: {inner.Message}".ReplaceLineEndings(" ");
                    string? frame = inner.StackTrace?.Split('\n').Select(l => l.Trim()).FirstOrDefault();
                    if (frame is not null)
                    {
                        failure += " @ " + frame;
                    }
                }
                finally
                {
                    try
                    {
                        component?.Dispose();
                    }
                    catch (Exception)
                    {
                        // Only the steps above count.
                    }
                }

                string texts = string.Join("|", canvas.Texts.Where(t => t.Trim().Length > 0).Distinct().Take(8));
                report.AppendLine($"{(failure is null ? "OK  " : "FAIL")} {factory.FileName} [{factory.ComponentName}] {string.Join(" > ", steps)}"
                    + (failure is null ? "" : $" !! {failure}")
                    + (texts.Length > 0 ? $" texts: {texts}" : ""));
                Flush();
            }
        }
        finally
        {
            window.Session.Execute(TimerCommand.ResetWithoutSaving);
            window.Session.State.Run.HasChanged = false;
            window.CurrentLayout.HasChanged = false;
            window.Close();
            HeadlessUi.Delete(dir2);
        }

        report.AppendLine("== done");
        Flush();
    }

    private static int Count(IHostControl control) => 1 + control.Children.Sum(Count);

    private static string NewRun()
    {
        var run = new Run { GameName = "Live", CategoryName = "Any%", AttemptCount = 2 };
        run.AddSegment("One");
        run.AddSegment("Two");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(20));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(8));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(9));
        run.AttemptHistory.Add(new Attempt(1, new SplitTime(TimeSpan.FromSeconds(20)), null, null, null));
        run.AttemptHistory.Add(new Attempt(2, default, null, null, null));
        run[0].SegmentHistory[1] = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].SegmentHistory[1] = new SplitTime(TimeSpan.FromSeconds(10));
        string dir = HeadlessUi.NewTempDir();
        string splitsPath = Path.Combine(dir, "run.lss");
        XmlRunSerializer.SaveFile(run, splitsPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var settings = new AppSettings();
        settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        return dir;
    }

    private sealed class Canvas : IHostCanvas
    {
        public List<string> Texts { get; } = [];

        public int Calls { get; private set; }

        public float DpiY => 96;

        public bool AntiAliasText => true;

        public void Fill(HostBrush brush, RectangleF rect) => Calls++;

        public void FillPolygon(HostBrush brush, IReadOnlyList<PointF> points) => Calls++;

        public void DrawLine(Color color, float width, PointF from, PointF to) => Calls++;

        public void DrawRectangle(Color color, float width, RectangleF rect) => Calls++;

        public void DrawString(string text, HostFont font, HostBrush brush, RectangleF layout, HostStringFormat format)
        {
            Calls++;
            Texts.Add(text);
        }

        public void DrawStringPath(string text, HostFont font, RectangleF layout, HostStringFormat format, HostBrush? fill, Color outline, float outlineWidth)
        {
            Calls++;
            Texts.Add(text);
        }

        public SizeF MeasureString(string text, HostFont font, float maxWidth, HostStringFormat format) =>
            new(Math.Min(maxWidth, text.Length * font.SizeInPixels * 0.5f), font.SizeInPixels * 1.33f);

        public Size MeasureText(string text, HostFont font) => new((int)(text.Length * font.SizeInPixels * 0.5f), (int)(font.SizeInPixels * 1.33f));

        public void PushClip(RectangleF rect)
        {
        }

        public void PopClip()
        {
        }
    }
}
