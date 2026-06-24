using System.Windows;
using System.Windows.Controls;

namespace RecipeManager;

public enum RecipeImportSource
{
    Website,
    Text
}

public partial class RecipeImportWindow : Window
{
    public RecipeImportSource Source { get; private set; } = RecipeImportSource.Website;
    public string ImportText { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;

    public RecipeImportWindow()
    {
        InitializeComponent();
        SourceTypeBox.SelectedIndex = 0;
        Loaded += (_, _) =>
        {
            TryPrefillClipboard();
            RefreshSourceView();
        };
    }

    private void SourceTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshSourceView();

    private void RefreshSourceView()
    {
        Source = SourceTypeBox.SelectedIndex switch
        {
            1 => RecipeImportSource.Text,
            _ => RecipeImportSource.Website
        };

        UrlPanel.Visibility = Source == RecipeImportSource.Website ? Visibility.Visible : Visibility.Collapsed;
        TextPanel.Visibility = Source == RecipeImportSource.Text ? Visibility.Visible : Visibility.Collapsed;
        TextPanelLabel.Text = "Recipe text or sharing code";
        HelpText.Text = Source switch
        {
            RecipeImportSource.Website => "Enter a recipe page URL. If the site blocks reading or is messy, paste the recipe text instead.",
            RecipeImportSource.Text => "Paste recipe text copied from a website, PDF, chat message, email, note, or paste an RM1 sharing code.",
            _ => string.Empty
        };
        ErrorText.Text = string.Empty;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        ImportText = TextImportBox.Text.Trim();
        Url = UrlBox.Text.Trim();

        if (Source == RecipeImportSource.Website)
        {
            if (Url.Length == 0)
            {
                ErrorText.Text = "Enter a recipe website URL first.";
                return;
            }
        }
        else if (ImportText.Length == 0)
        {
            ErrorText.Text = "Paste recipe text or an RM1 sharing code first.";
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TryPrefillClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText()) return;
            var clipboardText = Clipboard.GetText().Trim();
            if (clipboardText.Length == 0) return;
            if (clipboardText.StartsWith("RM1-BEGIN:", StringComparison.OrdinalIgnoreCase)
                || clipboardText.StartsWith("RM1:", StringComparison.OrdinalIgnoreCase))
            {
                SourceTypeBox.SelectedIndex = 1;
                TextImportBox.Text = clipboardText;
            }
            else if (clipboardText.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || clipboardText.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                SourceTypeBox.SelectedIndex = 0;
                UrlBox.Text = clipboardText;
            }
            else if (clipboardText.Length > 40)
            {
                SourceTypeBox.SelectedIndex = 1;
                TextImportBox.Text = clipboardText;
            }
        }
        catch
        {
            // Clipboard access can briefly be unavailable; users can still paste manually.
        }
    }
}
