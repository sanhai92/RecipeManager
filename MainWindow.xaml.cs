using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Documents;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using RecipeManager.Data;
using RecipeManager.Models;
using RecipeManager.Services;

namespace RecipeManager;

public partial class MainWindow : Window
{
    private readonly DatabaseService _database = new();
    private readonly UpdateService _updateService = new();
    private readonly ObservableCollection<Recipe> _visibleRecipes = [];
    private readonly List<IngredientChoice> _pantryChoices = [];
    private List<Recipe> _allRecipes = [];
    private int _displayServings = 1;

    public MainWindow()
    {
        InitializeComponent();
        CurrentSeasonText.Text = $"Season · {GetCurrentSeason(DateTime.Now)}";
        VersionText.Text = $"v{_updateService.CurrentVersion}";
        Loaded += MainWindow_Loaded;
        RecipesList.ItemsSource = _visibleRecipes;

        try
        {
            _database.Initialize();
            _database.SeedSampleRecipes();
            _database.ApplySampleCookingTimes();
            LoadPantryChoices();
            ReloadRecipes();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The recipe database could not be opened.\n\n{ex.Message}", "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Recipe? SelectedRecipe => RecipesList.SelectedItem as Recipe;

    private void ReloadRecipes(long selectId = 0)
    {
        _allRecipes = _database.GetRecipes();
        ApplyFilters();
        if (selectId != 0)
            RecipesList.SelectedItem = _visibleRecipes.FirstOrDefault(x => x.Id == selectId);
    }

    private void ApplyFilters()
    {
        var selectedId = SelectedRecipe?.Id ?? 0;
        var pantry = GetSelectedPantryIngredients();
        IEnumerable<Recipe> filtered = _allRecipes;

        if (pantry.Count > 0)
        {
            filtered = filtered.Where(recipe => pantry.All(searched =>
                recipe.Ingredients.Any(recipeIngredient => IngredientMatches(recipeIngredient.Name, searched))));
        }

        if (FavoritesOnlyBox.IsChecked == true)
            filtered = filtered.Where(x => x.IsFavorite);

        if (SeasonalOnlyBox.IsChecked == true)
        {
            var currentSeason = GetCurrentSeason(DateTime.Now);
            var seasonalIngredients = _pantryChoices
                .Where(x => x.Season.Equals(currentSeason, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(recipe => recipe.Ingredients.Any(x => seasonalIngredients.Contains(x.Name)));
        }

        var results = filtered.ToList();
        _visibleRecipes.Clear();
        foreach (var recipe in results) _visibleRecipes.Add(recipe);

        ListHeading.Text = pantry.Count > 0 ? "Recipes with these ingredients" : "All recipes";
        StatusText.Text = pantry.Count > 0
            ? $"{results.Count} recipe{(results.Count == 1 ? "" : "s")} contain all searched ingredients"
            : $"{results.Count} recipe{(results.Count == 1 ? "" : "s")}";

        RecipesList.SelectedItem = _visibleRecipes.FirstOrDefault(x => x.Id == selectedId);
        if (RecipesList.SelectedItem is null) ShowDetails(null);
    }

    private HashSet<string> GetSelectedPantryIngredients() => _pantryChoices
        .Where(x => x.IsSelected)
        .Select(x => NormalizeIngredient(x.Name))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool IngredientMatches(string required, string available)
    {
        var normalizedRequired = NormalizeIngredient(required);
        return normalizedRequired.Equals(available, StringComparison.OrdinalIgnoreCase)
            || normalizedRequired.Contains(available, StringComparison.OrdinalIgnoreCase)
            || available.Contains(normalizedRequired, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIngredient(string value) => value.Trim().ToLowerInvariant();

    private void ShowDetails(Recipe? recipe)
    {
        EmptyState.Visibility = recipe is null ? Visibility.Visible : Visibility.Collapsed;
        DetailsPanel.Visibility = recipe is null ? Visibility.Collapsed : Visibility.Visible;
        EditButton.IsEnabled = recipe is not null;
        DeleteButton.IsEnabled = recipe is not null;
        if (recipe is null) return;

        DetailTitle.Text = recipe.Title;
        DetailCuisine.Text = recipe.CuisineDisplay;
        _displayServings = Math.Max(1, recipe.Servings);
        ServingCountText.Text = _displayServings.ToString();
        RenderIngredients(recipe);
        DetailTools.Text = recipe.ToolsDisplay;
        DetailInstructions.Text = string.IsNullOrWhiteSpace(recipe.Instructions) ? "No instructions added." : recipe.Instructions;
        FavoriteButton.Content = recipe.FavoriteGlyph;
        DetailImageBorder.Visibility = recipe.ImageData is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        DetailImage.Source = recipe.ImageData is { Length: > 0 } ? LoadImage(recipe.ImageData) : null;
        OpenUrlButton.Visibility = string.IsNullOrWhiteSpace(recipe.SourceUrl) ? Visibility.Collapsed : Visibility.Visible;
        OpenUrlButton.ToolTip = recipe.SourceUrl;
    }

    private void RenderIngredients(Recipe recipe)
    {
        DetailIngredientsPanel.Children.Clear();
        var searchedIngredients = GetSelectedPantryIngredients();

        if (recipe.Ingredients.Count == 0)
        {
            DetailIngredientsPanel.Children.Add(new TextBlock { Text = "No ingredients listed" });
            return;
        }

        foreach (var ingredient in recipe.Ingredients)
        {
            var line = new TextBlock { LineHeight = 24, TextWrapping = TextWrapping.Wrap };
            var isInSeason = _pantryChoices.Any(x =>
                x.Name.Equals(ingredient.Name, StringComparison.OrdinalIgnoreCase)
                && x.Season.Equals(GetCurrentSeason(DateTime.Now), StringComparison.OrdinalIgnoreCase));
            line.Inlines.Add(new Run("• " + FormatIngredient(ingredient, recipe))
            {
                Foreground = isInSeason ? new SolidColorBrush(Color.FromRgb(47, 125, 60)) : Brushes.Black,
                FontWeight = isInSeason ? FontWeights.SemiBold : FontWeights.Normal
            });
            if (isInSeason)
            {
                line.Inlines.Add(new Run("  In season")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(29, 101, 48)),
                    Background = new SolidColorBrush(Color.FromRgb(221, 241, 212)),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11
                });
            }
            if (searchedIngredients.Any(searched => IngredientMatches(ingredient.Name, searched)))
            {
                line.Inlines.Add(new Run("  ✓")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(40, 145, 92)),
                    FontWeight = FontWeights.Bold
                });
            }
            DetailIngredientsPanel.Children.Add(line);
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var editor = new RecipeEditorWindow(_database.GetIngredientLibrary()) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            var id = _database.Save(editor.Recipe);
            ReloadRecipes(id);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { } selected) return;
        var editor = new RecipeEditorWindow(_database.GetIngredientLibrary(), selected) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            _database.Save(editor.Recipe);
            ReloadRecipes(editor.Recipe.Id);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { } selected) return;
        var answer = MessageBox.Show(this, $"Delete ‘{selected.Title}’? This cannot be undone.", "Delete recipe", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        _database.Delete(selected.Id);
        ReloadRecipes();
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { } selected) return;
        _database.SetFavorite(selected.Id, !selected.IsFavorite);
        ReloadRecipes(selected.Id);
    }

    private void FavoriteFromList_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Recipe recipe }) return;
        _database.SetFavorite(recipe.Id, !recipe.IsFavorite);
        ReloadRecipes(recipe.Id);
        e.Handled = true;
    }

    private void RecipesList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowDetails(SelectedRecipe);

    private void SearchChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) ApplyFilters();
    }

    private void DecreaseServings_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { } recipe || _displayServings <= 1) return;
        _displayServings--;
        ServingCountText.Text = _displayServings.ToString();
        RenderIngredients(recipe);
    }

    private void IncreaseServings_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { } recipe || _displayServings >= 100) return;
        _displayServings++;
        ServingCountText.Text = _displayServings.ToString();
        RenderIngredients(recipe);
    }

    private void PantryLibrarySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PantryIngredientsList is not null) RefreshPantryIngredientList();
    }

    private void PantryIngredientChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) ApplyFilters();
    }

    private void ClearPantry_Click(object sender, RoutedEventArgs e)
    {
        foreach (var ingredient in _pantryChoices) ingredient.IsSelected = false;
        RefreshPantryIngredientList();
        ApplyFilters();
    }

    private void ManageIngredients_Click(object sender, RoutedEventArgs e)
    {
        var manager = new IngredientManagerWindow(_database) { Owner = this };
        manager.ShowDialog();
        LoadPantryChoices();
        ReloadRecipes(SelectedRecipe?.Id ?? 0);
    }

    private void LoadPantryChoices()
    {
        var selected = _pantryChoices.Where(x => x.IsSelected).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _pantryChoices.Clear();
        _pantryChoices.AddRange(_database.GetIngredientLibrary()
            .Select(x => new IngredientChoice
            {
                Name = x.Name,
                PluralName = x.PluralName,
                Season = x.Season,
                IsSelected = selected.Contains(x.Name)
            }));
        RefreshPantryIngredientList();
    }

    private void RefreshPantryIngredientList()
    {
        var search = PantryLibrarySearchBox?.Text.Trim() ?? string.Empty;
        PantryIngredientsList.ItemsSource = _pantryChoices
            .Where(x => search.Length == 0 || x.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .ToList();
    }

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { SourceUrl.Length: > 0 } recipe) return;
        if (!Uri.TryCreate(recipe.SourceUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "This recipe URL is not a valid http or https address.", "Invalid URL", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private void CopyMissingIngredients_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { } recipe) return;
        var searchedIngredients = GetSelectedPantryIngredients();
        var missing = recipe.Ingredients
            .Where(ingredient => !searchedIngredients.Any(searched => IngredientMatches(ingredient.Name, searched)))
            .ToList();

        var body = missing.Count > 0
            ? string.Join(Environment.NewLine, missing.Select(x => "- " + FormatIngredient(x, recipe)))
            : "No missing ingredients.";
        var clipboardText = $"{recipe.Title}{Environment.NewLine}{Environment.NewLine}{body}";

        try
        {
            Clipboard.SetText(clipboardText);
            StatusText.Text = missing.Count > 0
                ? $"Copied {missing.Count} missing ingredient{(missing.Count == 1 ? "" : "s")} for {recipe.Title}"
                : $"All ingredients for {recipe.Title} are already matched";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The ingredients could not be copied.\n\n{ex.Message}", "Clipboard error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static BitmapImage LoadImage(byte[] imageData)
    {
        using var stream = new MemoryStream(imageData);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private string FormatIngredient(RecipeIngredient ingredient, Recipe recipe)
    {
        if (!ingredient.Quantity.HasValue) return ingredient.Name;
        var scaled = ingredient.Quantity.Value * _displayServings / Math.Max(1, recipe.Servings);
        var quantity = scaled.ToString("0.##", CultureInfo.CurrentCulture);
        var unit = ingredient.Unit.Trim();
        var displayName = unit.Length == 0 && Math.Abs(scaled - 1) > 0.0001
            ? GetPluralIngredientName(ingredient.Name)
            : ingredient.Name;
        return string.Join(" ", new[] { quantity, unit, displayName }.Where(x => x.Length > 0));
    }

    private string GetPluralIngredientName(string name)
    {
        var definition = _pantryChoices.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(definition?.PluralName)) return definition.PluralName;
        return Pluralize(name);
    }

    private static string Pluralize(string name)
    {
        var lower = name.ToLowerInvariant();
        string[] uncountable = ["rice", "flour", "milk", "butter", "sugar", "salt", "oil", "cheese", "bread", "cream", "water", "pepper", "garlic"];
        if (uncountable.Contains(lower) || lower.EndsWith('s')) return name;
        if (lower.EndsWith("ch") || lower.EndsWith("sh") || lower.EndsWith('x') || lower.EndsWith('z')) return name + "es";
        if (lower.EndsWith('y') && lower.Length > 1 && !"aeiou".Contains(lower[^2])) return name[..^1] + "ies";
        if (lower.EndsWith('o')) return name + "es";
        return name + "s";
    }

    private static string GetCurrentSeason(DateTime date) => date.Month switch
    {
        3 or 4 or 5 => "Spring",
        6 or 7 or 8 => "Summer",
        9 or 10 or 11 => "Autumn",
        _ => "Winter"
    };

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        var result = await _updateService.CheckAsync();
        if (result.IsUpdateAvailable)
        {
            UpdateButton.Content = $"Update {result.LatestVersion} available";
            UpdateButton.Tag = result.ReleaseUrl;
            UpdateButton.Background = (System.Windows.Media.Brush)FindResource("LemonBrush");
            UpdateButton.Foreground = (System.Windows.Media.Brush)FindResource("DarkTextBrush");
            UpdateButton.Padding = new Thickness(8, 3, 8, 3);
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Checking…";
        var result = await _updateService.CheckAsync();
        UpdateButton.IsEnabled = true;

        if (!result.IsConfigured)
        {
            UpdateButton.Content = "Updates not configured";
            MessageBox.Show(this, "Update checks become active in builds published through the included GitHub release workflow.",
                "Updates", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            UpdateButton.Content = "Check for updates";
            MessageBox.Show(this, $"The update check could not be completed.\n\n{result.Error}",
                "Update check", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!result.IsUpdateAvailable)
        {
            UpdateButton.Content = "Check for updates";
            MessageBox.Show(this, $"Recipe Manager {result.CurrentVersion} is up to date.",
                "No updates available", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        UpdateButton.Content = $"Update {result.LatestVersion} available";
        if (string.IsNullOrWhiteSpace(result.InstallerUrl))
        {
            var openPage = MessageBox.Show(this,
                $"Recipe Manager {result.LatestVersion} is available, but its installer was not found. Open the release page?",
                "Installer unavailable", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (openPage == MessageBoxResult.Yes && result.ReleaseUrl is { Length: > 0 })
                Process.Start(new ProcessStartInfo(result.ReleaseUrl) { UseShellExecute = true });
            return;
        }

        var answer = MessageBox.Show(this,
            $"Recipe Manager {result.LatestVersion} is available. Download and install it now?\n\nThe app will close and reopen automatically. Your recipes will be preserved.",
            "Install update", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            UpdateButton.IsEnabled = false;
            var progress = new Progress<double>(value => UpdateButton.Content = $"Downloading… {value:0}%");
            var installer = await _updateService.DownloadInstallerAsync(result, progress);
            UpdateButton.Content = "Installing…";
            Process.Start(new ProcessStartInfo(installer)
            {
                UseShellExecute = true,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /NORESTART"
            });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateButton.IsEnabled = true;
            UpdateButton.Content = "Check for updates";
            MessageBox.Show(this, $"The update could not be installed.\n\n{ex.Message}",
                "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
