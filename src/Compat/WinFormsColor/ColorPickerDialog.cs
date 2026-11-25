// Look-alike of Fetze's WinFormsColor (the color picker LiveSplit ships and components use):
// ColorPickerDialog shows the app's color picker, modal, following the color while picking.
using System;
using System.Drawing;
using System.Windows.Forms;
using LiveSplitNative.ComponentHost.Compat;

namespace Fetze.WinFormsColor;

public class ColorPickerDialog : Form
{
    private Color _selected = Color.Black;

    public event EventHandler SelectedColorChanged;

    public Color OldColor { get; set; } = Color.Black;

    public Color SelectedColor
    {
        get => _selected;
        set
        {
            if (_selected != value)
            {
                _selected = value;
                SelectedColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool AlphaEnabled { get; set; } = true;

    protected override DialogResult RunModal(IWin32Window owner)
    {
        if (HostDialogs.Current is not { } dialogs)
        {
            return DialogResult.Cancel;
        }

        Color? picked = dialogs.PickColorModal(OldColor, owner, color => SelectedColor = color);
        SelectedColor = picked ?? OldColor;
        return picked is null ? DialogResult.Cancel : DialogResult.OK;
    }
}
