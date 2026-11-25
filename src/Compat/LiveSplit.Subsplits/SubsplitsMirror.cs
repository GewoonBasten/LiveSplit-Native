// LiveSplit.Subsplits as other components see it in LiveSplitState.Layout: the app's built-in
// Subsplits component under Windows' type names (SplitsComponent, whose InternalComponent's
// VisibleComponents are SplitComponents with Split, Header, CollapsedSplit, LabelsList,
// ColumnsList and Cache; ColumnData, ColumnType), so a component that looks into Windows' Subsplits
// by reflection finds it (Best World Segments' gold on a world's collapsed row). A color a
// component gives a column's label goes to the app's row (IHostSubsplits.SetColumnColor).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplitNative.ComponentHost.Compat;

namespace LiveSplit.UI.Components
{
    public enum ColumnType
    {
        Delta,
        SplitTime,
        DeltaorSplitTime,
        SegmentDelta,
        SegmentTime,
        SegmentDeltaorSegmentTime,
        CustomVariable,
    }

    public class ColumnData
    {
        public ColumnData(string name, ColumnType type, string comparison, string method)
        {
            Name = name;
            Type = type;
            Comparison = comparison;
            TimingMethod = method;
        }

        public string Name { get; set; }

        public ColumnType Type { get; set; }

        public string Comparison { get; set; }

        public string TimingMethod { get; set; }
    }

    /// <summary>The Subsplits component (mirrored): its rows are InternalComponent's VisibleComponents.</summary>
    public class SplitsComponent : IComponent
    {
        private readonly IHostSubsplits _host;
        private readonly LiveSplitState _state;

        public SplitsComponent(IHostSubsplits host, LiveSplitState state)
        {
            _host = host;
            _state = state;
        }

        /// <summary>The rows as they are now, one SplitComponent each.</summary>
        public ComponentRendererComponent InternalComponent
        {
            get
            {
                List<ColumnData> columns = _host.Columns
                    .Select(c => new ColumnData(c.Name, Enum.TryParse(c.Type, out ColumnType type) ? type : ColumnType.Delta, c.Comparison, c.TimingMethod))
                    .ToList();
                IReadOnlyList<HostSubsplitsRow> rows = _host.Rows;
                var visible = new List<IComponent>(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    visible.Add(new SplitComponent(_host, i, rows[i], _state, columns));
                }

                return new ComponentRendererComponent { VisibleComponents = visible };
            }
        }

        public string ComponentName => "Subsplits";

        public float HorizontalWidth => 0;

        public float MinimumHeight => 0;

        public float VerticalHeight => 0;

        public float MinimumWidth => 0;

        public float PaddingTop => 0;

        public float PaddingBottom => 0;

        public float PaddingLeft => 0;

        public float PaddingRight => 0;

        public IDictionary<string, Action> ContextMenuControls => null;

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
        }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
        }

        public Control GetSettingsControl(LayoutMode mode) => null;

        public XmlNode GetSettings(XmlDocument document) => document.CreateElement("Settings");

        public void SetSettings(XmlNode settings)
        {
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>One row of the Subsplits component (mirrored).</summary>
    public class SplitComponent : IComponent
    {
        public SplitComponent(IHostSubsplits host, int row, HostSubsplitsRow view, LiveSplitState state, IList<ColumnData> columns)
        {
            Split = view.SplitIndex >= 0 && view.SplitIndex < state.Run.Count ? state.Run[view.SplitIndex] : null;
            Header = view.Header;
            CollapsedSplit = view.CollapsedSplit;
            ColumnsList = columns;
            LabelsList = new List<SimpleLabel>();
            for (int c = 0; c < columns.Count; c++)
            {
                int column = c;
                var label = new SimpleLabel { ForeColor = c < view.ColumnColors.Count ? view.ColumnColors[c] : Color.White };
                label.ForeColorSet = color => host.SetColumnColor(row, column, color);
                LabelsList.Add(label);
            }
        }

        public ISegment Split { get; set; }

        public bool Header { get; set; }

        public bool CollapsedSplit { get; set; }

        public IList<SimpleLabel> LabelsList { get; set; }

        public IEnumerable<ColumnData> ColumnsList { get; set; }

        public GraphicsCache Cache { get; set; } = new();

        public string ComponentName => "Split";

        public float HorizontalWidth => 0;

        public float MinimumHeight => 0;

        public float VerticalHeight => 0;

        public float MinimumWidth => 0;

        public float PaddingTop => 0;

        public float PaddingBottom => 0;

        public float PaddingLeft => 0;

        public float PaddingRight => 0;

        public IDictionary<string, Action> ContextMenuControls => null;

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
        }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
        }

        public Control GetSettingsControl(LayoutMode mode) => null;

        public XmlNode GetSettings(XmlDocument document) => document.CreateElement("Settings");

        public void SetSettings(XmlNode settings)
        {
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
        }

        public void Dispose()
        {
        }
    }
}
