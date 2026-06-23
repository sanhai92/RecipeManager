using System.Windows;
using System.Windows.Controls;
using RecipeManager.Data;
using RecipeManager.Models;

namespace RecipeManager;

public partial class IngredientManagerWindow : Window
{
    private readonly DatabaseService _database;

    public IngredientManagerWindow(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
        SeasonBox.ItemsSource = new[] { "No season", "Spring", "Summer", "Autumn", "Winter" };
        SeasonBox.SelectedIndex = 0;
        CategoryBox.ItemsSource = new[]
        {
            "No category", "Fruit", "Vegetable", "Meat", "Fish & Seafood", "Dairy", "Dairy Alternative",
            "Grain & Pasta", "Legume", "Herb & Spice", "Condiment & Sauce", "Baking", "Other"
        };
        CategoryBox.SelectedIndex = 0;
        Reload();
    }

    private IngredientDefinition? SelectedIngredient => IngredientList.SelectedItem as IngredientDefinition;

    private void Reload(long selectId = 0)
    {
        IngredientList.ItemsSource = _database.GetIngredientLibrary();
        if (selectId != 0)
            IngredientList.SelectedItem = IngredientList.Items.Cast<IngredientDefinition>().FirstOrDefault(x => x.Id == selectId);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _database.AddIngredient(NameBox.Text, PluralNameBox.Text, AliasesBox.Text, SelectedSeason(), SelectedCategory());
            NameBox.Clear();
            PluralNameBox.Clear();
            AliasesBox.Clear();
            SeasonBox.SelectedIndex = 0;
            CategoryBox.SelectedIndex = 0;
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ingredient not added", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedIngredient is not { } selected)
        {
            MessageBox.Show(this, "Select an ingredient to rename.", "No ingredient selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            _database.RenameIngredient(selected.Id, NameBox.Text, PluralNameBox.Text, AliasesBox.Text, SelectedSeason(), SelectedCategory());
            Reload(selected.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ingredient not renamed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedIngredient is not { } selected)
        {
            MessageBox.Show(this, "Select an ingredient to delete.", "No ingredient selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var usageCount = _database.DeleteIngredient(selected.Id);
        if (usageCount > 0)
        {
            MessageBox.Show(this,
                $"‘{selected.Name}’ is used by {usageCount} recipe{(usageCount == 1 ? "" : "s")}. Remove or replace it in those recipes before deleting it.",
                "Ingredient is in use", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        NameBox.Clear();
        PluralNameBox.Clear();
        AliasesBox.Clear();
        SeasonBox.SelectedIndex = 0;
        CategoryBox.SelectedIndex = 0;
        Reload();
    }

    private void IngredientList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedIngredient is not { } selected) return;
        NameBox.Text = selected.Name;
        PluralNameBox.Text = selected.PluralName;
        AliasesBox.Text = selected.Aliases;
        SeasonBox.SelectedItem = string.IsNullOrWhiteSpace(selected.Season) ? "No season" : selected.Season;
        CategoryBox.SelectedItem = string.IsNullOrWhiteSpace(selected.Category) ? "No category" : selected.Category;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private string SelectedSeason() => SeasonBox.SelectedIndex <= 0 ? string.Empty : SeasonBox.SelectedItem?.ToString() ?? string.Empty;
    private string SelectedCategory() => CategoryBox.SelectedIndex <= 0 ? string.Empty : CategoryBox.SelectedItem?.ToString() ?? string.Empty;
}
