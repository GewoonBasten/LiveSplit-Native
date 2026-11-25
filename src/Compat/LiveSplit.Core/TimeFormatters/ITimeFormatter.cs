// Port of LiveSplit.Core TimeFormatters/ITimeFormatter.cs @ 93a11b85 (MIT).
using System;

namespace LiveSplit.TimeFormatters;

public interface ITimeFormatter
{
    string Format(TimeSpan? time);
}

public static class TimeFormatConstants
{
    public static readonly string MINUS = "−";
    public static readonly string DASH = "-";
}
