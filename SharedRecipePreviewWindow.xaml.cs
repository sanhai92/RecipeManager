using System.Globalization;
using System.Windows;
using RecipeManager.Models;

namespace RecipeManager;

public partial class SharedRecipePreviewWindow : Window
{
    private readonly List<SharedIngredientPreview> _ingredientPreviews;
    public Recipe Recipe { get; }
    public IReadOnlyCollection<IngredientDefinition> IngredientsToAddToLibrary => _ingredientPreviews
        .Where(item => !item.IsKnown && item.AddToLibrary)
        .Select(item => item.ToIngredientDefinition())
        .ToList();

    public SharedRecipePreviewWindow(Recipe recipe, IEnumerable<IngredientDefinition> ingredientLibrary,
        IEnumerable<IngredientDefinition> sharedIngredientDefinitions)
    {
        InitializeComponent();
        Recipe = recipe;
        var definitions = ingredientLibrary.ToList();
        var sharedDefinitions = sharedIngredientDefinitions.ToList();
        _ingredientPreviews = recipe.Ingredients.Select(ingredient =>
        {
            var known = definitions.Any(definition =>
                definition.Name.Equals(ingredient.Name, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(definition.PluralName)
                    && definition.PluralName.Equals(ingredient.Name, StringComparison.OrdinalIgnoreCase)));
            var sharedDefinition = sharedDefinitions.FirstOrDefault(definition =>
                definition.Name.Equals(ingredient.Name, StringComparison.OrdinalIgnoreCase));
            return new SharedIngredientPreview(ingredient, known, sharedDefinition);
        }).ToList();

        RecipeTitleText.Text = recipe.Title;
        RecipeSummaryText.Text = string.Join("  |  ", new[]
        {
            string.IsNullOrWhiteSpace(recipe.Cuisine) ? null : recipe.Cuisine,
            $"{recipe.CookingTimeMinutes} min",
            $"{recipe.Servings} serving{(recipe.Servings == 1 ? string.Empty : "s")}" 
        }.Where(text => text is not null));
        IngredientsList.ItemsSource = _ingredientPreviews;
        InstructionsText.Text = recipe.Instructions;
        ToolsText.Text = recipe.Tools.Count == 0 ? "No special tools" : string.Join(", ", recipe.Tools);
        if (!string.IsNullOrWhiteSpace(recipe.SourceUrl))
        {
            SourceText.Text = recipe.SourceUrl;
            SourcePanel.Visibility = Visibility.Visible;
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed class SharedIngredientPreview
    {
        public string Name { get; }
        public string QuantityText { get; }
        public bool IsKnown { get; }
        public bool AddToLibrary { get; set; }
        public string PluralName { get; }
        public string Season { get; }
        public string Category { get; }
        public string StatusText => IsKnown
            ? "Already available - will not be added again"
            : "New to your ingredients";
        public string SharedDetailsText { get; }
        public Visibility SharedDetailsVisibility => SharedDetailsText.Length == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        public Visibility AddControlVisibility => IsKnown ? Visibility.Collapsed : Visibility.Visible;

        public SharedIngredientPreview(RecipeIngredient ingredient, bool isKnown, IngredientDefinition? sharedDefinition)
        {
            Name = ingredient.Name;
            IsKnown = isKnown;
            PluralName = sharedDefinition?.PluralName ?? string.Empty;
            Season = sharedDefinition?.Season ?? string.Empty;
            Category = sharedDefinition?.Category ?? string.Empty;
            var quantity = ingredient.Quantity?.ToString("0.##", CultureInfo.CurrentCulture);
            QuantityText = string.Join(" ", new[] { quantity, ingredient.Unit }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            if (QuantityText.Length == 0) QuantityText = "-";
            SharedDetailsText = IsKnown
                ? string.Empty
                : string.Join("  |  ", new[]
                {
                    string.IsNullOrWhiteSpace(PluralName) ? null : $"Plural: {PluralName}",
                    string.IsNullOrWhiteSpace(Season) ? null : $"Season: {Season}",
                    string.IsNullOrWhiteSpace(Category) ? null : $"Category: {Category}"
                }.Where(detail => detail is not null));
        }

        public IngredientDefinition ToIngredientDefinition() => new()
        {
            Name = Name,
            PluralName = PluralName,
            Season = Season,
            Category = Category
        };
    }
}
