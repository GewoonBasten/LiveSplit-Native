using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplitNative.Core.Racing;

namespace LiveSplitNative.App;

/// <summary>
/// Port of LiveSplit.View RaceProviderManagingDialog @ 93a11b85. It works on the list it is
/// given (Settings opens it with copies): checking a service enables it at once, selecting one
/// shows its Website / Rules links (a label is hidden when its link is empty) and its settings.
/// OK closes with true, Cancel and closing with false.
/// </summary>
public partial class RaceProvidersWindow : Window
{
    private readonly IList<RaceProviderSettings> _settings;
    private readonly List<CheckBox> _boxes = [];

    public RaceProvidersWindow()
        : this(RaceProviderSettings.CreateStandard())
    {
    }

    public RaceProvidersWindow(IList<RaceProviderSettings> settings)
    {
        InitializeComponent();
        DialogChrome.ConfigureDialog(this);
        _settings = settings;

        var rows = new List<Control>();
        foreach (RaceProviderSettings provider in _settings)
        {
            var box = new CheckBox { IsChecked = provider.Enabled, VerticalAlignment = VerticalAlignment.Center, MinWidth = 0 };
            RaceProviderSettings captured = provider;
            int index = _boxes.Count;
            box.IsCheckedChanged += (_, _) =>
            {
                // providerListBox_ItemCheck; the row is selected too.
                captured.Enabled = box.IsChecked == true;
                ProviderList.SelectedIndex = index;
            };
            _boxes.Add(box);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            row.Children.Add(box);
            row.Children.Add(new TextBlock { Text = provider.DisplayName, VerticalAlignment = VerticalAlignment.Center });
            rows.Add(row);
        }

        ProviderList.ItemsSource = rows;
        ProviderList.SelectionChanged += (_, _) => OnSelectionChanged();
    }

    /// <summary>The list being edited.</summary>
    public IList<RaceProviderSettings> Settings => _settings;

    /// <summary>Selects a service, as clicking its name does (tests and proofs).</summary>
    public void Select(int index) => ProviderList.SelectedIndex = index;

    /// <summary>Checks or unchecks a service, as clicking its box does (tests and proofs).</summary>
    public void SetChecked(int index, bool isChecked) => _boxes[index].IsChecked = isChecked;

    // providerListBox_SelectedIndexChanged.
    private void OnSelectionChanged()
    {
        int index = ProviderList.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        RaceProviderSettings provider = _settings[index];
        WebsiteLink.Content = provider.WebsiteLink;
        RulesLink.Content = provider.RulesLink;
        SettingsPanel.Children.Clear();
        SettingsPanel.Children.Add(CreateSettingsControl(provider));
        WebsiteTextLabel.IsVisible = !string.IsNullOrEmpty(provider.WebsiteLink);
        RulesTextLabel.IsVisible = !string.IsNullOrEmpty(provider.RulesLink);
    }

    /// <summary>
    /// GetSettingsControl: none of the services has options. The base class centers its label at
    /// the top; RacetimeSettingsControl's starts at the left; an unloaded plugin's is red.
    /// </summary>
    private static Control CreateSettingsControl(RaceProviderSettings provider) => provider switch
    {
        UnloadedRaceProviderSettings => SettingsLabel(RaceProviderSettings.NotLoadedText, TextAlignment.Center, error: true),
        RacetimeSettings => SettingsLabel(RaceProviderSettings.NoOptionsText, TextAlignment.Left, error: false),
        _ => SettingsLabel(RaceProviderSettings.NoOptionsText, TextAlignment.Center, error: false),
    };

    private static TextBlock SettingsLabel(string text, TextAlignment alignment, bool error)
    {
        var label = new TextBlock
        {
            Text = text,
            TextAlignment = alignment,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (error)
        {
            label.Bind(TextBlock.ForegroundProperty, label.GetResourceObservable("LsDialogErrorForeground"));
        }

        return label;
    }

    private void OnLinkClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Content: string { Length: > 0 } url })
        {
            RacetimeController.OpenBrowser(url);
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
