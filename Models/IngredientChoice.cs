namespace RecipeManager.Models;

public sealed class IngredientChoice
{
    public string Name { get; init; } = string.Empty;
    public string PluralName { get; init; } = string.Empty;
    public string Aliases { get; init; } = string.Empty;
    public string Season { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
    public string QuantityText { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public bool IsInLibrary { get; init; } = true;

    public string DisplayName
    {
        get
        {
            var display = string.Join("  ·  ", new[] { Name, Category, Season }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            return IsInLibrary ? display : $"{display}  ·  New ingredient";
        }
    }
}
