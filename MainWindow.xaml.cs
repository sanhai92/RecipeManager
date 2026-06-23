using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Documents;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using RecipeManager.Data;
using RecipeManager.Models;
using RecipeManager.Services;

namespace RecipeManager;

public partial class MainWindow : Window
{
    private readonly DatabaseService _database = new();
    private readonly UpdateService _updateService = new();
    private readonly OfflinePhotoImportService _photoImportService = new();
    private readonly ObservableCollection<Recipe> _visibleRecipes = [];
    private readonly ObservableCollection<InlineIngredientRow> _inlineIngredientRows = [];
    private readonly List<IngredientChoice> _pantryChoices = [];
    private List<Recipe> _allRecipes = [];
    private int _displayServings = 1;
    private CancellationTokenSource? _copyToastCancellation;
    private DispatcherTimer? _editButtonBlinkTimer;
    private bool _isInlineEditing;

    public ObservableCollection<string> InlineIngredientNames { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        InlineIngredientsGrid.ItemsSource = _inlineIngredientRows;
        CurrentSeasonText.Text = $"Season · {GetCurrentSeason(DateTime.Now)}";
        VersionText.Text = $"v{_updateService.CurrentVersion}";
        Loaded += MainWindow_Loaded;
        PreviewMouseDown += MainWindow_PreviewMouseDown;
        RecipesList.ItemsSource = _visibleRecipes;
        SeasonalFilterBox.ItemsSource = new[] { "All seasons", "Spring", "Summer", "Autumn", "Winter" };
        FavoriteFilterBox.ItemsSource = new[] { "All", "Yes", "No" };
        SeasonalFilterBox.SelectedIndex = 0;
        FavoriteFilterBox.SelectedIndex = 0;

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
        ApplyProteinIcons();
        RefreshCuisineFilter();
        ApplyFilters();
        if (selectId != 0)
            RecipesList.SelectedItem = _visibleRecipes.FirstOrDefault(x => x.Id == selectId);
    }

    private void ApplyFilters()
    {
        var selectedId = SelectedRecipe?.Id ?? 0;
        var pantry = GetSelectedPantryIngredients();
        var nameSearch = RecipeNameSearchBox?.Text.Trim() ?? string.Empty;
        var tagSearch = TagSearchBox?.Text.Trim() ?? string.Empty;
        IEnumerable<Recipe> filtered = _allRecipes;

        if (nameSearch.Length > 0)
            filtered = filtered.Where(recipe => BilingualSearchService.IsLooseMatch(recipe.Title, nameSearch));

        if (tagSearch.Length > 0)
            filtered = filtered.Where(recipe => BilingualSearchService.IsLooseMatch(string.Join(' ', recipe.Tags), tagSearch));

        if (pantry.Count > 0)
        {
            filtered = filtered.Where(recipe => pantry.All(searched =>
                recipe.Ingredients.Any(recipeIngredient => IngredientMatches(recipeIngredient.Name, searched))));
        }

        if (FavoriteFilterBox?.SelectedItem?.ToString() == "Yes")
            filtered = filtered.Where(x => x.IsFavorite);
        else if (FavoriteFilterBox?.SelectedItem?.ToString() == "No")
            filtered = filtered.Where(x => !x.IsFavorite);

        var selectedSeason = SeasonalFilterBox?.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(selectedSeason) && selectedSeason != "All seasons")
            filtered = filtered.Where(recipe => RecipeMatchesSeason(recipe, selectedSeason));

        var selectedCuisine = CuisineFilterBox?.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(selectedCuisine) && selectedCuisine != "All cuisines")
            filtered = filtered.Where(recipe => recipe.Cuisine.Equals(selectedCuisine, StringComparison.OrdinalIgnoreCase));

        var results = filtered.ToList();
        _visibleRecipes.Clear();
        foreach (var recipe in results) _visibleRecipes.Add(recipe);

        var hasFilters = nameSearch.Length > 0 || tagSearch.Length > 0 || pantry.Count > 0
            || FavoriteFilterBox?.SelectedIndex > 0 || SeasonalFilterBox?.SelectedIndex > 0
            || CuisineFilterBox?.SelectedIndex > 0;
        ListHeading.Text = hasFilters ? "Matching recipes" : "All recipes";
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
        return BilingualSearchService.AreEquivalent(required, available);
    }

    private static string NormalizeIngredient(string value) => value.Trim().ToLowerInvariant();

    private bool RecipeMatchesSeason(Recipe recipe, string season)
    {
        var seasonalIngredients = _pantryChoices
            .Where(choice => choice.Season.Equals(season, StringComparison.OrdinalIgnoreCase))
            .Select(choice => choice.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return recipe.Ingredients.Any(ingredient =>
            seasonalIngredients.Any(seasonal => IngredientMatches(ingredient.Name, seasonal)));
    }

    private void RecipeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilters();
    }

    private void FilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilters();
    }

    private void PantryPopupButton_Click(object sender, RoutedEventArgs e) =>
        PantryPopup.IsOpen = !PantryPopup.IsOpen;

    private void RefreshCuisineFilter()
    {
        if (CuisineFilterBox is null) return;
        var selected = CuisineFilterBox.SelectedItem?.ToString() ?? "All cuisines";
        var cuisines = _allRecipes.Select(recipe => recipe.Cuisine.Trim())
            .Where(cuisine => cuisine.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(cuisine => cuisine, StringComparer.CurrentCultureIgnoreCase)
            .Prepend("All cuisines")
            .ToList();
        CuisineFilterBox.ItemsSource = cuisines;
        CuisineFilterBox.SelectedItem = cuisines.Contains(selected, StringComparer.CurrentCultureIgnoreCase)
            ? cuisines.First(cuisine => cuisine.Equals(selected, StringComparison.CurrentCultureIgnoreCase))
            : "All cuisines";
    }

    private void ApplyProteinIcons()
    {
        foreach (var recipe in _allRecipes)
        {
            var hasChicken = recipe.Ingredients.Any(ingredient => IsChicken(ingredient.Name));
            var hasFish = recipe.Ingredients.Any(ingredient => IsFish(ingredient.Name));
            var hasMeat = recipe.Ingredients.Any(ingredient => IsMeat(ingredient.Name));
            var icons = new List<string>();
            var descriptions = new List<string>();
            if (hasMeat) { icons.Add("🥩"); descriptions.Add("Meat"); }
            if (hasFish) { icons.Add("🐟"); descriptions.Add("Fish or seafood"); }
            if (hasChicken) { icons.Add("🍗"); descriptions.Add("Chicken"); }
            if (icons.Count == 0) { icons.Add("🥦"); descriptions.Add("No meat, fish, or chicken"); }
            recipe.ProteinIcons = string.Join(" ", icons);
            recipe.ProteinIconsDescription = string.Join(", ", descriptions);
            recipe.ProteinKinds = descriptions.Select(description => description switch
            {
                "Meat" => "Meat",
                "Fish or seafood" => "Fish",
                "Chicken" => "Chicken",
                _ => "Vegetable"
            }).ToList();
        }
    }

    private bool IsChicken(string ingredientName)
    {
        if (IsPlantBasedAlternative(ingredientName)) return false;
        return ContainsAny(ingredientName, "chicken", "chicken breast", "poultry", "hen", "kip", "kipfilet", "kippenborst")
            || BilingualSearchService.AreEquivalent(ingredientName, "chicken")
            || FindIngredientCategory(ingredientName).Equals("Poultry", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsFish(string ingredientName)
    {
        if (IsPlantBasedAlternative(ingredientName)) return false;
        return FindIngredientCategory(ingredientName).Equals("Fish & Seafood", StringComparison.OrdinalIgnoreCase)
            || BilingualSearchService.AreEquivalent(ingredientName, "fish")
            || BilingualSearchService.AreEquivalent(ingredientName, "shrimp")
            || ContainsAny(ingredientName, "salmon", "tuna", "cod", "haddock", "anchovy", "sardine", "prawn", "crab", "lobster", "mussel");
    }

    private bool IsMeat(string ingredientName)
    {
        if (IsPlantBasedAlternative(ingredientName) || IsChicken(ingredientName) || IsFish(ingredientName)) return false;
        return FindIngredientCategory(ingredientName).Equals("Meat", StringComparison.OrdinalIgnoreCase)
            || ContainsAny(ingredientName, "beef", "pork", "lamb", "veal", "bacon", "ham", "steak", "minced meat", "ground meat", "ground beef", "sausage", "rundvlees", "varkensvlees");
    }

    private string FindIngredientCategory(string ingredientName) => _pantryChoices
        .FirstOrDefault(choice => choice.Name.Equals(ingredientName, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(choice.PluralName)
                && choice.PluralName.Equals(ingredientName, StringComparison.OrdinalIgnoreCase)))?.Category ?? string.Empty;

    private static bool IsPlantBasedAlternative(string ingredientName) =>
        ContainsAny(ingredientName, "vegan", "vegetarian", "plant based", "plant-based", "meatless", "mock");

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private void ShowDetails(Recipe? recipe)
    {
        if (_isInlineEditing)
            EndInlineEditMode();

        EmptyState.Visibility = recipe is null ? Visibility.Visible : Visibility.Collapsed;
        DetailsPanel.Visibility = recipe is null ? Visibility.Collapsed : Visibility.Visible;
        EditButton.IsEnabled = recipe is not null;
        DeleteButton.IsEnabled = recipe is not null;
        if (recipe is null) return;

        DetailTitle.Text = recipe.Title;
        DetailProteinIcons.ItemsSource = recipe.ProteinKinds;
        DetailProteinIcons.ToolTip = recipe.ProteinIconsDescription;
        DetailCuisine.Text = recipe.CuisineDisplay;
        DetailTagsItems.ItemsSource = recipe.Tags;
        DetailTagsItems.Visibility = recipe.Tags.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _displayServings = Math.Max(1, recipe.Servings);
        ServingCountText.Text = _displayServings.ToString();
        RenderIngredients(recipe);
        DetailTools.Text = recipe.ToolsDisplay;
        DetailInstructions.Text = string.IsNullOrWhiteSpace(recipe.Instructions) ? "No instructions added." : recipe.Instructions;
        FavoriteButton.Content = recipe.FavoriteGlyph;
        DetailImageBorder.Visibility = Visibility.Visible;
        DetailImage.Source = recipe.ImageData is { Length: > 0 } ? LoadImage(recipe.ImageData) : null;
        DetailImagePlaceholder.Visibility = recipe.ImageData is { Length: > 0 } ? Visibility.Collapsed : Visibility.Visible;
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

    private async void ImportPhoto_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose one or more recipe photos",
            Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            PhotoImportProgressOverlay.Visibility = Visibility.Visible;
            StatusText.Text = "Reading recipe photo offline…";
            var imported = await _photoImportService.ImportAsync(dialog.FileNames);
            PhotoImportProgressOverlay.Visibility = Visibility.Collapsed;
            Mouse.OverrideCursor = null;

            var editor = new RecipeEditorWindow(_database.GetIngredientLibrary(), imported.Recipe, imported.RawText) { Owner = this };
            if (editor.ShowDialog() != true)
            {
                StatusText.Text = "Photo import cancelled";
                return;
            }

            var id = _database.Save(editor.Recipe);
            LoadPantryChoices();
            ReloadRecipes(id);
            StatusText.Text = $"Imported {editor.Recipe.Title} from photo";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"The photo could not be imported offline.\n\n{ex.Message}\n\nTry a sharper photo with good lighting, or install the matching OCR language in Windows Language settings.",
                "Photo import failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Photo import failed";
        }
        finally
        {
            PhotoImportProgressOverlay.Visibility = Visibility.Collapsed;
            Mouse.OverrideCursor = null;
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { } selected) return;
        BeginInlineEdit(selected);
    }

    private void BeginInlineEdit(Recipe recipe)
    {
        _isInlineEditing = true;
        InlineTitleBox.Text = recipe.Title;
        InlineCuisineBox.Text = recipe.Cuisine;
        InlineTagsBox.Text = string.Join(", ", recipe.Tags);
        InlineServingsBox.Text = Math.Max(1, recipe.Servings).ToString(CultureInfo.CurrentCulture);
        InlineSourceUrlBox.Text = recipe.SourceUrl;
        InlineToolsBox.Text = string.Join(Environment.NewLine, recipe.Tools);
        InlineInstructionsBox.Text = recipe.Instructions;

        _inlineIngredientRows.Clear();
        foreach (var ingredient in recipe.Ingredients)
        {
            _inlineIngredientRows.Add(new InlineIngredientRow
            {
                QuantityText = ingredient.Quantity?.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty,
                Unit = ingredient.Unit,
                Name = ingredient.Name
            });
        }

        ToggleInlineEditMode(true);
        StatusText.Text = $"Editing {recipe.Title}";
        InlineTitleBox.Focus();
        InlineTitleBox.SelectAll();
    }

    private void SaveInlineEdit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { } selected) return;

        InlineIngredientsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        InlineIngredientsGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var title = InlineTitleBox.Text.Trim();
        if (title.Length == 0)
        {
            MessageBox.Show(this, "Recipe name is required.", "Cannot save recipe", MessageBoxButton.OK, MessageBoxImage.Information);
            InlineTitleBox.Focus();
            return;
        }

        if (!int.TryParse(InlineServingsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var servings)
            || servings < 1 || servings > 100)
        {
            MessageBox.Show(this, "Servings must be a whole number between 1 and 100.", "Cannot save recipe", MessageBoxButton.OK, MessageBoxImage.Information);
            InlineServingsBox.Focus();
            return;
        }

        var sourceUrl = InlineSourceUrlBox.Text.Trim();
        if (sourceUrl.Length > 0
            && (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            MessageBox.Show(this, "Recipe website must be a valid http or https address.", "Cannot save recipe", MessageBoxButton.OK, MessageBoxImage.Information);
            InlineSourceUrlBox.Focus();
            return;
        }

        var library = _database.GetIngredientLibrary();
        var ingredients = new List<RecipeIngredient>();
        foreach (var row in _inlineIngredientRows)
        {
            var name = row.Name.Trim();
            if (name.Length == 0) continue;

            var definition = library.FirstOrDefault(definition =>
                BilingualSearchService.MatchesIngredient(definition, name));
            if (definition is null)
            {
                MessageBox.Show(this,
                    $"'{name}' is not in your ingredient library yet.\n\nAdd it through Ingredients first, then choose it here.",
                    "Unknown ingredient", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double? quantity = null;
            if (!string.IsNullOrWhiteSpace(row.QuantityText))
            {
                if (!double.TryParse(row.QuantityText.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)
                    || parsed < 0)
                {
                    MessageBox.Show(this, $"Quantity for '{name}' is not valid.", "Cannot save recipe", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                quantity = parsed;
            }

            ingredients.Add(new RecipeIngredient
            {
                Name = definition.Name,
                Quantity = quantity,
                Unit = row.Unit.Trim()
            });
        }

        selected.Title = title;
        selected.Cuisine = InlineCuisineBox.Text.Trim();
        selected.Servings = servings;
        selected.SourceUrl = sourceUrl;
        selected.Instructions = InlineInstructionsBox.Text.Trim();
        selected.Ingredients = ingredients;
        selected.Tools = SplitLines(InlineToolsBox.Text);
        selected.Tags = SplitTags(InlineTagsBox.Text);

        _database.Save(selected);
        EndInlineEditMode();
        LoadPantryChoices();
        ReloadRecipes(selected.Id);
        StatusText.Text = $"Saved {selected.Title}";
    }

    private void CancelInlineEdit_Click(object sender, RoutedEventArgs e)
    {
        EndInlineEditMode();
        ShowDetails(SelectedRecipe);
        StatusText.Text = "Editing cancelled";
    }

    private void AddInlineIngredient_Click(object sender, RoutedEventArgs e)
    {
        _inlineIngredientRows.Add(new InlineIngredientRow());
        InlineIngredientsGrid.SelectedIndex = _inlineIngredientRows.Count - 1;
        InlineIngredientsGrid.ScrollIntoView(InlineIngredientsGrid.SelectedItem);
    }

    private void RemoveInlineIngredient_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is InlineIngredientRow row)
            _inlineIngredientRows.Remove(row);
    }

    private void InlineIngredientPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is InlineIngredientRow row)
            row.IsPickerOpen = false;
    }

    private void ToggleInlineEditMode(bool isEditing)
    {
        DetailReadHeader.Visibility = isEditing ? Visibility.Collapsed : Visibility.Visible;
        InlineEditHeader.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
        DetailReadSections.Visibility = isEditing ? Visibility.Collapsed : Visibility.Visible;
        InlineEditSections.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
        DetailActionButtons.Visibility = isEditing ? Visibility.Collapsed : Visibility.Visible;
        EditButton.Visibility = isEditing ? Visibility.Collapsed : Visibility.Visible;
        SaveInlineEditButton.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
        CancelInlineEditButton.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
        FavoriteButton.IsEnabled = !isEditing;
        RecipesList.IsEnabled = !isEditing;
    }

    private void EndInlineEditMode()
    {
        _isInlineEditing = false;
        ToggleInlineEditMode(false);
        _inlineIngredientRows.Clear();
        StopEditButtonBlink();
    }

    private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isInlineEditing) return;
        if (IsInsideRecipePanel(e.OriginalSource as DependencyObject)) return;

        e.Handled = true;
        ShowCopyToast("Finish editing your recipe first");
        BlinkInlineEditButtons();
    }

    private bool IsInsideRecipePanel(DependencyObject? source)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, DetailsPanel))
                return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void BlinkInlineEditButtons()
    {
        StopEditButtonBlink();
        var normalSave = SaveInlineEditButton.Background;
        var normalCancel = CancelInlineEditButton.Background;
        var highlight = (Brush)FindResource("PeachBrush");
        var ticks = 0;

        _editButtonBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _editButtonBlinkTimer.Tick += (_, _) =>
        {
            ticks++;
            var isHighlighted = ticks % 2 == 1;
            SaveInlineEditButton.Background = isHighlighted ? highlight : normalSave;
            CancelInlineEditButton.Background = isHighlighted ? highlight : normalCancel;

            if (ticks >= 6)
            {
                StopEditButtonBlink();
                SaveInlineEditButton.Background = normalSave;
                CancelInlineEditButton.Background = normalCancel;
            }
        };
        _editButtonBlinkTimer.Start();
    }

    private void StopEditButtonBlink()
    {
        _editButtonBlinkTimer?.Stop();
        _editButtonBlinkTimer = null;
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
        RefreshSelectedPantryChips();
        if (IsLoaded) ApplyFilters();
    }

    private void RemovePantryIngredient_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not IngredientChoice ingredient) return;
        ingredient.IsSelected = false;
        RefreshPantryIngredientList();
        RefreshSelectedPantryChips();
        ApplyFilters();
    }

    private void ClearPantry_Click(object sender, RoutedEventArgs e)
    {
        foreach (var ingredient in _pantryChoices) ingredient.IsSelected = false;
        RefreshPantryIngredientList();
        RefreshSelectedPantryChips();
        ApplyFilters();
    }

    private void ClearAllFilters_Click(object sender, RoutedEventArgs e)
    {
        RecipeNameSearchBox.Clear();
        TagSearchBox.Clear();
        SeasonalFilterBox.SelectedIndex = 0;
        FavoriteFilterBox.SelectedIndex = 0;
        CuisineFilterBox.SelectedIndex = 0;
        PantryLibrarySearchBox.Clear();
        foreach (var ingredient in _pantryChoices) ingredient.IsSelected = false;
        RefreshPantryIngredientList();
        RefreshSelectedPantryChips();
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
                Aliases = x.Aliases,
                Season = x.Season,
                Category = x.Category,
                IsSelected = selected.Contains(x.Name)
            }));
        InlineIngredientNames.Clear();
        foreach (var name in _pantryChoices
                     .Select(choice => choice.Name)
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
            InlineIngredientNames.Add(name);
        RefreshPantryIngredientList();
        RefreshSelectedPantryChips();
    }

    private void RefreshPantryIngredientList()
    {
        var search = PantryLibrarySearchBox?.Text.Trim() ?? string.Empty;
        PantryIngredientsList.ItemsSource = _pantryChoices
            .Where(x => search.Length == 0 || BilingualSearchService.IsLooseMatch($"{x.Name} {x.PluralName} {x.Aliases}", search))
            .ToList();
    }

    private void RefreshSelectedPantryChips()
    {
        var selected = _pantryChoices.Where(ingredient => ingredient.IsSelected).ToList();
        SelectedPantryChips.ItemsSource = selected;
        PantrySelectionSummary.Text = selected.Count == 0
            ? string.Empty
            : "Filtering with: " + string.Join(", ", selected.Select(ingredient => ingredient.Name));
        PantrySelectionSummary.ToolTip = selected.Count == 0
            ? null
            : string.Join(Environment.NewLine, selected.Select(ingredient => ingredient.Name));
        PantrySelectionSummary.Visibility = selected.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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
            ShowCopyToast(missing.Count > 0
                ? $"✓ {missing.Count} missing ingredient{(missing.Count == 1 ? "" : "s")} copied"
                : "✓ Text copied");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The ingredients could not be copied.\n\n{ex.Message}", "Clipboard error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShareRecipeCode_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecipe is not { } recipe) return;
        try
        {
            var code = RecipeShareService.Encode(recipe, _database.GetIngredientLibrary());
            Clipboard.SetText(code);
            ShowCopyToast("✓ Recipe code copied");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The recipe code could not be copied.\n\n{ex.Message}",
                "Sharing failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ShowCopyToast(string message)
    {
        _copyToastCancellation?.Cancel();
        _copyToastCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _copyToastCancellation = cancellation;
        CopyToastText.Text = message;
        CopyToast.Visibility = Visibility.Visible;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2.4), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ReferenceEquals(_copyToastCancellation, cancellation))
        {
            CopyToast.Visibility = Visibility.Collapsed;
            _copyToastCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ImportRecipeCode_Click(object sender, RoutedEventArgs e)
    {
        var importWindow = new RecipeCodeImportWindow { Owner = this };
        if (importWindow.ShowDialog() != true || importWindow.DecodedShare is not { } decodedShare) return;
        var importedRecipe = decodedShare.Recipe;
        var ingredientLibrary = _database.GetIngredientLibrary();

        var preview = new SharedRecipePreviewWindow(
            importedRecipe, ingredientLibrary, decodedShare.IngredientDefinitions) { Owner = this };
        if (preview.ShowDialog() != true) return;

        var editingLibrary = ingredientLibrary
            .Concat(preview.IngredientsToAddToLibrary)
            .GroupBy(ingredient => ingredient.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var editor = new RecipeEditorWindow(editingLibrary, importedRecipe) { Owner = this };
        if (editor.ShowDialog() != true) return;
        importedRecipe = editor.Recipe;

        var existing = _allRecipes.FirstOrDefault(recipe =>
            recipe.Title.Equals(importedRecipe.Title, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            var choice = MessageBox.Show(this,
                $"A recipe named ‘{existing.Title}’ already exists.\n\nChoose Yes to replace it, No to add the shared recipe as a copy, or Cancel to stop.",
                "Recipe already exists", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel) return;
            if (choice == MessageBoxResult.Yes)
            {
                importedRecipe.Id = existing.Id;
                importedRecipe.IsFavorite = existing.IsFavorite;
                importedRecipe.ImageData ??= existing.ImageData;
            }
            else
            {
                importedRecipe.Id = 0;
                importedRecipe.Title = GetUniqueRecipeTitle(importedRecipe.Title);
            }
        }

        var id = _database.Save(importedRecipe, preview.IngredientsToAddToLibrary);
        LoadPantryChoices();
        ReloadRecipes(id);
        StatusText.Text = $"Imported {importedRecipe.Title}";
    }

    private string GetUniqueRecipeTitle(string baseTitle)
    {
        var candidate = $"{baseTitle} (shared)";
        var number = 2;
        while (_allRecipes.Any(recipe => recipe.Title.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseTitle} (shared {number++})";
        return candidate;
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

    private static List<string> SplitLines(string text) => text
        .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static List<string> SplitTags(string text) => text
        .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(tag => tag.Trim().TrimStart('#'))
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static string GetCurrentSeason(DateTime date) => date.Month switch
    {
        3 or 4 or 5 => "Spring",
        6 or 7 or 8 => "Summer",
        9 or 10 or 11 => "Autumn",
        _ => "Winter"
    };

    private void HighlightAvailableUpdate(UpdateCheckResult result)
    {
        UpdateButton.Content = $"↑ Update {result.LatestVersion} available";
        UpdateButton.Tag = result.ReleaseUrl;
        UpdateButton.ToolTip = "A newer version of Recipe Manager is ready to install";
        UpdateButton.Background = (Brush)FindResource("LemonBrush");
        UpdateButton.Foreground = (Brush)FindResource("DarkTextBrush");
        UpdateButton.FontWeight = FontWeights.Bold;
        UpdateButton.Padding = new Thickness(10, 4, 10, 4);
    }

    private void ResetUpdateButton()
    {
        UpdateButton.Content = "Check for updates";
        UpdateButton.Tag = null;
        UpdateButton.ToolTip = null;
        UpdateButton.Background = Brushes.Transparent;
        UpdateButton.Foreground = (Brush)FindResource("PrimaryBrush");
        UpdateButton.FontWeight = FontWeights.SemiBold;
        UpdateButton.Padding = new Thickness(4, 1, 4, 1);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        var updateSummary = ReleaseNotesService.GetUpdateSummary(_updateService.CurrentVersion);
        if (updateSummary is not null)
            new WhatsNewWindow(updateSummary) { Owner = this }.ShowDialog();
        var result = await _updateService.CheckAsync();
        if (result.IsUpdateAvailable)
            HighlightAvailableUpdate(result);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Checking…";
        var result = await _updateService.CheckAsync();
        UpdateButton.IsEnabled = true;

        if (!result.IsConfigured)
        {
            ResetUpdateButton();
            UpdateButton.Content = "Updates not configured";
            MessageBox.Show(this, "Update checks become active in builds published through the included GitHub release workflow.",
                "Updates", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            ResetUpdateButton();
            MessageBox.Show(this, $"The update check could not be completed.\n\n{result.Error}",
                "Update check", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!result.IsUpdateAvailable)
        {
            ResetUpdateButton();
            MessageBox.Show(this, $"Recipe Manager {result.CurrentVersion} is up to date.",
                "No updates available", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        HighlightAvailableUpdate(result);
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
            UpdateButton.Content = "Backing up recipes…";
            _database.CreateBackup($"before-update-{result.LatestVersion}");
            var progress = new Progress<double>(value => UpdateButton.Content = $"Downloading… {value:0}%");
            var installer = await _updateService.DownloadInstallerAsync(result, progress);
            ReleaseNotesService.MarkPendingUpdate(result.CurrentVersion, result.LatestVersion ?? result.CurrentVersion);
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
            ResetUpdateButton();
            MessageBox.Show(this, $"The update could not be installed.\n\n{ex.Message}",
                "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BackupRecipes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var backupPath = _database.CreateBackup();
            StatusText.Text = "Recipe backup created";
            MessageBox.Show(this,
                $"Your recipes and pictures were backed up successfully.\n\n{backupPath}\n\nThe five newest backups are kept automatically.",
                "Backup complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The backup could not be created.\n\n{ex.Message}",
                "Backup failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreRecipes_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Restore recipe backup",
            InitialDirectory = Directory.Exists(_database.BackupsFolder)
                ? _database.BackupsFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Filter = "Recipe database backups (*.db)|*.db|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        var answer = MessageBox.Show(this,
            "Restore this backup?\n\nYour current recipes will first be backed up automatically, then replaced by the selected copy.",
            "Restore recipes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            _database.RestoreBackup(dialog.FileName);
            LoadPantryChoices();
            ReloadRecipes();
            StatusText.Text = "Recipe backup restored";
            MessageBox.Show(this, "Your recipes and pictures were restored successfully.",
                "Restore complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"The backup could not be restored. Your previous database has been kept.\n\n{ex.Message}",
                "Restore failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WhatsNew_Click(object sender, RoutedEventArgs e)
    {
        var summary = ReleaseNotesService.GetCurrentVersionSummary(_updateService.CurrentVersion);
        if (summary is not null)
            new WhatsNewWindow(summary, showVersionComparison: false) { Owner = this }.ShowDialog();
    }

    private sealed class InlineIngredientRow : INotifyPropertyChanged
    {
        private string _quantityText = string.Empty;
        private string _unit = string.Empty;
        private string _name = string.Empty;
        private bool _isPickerOpen;

        public string QuantityText
        {
            get => _quantityText;
            set => SetField(ref _quantityText, value);
        }

        public string Unit
        {
            get => _unit;
            set => SetField(ref _unit, value);
        }

        public string Name
        {
            get => _name;
            set
            {
                if (SetField(ref _name, value))
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        public bool IsPickerOpen
        {
            get => _isPickerOpen;
            set => SetField(ref _isPickerOpen, value);
        }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Choose ingredient" : Name;

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
        {
            if (field == value) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private bool SetField(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
        {
            if (field == value) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
