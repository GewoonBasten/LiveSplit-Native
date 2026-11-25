// Port of LiveSplit.Core UI/RenameEventArgs.cs @ 93a11b85 (MIT), unchanged.
using System;

namespace LiveSplit.UI;

public class RenameEventArgs : EventArgs
{
    public string OldName { get; set; }
    public string NewName { get; set; }
}
