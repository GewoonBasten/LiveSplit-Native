namespace LiveSplitNative.Core.Options;

/// <summary>Port of LiveSplit.Options.ServerStartupType (Settings > Server > Startup Behavior).</summary>
public enum ServerStartupType
{
    Off = 0,
    TCP,
    Websocket,
    PreviousState,
}

/// <summary>Port of LiveSplit.Options.ServerStateType: which server is running.</summary>
public enum ServerStateType
{
    Off = 0,
    TCP,
    Websocket,
}
