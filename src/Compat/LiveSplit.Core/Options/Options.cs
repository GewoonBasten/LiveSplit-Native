// LiveSplit.Options as components see it: LayoutSettings (the app's layout settings, read live)
// and Log.
using System;
using System.Drawing;
using LiveSplitNative.Core.UI;
using Native = LiveSplitNative.Core;

namespace LiveSplit.Options
{
    public class LayoutSettings : ICloneable
    {
        private readonly Func<Native.UI.LayoutSettings> _settings;

        internal LayoutSettings(Func<Native.UI.LayoutSettings> settings) => _settings = settings;

        private Native.UI.LayoutSettings S => _settings();

        public Color TextColor { get => C(S.TextColor); set => S.TextColor = A(value); }

        /// <summary>
        /// LiveSplitNative's Component Spacing (Layout Settings): the extra height of a one-line
        /// information row; 6 gives Windows' 31 high rows.
        /// </summary>
        internal float ComponentSpacing => S?.ComponentSpacing ?? Native.UI.LayoutSettings.DefaultComponentSpacing;

        public Color BackgroundColor { get => C(S.BackgroundColor); set => S.BackgroundColor = A(value); }

        public Color BackgroundColor2 { get => C(S.BackgroundColor2); set => S.BackgroundColor2 = A(value); }

        public Color ThinSeparatorsColor { get => C(S.ThinSeparatorsColor); set => S.ThinSeparatorsColor = A(value); }

        public Color SeparatorsColor { get => C(S.SeparatorsColor); set => S.SeparatorsColor = A(value); }

        public Color PersonalBestColor { get => C(S.PersonalBestColor); set => S.PersonalBestColor = A(value); }

        public Color AheadGainingTimeColor { get => C(S.AheadGainingTimeColor); set => S.AheadGainingTimeColor = A(value); }

        public Color AheadLosingTimeColor { get => C(S.AheadLosingTimeColor); set => S.AheadLosingTimeColor = A(value); }

        public Color BehindGainingTimeColor { get => C(S.BehindGainingTimeColor); set => S.BehindGainingTimeColor = A(value); }

        public Color BehindLosingTimeColor { get => C(S.BehindLosingTimeColor); set => S.BehindLosingTimeColor = A(value); }

        public Color BestSegmentColor { get => C(S.BestSegmentColor); set => S.BestSegmentColor = A(value); }

        public Color NotRunningColor { get => C(S.NotRunningColor); set => S.NotRunningColor = A(value); }

        public Color PausedColor { get => C(S.PausedColor); set => S.PausedColor = A(value); }

        public Color TextOutlineColor { get => C(S.TextOutlineColor); set => S.TextOutlineColor = A(value); }

        public Color ShadowsColor { get => C(S.ShadowsColor); set => S.ShadowsColor = A(value); }

        public bool ShowBestSegments { get => S.ShowBestSegments; set => S.ShowBestSegments = value; }

        public bool AlwaysOnTop { get => S.AlwaysOnTop; set => S.AlwaysOnTop = value; }

        public bool AntiAliasing { get => S.AntiAliasing; set => S.AntiAliasing = value; }

        public bool DropShadows { get => S.DropShadows; set => S.DropShadows = value; }

        public bool UseRainbowColor { get => S.UseRainbowColor; set => S.UseRainbowColor = value; }

        public float Opacity { get => S.Opacity; set => S.Opacity = value; }

        public bool MousePassThroughWhileRunning { get => S.MousePassThroughWhileRunning; set => S.MousePassThroughWhileRunning = value; }

        public float ImageOpacity { get => S.ImageOpacity; set => S.ImageOpacity = value; }

        public float ImageBlur { get => S.ImageBlur; set => S.ImageBlur = value; }

        /// <summary>The layout's fonts, in pixels (Windows' layout fonts are pixel fonts).</summary>
        public Font TimerFont => FontOf(S.TimerFontFamily, S.TimerFontSizePx, S.TimerFontBold, S.TimerFontItalic);

        public Font TimesFont => FontOf(S.TimesFontFamily, S.TimesFontSizePx, S.TimesFontBold, S.TimesFontItalic);

        public Font TextFont => FontOf(S.TextFontFamily, S.TextFontSizePx, S.TextFontBold, S.TextFontItalic);

        public object Clone() => new LayoutSettings(_settings);

        private static Font FontOf(string family, float sizePx, bool bold, bool italic) =>
            new(family, Math.Max(1, sizePx), (bold ? FontStyle.Bold : FontStyle.Regular) | (italic ? FontStyle.Italic : FontStyle.Regular), GraphicsUnit.Pixel);

        private static Color C(ArgbColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

        private static ArgbColor A(Color c) => new(c.A, c.R, c.G, c.B);
    }

    public static class Log
    {
        public static void Error(Exception ex) => Console.Error.WriteLine("[component] " + ex);

        public static void Error(string message) => Console.Error.WriteLine("[component] " + message);

        public static void Warning(string message) => Console.Error.WriteLine("[component] " + message);

        public static void Info(string message)
        {
        }
    }
}
