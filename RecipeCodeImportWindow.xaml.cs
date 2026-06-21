using System.Windows;
using RecipeManager.Models;
using RecipeManager.Services;

namespace RecipeManager;

public partial class RecipeCodeImportWindow : Window
{
    public DecodedRecipeShare? DecodedShare { get; private set; }

    public RecipeCodeImportWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            try
            {
                var clipboardText = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty;
                if (clipboardText.StartsWith("RM1:", StringComparison.OrdinalIgnoreCase))
                    CodeBox.Text = clipboardText;
            }
            catch
            {
                // Clipboard access can briefly be unavailable; users can still paste manually.
            }
            CodeBox.Focus();
            CodeBox.SelectAll();
        };
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DecodedShare = RecipeShareService.Decode(CodeBox.Text);
            DialogResult = true;
        }
        catch (FormatException ex)
        {
            ErrorText.Text = ex.Message;
            CodeBox.Focus();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
