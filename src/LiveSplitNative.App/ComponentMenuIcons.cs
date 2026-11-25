using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>Small square icons for Layout Editor Add Component menu (LiveSplit-style).</summary>
internal static class ComponentMenuIcons
{
    public static Control? ForPath(string path) => path switch
    {
        StandardLayoutFactory.TimerPath => TimerIcon(),
        StandardLayoutFactory.DetailedTimerPath => DetailedTimerIcon(),
        StandardLayoutFactory.SubsplitsPath => SubsplitsIcon(),
        StandardLayoutFactory.SplitsPath => SplitsIcon(),
        _ => null,
    };

    /// <summary>Simple digital-timer glyph in a 16×16 square.</summary>
    public static Control TimerIcon() => IconHost(
        new TextBlock
        {
            Text = "0.0",
            FontSize = 7,
            FontWeight = FontWeight.Bold,
            FontFamily = new FontFamily(AppFonts.TimerFamily),
            Foreground = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        });

    /// <summary>
    /// Detailed Timer: main time bar + thinner segment bar (distinct from plain Timer).
    /// </summary>
    public static Control DetailedTimerIcon()
    {
        var stack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Children =
            {
                new Rectangle
                {
                    Width = 12,
                    Height = 5,
                    Fill = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
                    RadiusX = 0.5,
                    RadiusY = 0.5,
                },
                new Rectangle
                {
                    Width = 12,
                    Height = 3,
                    Fill = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                    RadiusX = 0.5,
                    RadiusY = 0.5,
                },
            },
        };
        return IconHost(stack);
    }

    /// <summary>Plain Splits: three equal list rows.</summary>
    public static Control SplitsIcon()
    {
        var stack = new StackPanel
        {
            Spacing = 1.5,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Children =
            {
                RowBar(12, 0x20),
                RowBar(12, 0x20),
                RowBar(12, 0x20),
            },
        };
        return IconHost(stack);
    }

    /// <summary>Subsplits: parent row + indented child rows (distinct from Splits).</summary>
    public static Control SubsplitsIcon()
    {
        var stack = new StackPanel
        {
            Spacing = 1.5,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Margin = new Thickness(1, 0, 0, 0),
            Children =
            {
                RowBar(12, 0x20),
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Children =
                    {
                        new Border { Width = 3 },
                        RowBar(9, 0x55),
                    },
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Children =
                    {
                        new Border { Width = 3 },
                        RowBar(9, 0x55),
                    },
                },
            },
        };
        return IconHost(stack);
    }

    private static Rectangle RowBar(double width, byte gray) => new()
    {
        Width = width,
        Height = 2.5,
        Fill = new SolidColorBrush(Color.FromRgb(gray, gray, gray)),
        RadiusX = 0.5,
        RadiusY = 0.5,
    };

    private static Border IconHost(Control child) => new()
    {
        Width = 16,
        Height = 16,
        Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xAD, 0xAD, 0xAD)),
        BorderThickness = new Thickness(1),
        Child = child,
    };
}
