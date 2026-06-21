using System.Windows;
using System.Windows.Controls;
using RecipeManager.Services;

namespace RecipeManager;

public partial class WhatsNewWindow : Window
{
    public WhatsNewWindow(UpdateSummary summary, bool showVersionComparison = true)
    {
        InitializeComponent();
        VersionSummaryText.Text = showVersionComparison
            ? $"Updated from version {summary.FromVersion} to {summary.ToVersion}"
            : $"What's included in version {summary.ToVersion}";

        foreach (var version in summary.Versions)
        {
            ChangesPanel.Children.Add(new TextBlock
            {
                Text = $"Version {version.Version} - {version.Heading}",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrush"),
                Margin = new Thickness(0, 0, 0, 9)
            });

            foreach (var change in version.Changes)
            {
                var card = new Border
                {
                    Background = (System.Windows.Media.Brush)FindResource("MintBrush"),
                    BorderBrush = (System.Windows.Media.Brush)FindResource("MintStrongBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(13, 10, 13, 10),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                var content = new StackPanel();
                content.Children.Add(new TextBlock
                {
                    Text = change.Title,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 15,
                    Foreground = (System.Windows.Media.Brush)FindResource("DarkTextBrush")
                });
                content.Children.Add(new TextBlock
                {
                    Text = change.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0),
                    Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush")
                });
                card.Child = content;
                ChangesPanel.Children.Add(card);
            }

            ChangesPanel.Children.Add(new Border { Height = 10 });
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
