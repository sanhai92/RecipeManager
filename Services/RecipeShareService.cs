using System.IO.Compression;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RecipeManager.Models;

namespace RecipeManager.Services;

public sealed record DecodedRecipeShare(Recipe Recipe, IReadOnlyList<IngredientDefinition> IngredientDefinitions);

public static class RecipeShareService
{
    private const string Prefix = "RM1:";
    private const int MaximumCodeLength = 100_000;
    private const int MaximumDecodedBytes = 1_000_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Encode(Recipe recipe, IEnumerable<IngredientDefinition> ingredientLibrary)
    {
        var definitions = ingredientLibrary.ToList();
        var shared = new SharedRecipe
        {
            Title = recipe.Title,
            Cuisine = recipe.Cuisine,
            CookingTimeMinutes = recipe.CookingTimeMinutes,
            Servings = recipe.Servings,
            Instructions = recipe.Instructions,
            SourceUrl = recipe.SourceUrl,
            Ingredients = recipe.Ingredients.Select(item =>
            {
                var definition = definitions.FirstOrDefault(candidate =>
                    candidate.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(candidate.PluralName)
                        && candidate.PluralName.Equals(item.Name, StringComparison.OrdinalIgnoreCase)));
                return new SharedIngredient
                {
                    Name = item.Name,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    PluralName = definition?.PluralName ?? string.Empty,
                    Season = definition?.Season ?? string.Empty,
                    Category = definition?.Category ?? string.Empty
                };
            }).ToList(),
            Tools = [.. recipe.Tools]
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(shared, JsonOptions);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(json);

        return Prefix + Convert.ToBase64String(output.ToArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static DecodedRecipeShare Decode(string code)
    {
        var compactCode = string.Concat((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c)));
        if (!compactCode.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("This is not a Recipe Manager sharing code. It should begin with RM1:.");
        if (compactCode.Length > MaximumCodeLength)
            throw new FormatException("This recipe code is too large to import safely.");

        try
        {
            var payload = compactCode[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            byte[] compressed;
            try
            {
                compressed = Convert.FromBase64String(payload);
            }
            catch (FormatException ex)
            {
                throw new FormatException("The recipe code is damaged or incomplete.", ex);
            }
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaximumDecodedBytes)
                    throw new FormatException("This recipe code expands beyond the safe import limit.");
                output.Write(buffer, 0, read);
            }

            var shared = JsonSerializer.Deserialize<SharedRecipe>(output.ToArray(), JsonOptions)
                ?? throw new FormatException("The recipe code contains no recipe.");
            shared.Title ??= string.Empty;
            shared.Cuisine ??= string.Empty;
            shared.Instructions ??= string.Empty;
            shared.SourceUrl ??= string.Empty;
            shared.Ingredients ??= [];
            shared.Tools ??= [];
            foreach (var ingredient in shared.Ingredients.Where(item => item is not null))
            {
                ingredient.Name ??= string.Empty;
                ingredient.Unit ??= string.Empty;
                ingredient.PluralName ??= string.Empty;
                ingredient.Season ??= string.Empty;
                ingredient.Category ??= string.Empty;
            }
            Validate(shared);
            var recipe = new Recipe
            {
                Title = shared.Title.Trim(),
                Cuisine = shared.Cuisine.Trim(),
                CookingTimeMinutes = shared.CookingTimeMinutes,
                Servings = shared.Servings,
                Instructions = shared.Instructions.Trim(),
                SourceUrl = shared.SourceUrl.Trim(),
                Ingredients = shared.Ingredients.Select(item => new RecipeIngredient
                {
                    Name = item.Name.Trim(),
                    Quantity = item.Quantity,
                    Unit = item.Unit.Trim()
                }).ToList(),
                Tools = shared.Tools.Select(tool => tool.Trim()).Where(tool => tool.Length > 0).ToList()
            };
            var definitions = shared.Ingredients.Select(item => new IngredientDefinition
            {
                Name = item.Name.Trim(),
                PluralName = item.PluralName.Trim(),
                Season = item.Season.Trim(),
                Category = item.Category.Trim()
            }).ToList();
            return new DecodedRecipeShare(recipe, definitions);
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            throw new FormatException("The recipe code is damaged or incomplete.", ex);
        }
    }

    private static void Validate(SharedRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.Title) || recipe.Title.Length > 200)
            throw new FormatException("The shared recipe has an invalid name.");
        if (recipe.CookingTimeMinutes is < 1 or > 1440)
            throw new FormatException("The shared recipe has an invalid cooking time.");
        if (recipe.Servings is < 1 or > 100)
            throw new FormatException("The shared recipe has an invalid serving count.");
        if (recipe.Instructions.Length > 200_000 || recipe.SourceUrl.Length > 2_000 || recipe.Cuisine.Length > 200)
            throw new FormatException("The shared recipe contains text that is too long.");
        if (recipe.Ingredients.Count > 200 || recipe.Tools.Count > 100)
            throw new FormatException("The shared recipe contains too many ingredients or tools.");
        if (recipe.Ingredients.Any(item => item is null
                                           || string.IsNullOrWhiteSpace(item.Name)
                                           || item.Name.Length > 200
                                           || item.Unit.Length > 50
                                           || item.PluralName.Length > 200
                                           || item.Season.Length > 50
                                           || item.Category.Length > 100
                                           || item.Quantity is <= 0 or > 1_000_000))
            throw new FormatException("The shared recipe contains an invalid ingredient.");
        if (recipe.Tools.Any(tool => tool.Length > 200))
            throw new FormatException("The shared recipe contains an invalid kitchen tool.");
    }

    private sealed class SharedRecipe
    {
        [JsonPropertyName("t")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("c")] public string Cuisine { get; set; } = string.Empty;
        [JsonPropertyName("m")] public int CookingTimeMinutes { get; set; }
        [JsonPropertyName("s")] public int Servings { get; set; }
        [JsonPropertyName("i")] public string Instructions { get; set; } = string.Empty;
        [JsonPropertyName("u")] public string SourceUrl { get; set; } = string.Empty;
        [JsonPropertyName("g")] public List<SharedIngredient> Ingredients { get; set; } = [];
        [JsonPropertyName("k")] public List<string> Tools { get; set; } = [];
    }

    private sealed class SharedIngredient
    {
        [JsonPropertyName("n")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("q")] public double? Quantity { get; set; }
        [JsonPropertyName("u")] public string Unit { get; set; } = string.Empty;
        [JsonPropertyName("p")] public string PluralName { get; set; } = string.Empty;
        [JsonPropertyName("s")] public string Season { get; set; } = string.Empty;
        [JsonPropertyName("c")] public string Category { get; set; } = string.Empty;
    }
}
