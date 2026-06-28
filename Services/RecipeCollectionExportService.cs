using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using RecipeManager.Models;

namespace RecipeManager.Services;

public static class RecipeCollectionExportService
{
    private const string FormatName = "PocketRecipeCollection";
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void ExportZip(
        string zipPath,
        IReadOnlyList<Recipe> recipes,
        IReadOnlyList<IngredientDefinition> ingredientLibrary)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
            throw new ArgumentException("Choose where to save the export file.", nameof(zipPath));

        var folder = Path.GetDirectoryName(Path.GetFullPath(zipPath));
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        var export = CreateExport(recipes, ingredientLibrary);
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        WriteJson(archive, "metadata.json", export.Metadata);
        WriteJson(archive, "ingredients.json", export.Ingredients);
        WriteJson(archive, "recipes.json", export.Recipes);
        WriteText(archive, "README.txt",
            "Recipe Manager collection export\n\n" +
            "This zip contains recipes and the ingredient library as JSON.\n" +
            "Pictures are intentionally not included.\n" +
            "Format: PocketRecipeCollection v1\n");
    }

    private static CollectionExport CreateExport(
        IReadOnlyList<Recipe> recipes,
        IReadOnlyList<IngredientDefinition> ingredientLibrary)
    {
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        return new CollectionExport
        {
            Metadata = new ExportMetadata
            {
                Format = FormatName,
                FormatVersion = FormatVersion,
                SourceApp = "Recipe Manager",
                SourceAppVersion = appVersion,
                ExportedAtUtc = DateTime.UtcNow,
                IncludesPictures = false,
                RecipeCount = recipes.Count,
                IngredientCount = ingredientLibrary.Count
            },
            Ingredients = ingredientLibrary.Select(ingredient => new ExportIngredient
            {
                Id = ingredient.Id,
                Name = ingredient.Name,
                PluralName = ingredient.PluralName,
                Aliases = ingredient.Aliases,
                Season = ingredient.Season,
                Category = ingredient.Category
            }).ToList(),
            Recipes = recipes.Select(recipe => new ExportRecipe
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Cuisine = recipe.Cuisine,
                CookingTimeMinutes = recipe.CookingTimeMinutes,
                Servings = recipe.Servings,
                Instructions = recipe.Instructions,
                IsFavorite = recipe.IsFavorite,
                SourceUrl = recipe.SourceUrl,
                Tags = [.. recipe.Tags],
                Tools = [.. recipe.Tools],
                Ingredients = recipe.Ingredients.Select(ingredient => new ExportRecipeIngredient
                {
                    Name = ingredient.Name,
                    Quantity = ingredient.Quantity,
                    Unit = ingredient.Unit
                }).ToList()
            }).ToList()
        };
    }

    private static void WriteJson<T>(ZipArchive archive, string entryName, T value)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.SmallestSize);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }

    private static void WriteText(ZipArchive archive, string entryName, string value)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.SmallestSize);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(value);
    }

    private sealed class CollectionExport
    {
        public ExportMetadata Metadata { get; set; } = new();
        public List<ExportIngredient> Ingredients { get; set; } = [];
        public List<ExportRecipe> Recipes { get; set; } = [];
    }

    private sealed class ExportMetadata
    {
        public string Format { get; set; } = FormatName;
        public int FormatVersion { get; set; } = RecipeCollectionExportService.FormatVersion;
        public string SourceApp { get; set; } = string.Empty;
        public string SourceAppVersion { get; set; } = string.Empty;
        public DateTime ExportedAtUtc { get; set; }
        public bool IncludesPictures { get; set; }
        public int RecipeCount { get; set; }
        public int IngredientCount { get; set; }
    }

    private sealed class ExportIngredient
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PluralName { get; set; } = string.Empty;
        public string Aliases { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    private sealed class ExportRecipe
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Cuisine { get; set; } = string.Empty;
        public int CookingTimeMinutes { get; set; }
        public int Servings { get; set; }
        public string Instructions { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
        public string SourceUrl { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = [];
        public List<string> Tools { get; set; } = [];
        public List<ExportRecipeIngredient> Ingredients { get; set; } = [];
    }

    private sealed class ExportRecipeIngredient
    {
        public string Name { get; set; } = string.Empty;
        public double? Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}
