using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// LiveSplit.TheRun CollectorSettings (settings version 1.0.0). The upload key is not part of
/// the layout: it lives in its own file (<see cref="TheRunUploadKey"/>); old layouts kept it in
/// &lt;Path&gt;, which is read once and moved there.
/// </summary>
public sealed class TheRunComponentSettings
{
    /// <summary>An upload key an old layout kept in its settings (never written back).</summary>
    public string Path { get; set; } = "";

    public bool IsStatsUploadingEnabled { get; set; } = true;
    public bool IsUploadOnResetEnabled { get; set; } = true;
    public bool IsLiveTrackingEnabled { get; set; } = true;
    public bool IsLayoutPathUploadEnabled { get; set; }

    public static TheRunComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        if (root.ChildNodes.Count == 0)
        {
            return new TheRunComponentSettings();
        }

        // SetSettings: missing switches are on, except Upload Layout Path.
        return new TheRunComponentSettings
        {
            Path = ComponentSettingsXml.String(root, "Path", ""),
            IsStatsUploadingEnabled = root["IsStatsUploadingEnabled"] is null || ComponentSettingsXml.Bool(root, "IsStatsUploadingEnabled", false),
            IsUploadOnResetEnabled = root["IsUploadOnResetEnabled"] is null || ComponentSettingsXml.Bool(root, "IsUploadOnResetEnabled", false),
            IsLiveTrackingEnabled = root["IsLiveTrackingEnabled"] is null || ComponentSettingsXml.Bool(root, "IsLiveTrackingEnabled", false),
            IsLayoutPathUploadEnabled = root["IsLayoutPathUploadEnabled"] is not null && ComponentSettingsXml.Bool(root, "IsLayoutPathUploadEnabled", false),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.0.0");
        ComponentSettingsXml.SetText(doc, parent, "IsStatsUploadingEnabled", IsStatsUploadingEnabled.ToString());
        ComponentSettingsXml.SetText(doc, parent, "IsUploadOnResetEnabled", IsUploadOnResetEnabled.ToString());
        ComponentSettingsXml.SetText(doc, parent, "IsLiveTrackingEnabled", IsLiveTrackingEnabled.ToString());
        ComponentSettingsXml.SetText(doc, parent, "IsLayoutPathUploadEnabled", IsLayoutPathUploadEnabled.ToString());
    });
}

/// <summary>
/// CollectorSettings' upload key file: "Livesplit.TheRun/uploadkey.txt" in the local application
/// data folder (on a Mac ~/Library/Application Support), shared by every layout.
/// </summary>
public static class TheRunUploadKey
{
    /// <summary>The folder holding Livesplit.TheRun (the tests use a temporary one).</summary>
    public static string Folder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string FilePath => System.IO.Path.Combine(Folder, "Livesplit.TheRun", "uploadkey.txt");

    /// <summary>The saved key, or "" without one.</summary>
    public static string Read()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : "";
        }
        catch (IOException)
        {
            return "";
        }
    }

    /// <summary>SaveUploadKey: the key typed in the settings (an empty box clears it).</summary>
    public static void Save(string key)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, key);
    }

    /// <summary>GetUploadKey: an old layout's key is moved to the file first; then the file's key.</summary>
    public static string Resolve(TheRunComponentSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.Path))
        {
            string key = settings.Path;
            Save(key);
            settings.Path = "";
            return key;
        }

        return Read();
    }
}
