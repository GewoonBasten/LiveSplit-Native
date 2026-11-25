// Minimal stand-ins for the parts of upstream LiveSplit.Core that the vendored model files
// reference but that pull in WinForms, GDI+, speedrun.com, the event log or the network. Each shim
// implements only what the vendored files touch. The timing, comparison and formatting
// logic under Upstream/ stays byte-for-byte upstream.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Xml;

namespace System.Drawing
{
    /// <summary>
    /// Upstream icons are GDI+ images; the timer logic only stores and copies the reference.
    /// StandardFormatsRunFactory decodes icons with FromStream: here every image decodes, and
    /// keeps its size so the corpus tests can tell an icon from none.
    /// </summary>
    public class Image : IDisposable
    {
        public long Length { get; private set; }

        public static Image FromStream(System.IO.Stream stream) => new() { Length = stream.Length };

        public void Dispose()
        {
        }
    }
}

namespace System.Windows.Forms
{
    /// <summary>LiveSplitState keeps a reference to the timer form; the model never uses it.</summary>
    public class Form
    {
    }
}

namespace LiveSplit.UI
{
    public interface ILayout : ICloneable
    {
    }

    public sealed class ShimLayout : ILayout
    {
        public object Clone() => new ShimLayout();
    }

    public static class SettingsHelper
    {
        public static XmlAttribute ToAttribute(XmlDocument document, string name, bool value)
        {
            XmlAttribute attribute = document.CreateAttribute(name);
            attribute.Value = value.ToString();
            return attribute;
        }

        public static XmlAttribute ToAttribute(XmlDocument document, string name, string value)
        {
            XmlAttribute attribute = document.CreateAttribute(name);
            attribute.Value = value;
            return attribute;
        }
    }
}

namespace LiveSplit.Options
{
    public interface ISettings : ICloneable
    {
        bool SimpleSumOfBest { get; set; }

        int HcpHistorySize { get; set; }

        int HcpNBestRuns { get; set; }
    }

    /// <summary>Defaults as upstream StandardSettingsFactory.</summary>
    public sealed class ShimSettings : ISettings
    {
        public bool SimpleSumOfBest { get; set; }

        public int HcpHistorySize { get; set; } = 20;

        public int HcpNBestRuns { get; set; } = 8;

        public object Clone() => new ShimSettings
        {
            SimpleSumOfBest = SimpleSumOfBest,
            HcpHistorySize = HcpHistorySize,
            HcpNBestRuns = HcpNBestRuns,
        };
    }

    public sealed class LayoutSettings : ICloneable
    {
        public object Clone() => new LayoutSettings();
    }

    /// <summary>Upstream's log (the Windows event log); the corpus tests only need it to exist.</summary>
    public static class Log
    {
        public static void Error(Exception exception) => Console.Error.WriteLine(exception.Message);
    }
}

namespace LiveSplit.Web
{
    /// <summary>Upstream's JSON helper; only the ToJson methods use it, and the tests never call them.</summary>
    public sealed class DynamicJsonObject : DynamicObject
    {
        private readonly Dictionary<string, object> _values = new();

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _values[binder.Name] = value;
            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) =>
            _values.TryGetValue(binder.Name, out result);
    }
}

namespace LiveSplit.Model.Input
{
    // Upstream declares this in Model/Input/CompositeHook.cs next to the Windows hooks.
    public delegate void EventHandlerT<T>(object sender, T value);
}

namespace LiveSplit.Model
{
    /// <summary>
    /// Upstream TimeStamp, with the clock replaced: it reads LiveSplitNative's clock source,
    /// so in tests both implementations see the same ManualTimeSource. PersistentDrift stays
    /// at 1.0 (no NTP), which is upstream's value before the first drift measurement.
    /// </summary>
    public sealed class TimeStamp
    {
        public static double PersistentDrift { get; set; } = 1.0;

        public static double NewDrift { get; set; } = 1.0;

        private readonly TimeSpan value;

        private TimeStamp(TimeSpan value)
        {
            this.value = value;
        }

        public static TimeStamp Now =>
            new(TimeSpan.FromTicks((long)(LiveSplitNative.Core.Timing.TimeStamp.Source.Now.Ticks / PersistentDrift)));

        public static bool IsSyncedWithAtomicClock => false;

        public static AtomicDateTime CurrentDateTime => new(DateTime.UtcNow, false);

        public static TimeSpan operator -(TimeStamp a, TimeStamp b) => a.value - b.value;

        public static TimeStamp operator -(TimeStamp a, TimeSpan b) => new(a.value - b);
    }

    public sealed class CustomVariable
    {
        public CustomVariable()
            : this(null, false)
        {
        }

        public CustomVariable(string value, bool isPermanent = false)
        {
            Value = value;
            IsPermanent = isPermanent;
        }

        public string Value { get; set; }

        public bool IsPermanent { get; private set; }

        public CustomVariable AsPermanent()
        {
            IsPermanent = true;
            return this;
        }
    }

    /// <summary>speedrun.com-backed metadata upstream; here only the fields the model reads.</summary>
    public sealed class RunMetadata
    {
        public RunMetadata(IRun run)
        {
        }

        public string RunID { get; set; }

        public string PlatformName { get; set; } = "";

        public string RegionName { get; set; } = "";

        public bool UsesEmulator { get; set; }

        public bool GameAvailable => false;

        public bool CategoryAvailable => false;

        public ShimGame Game => null;

        public ShimCategory Category => null;

        public ShimRegion Region => null;

        public Dictionary<string, string> VariableValueNames { get; } = new();

        public Dictionary<string, CustomVariable> CustomVariables { get; } = new();

        public CustomVariable GetOrAddCustomVariable(string name)
        {
            if (!CustomVariables.TryGetValue(name, out CustomVariable variable))
            {
                CustomVariables.Add(name, variable = new());
            }

            return variable;
        }

        public void Refresh()
        {
        }

        public RunMetadata Clone(IRun run)
        {
            var clone = new RunMetadata(run)
            {
                RunID = RunID,
                PlatformName = PlatformName,
                RegionName = RegionName,
                UsesEmulator = UsesEmulator,
            };
            foreach (KeyValuePair<string, string> pair in VariableValueNames)
            {
                clone.VariableValueNames[pair.Key] = pair.Value;
            }

            foreach (KeyValuePair<string, CustomVariable> pair in CustomVariables)
            {
                clone.CustomVariables[pair.Key] = pair.Value;
            }

            return clone;
        }
    }

    public sealed class ShimGame
    {
        public List<ShimVariable> FullGameVariables { get; } = new();

        public List<object> Regions { get; } = new();

        public List<object> Platforms { get; } = new();
    }

    public sealed class ShimVariable
    {
        public string CategoryID { get; set; }

        public string Name { get; set; }
    }

    public sealed class ShimCategory
    {
        public string ID { get; set; }
    }

    public sealed class ShimRegion
    {
        public string Abbreviation { get; set; }
    }

    public sealed class AutoSplitter
    {
        public bool IsActivated { get; set; }

        public AutoSplitter Clone() => new() { IsActivated = IsActivated };
    }
}
