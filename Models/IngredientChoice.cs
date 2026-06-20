namespace RecipeManager.Models;

public sealed class IngredientChoice
{
    public string Name { get; init; } = string.Empty;
    public string PluralName { get; init; } = string.Empty;
    public string Season { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
    public string QuantityText { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(Season) ? Name : $"{Name}  ·  {Season}";
}
