using System.Windows;
using System.IO;
using System.Globalization;
using Microsoft.Win32;
using System.Windows.Media.Imaging;
using RecipeManager.Models;
using RecipeManager.Services;

namespace RecipeManager;

public partial class RecipeEditorWindow : Window
{
    private readonly List<IngredientChoice> _ingredientChoices = [];
    public Recipe Recipe { get; }

    public RecipeEditorWindow(IEnumerable<IngredientDefinition> ingredientLibrary, Recipe? recipe = null, string? photoOcrText = null)
    {
        InitializeComponent();
        Recipe = recipe is null
            ? new Recipe()
            : new Recipe
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Cuisine = recipe.Cuisine,
                CookingTimeMinutes = recipe.CookingTimeMinutes,
                Servings = recipe.Servings,
                Instructions = recipe.Instructions,
                IsFavorite = recipe.IsFavorite,
                SourceUrl = recipe.SourceUrl,
                ImageData = recipe.ImageData is null ? null : [.. recipe.ImageData],
                Ingredients = recipe.Ingredients.Select(x => new RecipeIngredient
                {
                    Name = x.Name,
                    Quantity = x.Quantity,
                    Unit = x.Unit
                }).ToList(),
                Tools = [.. recipe.Tools],
                Tags = [.. recipe.Tags]
            };

        var ingredientDefinitions = ingredientLibrary.ToList();
        foreach (var recipeIngredient in Recipe.Ingredients)
        {
            var knownIngredient = ingredientDefinitions.FirstOrDefault(definition =>
                BilingualSearchService.MatchesIngredient(definition, recipeIngredient.Name));
            if (knownIngredient is not null)
                recipeIngredient.Name = knownIngredient.Name;
        }

        var selectedIngredients = Recipe.Ingredients
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var libraryNames = ingredientDefinitions.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var externalIngredientNames = Recipe.Ingredients
            .Select(item => item.Name)
            .Where(name => !libraryNames.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var availableIngredients = ingredientDefinitions
            .Concat(Recipe.Ingredients
                .Where(item => !ingredientDefinitions.Any(definition => definition.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(item => new IngredientDefinition { Name = item.Name }))
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
        _ingredientChoices = availableIngredients
            .Select(x => new IngredientChoice
            {
                Name = x.Name,
                PluralName = x.PluralName,
                Aliases = x.Aliases,
                Season = x.Season,
                Category = x.Category,
                IsInLibrary = libraryNames.Contains(x.Name),
                IsSelected = selectedIngredients.ContainsKey(x.Name),
                QuantityText = selectedIngredients.TryGetValue(x.Name, out var existing) && existing.Quantity.HasValue
                    ? existing.Quantity.Value.ToString("0.##", CultureInfo.CurrentCulture)
                    : string.Empty,
                Unit = selectedIngredients.TryGetValue(x.Name, out existing) ? existing.Unit : string.Empty
            })
            .OrderBy(choice => choice.IsInLibrary ? 1 : 0)
            .ThenBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (externalIngredientNames.Count > 0)
        {
            ExternalIngredientsNotice.Text =
                $"New ingredients from this import are shown first: {string.Join(", ", externalIngredientNames)}.";
            ExternalIngredientsNotice.Visibility = Visibility.Visible;
        }

        Title = recipe is null ? "Add recipe" : "Edit recipe";
        TitleBox.Text = Recipe.Title;
        CuisineBox.Text = Recipe.Cuisine;
        CookingTimeBox.Text = Recipe.CookingTimeMinutes > 0 ? Recipe.CookingTimeMinutes.ToString() : string.Empty;
        ServingsBox.Text = Recipe.Servings.ToString();
        InstructionsBox.Text = Recipe.Instructions;
        UrlBox.Text = Recipe.SourceUrl;
        UpdatePictureStatus();
        RefreshIngredientChoices();
        ToolsBox.Text = string.Join(Environment.NewLine, Recipe.Tools);
        TagsBox.Text = string.Join(", ", Recipe.Tags);
        if (!string.IsNullOrWhiteSpace(photoOcrText))
        {
            PhotoTextBox.Text = photoOcrText;
            PhotoTextReview.Visibility = Visibility.Visible;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "Please enter a recipe name.", "Recipe name required", MessageBoxButton.OK, MessageBoxImage.Information);
            TitleBox.Focus();
            return;
        }

        Recipe.Title = TitleBox.Text.Trim();
        if (!int.TryParse(CookingTimeBox.Text, out var cookingTime) || cookingTime < 1 || cookingTime > 1440)
        {
            MessageBox.Show(this, "Enter a cooking time between 1 and 1440 minutes.", "Valid cooking time required", MessageBoxButton.OK, MessageBoxImage.Information);
            CookingTimeBox.Focus();
            return;
        }

        if (!int.TryParse(ServingsBox.Text, out var servings) || servings < 1 || servings > 100)
        {
            MessageBox.Show(this, "Enter a serving count between 1 and 100.", "Valid servings required", MessageBoxButton.OK, MessageBoxImage.Information);
            ServingsBox.Focus();
            return;
        }

        var ingredients = new List<RecipeIngredient>();
        foreach (var choice in _ingredientChoices.Where(x => x.IsSelected))
        {
            double? quantity = null;
            if (!string.IsNullOrWhiteSpace(choice.QuantityText))
            {
                if (!double.TryParse(choice.QuantityText, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) || parsed <= 0)
                {
                    MessageBox.Show(this, $"Enter a valid positive quantity for {choice.Name}.", "Invalid ingredient quantity", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                quantity = parsed;
            }
            ingredients.Add(new RecipeIngredient { Name = choice.Name, Quantity = quantity, Unit = choice.Unit.Trim() });
        }

        Recipe.Cuisine = CuisineBox.Text.Trim();
        Recipe.CookingTimeMinutes = cookingTime;
        Recipe.Servings = servings;
        Recipe.Instructions = InstructionsBox.Text.Trim();
        Recipe.SourceUrl = UrlBox.Text.Trim();
        Recipe.Ingredients = ingredients;
        Recipe.Tools = SplitLines(ToolsBox.Text);
        Recipe.Tags = SplitTags(TagsBox.Text);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ChoosePicture_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Choose a recipe picture",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            Recipe.ImageData = File.ReadAllBytes(picker.FileName);
            UpdatePictureStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"That picture could not be read.\n\n{ex.Message}", "Picture error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemovePicture_Click(object sender, RoutedEventArgs e)
    {
        Recipe.ImageData = null;
        UpdatePictureStatus();
    }

    private void UpdatePictureStatus()
    {
        PictureStatusText.Text = Recipe.ImageData is { Length: > 0 } ? "Picture attached" : "No picture";
        PicturePreview.Source = Recipe.ImageData is { Length: > 0 } ? LoadImage(Recipe.ImageData) : null;
        PicturePreviewBorder.Visibility = Recipe.ImageData is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    }

    private static BitmapImage LoadImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 260;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void IngredientSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IngredientChoicesList is not null) RefreshIngredientChoices();
    }

    private void RefreshIngredientChoices()
    {
        var search = IngredientSearchBox?.Text.Trim() ?? string.Empty;
        IngredientChoicesList.ItemsSource = _ingredientChoices
            .Where(x => search.Length == 0 || BilingualSearchService.IsLooseMatch($"{x.Name} {x.PluralName} {x.Aliases} {x.Category}", search))
            .ToList();
    }

    private static List<string> SplitLines(string text) => text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    private static List<string> SplitTags(string text) => text
        .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .ToList();
}
