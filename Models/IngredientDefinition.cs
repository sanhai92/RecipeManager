namespace RecipeManager.Models;

public sealed class IngredientDefinition
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PluralName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName => string.Join("  ·  ", new[] { Name, Category, Season }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
    public override string ToString() => Name;
}
