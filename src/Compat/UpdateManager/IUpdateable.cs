// Port of LiveSplit's UpdateManager.IUpdateable (MIT, LiveSplit/src/UpdateManager/IUpdateable.cs):
// component factories implement it for Windows' updater, which this app does not run.
using System;

namespace UpdateManager;

public interface IUpdateable
{
    string UpdateName { get; }

    string XMLURL { get; }

    string UpdateURL { get; }

    Version Version { get; }
}
