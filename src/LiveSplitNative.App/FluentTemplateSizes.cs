using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace LiveSplitNative.App;

/// <summary>
/// Fluent's CheckBox and RadioButton templates give the box (or circle) a 32 px tall area at
/// the top of the control. A value set in a template outranks every style, so the dialogs'
/// 24 px MinHeight never took: every check box and radio button was 32 px, a column of them
/// 36-38 px apart (Windows LiveSplit uses 29 px rows) and a row holding one (the Layout
/// Editor's) grew its buttons to 36 px. This sets that area to 24 px (the 20 px box with 2 px
/// around it, as tall as the fields) on every check box and radio button when its template is
/// applied. Proof 93 (93-text-audit.txt) measures the result.
/// </summary>
internal static class FluentTemplateSizes
{
    public const double ToggleMarkAreaHeight = 24;

    public static void Register()
    {
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<CheckBox>((box, e) =>
        {
            // RootGrid > Grid (Height 32) holding NormalRectangle and the check glyph.
            if (e.NameScope.Find<Grid>("RootGrid") is { } root
                && root.Children.OfType<Grid>().FirstOrDefault() is { } markArea)
            {
                markArea.Height = ToggleMarkAreaHeight;
            }
        });
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<RadioButton>((radio, e) =>
        {
            // RootBorder > Grid > Grid (Height 32) holding the ellipses.
            if (e.NameScope.Find<Border>("RootBorder")?.Child is Grid columns
                && columns.Children.OfType<Grid>().FirstOrDefault() is { } markArea)
            {
                markArea.Height = ToggleMarkAreaHeight;
            }
        });
    }
}
