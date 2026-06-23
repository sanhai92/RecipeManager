namespace RecipeManager.Models;

public sealed class IngredientDefinition
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PluralName { get; set; } = string.Empty;
    public string Aliases { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName => string.Join("  ·  ", new[] { Name, Category, Season,
            string.IsNullOrWhiteSpace(Aliases) ? null : $"Aliases: {Aliases}" }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
    public IEnumerable<string> AllNames => new[] { Name, PluralName }
        .Concat(Aliases.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase);
    public override string ToString() => Name;
}
