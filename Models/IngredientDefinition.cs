namespace RecipeManager.Models;

public sealed class IngredientDefinition
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PluralName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(Season) ? Name : $"{Name}  ·  {Season}";
    public override string ToString() => Name;
}
