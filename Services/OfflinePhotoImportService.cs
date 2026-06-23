using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RecipeManager.Models;

namespace RecipeManager.Services;

public sealed class OfflinePhotoImportService
{
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
            && !ServingPattern.IsMatch(line) && !TimePattern.IsMatch(line)) ?? "Recipe from photo";
        title = Regex.Replace(title, @"^JUMBO\s+", string.Empty, RegexOptions.IgnoreCase).Trim();
        title = Regex.Replace(title, @"-\s+", string.Empty).Trim();
        var fullText = string.Join(" ", lines);
        var servingsMatch = ServingPattern.Match(fullText);
        var servingsBeforeLabelMatch = ServingBeforeLabelPattern.Match(fullText);
        var timeMatch = TimePattern.Match(fullText);
        var timeBeforeLabelMatch = TimeBeforeLabelPattern.Match(fullText);

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
        var match = IngredientPattern.Match(line);
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
        value = value.Replace(" ", string.Empty).Replace(',', '.');
        if (value.Contains('/'))
        {
            var parts = value.Split('/');
            if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) && denominator != 0)
                return numerator / denominator;
        }
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    private static bool IsIngredientHeader(string line) => IngredientHeaders.Any(header => NormalizeHeader(line).StartsWith(header, StringComparison.OrdinalIgnoreCase));
    private static bool IsInstructionHeader(string line)
    {
        var normalized = Regex.Replace(NormalizeHeader(line), @"^\d+\s*", string.Empty);
        return InstructionHeaders.Any(header => normalized.StartsWith(header, StringComparison.OrdinalIgnoreCase))
            || (normalized.Contains("bereid", StringComparison.OrdinalIgnoreCase) && normalized.Contains("wijze", StringComparison.OrdinalIgnoreCase));
    }
    private static string NormalizeHeader(string line) => Regex.Replace(line.Trim().TrimEnd(':'), @"\s+", " ");
}

public sealed record PhotoImportResult(Recipe Recipe, string RawText);
