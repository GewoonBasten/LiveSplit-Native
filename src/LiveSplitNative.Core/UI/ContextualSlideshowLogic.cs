namespace LiveSplitNative.Core.UI;

/// <summary>
/// Port of LiveSplit.ContextualSlideshow's ContextualSlideshowComponent (LiveSplit/
/// LiveSplit.ContextualSlideshow, 1.6, MIT): one place that shows one of its components at a
/// time, always the most relevant one. A component that changes what it shows joins the queue;
/// the first in the queue is shown. It gives way to the next after 8 seconds, or after 3 seconds
/// in which it did not change; after 12 seconds without a change of turn the component that has
/// waited longest joins again.
/// </summary>
/// <remarks>
/// The upstream component learns a change through each component's invalidation (its cached
/// texts and colors changed); here the caller tells which components changed since the last
/// update. Times are TimeStamp.Now's: a monotonic clock, passed in.
/// </remarks>
public sealed class ContextualSlideshowLogic
{
    /// <summary>SlideshowComponent.LastDequeue of each component, by index.</summary>
    private readonly TimeSpan[] _lastDequeue;
    private readonly Queue<int> _queue = new();
    private TimeSpan? _lastSwap;
    private TimeSpan? _lastInvalidation;

    /// <param name="count">How many components (upstream: Possible Time Save, Previous Segment, Run Prediction).</param>
    /// <param name="now">The clock when the component is made (each LastDequeue starts at it).</param>
    public ContextualSlideshowLogic(int count, TimeSpan now)
    {
        _lastDequeue = Enumerable.Repeat(now, count).ToArray();
    }

    /// <summary>The shown component (the first in the queue), or null when none has changed yet.</summary>
    public int? Shown => _queue.Count > 0 ? _queue.Peek() : null;

    /// <summary>Whether the place should be drawn again (the shown component changed, or another is shown).</summary>
    public bool Invalidated { get; private set; }

    /// <summary>ContextualSlideshowComponent.Update: which components changed since the last update.</summary>
    public void Update(IReadOnlyList<bool> changed, TimeSpan now)
    {
        Invalidated = false;

        // invalidateAllComponents: every component updates; one that invalidates joins the queue,
        // and the shown one keeps its turn alive.
        for (int i = 0; i < changed.Count && i < _lastDequeue.Length; i++)
        {
            if (changed[i])
            {
                Enqueue(i, now);
                if (_queue.Peek() == i)
                {
                    _lastInvalidation = now;
                    Invalidated = true;
                }
            }
        }

        // possiblySwapOutComponent.
        if (_queue.Count > 1
            && (now - (_lastSwap ?? now) > TimeSpan.FromSeconds(8)
                || now - (_lastInvalidation ?? now) > TimeSpan.FromSeconds(3)))
        {
            _lastSwap = now;
            int dequeued = _queue.Dequeue();
            _lastDequeue[dequeued] = now;
            _lastInvalidation = now;
            Invalidated = true;
        }

        // possiblyEnqueueComponentsThatDontInvalidate: the one waiting longest since its turn.
        if (now - (_lastSwap ?? now) > TimeSpan.FromSeconds(12))
        {
            int? oldest = Enumerable.Range(0, _lastDequeue.Length)
                .Where(i => !_queue.Contains(i))
                .OrderBy(i => now - _lastDequeue[i])
                .Select(i => (int?)i)
                .LastOrDefault();
            if (oldest is { } index)
            {
                Enqueue(index, now);
            }
        }
    }

    private void Enqueue(int index, TimeSpan now)
    {
        if (!_queue.Contains(index))
        {
            if (_queue.Count == 0)
            {
                _lastSwap = now;
            }

            _queue.Enqueue(index);
        }
    }
}

/// <summary>
/// Contextual Slideshow has no settings of its own on Windows (an empty &lt;Settings /&gt;).
/// LiveSplitNative keeps a background for the row (none by default, as Windows draws it), which a
/// new one takes from the row above it (Basten 28 Oct: new components join the rows above); there
/// is no settings page for it.
/// </summary>
public sealed class ContextualSlideshowComponentSettings
{
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";

    /// <summary>The components it shows, in its order (ContextualSlideshowComponent's constructor).</summary>
    public static readonly string[] ComponentPaths =
    [
        StandardLayoutFactory.PossibleTimeSavePath,
        StandardLayoutFactory.PreviousSegmentPath,
        StandardLayoutFactory.RunPredictionPath,
    ];

    public static ContextualSlideshowComponentSettings FromInnerXml(string? innerXml)
    {
        System.Xml.XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new ContextualSlideshowComponentSettings
        {
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
    });
}
