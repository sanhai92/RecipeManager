namespace RecipeManager.Models;

public sealed class RecipeIngredient
{
    public string Name { get; set; } = string.Empty;
    public double? Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}
