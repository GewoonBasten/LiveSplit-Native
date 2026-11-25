using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace LiveSplitNative.App;

/// <summary>
/// Port of LiveSplit's EditHistoryDialog (93a11b85): the recent files, newest first, with Remove
/// Selected; OK gives the history back without the removed ones (oldest first, as kept), Cancel
/// changes nothing. Empty entries (no splits / the default layout) are kept but not listed.
/// </summary>
internal sealed class EditHistoryWindow : Window
{
    private readonly List<string> _history;
    private readonly ListBox _list;

    public EditHistoryWindow(IEnumerable<string> history)
    {
        DialogChrome.ConfigureDialog(this);
        Title = "Edit History";
        Width = 420;
        MinWidth = 300;
        SizeToContent = SizeToContent.Height;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _history = [.. history.Reverse()];
        _list = new ListBox
        {
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            SelectionMode = SelectionMode.Multiple,
            Height = 200,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        // Rows with room, as the settings look's lists.
        _list.Styles.Add(new Avalonia.Styling.Style(x => x.OfType<ListBoxItem>())
        {
            Setters = { new Avalonia.Styling.Setter(ListBoxItem.PaddingProperty, new Thickness(12, 8)) },
        });
        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = _list,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("LsDialogCardBackground"));
        card.Bind(Border.BorderBrushProperty, card.GetResourceObservable("LsDialogCardBorderBrush"));

        var remove = new Button { Content = "Remove Selected", HorizontalAlignment = HorizontalAlignment.Left };
        DialogChrome.Style(remove);
        remove.Click += (_, _) => RemoveSelected();
        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        ok.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);

        Content = new DockPanel
        {
            Children =
            {
                DialogChrome.Footer(cancel, ok),
                new StackPanel
                {
                    Margin = new Thickness(24, 20),
                    Spacing = 12,
                    Children = { card, remove },
                },
            },
        };
        Fill();
    }

    /// <summary>The history after OK (oldest first, as settings.cfg keeps it).</summary>
    public IReadOnlyList<string> History => _history.AsEnumerable().Reverse().ToList();

    /// <summary>The listed entries (tests).</summary>
    internal IReadOnlyList<string> Listed => _list.Items.Cast<string>().ToList();

    /// <summary>Selects entries as a click would (tests).</summary>
    internal void SelectForTests(params string[] paths)
    {
        foreach (string path in paths)
        {
            _list.SelectedItems!.Add(path);
        }
    }

    /// <summary>btnRemove_Click: the selected entries leave the history.</summary>
    internal void RemoveSelected()
    {
        foreach (string item in _list.SelectedItems!.Cast<string>().ToList())
        {
            _history.Remove(item);
        }

        Fill();
    }

    private void Fill() => _list.ItemsSource = _history.Where(x => !string.IsNullOrEmpty(x)).ToList();
}
