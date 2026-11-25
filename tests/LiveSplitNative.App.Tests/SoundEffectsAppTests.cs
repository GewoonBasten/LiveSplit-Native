using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Sound Effects (LiveSplit.Sound, 93a11b85): its settings page and the sounds the timer plays.</summary>
[Collection(MainWindowCollection.Name)]
public class SoundEffectsAppTests
{
    private sealed class RecordingPlayer : ISoundPlayer
    {
        public List<string> Played { get; } = [];

        public void Stop()
        {
        }

        public void Play(string path, float volume, int outputDevice) => Played.Add(Path.GetFileName(path));
    }

    [AvaloniaFact]
    public void Settings_Have_Sound_Files_And_Volume_Mixer_Like_Windows()
    {
        Func<IReadOnlyList<string>> devices = ComponentSettingsEditor.SoundOutputDevices;
        ComponentSettingsEditor.SoundOutputDevices = () => ["Built-in Speakers", "Headphones"];
        try
        {
            var editor = new ComponentSettingsEditor(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.SoundPath));
            int notified = 0;
            editor.SettingsChanged = () => notified++;
            TabControl tabs = editor.GetLogicalDescendants().OfType<TabControl>().Single();
            Assert.Equal(["Sound Files", "Volume Mixer"], tabs.Items.OfType<TabItem>().Select(t => ((TextBlock)t.Header!).Text));
            List<string> labels = editor.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains("Drag and Drop Mode:", labels);
            Assert.Contains("Output Device:", labels);
            Assert.Contains("General Volume:", labels);
            foreach (SoundEventType type in SoundEventTypes.All)
            {
                // A files row and a volume row each.
                Assert.Equal(2, labels.Count(l => l == type.Label()));
            }

            editor.SetSoundFilesForTests(SoundEventType.Split, true, "/s/one.wav", "/s/two.wav");
            Assert.True(notified > 0);
            TextBox box = editor.GetLogicalDescendants().OfType<TextBox>().First();
            Assert.Equal("/s/one.wav, /s/two.wav", box.Text);

            // Dropped files: Clear and Add Files replaces, Append Files adds new ones.
            editor.SetSoundFilesForTests(SoundEventType.Split, null, "/s/three.wav");
            Assert.Equal("/s/three.wav", box.Text);
            editor.GetLogicalDescendants().OfType<RadioButton>().Single(r => Equals(r.Content, "Append Files")).IsChecked = true;
            editor.SetSoundFilesForTests(SoundEventType.Split, null, "/s/three.wav", "/s/four.wav");
            Assert.Equal("/s/three.wav, /s/four.wav", box.Text);

            ComboBox device = editor.GetLogicalDescendants().OfType<ComboBox>().Single();
            Assert.Equal(0, device.SelectedIndex);
            device.SelectedIndex = 1;
            List<Slider> sliders = editor.GetLogicalDescendants().OfType<Slider>().ToList();
            Assert.Equal(1 + SoundEventTypes.All.Count, sliders.Count);
            sliders[0].Value = 60;
            sliders[1].Value = 25;

            SoundComponentSettings settings = SoundComponentSettings.FromInnerXml(editor.Apply().SettingsInnerXml);
            Assert.Equal(["/s/three.wav", "/s/four.wav"], settings.Sounds[SoundEventType.Split].FilePaths);
            Assert.False(settings.IsClearAddDragDrop);
            Assert.Equal(1, settings.OutputDevice);
            Assert.Equal(60, settings.GeneralVolume);
            Assert.Equal(25, settings.Sounds[SoundEventType.Split].Volume);
        }
        finally
        {
            ComponentSettingsEditor.SoundOutputDevices = devices;
        }
    }

    [AvaloniaFact]
    public void Timer_Events_Play_Their_Sounds()
    {
        Func<ISoundPlayer> create = SoundEffectsController.CreatePlayer;
        var player = new RecordingPlayer();
        SoundEffectsController.CreatePlayer = () => player;
        string dir = HeadlessUi.NewTempDir();
        try
        {
            string settingsPath = Path.Combine(dir, "settings.cfg");
            XmlAppSettingsSerializer.SaveFile(new AppSettings(), settingsPath);
            AppCli.Parse(["--settings", settingsPath]);
            var window = new MainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var sounds = new SoundComponentSettings();
            foreach (SoundEventType type in new[] { SoundEventType.StartTimer, SoundEventType.Pause, SoundEventType.Resume, SoundEventType.Reset })
            {
                string file = Path.Combine(dir, type + ".wav");
                File.WriteAllBytes(file, [0]);
                sounds.Sounds[type].FilePaths = [file];
            }

            sounds.Sounds[SoundEventType.Split].FilePaths = [Path.Combine(dir, "missing.wav")];
            window.CurrentLayout.Components.Add(new LayoutComponent(StandardLayoutFactory.SoundPath, sounds.ToInnerXml()));

            window.Session.Execute(TimerCommand.StartOrSplit);
            window.Session.Execute(TimerCommand.Pause);
            window.Session.Execute(TimerCommand.Pause);
            window.Session.Execute(TimerCommand.ResetWithoutSaving);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["StartTimer.wav", "Pause.wav", "Resume.wav", "Reset.wav"], player.Played);
            Assert.Equal(TimerPhase.NotRunning, window.Session.State.CurrentPhase);

            window.CurrentLayout.HasChanged = false;
            window.Session.State.Run.HasChanged = false;
            window.Close();
        }
        finally
        {
            SoundEffectsController.CreatePlayer = create;
            HeadlessUi.Delete(dir);
        }
    }
}
