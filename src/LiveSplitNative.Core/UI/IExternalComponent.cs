namespace LiveSplitNative.Core.UI;

/// <summary>
/// A component the app does not draw itself (a Windows component DLL loaded through the
/// component host), kept on <see cref="LayoutComponent.RuntimeState"/>: what the layout needs of
/// it (its name and ComponentRenderer's sizes).
/// </summary>
public interface IExternalComponent
{
    string ComponentName { get; }

    double VerticalHeight { get; }

    double HorizontalWidth { get; }

    double PaddingTop { get; }

    double PaddingBottom { get; }

    double PaddingLeft { get; }

    double PaddingRight { get; }
}
