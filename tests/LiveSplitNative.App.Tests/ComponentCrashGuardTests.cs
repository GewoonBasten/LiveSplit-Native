using System.Reflection;
using System.Runtime.Loader;
using System.Xml;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.ComponentHost.Compat;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// The guard against a downloaded component taking the app down (ComponentCrashGuard), and fonts
/// in a component's settings kept as Windows keeps them (the official Title component's settings
/// page crashed the app on a font that came back empty).
/// </summary>
[Collection(MainWindowCollection.Name)]
public class ComponentCrashGuardTests
{
    [Fact]
    public void Components_Are_Left_Off_Only_When_The_Last_Session_Did_Not_End()
    {
        string dir = HeadlessUi.NewTempDir();
        try
        {
            string marker = Path.Combine(dir, ComponentCrashGuard.MarkerName);
            string components = Path.Combine(dir, "Components");
            Directory.CreateDirectory(components);
            File.WriteAllText(Path.Combine(components, "B.dll"), "");
            File.WriteAllText(Path.Combine(components, "a.dll"), "");
            File.WriteAllText(Path.Combine(components, "notes.txt"), "");

            // A normal start: no marker.
            Assert.Empty(ComponentCrashGuard.Check(marker, components));

            // The last session's marker is still there: its DLLs, by name.
            File.WriteAllText(marker, "");
            Assert.Equal(["a.dll", "B.dll"], ComponentCrashGuard.Check(marker, components));

            // No downloaded components: nothing to leave off.
            Assert.Empty(ComponentCrashGuard.Check(marker, Path.Combine(dir, "missing")));
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }

    [Fact]
    public void The_Apps_Own_Exceptions_Are_Not_From_A_Component()
    {
        Exception ex;
        try
        {
            throw new InvalidOperationException("app");
        }
        catch (InvalidOperationException caught)
        {
            ex = caught;
        }

        Assert.False(ComponentCrashGuard.IsFromComponent(ex));
    }

    [AvaloniaFact]
    public async Task Turn_On_Loads_The_Components_Left_Off()
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        bool? before = MainWindow.AskAboutComponentsAnswerForTests;
        try
        {
            ExternalComponents.UserComponentsOff = true;
            MainWindow.AskAboutComponentsAnswerForTests = false;
            await window.AskAboutComponentsAfterCrashAsync(["X.dll"]);
            Assert.True(ExternalComponents.UserComponentsOff);

            MainWindow.AskAboutComponentsAnswerForTests = true;
            await window.AskAboutComponentsAfterCrashAsync(["X.dll"]);
            Assert.False(ExternalComponents.UserComponentsOff);
        }
        finally
        {
            ExternalComponents.UserComponentsOff = false;
            MainWindow.AskAboutComponentsAnswerForTests = before;
            window.CurrentLayout.HasChanged = false;
            window.Session.State.Run.HasChanged = false;
            window.Close();
        }
    }

    /// <summary>
    /// SettingsHelper.CreateSetting / GetFontFromElement of the look-alike LiveSplit.Core: a font
    /// is written as Windows writes it (base64 in CDATA) and read back; an empty element is null.
    /// </summary>
    [Fact]
    public void Fonts_In_Component_Settings_Come_Back()
    {
        var context = new LookAlikes(CompatComponentLoader.DefaultCompatDirectory);
        Assembly core = context.LoadFromAssemblyName(new AssemblyName("LiveSplit.Core"));
        Assembly drawing = context.LoadFromAssemblyName(new AssemblyName("System.Drawing"));
        Type helper = core.GetType("LiveSplit.UI.SettingsHelper", throwOnError: true)!;
        Type fontType = drawing.GetType("System.Drawing.Font", throwOnError: true)!;
        Type styleType = drawing.GetType("System.Drawing.FontStyle", throwOnError: true)!;
        Type unitType = drawing.GetType("System.Drawing.GraphicsUnit", throwOnError: true)!;
        object font = Activator.CreateInstance(fontType, "Segoe UI", 16f, Enum.ToObject(styleType, 1), Enum.ToObject(unitType, 2))!;

        var document = new XmlDocument();
        XmlElement parent = document.CreateElement("Settings");
        MethodInfo create = helper.GetMethod("CreateSetting", [typeof(XmlDocument), typeof(XmlElement), typeof(string), fontType])!;
        create.Invoke(null, [document, parent, "TitleFont", font]);
        XmlElement element = (XmlElement)parent["TitleFont"]!;
        Assert.IsType<XmlCDataSection>(element.FirstChild);

        MethodInfo read = helper.GetMethod("GetFontFromElement")!;
        object? back = read.Invoke(null, [element]);
        Assert.NotNull(back);
        object family = fontType.GetProperty("FontFamily")!.GetValue(back)!;
        Assert.Equal("Segoe UI", family.GetType().GetProperty("Name")!.GetValue(family));
        Assert.Equal(16f, (float)fontType.GetProperty("Size")!.GetValue(back)!);
        Assert.Equal(1, Convert.ToInt32(fontType.GetProperty("Style")!.GetValue(back)));
        Assert.Equal(2, Convert.ToInt32(fontType.GetProperty("Unit")!.GetValue(back)));

        // No font: an empty element, read as null (as on Windows).
        create.Invoke(null, [document, parent, "None", null]);
        Assert.Null(read.Invoke(null, [parent["None"]]));
    }

    /// <summary>The look-alikes in a context of their own, as CompatComponentLoader loads them.</summary>
    private sealed class LookAlikes(string compat) : AssemblyLoadContext("crash-guard-tests")
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string name = assemblyName.Name ?? "";
            if (CompatComponentLoader.LookAlikes.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return LoadFromAssemblyPath(Path.Combine(compat, name + ".dll"));
            }

            return null;
        }
    }
}
