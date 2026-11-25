using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// The Auto Splitting Runtime with a real WebAssembly auto splitter (tests/fixtures/asr-test.wasm:
/// one setting "start", sets the variable "phase", starts a stopped timer). Needs libasr_capi
/// beside the tests (native/README.md); without it only the parts that need no library run.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class AutoSplittingRuntimeAppTests
{
    private static string Fixture(string name)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "tests", "fixtures", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(name);
    }

    [Fact]
    public void Component_Is_In_The_Control_Category()
    {
        StandardLayoutFactory.BuiltinComponentInfo info = StandardLayoutFactory.BuiltinCatalog.Single(c => c.Path == StandardLayoutFactory.AutoSplittingRuntimePath);
        Assert.Equal("Auto Splitting Runtime", info.DisplayName);
        Assert.Equal(StandardLayoutFactory.ComponentCategory.Control, info.Category);
        // A file-select widget's filter (WinForms form) as file types for the picker.
        var types = ComponentSettingsEditor.FileTypes("Save files|*.sav;*.srm|All files (*.*)|*.*");
        Assert.Equal(["Save files", "All files (*.*)"], types.Select(t => t.Name));
        Assert.Equal(["*.sav", "*.srm"], types[0].Patterns!);
        Assert.Equal(["*"], types[1].Patterns!);
    }

    [AvaloniaFact]
    public void Auto_Splitter_Starts_The_Timer_And_Its_Settings_Go_Into_The_Layout()
    {
        if (!AsrRuntime.IsAvailable)
        {
            Console.WriteLine("libasr_capi is not built: skipping the runtime test.");
            return;
        }

        string dir = HeadlessUi.NewTempDir();
        try
        {
            string settingsPath = Path.Combine(dir, "settings.cfg");
            XmlAppSettingsSerializer.SaveFile(new AppSettings(), settingsPath);
            AppCli.Parse(["--settings", settingsPath]);
            var window = new MainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            LiveSplitState state = window.Session.State;

            // The auto splitter with its "start" setting off: it runs but does not start.
            Layout layout = window.CurrentLayout.Clone();
            LayoutComponent component = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.AutoSplittingRuntimePath);
            component.SettingsInnerXml = new AutoSplittingRuntimeSettings
            {
                ScriptPath = Fixture("asr-test.wasm"),
                CustomSettings = [new("start", AsrSettingValue.FromBool(false))],
            }.ToInnerXml();
            layout.Components.Add(component);
            window.ApplyLayoutForTests(layout);
            AsrComponentRuntime runtime = AutoSplittingRuntimeController.For(window.CurrentLayout.Components[^1]);
            Assert.NotNull(runtime.Runtime);
            AutoSplittingRuntimeController.Current!.StepAll();
            Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);
            Assert.Equal("idle", state.Run.Metadata.CustomVariableValue("phase"));

            // Its settings widget, as the settings page shows it.
            AsrWidget widget = Assert.Single(runtime.Runtime!.GetWidgets());
            Assert.Equal(("bool", "start", "Start the run", false), (widget.Kind, widget.Key, widget.Description, widget.Bool));
            var editor = new ComponentSettingsEditor(window.CurrentLayout.Components[^1]);
            CheckBox check = editor.GetLogicalDescendants().OfType<CheckBox>().Single(c => (string?)c.Content == "Start the run");
            Assert.False(check.IsChecked);

            // Turning it on there: the next tick starts the timer, and the layout keeps it on.
            check.IsChecked = true;
            AutoSplittingRuntimeController.Current.StepAll();
            Assert.Equal(TimerPhase.Running, state.CurrentPhase);
            AutoSplittingRuntimeController.Current.SyncSettings();
            AutoSplittingRuntimeSettings saved = AutoSplittingRuntimeSettings.FromInnerXml(window.CurrentLayout.Components[^1].SettingsInnerXml);
            Assert.True(saved.CustomSettings!.Single(p => p.Key == "start").Value.Bool);

            window.Session.State.Run.HasChanged = false;
            window.CurrentLayout.HasChanged = false;
            window.Close();
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }

    [AvaloniaFact]
    public void Settings_Page_Has_Script_Path_And_Settings()
    {
        var editor = new ComponentSettingsEditor(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.AutoSplittingRuntimePath));
        List<string> texts = editor.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
        Assert.Contains("Script Path:", texts);
        Assert.Contains("Auto Splitter Settings", texts);
        Assert.Contains(editor.GetLogicalDescendants().OfType<Button>(), b => (string?)b.Content == "Browse...");
    }
}
