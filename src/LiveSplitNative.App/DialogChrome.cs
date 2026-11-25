using Avalonia;
using Avalonia.Controls;

namespace LiveSplitNative.App;

/// <summary>
/// Dialog chrome helpers. Dialogs follow <see cref="AppThemeController"/> (System/Light/Dark);
/// the timer <see cref="MainWindow"/> keeps layout/.lsl colors independent of Appearance.
/// </summary>
internal static class DialogChrome
{
    public const string DialogClass = "dialog";
    public const string DialogSideClass = "dialogSide";

    /// <summary>~12pt LiveSplit/Mac dialog control size (not Fluent Display).</summary>
    public const double ControlFontSize = AppFonts.ControlFontSize;

    public static Button Style(Button button, bool side = false)
    {
        button.Classes.Add(side ? DialogSideClass : DialogClass);
        // The settings look: a dialog's default button (OK) in the accent color.
        if (button.IsDefault && !side)
        {
            button.Classes.Add("primary");
        }

        button.FontSize = ControlFontSize;
        return button;
    }

    public static void StyleDialogButtons(params Button[] buttons)
    {
        foreach (Button button in buttons)
        {
            Style(button);
        }
    }

    /// <summary>
    /// The settings look's footer: a bar along the bottom with the buttons on the right, given
    /// in reading order (Cancel first, the default button last). Every dialog uses this, so a
    /// new dialog gets the same footer without restyling.
    /// </summary>
    public static Border Footer(params Control[] buttons)
    {
        var row = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 10,
        };
        foreach (Control control in buttons)
        {
            if (control is Button button && !button.Classes.Contains(DialogClass))
            {
                Style(button);
            }

            row.Children.Add(control);
        }

        var footer = new Border { Child = row, [DockPanel.DockProperty] = Dock.Bottom };
        footer.Classes.Add("dialogFooter");
        return footer;
    }

    /// <summary>
    /// A message or question window in the settings look: the text on the page, the buttons
    /// in the footer (see <see cref="Footer"/>).
    /// </summary>
    public static Window Message(string title, string message, double width, params Button[] buttons)
    {
        return ConfigureDialog(new Window
        {
            Title = title,
            Width = width,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new DockPanel
            {
                Children =
                {
                    Footer(buttons),
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = ControlFontSize,
                        Margin = new Thickness(24, 20, 24, 20),
                    },
                },
            },
        });
    }

    /// <summary>
    /// <see cref="Message"/> with a control under the text (a "Don't ask again" box, say).
    /// </summary>
    public static Window MessageWith(string title, string message, double width, Control below, params Button[] buttons)
    {
        return ConfigureDialog(new Window
        {
            Title = title,
            Width = width,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new DockPanel
            {
                Children =
                {
                    Footer(buttons),
                    new StackPanel
                    {
                        Spacing = 14,
                        Margin = new Thickness(24, 20, 24, 20),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = message,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                FontSize = ControlFontSize,
                            },
                            below,
                        },
                    },
                },
            },
        });
    }

    /// <summary>
    /// Windows' InputBox in the settings look: one labelled text box in a card, Cancel and OK in
    /// the footer. The text on OK, null on Cancel. Tests answer through <see cref="PromptAnswersForTests"/>.
    /// </summary>
    public static async Task<string?> PromptAsync(Window owner, string title, string label, string initial = "", double fieldWidth = 280, double labelWidth = 110)
    {
        if (PromptAnswersForTests is { } answers)
        {
            return answers.Count > 0 ? answers.Dequeue() : null;
        }

        (Window dialog, TextBox box, Button ok, Button cancel) = PromptWindow(title, label, initial, fieldWidth, labelWidth);
        string? result = null;
        ok.Click += (_, _) =>
        {
            result = box.Text ?? "";
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        dialog.Opened += (_, _) => box.Focus();
        await OwnedDialog.ShowAsync(owner, dialog);
        return result;
    }

    /// <summary>The window <see cref="PromptAsync"/> shows (also for the proofs).</summary>
    internal static (Window Dialog, TextBox Box, Button Ok, Button Cancel) PromptWindow(
        string title, string label, string initial = "", double fieldWidth = 280, double labelWidth = 110)
    {
        (Window dialog, IReadOnlyList<TextBox> boxes, Button ok, Button cancel) =
            PromptFieldsWindow(title, [(label, initial)], fieldWidth, labelWidth);
        return (dialog, boxes[0], ok, cancel);
    }

    /// <summary>InputBox with several labelled text boxes in one card (e.g. Name and URL).</summary>
    internal static (Window Dialog, IReadOnlyList<TextBox> Boxes, Button Ok, Button Cancel) PromptFieldsWindow(
        string title, IReadOnlyList<(string Label, string Initial)> fields, double fieldWidth = 280, double labelWidth = 110)
    {
        var boxes = fields.Select(f => new TextBox { Text = f.Initial, Width = fieldWidth }).ToList();
        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var card = new SettingsCard { Margin = new Thickness(24, 20) };
        for (int i = 0; i < fields.Count; i++)
        {
            card.Children.Add(new SettingsField { Label = fields[i].Label, Field = boxes[i] });
        }

        var dialog = ConfigureDialog(new Window
        {
            Title = UiLocalizer.T(title),
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel { Children = { Footer(cancel, ok), card } },
        });
        SettingsLayout.SetLabelWidth(dialog, labelWidth);
        UiLocalizer.Apply(dialog);
        return (dialog, boxes, ok, cancel);
    }

    /// <summary>
    /// <see cref="PromptAsync"/> with several fields: their texts on OK, null on Cancel. Tests answer
    /// through <see cref="PromptFieldsAnswersForTests"/>.
    /// </summary>
    public static async Task<string[]?> PromptFieldsAsync(
        Window owner, string title, IReadOnlyList<(string Label, string Initial)> fields, double fieldWidth = 280, double labelWidth = 110)
    {
        if (PromptFieldsAnswersForTests is { } answers)
        {
            return answers.Count > 0 ? answers.Dequeue() : null;
        }

        (Window dialog, IReadOnlyList<TextBox> boxes, Button ok, Button cancel) = PromptFieldsWindow(title, fields, fieldWidth, labelWidth);
        string[]? result = null;
        ok.Click += (_, _) =>
        {
            result = [.. boxes.Select(b => b.Text ?? "")];
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        dialog.Opened += (_, _) => boxes[0].Focus();
        await OwnedDialog.ShowAsync(owner, dialog);
        return result;
    }

    /// <summary>The answers <see cref="PromptFieldsAsync"/> gives instead of asking (tests); null answers Cancel.</summary>
    internal static Queue<string[]?>? PromptFieldsAnswersForTests { get; set; }

    /// <summary>The answers <see cref="PromptAsync"/> gives instead of asking (tests); null answers Cancel.</summary>
    internal static Queue<string?>? PromptAnswersForTests { get; set; }

    /// <summary>Apply app Appearance + LiveSplit chrome font (not forced Light).</summary>
    public static T ConfigureDialog<T>(T window) where T : Window
    {
        AppThemeController.ConfigureDialog(window);
        return window;
    }

    /// <summary>Legacy name — same as <see cref="ConfigureDialog{T}"/>.</summary>
    public static T ConfigureLightDialog<T>(T window) where T : Window => ConfigureDialog(window);

    /// <summary>
    /// Prefer growing the window to content (no nested scroll clip). Cap at screen
    /// working area so huge Layout Settings pages can still scroll as last resort.
    /// </summary>
    public static void PreferFitToContentHeight(Window window, Visual? relativeTo = null)
    {
        window.SizeToContent = SizeToContent.Height;
        window.MaxHeight = MenuChrome.ResolveScreenMaxHeightDipPublic(relativeTo ?? window);
    }
}
