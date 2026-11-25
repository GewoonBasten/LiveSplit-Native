using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>LiveSplit.Sound EventType: the situations a sound can be set for, in Windows' order.</summary>
public enum SoundEventType
{
    Split,
    SplitAheadGaining,
    SplitAheadLosing,
    SplitBehindGaining,
    SplitBehindLosing,
    BestSegment,
    UndoSplit,
    SkipSplit,
    PersonalBest,
    NotAPersonalBest,
    Reset,
    Pause,
    Resume,
    StartTimer,
}

public static class SoundEventTypes
{
    public static IReadOnlyList<SoundEventType> All { get; } = Enum.GetValues<SoundEventType>();

    /// <summary>EventTypeEx.GetName: the label of the type's row in the settings.</summary>
    public static string Label(this SoundEventType type) => type switch
    {
        SoundEventType.Split => "Split:",
        SoundEventType.SplitAheadGaining => "Split (Ahead, Gaining Time):",
        SoundEventType.SplitAheadLosing => "Split (Ahead, Losing Time):",
        SoundEventType.SplitBehindGaining => "Split (Behind, Gaining Time):",
        SoundEventType.SplitBehindLosing => "Split (Behind, Losing Time):",
        SoundEventType.BestSegment => "Split (Best Segment):",
        SoundEventType.UndoSplit => "Undo Split:",
        SoundEventType.SkipSplit => "Skip Split:",
        SoundEventType.PersonalBest => "Personal Best:",
        SoundEventType.NotAPersonalBest => "Not a Personal Best:",
        SoundEventType.Reset => "Reset:",
        SoundEventType.Pause => "Pause:",
        SoundEventType.Resume => "Resume:",
        SoundEventType.StartTimer => "Start Timer:",
        _ => "no name",
    };
}

/// <summary>LiveSplit.Sound SoundData: the files one of the situations plays (one at random) and its volume.</summary>
public sealed class SoundData
{
    public List<string> FilePaths { get; set; } = [];

    /// <summary>0-100.</summary>
    public int Volume { get; set; } = 100;
}

/// <summary>LiveSplit.Sound SoundSettings (settings version 1.6).</summary>
public sealed class SoundComponentSettings
{
    /// <summary>The output device's index in the device list (WaveOut device number on Windows).</summary>
    public int OutputDevice { get; set; }

    /// <summary>0-100; every sound's volume is scaled by it.</summary>
    public int GeneralVolume { get; set; } = 100;

    /// <summary>Drag and Drop Mode: Clear and Add Files (true) or Append Files.</summary>
    public bool IsClearAddDragDrop { get; set; } = true;

    public Dictionary<SoundEventType, SoundData> Sounds { get; } =
        SoundEventTypes.All.ToDictionary(type => type, _ => new SoundData());

    public static SoundComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        var settings = new SoundComponentSettings();
        if (root.ChildNodes.Count == 0)
        {
            return settings;
        }

        // SetSettings: a missing volume reads as 0 (SettingsHelper.ParseInt's default).
        foreach (SoundEventType type in SoundEventTypes.All)
        {
            settings.Sounds[type].FilePaths = ParsePathList(root[type.ToString()]);
            settings.Sounds[type].Volume = ComponentSettingsXml.Int(root, $"{type}Volume", 0);
        }

        settings.OutputDevice = ComponentSettingsXml.Int(root, nameof(OutputDevice), 0);
        settings.GeneralVolume = ComponentSettingsXml.Int(root, nameof(GeneralVolume), 100);
        settings.IsClearAddDragDrop = ComponentSettingsXml.Bool(root, nameof(IsClearAddDragDrop), true);
        return settings;
    }

    /// <summary>ParsePathListSetting: the &lt;path&gt; children, or the old single-path text.</summary>
    private static List<string> ParsePathList(XmlElement? element)
    {
        if (element is null)
        {
            return [];
        }

        var paths = new List<string>();
        foreach (XmlNode node in element.SelectNodes("./path/text()")!)
        {
            if (node is XmlText text)
            {
                paths.Add(text.Data);
            }
        }

        if (paths.Count == 0 && element.SelectSingleNode("./text()") is XmlText old)
        {
            paths.Add(old.Data);
        }

        return paths;
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.6");
        ComponentSettingsXml.SetText(doc, parent, nameof(OutputDevice), OutputDevice.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, nameof(GeneralVolume), GeneralVolume.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, nameof(IsClearAddDragDrop), IsClearAddDragDrop.ToString());
        foreach (SoundEventType type in SoundEventTypes.All)
        {
            // CreatePathListSetting: one <path> per file (an empty element without files).
            XmlElement list = doc.CreateElement(type.ToString());
            foreach (string path in Sounds[type].FilePaths)
            {
                ComponentSettingsXml.SetText(doc, list, "path", path);
            }

            parent.AppendChild(list);
            ComponentSettingsXml.SetText(doc, parent, $"{type}Volume", Sounds[type].Volume.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    });
}
