namespace RecipeManager.Models;

public sealed class Recipe
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Cuisine { get; set; } = string.Empty;
    public int CookingTimeMinutes { get; set; }
    public int Servings { get; set; } = 4;
    public string Instructions { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = [];
    public List<string> Tools { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string ProteinIcons { get; set; } = "🥦";
    public string ProteinIconsDescription { get; set; } = "No meat, fish, or chicken";
    public List<string> ProteinKinds { get; set; } = ["Vegetable"];

    public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    public string CuisineDisplay => string.IsNullOrWhiteSpace(Cuisine) ? "Cuisine not specified" : Cuisine;
    public string CookingTimeDisplay => CookingTimeMinutes > 0 ? $"{CookingTimeMinutes} min" : "Time not set";
    public string ToolsDisplay => Tools.Count == 0 ? "No special tools" : string.Join(Environment.NewLine, Tools.Select(x => "• " + x));
    public string TagsDisplay => string.Join("  ", Tags.Select(tag => "#" + tag));
}
