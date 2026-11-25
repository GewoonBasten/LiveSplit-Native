namespace LiveSplitNative.ComponentHost;

public enum ComponentLoadStatus
{
    LoadedMetadata,
    TypesInspectable,
    FailedWinFormsOrDrawing,
    FailedNativeOrBadImage,
    FailedMissingDependency,
    FailedOther,
    SkippedNative,
}

public sealed record ComponentLoadResult(
    string Path,
    bool AssemblyLoadSucceeded,
    bool TypesInspectable,
    ComponentLoadStatus Status,
    string Detail);
