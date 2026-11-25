// LiveSplit.Model as components see it (ports of LiveSplit.Core @ 93a11b85, MIT): Time, Attempt,
// SegmentHistory, ISegment / Segment, IRun / Run, the enums and EventHandlerT. The app's run is
// mirrored into these (Compat/RunMirror) whenever it changes.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Xml;
using LiveSplit.Model.Comparisons;

namespace LiveSplit.Model
{
    public enum TimerPhase
    {
        NotRunning,
        Running,
        Ended,
        Paused,
    }

    public enum TimingMethod
    {
        RealTime,
        GameTime,
    }

    public struct Time
    {
        public static readonly Time Zero = new(TimeSpan.Zero, TimeSpan.Zero);

        public Time(TimeSpan? realTime = null, TimeSpan? gameTime = null)
            : this()
        {
            RealTime = realTime;
            GameTime = gameTime;
        }

        public Time(TimingMethod method, TimeSpan? time)
            : this() => this[method] = time;

        public Time(Time time)
            : this()
        {
            RealTime = time.RealTime;
            GameTime = time.GameTime;
        }

        public TimeSpan? RealTime { get; set; }

        public TimeSpan? GameTime { get; set; }

        public TimeSpan? this[TimingMethod method]
        {
            readonly get => method == TimingMethod.RealTime ? RealTime : GameTime;
            set
            {
                if (method == TimingMethod.RealTime)
                {
                    RealTime = value;
                }
                else
                {
                    GameTime = value;
                }
            }
        }

        public static Time FromXml(XmlElement element)
        {
            var newTime = new Time();
            if (element["RealTime"] is { } real && TimeSpan.TryParse(real.InnerText, out TimeSpan x))
            {
                newTime.RealTime = x;
            }

            if (element["GameTime"] is { } game && TimeSpan.TryParse(game.InnerText, out x))
            {
                newTime.GameTime = x;
            }

            return newTime;
        }

        public readonly XmlElement ToXml(XmlDocument document, string name = "Time")
        {
            XmlElement parent = document.CreateElement(name);
            if (RealTime != null)
            {
                XmlElement realTime = document.CreateElement("RealTime");
                realTime.InnerText = RealTime.ToString();
                parent.AppendChild(realTime);
            }

            if (GameTime != null)
            {
                XmlElement gameTime = document.CreateElement("GameTime");
                gameTime.InnerText = GameTime.ToString();
                parent.AppendChild(gameTime);
            }

            return parent;
        }

        public static Time ParseText(string text)
        {
            string[] splits = text.Split('|');
            var newTime = new Time { RealTime = TimeSpan.TryParse(splits[0].TrimEnd(), out TimeSpan x) ? x : null };
            if (splits.Length > 1)
            {
                newTime.GameTime = TimeSpan.TryParse(splits[1].TrimStart(), out x) ? x : null;
            }

            return newTime;
        }

        public override readonly string ToString() => $"{RealTime} | {GameTime}";

        public static Time operator +(Time a, Time b) => new(a.RealTime + b.RealTime, a.GameTime + b.GameTime);

        public static Time operator -(Time a, Time b) => new(a.RealTime - b.RealTime, a.GameTime - b.GameTime);
    }

    public struct AtomicDateTime
    {
        public AtomicDateTime(DateTime time, bool syncedWithAtomicClock)
        {
            Time = time;
            SyncedWithAtomicClock = syncedWithAtomicClock;
        }

        public DateTime Time { get; }

        public bool SyncedWithAtomicClock { get; }

        public static TimeSpan operator -(AtomicDateTime a, AtomicDateTime b) => a.Time - b.Time;

        public static TimeSpan operator -(AtomicDateTime a, DateTime b) => a.Time - b;

        public static TimeSpan operator -(DateTime a, AtomicDateTime b) => a - b.Time;
    }

    public struct Attempt
    {
        public Attempt(int index, Time time, AtomicDateTime? started, AtomicDateTime? ended, TimeSpan? pauseTime)
            : this()
        {
            Index = index;
            Time = time;
            Started = started;
            Ended = ended;
            PauseTime = pauseTime;
        }

        public int Index { get; set; }

        public Time Time { get; set; }

        public AtomicDateTime? Started { get; set; }

        public AtomicDateTime? Ended { get; set; }

        public TimeSpan? PauseTime { get; set; }

        public readonly TimeSpan? Duration => Ended.HasValue && Started.HasValue ? Ended - Started : Time.RealTime;
    }

    public class SegmentHistory : Dictionary<int, Time>, ICloneable
    {
        public SegmentHistory(SegmentHistory history)
            : base(history)
        {
        }

        public SegmentHistory()
        {
        }

        public SegmentHistory Clone() => new(this);

        object ICloneable.Clone() => Clone();

        public int GetMinIndex() => Count > 0 ? Math.Min(this.Min(x => x.Key), 1) : 1;
    }

    public interface ISegment : ICloneable
    {
        Image Icon { get; set; }

        string Name { get; set; }

        Time PersonalBestSplitTime { get; set; }

        IComparisons Comparisons { get; set; }

        Time BestSegmentTime { get; set; }

        Time SplitTime { get; set; }

        SegmentHistory SegmentHistory { get; set; }

        Dictionary<string, string> CustomVariableValues { get; }
    }

    public class Segment : ISegment
    {
        public Segment(string name, Time pbSplitTime = default, Time bestSegmentTime = default, Image icon = null, Time splitTime = default)
        {
            Comparisons = new CompositeComparisons();
            Name = name;
            PersonalBestSplitTime = pbSplitTime;
            BestSegmentTime = bestSegmentTime;
            SplitTime = splitTime;
            Icon = icon;
            SegmentHistory = [];
            CustomVariableValues = [];
        }

        public Image Icon { get; set; }

        public string Name { get; set; }

        public Time PersonalBestSplitTime
        {
            get => Comparisons[Run.PersonalBestComparisonName];
            set => Comparisons[Run.PersonalBestComparisonName] = value;
        }

        public IComparisons Comparisons { get; set; }

        public Time BestSegmentTime { get; set; }

        public Time SplitTime { get; set; }

        public SegmentHistory SegmentHistory { get; set; }

        public Dictionary<string, string> CustomVariableValues { get; }

        public Segment Clone() => new(Name, PersonalBestSplitTime, BestSegmentTime, Icon, SplitTime)
        {
            Comparisons = (IComparisons)Comparisons.Clone(),
            SegmentHistory = SegmentHistory.Clone(),
        };

        object ICloneable.Clone() => Clone();
    }

    public class RunMetadata
    {
        public string RunID { get; set; }

        public string PlatformName { get; set; } = "";

        public string RegionName { get; set; } = "";

        public bool UsesEmulator { get; set; }

        public Dictionary<string, string> VariableValueNames { get; set; } = [];

        public bool GameAvailable => false;

        public bool CategoryAvailable => false;

        /// <summary>The run's custom variables' values by name (the app's run's permanent ones).</summary>
        internal Dictionary<string, string> CustomVariables { get; } = new(StringComparer.Ordinal);

        /// <summary>RunMetadata.CustomVariableValue: the variable's value; empty when the run has none of that name.</summary>
        public string CustomVariableValue(string name) =>
            name is not null && CustomVariables.TryGetValue(name, out string value) ? value ?? "" : "";
    }

    public interface IRun : IList<ISegment>, ICloneable, INotifyPropertyChanged
    {
        Image GameIcon { get; set; }

        string GameName { get; set; }

        string CategoryName { get; set; }

        TimeSpan Offset { get; set; }

        int AttemptCount { get; set; }

        IList<Attempt> AttemptHistory { get; set; }

        XmlElement AutoSplitterSettings { get; set; }

        IList<string> CustomComparisons { get; set; }

        IEnumerable<string> Comparisons { get; }

        RunMetadata Metadata { get; }

        bool HasChanged { get; set; }

        string FilePath { get; set; }

        string LayoutPath { get; set; }
    }

    public class Run : IRun
    {
        public const string PersonalBestComparisonName = "Personal Best";

        private readonly List<ISegment> _segments = [];

        public Run()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Image GameIcon { get; set; }

        public string GameName { get; set; } = "";

        public string CategoryName { get; set; } = "";

        public TimeSpan Offset { get; set; }

        public int AttemptCount { get; set; }

        public IList<Attempt> AttemptHistory { get; set; } = [];

        public XmlElement AutoSplitterSettings { get; set; }

        public IList<string> CustomComparisons { get; set; } = [PersonalBestComparisonName];

        public IList<string> GeneratedComparisons { get; set; } = [];

        public IEnumerable<string> Comparisons => CustomComparisons.Concat(GeneratedComparisons);

        public RunMetadata Metadata { get; } = new();

        public bool HasChanged { get; set; }

        public string FilePath { get; set; }

        public string LayoutPath { get; set; }

        public int Count => _segments.Count;

        public bool IsReadOnly => false;

        public ISegment this[int index]
        {
            get => _segments[index];
            set => _segments[index] = value;
        }

        public int IndexOf(ISegment item) => _segments.IndexOf(item);

        public void Insert(int index, ISegment item) => _segments.Insert(index, item);

        public void RemoveAt(int index) => _segments.RemoveAt(index);

        public void Add(ISegment item) => _segments.Add(item);

        public void Clear() => _segments.Clear();

        public bool Contains(ISegment item) => _segments.Contains(item);

        public void CopyTo(ISegment[] array, int arrayIndex) => _segments.CopyTo(array, arrayIndex);

        public bool Remove(ISegment item) => _segments.Remove(item);

        public IEnumerator<ISegment> GetEnumerator() => _segments.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _segments.GetEnumerator();

        public Run Clone()
        {
            var clone = new Run
            {
                GameName = GameName,
                CategoryName = CategoryName,
                Offset = Offset,
                AttemptCount = AttemptCount,
                AttemptHistory = [.. AttemptHistory],
                CustomComparisons = [.. CustomComparisons],
                GeneratedComparisons = [.. GeneratedComparisons],
                HasChanged = HasChanged,
                FilePath = FilePath,
                LayoutPath = LayoutPath,
                Source = Source,
            };
            foreach ((string name, string value) in Metadata.CustomVariables)
            {
                clone.Metadata.CustomVariables[name] = value;
            }

            foreach (ISegment segment in _segments)
            {
                clone.Add((ISegment)segment.Clone());
            }

            return clone;
        }

        object ICloneable.Clone() => Clone();

        /// <summary>The app's run this one mirrors (SumOfBest works on it); null for a run a component made.</summary>
        internal LiveSplitNative.Core.Model.Run Source { get; set; }

        internal void RaisePropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class RunExtensions
    {
        public static string GetExtendedName(this IRun run, bool useExtendedCategoryName = true)
        {
            string category = useExtendedCategoryName ? run.GetExtendedCategoryName() : run.CategoryName;
            return string.Join(" - ", new[] { run.GameName, category }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        public static string GetExtendedCategoryName(this IRun run, bool showRegion = false, bool showPlatform = false, bool showVariables = true, bool waitForOnlineData = false) =>
            run.CategoryName ?? "";

        public static int GetMinSegmentHistoryIndex(this IRun run) =>
            run.Count == 0 ? 1 : run.Min(segment => segment.SegmentHistory.GetMinIndex());
    }
}

namespace LiveSplit.Model.Input
{
    public delegate void EventHandlerT<T>(object sender, T value);
}

namespace LiveSplit.Model.Comparisons
{
    public interface IComparisons : IDictionary<string, Time>, ICloneable
    {
    }

    public class CompositeComparisons : Dictionary<string, Time>, IComparisons
    {
        /// <summary>CompositeComparisons.AddShortComparisonName: the app keeps the short names (racetime.gg's racers, say).</summary>
        public static void AddShortComparisonName(string longName, string shortName) =>
            LiveSplitNative.Core.Model.Comparisons.StandardComparisonGeneratorsFactory.AddShortComparisonName(longName, shortName);

        /// <summary>CompositeComparisons.GetShortComparisonName: "Best Segments" is "Best Segments", a race's comparisons the racer's name.</summary>
        public static string GetShortComparisonName(string longName) =>
            LiveSplitNative.Core.Model.Comparisons.StandardComparisonGeneratorsFactory.GetShortComparisonName(longName);

        public new Time this[string name]
        {
            get => TryGetValue(name, out Time time) ? time : default;
            set => base[name] = value;
        }

        Time IDictionary<string, Time>.this[string key]
        {
            get => this[key];
            set => this[key] = value;
        }

        public object Clone()
        {
            var clone = new CompositeComparisons();
            foreach (KeyValuePair<string, Time> pair in this)
            {
                clone[pair.Key] = pair.Value;
            }

            return clone;
        }
    }
}
