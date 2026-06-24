using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RecipeManager.Models;

namespace RecipeManager.Services;

public sealed class OfflinePhotoImportService
{
    private static readonly Regex IngredientPatternWeb = new(@"^\s*[-•*]?\s*(?:(?<quantity>\d+(?:[.,]\d+)?(?:\s*[\u00BC\u00BD\u00BE])?|[\u00BC\u00BD\u00BE]|\d+\s*/\s*\d+)\s*)?(?<unit>kg|g|mg|l|ml|cl|dl|tbsp|tsp|cups?|cup|tablespoons?|teaspoons?|el|tl|eetlepels?|theelepels?|stuks?|st\.?|cloves?|teentjes?|slices?|plakken?|zakjes?|blikjes?|kroppen?|krop|bosjes?|bos)?\s*(?<name>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimePatternV2 = new(@"(?:time|tijd|bereidingstijd|bereiden|cooking time)\s*[:\-]?\s*(\d{1,4})\s*(?:min|minutes?|minuten?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeBeforeLabelPatternV2 = new(@"(\d{1,4})\s*(?:min(?:\.|utes?|uten?)?\s*)?(?:bereiden|bereidingstijd|cooking time)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IngredientPatternV2 = new(@"^\s*[-â€¢•*]?\s*(?:(?<quantity>\d+(?:[.,]\d+)?(?:\s+[¼½¾])?|[¼½¾]|\d+\s*/\s*\d+)\s*)?(?<unit>kg|g|mg|l|ml|cl|dl|tbsp|tsp|cups?|cup|tablespoons?|teaspoons?|el|tl|eetlepels?|theelepels?|stuks?|st\.?|cloves?|teentjes?|slices?|plakken?|zakjes?|blikjes?|kroppen?|bosjes?)?\s*(?<name>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] IngredientHeadersV2 = ["ingredients", "ingredienten", "ingrediënten", "ingrediÃ«nten", "benodigdheden", "inhoud", "inhoud pakket"];
    private static readonly string[] InstructionHeadersV2 = ["instructions", "method", "directions", "preparation", "bereiding", "bereidingswijze", "bereidings wijze", "instructies", "werkwijze", "aan de slag"];

    private static readonly Regex ServingPattern = new(@"(?:serves?|servings?|porties?|personen?)\s*[:\-]?\s*(\d{1,3})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ServingBeforeLabelPattern = new(@"(\d{1,3})(?:\s*[-–]\s*(\d{1,3}))?\s*(?:servings?|porties?|personen?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimePattern = new(@"(?:time|tijd|bereidingstijd|cooking time)\s*[:\-]?\s*(\d{1,4})\s*(?:min|minutes?|minuten?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeBeforeLabelPattern = new(@"(\d{1,4})\s*(?:min(?:utes?|uten?)?\s*)?(?:bereidingstijd|cooking time)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IngredientPattern = new(@"^\s*[-•*]?\s*(?:(?<quantity>\d+(?:[.,]\d+)?|\d+\s*/\s*\d+)\s*)?(?<unit>kg|g|mg|l|ml|cl|dl|tbsp|tsp|cups?|cup|tablespoons?|teaspoons?|el|tl|eetlepels?|theelepels?|stuks?|cloves?|teentjes?|slices?|plakken?|zakjes?|blikjes?)?\s*(?<name>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] IngredientHeaders = ["ingredients", "ingredienten", "ingrediënten", "benodigdheden", "inhoud", "inhoud pakket"];
    private static readonly string[] InstructionHeaders = ["instructions", "method", "directions", "preparation", "bereiding", "bereidingswijze", "bereidings wijze", "instructies", "werkwijze"];

    public async Task<PhotoImportResult> ImportAsync(IReadOnlyList<string> imagePaths, CancellationToken cancellationToken = default)
    {
        if (imagePaths.Count == 0) throw new ArgumentException("Select at least one recipe photo.");
        var pages = new List<string>();
        foreach (var path in imagePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages.Add(await ReadImageTextAsync(path, cancellationToken));
        }

        var rawText = string.Join(Environment.NewLine + Environment.NewLine, pages.Where(text => !string.IsNullOrWhiteSpace(text)));
        if (string.IsNullOrWhiteSpace(rawText))
            throw new InvalidOperationException("No readable text was found. Try a sharper, straighter photo with good lighting.");

        var recipe = ParseRecipe(rawText);
        recipe.ImageData = await File.ReadAllBytesAsync(imagePaths[0], cancellationToken);
        recipe.Tags = ["photo import"];
        return new PhotoImportResult(recipe, rawText);
    }

    public PhotoImportResult ImportText(string text, string tag = "text import", string sourceUrl = "")
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Paste or provide some recipe text first.");

        var recipe = ParseRecipe(text);
        recipe.Tags = [tag];
        recipe.SourceUrl = sourceUrl;
        return new PhotoImportResult(recipe, text);
    }

    public async Task<PhotoImportResult> ImportWebsiteAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Enter a valid http or https recipe website address.");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RecipeManager/1.0");
        var html = await client.GetStringAsync(uri, cancellationToken);
        var text = ExtractReadableWebsiteText(html);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("No readable recipe text was found on that page. Try copying the recipe text and importing it as pasted text.");

        return ImportText(text, "website import", uri.AbsoluteUri);
    }

    public async Task<PhotoImportResult> ImportPdfAsync(string pdfPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            throw new FileNotFoundException("The selected PDF could not be found.", pdfPath);

        var extractor = FindPdfTextExtractor();
        if (extractor is null)
            throw new InvalidOperationException("PDF import needs a local text extractor such as Poppler's pdftotext. For now, open the PDF, copy the recipe text, and import it as pasted text. If the PDF is scanned, take a screenshot/photo and import it as an image.");

        var startInfo = new ProcessStartInfo
        {
            FileName = extractor,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("-layout");
        startInfo.ArgumentList.Add(Path.GetFullPath(pdfPath));
        startInfo.ArgumentList.Add("-");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("PDF text extraction could not be started.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var text = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException("The PDF text could not be extracted.\n\n" + error.Trim());
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("No selectable text was found in this PDF. If it is a scanned PDF, take a screenshot/photo and import it as an image.");

        return ImportText(text, "pdf import");
    }

    private static string ExtractReadableWebsiteText(string html)
    {
        var structuredRecipe = ExtractStructuredRecipeText(html);
        if (!string.IsNullOrWhiteSpace(structuredRecipe))
            return structuredRecipe;

        var title = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;
        html = Regex.Replace(html, @"<script\b[^<]*(?:(?!</script>)<[^<]*)*</script>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style\b[^<]*(?:(?!</style>)<[^<]*)*</style>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(br|p|div|li|h[1-6]|tr|section|article)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<[^>]+>", " ");
        var text = System.Net.WebUtility.HtmlDecode($"{title}\n{html}");
        text = Regex.Replace(text, @"[ \t]+", " ").Replace("\r", string.Empty);
        return RemoveWebsiteNoise(text);
    }

    private static string ExtractStructuredRecipeText(string html)
    {
        foreach (Match match in Regex.Matches(html, @"<script[^>]+type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var json = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            if (string.IsNullOrWhiteSpace(json)) continue;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (!TryFindRecipeElement(document.RootElement, out var recipeElement)) continue;

                var lines = new List<string>();
                var title = ReadJsonString(recipeElement, "name");
                if (!string.IsNullOrWhiteSpace(title)) lines.Add(title);

                var servings = ReadJsonString(recipeElement, "recipeYield");
                if (!string.IsNullOrWhiteSpace(servings)) lines.Add(servings);

                var minutes = ReadRecipeMinutes(recipeElement);
                if (minutes.HasValue) lines.Add($"{minutes.Value} min bereiden");

                var ingredients = ReadJsonStringArray(recipeElement, "recipeIngredient");
                if (ingredients.Count > 0)
                {
                    lines.Add("Ingrediënten");
                    lines.AddRange(ingredients);
                }

                var instructions = ReadRecipeInstructions(recipeElement);
                if (instructions.Count > 0)
                {
                    lines.Add("Aan de slag");
                    lines.AddRange(instructions.Select((step, index) => $"{index + 1}. {step}"));
                }

                if (ingredients.Count > 0 || instructions.Count > 0)
                    return string.Join(Environment.NewLine, lines);
            }
            catch (JsonException)
            {
                // Some pages contain invalid or multiple JSON-LD snippets. Just try the next one.
            }
        }

        return string.Empty;
    }

    private static bool TryFindRecipeElement(JsonElement element, out JsonElement recipeElement)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (JsonTypeContains(element, "Recipe"))
            {
                recipeElement = element;
                return true;
            }

            if (element.TryGetProperty("@graph", out var graph) && TryFindRecipeElement(graph, out recipeElement))
                return true;

            foreach (var property in element.EnumerateObject())
            {
                if (TryFindRecipeElement(property.Value, out recipeElement))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindRecipeElement(item, out recipeElement))
                    return true;
            }
        }

        recipeElement = default;
        return false;
    }

    private static bool JsonTypeContains(JsonElement element, string type)
    {
        if (!element.TryGetProperty("@type", out var typeElement)) return false;
        if (typeElement.ValueKind == JsonValueKind.String)
            return string.Equals(typeElement.GetString(), type, StringComparison.OrdinalIgnoreCase);
        if (typeElement.ValueKind == JsonValueKind.Array)
            return typeElement.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String
                && string.Equals(item.GetString(), type, StringComparison.OrdinalIgnoreCase));
        return false;
    }

    private static string ReadJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return string.Empty;
        return ReadJsonText(property);
    }

    private static List<string> ReadJsonStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return [];
        if (property.ValueKind == JsonValueKind.Array)
            return property.EnumerateArray()
                .Select(ReadJsonText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        var single = ReadJsonText(property);
        return string.IsNullOrWhiteSpace(single) ? [] : [single];
    }

    private static string ReadJsonText(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => Regex.Replace(element.GetString() ?? string.Empty, @"\s+", " ").Trim(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.Array => string.Join(" ", element.EnumerateArray().Select(ReadJsonText).Where(value => !string.IsNullOrWhiteSpace(value))),
            JsonValueKind.Object when element.TryGetProperty("text", out var text) => ReadJsonText(text),
            JsonValueKind.Object when element.TryGetProperty("name", out var name) => ReadJsonText(name),
            _ => string.Empty
        };
    }

    private static int? ReadRecipeMinutes(JsonElement recipeElement)
    {
        foreach (var property in new[] { "totalTime", "cookTime", "prepTime" })
        {
            var value = ReadJsonString(recipeElement, property);
            if (TryParseIsoDurationMinutes(value, out var minutes))
                return minutes;
        }

        return null;
    }

    private static bool TryParseIsoDurationMinutes(string value, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var match = Regex.Match(value, @"P(?:\d+D)?T?(?:(\d+)H)?(?:(\d+)M)?", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var hours = int.TryParse(match.Groups[1].Value, out var parsedHours) ? parsedHours : 0;
        var mins = int.TryParse(match.Groups[2].Value, out var parsedMinutes) ? parsedMinutes : 0;
        minutes = (hours * 60) + mins;
        return minutes > 0;
    }

    private static List<string> ReadRecipeInstructions(JsonElement recipeElement)
    {
        if (!recipeElement.TryGetProperty("recipeInstructions", out var instructions)) return [];
        var result = new List<string>();
        AddInstructionText(instructions, result);
        return result.Where(line => !string.IsNullOrWhiteSpace(line)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddInstructionText(JsonElement element, List<string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                result.Add(ReadJsonText(element));
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    AddInstructionText(item, result);
                break;
            case JsonValueKind.Object:
                if (element.TryGetProperty("text", out var text))
                    result.Add(ReadJsonText(text));
                else if (element.TryGetProperty("itemListElement", out var steps))
                    AddInstructionText(steps, result);
                else if (element.TryGetProperty("name", out var name))
                    result.Add(ReadJsonText(name));
                break;
        }
    }

    private static string RemoveWebsiteNoise(string text)
    {
        var lines = text.Split('\n')
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Length > 0)
            .Where(line => !IsWebsiteNoiseLine(line))
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsWebsiteNoiseLine(string line)
    {
        var lower = line.ToLowerInvariant();
        string[] noise =
        [
            "ga naar", "inloggen", "winkelmand", "producten", "bonus", "klantenservice", "privacybeleid",
            "cookiebeleid", "algemene voorwaarden", "gerelateerde recepten", "boodschappen", "albert heijn",
            "wat vond je", "voedingswaarden", "energie", "koolhydraten", "waarvan suikers", "natrium",
            "eiwit", "vet", "vezels", "ontdek meer", "kies producten", "bewaar", "nix 18", "thuiswinkel"
        ];
        return noise.Any(item => lower.Contains(item));
    }

    private static string? FindPdfTextExtractor()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder, "pdftotext.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static async Task<string> ReadImageTextAsync(string imagePath, CancellationToken cancellationToken)
    {
        var tileFolder = Path.Combine(Path.GetTempPath(), "RecipeManagerOcr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tileFolder);
        try
        {
            var pageParts = new List<string>();
            var overviewText = await ReadTextAsync(imagePath, 0, cancellationToken);
            if (!string.IsNullOrWhiteSpace(overviewText)) pageParts.Add(overviewText);
            var titleText = await ReadTextAsync(imagePath, 90, cancellationToken);
            if (!string.IsNullOrWhiteSpace(titleText)) pageParts.Add(titleText);
            foreach (var tile in CreateReadableTiles(imagePath, tileFolder))
            {
                string bestText = string.Empty;
                foreach (var rotation in new[] { 0, 90, 180, 270 })
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidate = await ReadTextAsync(tile, rotation, cancellationToken);
                    if (candidate.Length > bestText.Length) bestText = candidate;
                }
                if (!string.IsNullOrWhiteSpace(bestText)) pageParts.Add(bestText);
            }
            return string.Join(Environment.NewLine, pageParts);
        }
        finally
        {
            try { Directory.Delete(tileFolder, true); } catch { }
        }
    }

    private static IReadOnlyList<string> CreateReadableTiles(string imagePath, string outputFolder)
    {
        BitmapFrame frame;
        using (var stream = File.OpenRead(imagePath))
            frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        BitmapSource source = ApplyExifOrientation(frame);
        const int tileSize = 1050;
        var columns = Math.Max(1, (int)Math.Ceiling(source.PixelWidth / (double)tileSize));
        var rows = Math.Max(1, (int)Math.Ceiling(source.PixelHeight / (double)tileSize));
        var paths = new List<string>();
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            var x = column * tileSize;
            var y = row * tileSize;
            var width = Math.Min(tileSize, source.PixelWidth - x);
            var height = Math.Min(tileSize, source.PixelHeight - y);
            BitmapSource tile = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
            var scale = Math.Min(2.1, Math.Min(2300d / width, 2300d / height));
            if (scale > 1.05)
                tile = new TransformedBitmap(tile, new ScaleTransform(scale, scale));

            var path = Path.Combine(outputFolder, $"tile-{row}-{column}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(tile));
            using var output = File.Create(path);
            encoder.Save(output);
            paths.Add(path);
        }
        return paths;
    }

    private static BitmapSource ApplyExifOrientation(BitmapFrame frame)
    {
        if (frame.Metadata is not BitmapMetadata metadata) return frame;
        ushort orientation;
        try { orientation = Convert.ToUInt16(metadata.GetQuery("/app1/ifd/{ushort=274}"), CultureInfo.InvariantCulture); }
        catch { return frame; }
        var angle = orientation switch { 3 => 180, 6 => 90, 8 => 270, _ => 0 };
        return angle == 0 ? frame : new TransformedBitmap(frame, new RotateTransform(angle));
    }

    private static async Task<string> ReadTextAsync(string imagePath, int rotationDegrees, CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "OfflineOcr.ps1");
        if (!File.Exists(scriptPath)) throw new FileNotFoundException("The offline OCR component is missing.", scriptPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-ImagePath");
        startInfo.ArgumentList.Add(Path.GetFullPath(imagePath));
        startInfo.ArgumentList.Add("-RotationDegrees");
        startInfo.ArgumentList.Add(rotationDegrees.ToString(CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows OCR could not be started.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Windows could not read this photo offline. Make sure an OCR language is installed in Windows Language settings.\n\n" + error.Trim());
        return output.Trim();
    }

    private static Recipe ParseRecipe(string text)
    {
        var lines = text.Replace("\r", string.Empty).Split('\n')
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Length > 0).ToList();
        var ingredientHeader = lines.FindIndex(IsIngredientHeader);
        var instructionHeader = lines.FindIndex(IsInstructionHeader);
        var brandIndex = lines.FindIndex(line => line.Equals("JUMBO", StringComparison.OrdinalIgnoreCase));
        var title = brandIndex >= 0
            ? string.Join(" ", lines.Skip(brandIndex + 1).Take(4))
            : lines.FirstOrDefault(line => !line.Equals("JUMBO", StringComparison.OrdinalIgnoreCase)
            && !IsIngredientHeader(line) && !IsInstructionHeader(line)
            && !ServingPattern.IsMatch(line) && !TimePattern.IsMatch(line) && !TimePatternV2.IsMatch(line) && !TimeBeforeLabelPatternV2.IsMatch(line)) ?? "Recipe from photo";
        title = Regex.Replace(title, @"^JUMBO\s+", string.Empty, RegexOptions.IgnoreCase).Trim();
        title = Regex.Replace(title, @"\s*[-|]\s*(?:Allerhande|Albert Heijn|AH)\b.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        title = Regex.Replace(title, @"^\s*recept\s*[-:]\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
        title = Regex.Replace(title, @"-\s+", string.Empty).Trim();
        var fullText = string.Join(" ", lines);
        var servingsMatch = ServingPattern.Match(fullText);
        var servingsBeforeLabelMatch = ServingBeforeLabelPattern.Match(fullText);
        var timeMatch = TimePatternV2.Match(fullText);
        if (!timeMatch.Success) timeMatch = TimePattern.Match(fullText);
        var timeBeforeLabelMatch = TimeBeforeLabelPatternV2.Match(fullText);
        if (!timeBeforeLabelMatch.Success) timeBeforeLabelMatch = TimeBeforeLabelPattern.Match(fullText);

        var ingredients = new List<RecipeIngredient>();
        var ingredientHeaders = Enumerable.Range(0, lines.Count).Where(index => IsIngredientHeader(lines[index])).ToList();
        foreach (var headerIndex in ingredientHeaders)
        {
            var end = lines.FindIndex(headerIndex + 1, line => IsInstructionHeader(line)
                || IsIngredientHeader(line) || line.StartsWith("TIP", StringComparison.OrdinalIgnoreCase));
            if (end < 0) end = lines.Count;
            ingredients.AddRange(lines.Skip(headerIndex + 1).Take(end - headerIndex - 1)
                .Select(ParseIngredient).Where(item => item is not null).Cast<RecipeIngredient>());
        }
        var mergedIngredients = new List<RecipeIngredient>();
        foreach (var ingredient in ingredients)
        {
            var existing = mergedIngredients.FirstOrDefault(item =>
                BilingualSearchService.AreEquivalent(item.Name, ingredient.Name)
                || (BilingualSearchService.IsLooseMatch(item.Name, ingredient.Name)
                    && BilingualSearchService.IsLooseMatch(ingredient.Name, item.Name)));
            if (existing is null)
                mergedIngredients.Add(ingredient);
            else if (!existing.Quantity.HasValue && ingredient.Quantity.HasValue)
            {
                existing.Quantity = ingredient.Quantity;
                existing.Unit = ingredient.Unit;
            }
        }
        ingredients = mergedIngredients;

        var instructionEnd = ingredientHeader > instructionHeader ? ingredientHeader : lines.Count;
        List<string> instructionLines = instructionHeader >= 0
            ? lines.Skip(instructionHeader + 1).Take(Math.Max(0, instructionEnd - instructionHeader - 1)).ToList()
            : [];
        var instructions = instructionLines.Count > 0 ? string.Join(Environment.NewLine, instructionLines) : text.Trim();
        var servingsText = servingsBeforeLabelMatch.Success
            ? (servingsBeforeLabelMatch.Groups[2].Success ? servingsBeforeLabelMatch.Groups[2].Value : servingsBeforeLabelMatch.Groups[1].Value)
            : servingsMatch.Groups[1].Value;
        var timeText = timeMatch.Success ? timeMatch.Groups[1].Value : timeBeforeLabelMatch.Groups[1].Value;
        return new Recipe
        {
            Title = title.Length > 200 ? title[..200] : title,
            CookingTimeMinutes = int.TryParse(timeText, out var minutes) ? Math.Clamp(minutes, 1, 1440) : 30,
            Servings = int.TryParse(servingsText, out var servings) ? Math.Clamp(servings, 1, 100) : 4,
            Ingredients = ingredients,
            Instructions = instructions
        };
    }

    private static RecipeIngredient? ParseIngredient(string line)
    {
        if (line.Length > 250 || IsIngredientHeader(line) || IsInstructionHeader(line)) return null;
        var match = IngredientPatternWeb.Match(line);
        if (!match.Success) match = IngredientPatternV2.Match(line);
        if (!match.Success) match = IngredientPattern.Match(line);
        if (!match.Success) return null;
        var name = NormalizeRecognizedIngredientName(match.Groups["name"].Value.Trim(' ', '-', '•', '*', ':'));
        if (name.Length < 2) return null;
        return new RecipeIngredient
        {
            Name = name,
            Quantity = ParseQuantity(match.Groups["quantity"].Value),
            Unit = match.Groups["unit"].Value.Trim()
        };
    }

    private static string NormalizeRecognizedIngredientName(string name)
    {
        name = Regex.Replace(name, @"^RODE\s+(?:UI|UL|IJI|UJI)$", "rode ui", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\bTOMATENPURE$", "tomatenpuree", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\bKRIJIDENMIX\b", "kruidenmix", RegexOptions.IgnoreCase);
        return name.Trim();
    }

    private static double? ParseQuantity(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().Replace(',', '.');
        var mixedFraction = Regex.Match(value, @"^(?<whole>\d+(?:\.\d+)?)\s*(?<fraction>[\u00BC\u00BD\u00BE])$");
        if (mixedFraction.Success
            && double.TryParse(mixedFraction.Groups["whole"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var whole)
            && TryParseUnicodeFraction(mixedFraction.Groups["fraction"].Value, out var fraction))
            return whole + fraction;

        if (TryParseUnicodeFraction(value, out var unicodeFraction))
            return unicodeFraction;

        value = value.Replace(" ", string.Empty);
        if (value.Contains('/'))
        {
            var parts = value.Split('/');
            if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) && denominator != 0)
                return numerator / denominator;
        }
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    private static bool TryParseUnicodeFraction(string value, out double fraction)
    {
        fraction = value.Trim() switch
        {
            "\u00BC" => 0.25,
            "\u00BD" => 0.5,
            "\u00BE" => 0.75,
            _ => 0
        };
        return fraction > 0;
    }

    private static bool IsIngredientHeader(string line)
    {
        var normalized = NormalizeHeader(line);
        return IngredientHeadersV2.Any(header => normalized.StartsWith(header, StringComparison.OrdinalIgnoreCase))
            || IngredientHeaders.Any(header => normalized.StartsWith(header, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInstructionHeader(string line)
    {
        var normalized = Regex.Replace(NormalizeHeader(line), @"^\d+\s*", string.Empty);
        return InstructionHeadersV2.Any(header => normalized.StartsWith(header, StringComparison.OrdinalIgnoreCase))
            || InstructionHeaders.Any(header => normalized.StartsWith(header, StringComparison.OrdinalIgnoreCase))
            || (normalized.Contains("bereid", StringComparison.OrdinalIgnoreCase) && normalized.Contains("wijze", StringComparison.OrdinalIgnoreCase));
    }
    private static string NormalizeHeader(string line) => Regex.Replace(line.Trim().TrimEnd(':'), @"\s+", " ");
}

public sealed record PhotoImportResult(Recipe Recipe, string RawText);
