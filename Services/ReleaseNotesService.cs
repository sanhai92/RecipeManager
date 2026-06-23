using System.IO;
using System.Text.Json;

namespace RecipeManager.Services;

public sealed record ReleaseNote(string Title, string Description);
public sealed record VersionReleaseNotes(string Version, string Heading, IReadOnlyList<ReleaseNote> Changes);
public sealed record UpdateSummary(string FromVersion, string ToVersion, IReadOnlyList<VersionReleaseNotes> Versions);

public static class ReleaseNotesService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecipeManager");
    private static readonly string PendingUpdatePath = Path.Combine(AppFolder, "pending-update.json");
    private static readonly string LastRunVersionPath = Path.Combine(AppFolder, "last-run-version.txt");

    private static readonly VersionReleaseNotes[] Notes =
    [
        new("1.0.2", "Safer updates and backups",
        [
            new("Recipes protected during updates", "The app creates a verified backup before installing an update."),
            new("Backup and restore controls", "You can manually back up recipes and restore one of the five newest copies."),
            new("Updates are easier to spot", "The update button is highlighted whenever a newer version is available.")
        ]),
        new("1.0.3", "Sharing and finding recipes",
        [
            new("Share recipes with a short code", "Copy an RM1 code and send it through any messaging app."),
            new("Review ingredients before importing", "Known and new ingredients are clearly separated. You decide which new ingredients join your managed list."),
            new("Edit before importing", "Shared recipes now open in the full recipe editor, where you can replace ingredients and change amounts, units, and every other detail before saving."),
            new("Plural and season details are shared", "New ingredients can keep their plural name and seasonal information."),
            new("Organize ingredients by category", "Ingredients can be labeled as fruit, vegetable, meat, dairy, and more. Categories are included in RM1 sharing codes."),
            new("Vegan starter recipes", "New installations include twenty vegan recipes with quantities, tools, cooking times, and categorized ingredients for easy testing."),
            new("Free-form recipe tags", "Add your own tags to recipes, search by them, and keep them when sharing with RM1 codes."),
            new("Import a recipe photo offline", "Turn one or more recipe photos into an editable draft using Windows OCR. The photos and recognized text stay on your computer."),
            new("Ingredient aliases", "Store multiple Dutch, English, singular, or plural names for an ingredient. Aliases are used by search, photo import, and RM1 sharing."),
            new("Find recipes by name", "Search by partial names or small spelling mistakes."),
            new("Cleaner recipe list", "The main list now focuses on recipe names and favorites, with clearer button hover colors and copy confirmations.")
        ]),
        new("1.0.4", "Offline photo import and smarter ingredients",
        [
            new("Import recipe photos offline", "Use a photo of a recipe to create an editable draft while keeping the image attached to the recipe."),
            new("Clear import progress", "A small progress screen now shows while the app is reading a recipe photo."),
            new("Better Dutch ingredient matching", "Common Dutch words and plurals such as aardappels, knoflook, and rode ui are matched to known ingredients more reliably."),
            new("Review unrecognized photo text", "The editor shows the text that was found but not confidently imported, so nothing important disappears silently."),
            new("Ingredient aliases", "Ingredients can store alternate names and plurals, and those aliases are included when sharing recipes.")
        ]),
        new("1.0.5", "Faster inline editing",
        [
            new("Edit recipes on the main page", "The Edit button now unlocks the recipe details directly on the main screen, with Save and Cancel controls beside the fields."),
            new("Compact ingredient editing", "Ingredients can be adjusted in a small table with quantity, unit, ingredient picker, add, and remove controls."),
            new("Clear editing guard", "Clicking outside the recipe panel while editing reminds you to save or cancel first, and briefly highlights the action buttons."),
            new("Better app icon", "The taskbar, app window, installer, and desktop shortcut now use a more recognizable Recipe Manager icon."),
            new("Polished dropdowns", "The main filters use the Fresh Garden pill style, while ingredient picking avoids unwanted edge-hover scrolling.")
        ])
    ];

    public static void MarkPendingUpdate(string fromVersion, string toVersion)
    {
        Directory.CreateDirectory(AppFolder);
        File.WriteAllText(PendingUpdatePath, JsonSerializer.Serialize(new PendingUpdate(fromVersion, toVersion)));
    }

    public static UpdateSummary? GetUpdateSummary(string currentVersion)
    {
        Directory.CreateDirectory(AppFolder);
        var fromVersion = ReadPreviousVersion(currentVersion);
        File.WriteAllText(LastRunVersionPath, currentVersion);
        if (fromVersion is null || !TryVersion(fromVersion, out var from) || !TryVersion(currentVersion, out var current)
            || current <= from)
            return null;

        var applicableNotes = Notes
            .Where(note => TryVersion(note.Version, out var version) && version > from && version <= current)
            .OrderBy(note => Version.Parse(note.Version))
            .ToList();
        if (applicableNotes.Count == 0) return null;
        return new UpdateSummary(fromVersion, currentVersion, applicableNotes);
    }

    public static UpdateSummary? GetCurrentVersionSummary(string currentVersion)
    {
        var notes = Notes.FirstOrDefault(note =>
            note.Version.Equals(currentVersion.TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase));
        return notes is null
            ? null
            : new UpdateSummary(currentVersion, currentVersion, [notes]);
    }

    private static string? ReadPreviousVersion(string currentVersion)
    {
        if (File.Exists(PendingUpdatePath))
        {
            try
            {
                var pending = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(PendingUpdatePath));
                if (pending is not null && TryVersion(pending.ToVersion, out var target)
                    && TryVersion(currentVersion, out var current) && current >= target)
                {
                    File.Delete(PendingUpdatePath);
                    return pending.FromVersion;
                }
            }
            catch
            {
                // A damaged marker should never prevent the app from opening.
            }
        }

        try
        {
            return File.Exists(LastRunVersionPath) ? File.ReadAllText(LastRunVersionPath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryVersion(string value, out Version version) =>
        Version.TryParse(value.TrimStart('v', 'V').Split('-', '+')[0], out version!);

    private sealed record PendingUpdate(string FromVersion, string ToVersion);
}
