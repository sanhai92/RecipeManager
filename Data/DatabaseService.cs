using Microsoft.Data.Sqlite;
using RecipeManager.Models;
using System.IO;

namespace RecipeManager.Data;

public sealed class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecipeManager");
        Directory.CreateDirectory(appFolder);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(appFolder, "recipes.db"),
            ForeignKeys = true
        }.ToString();
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Recipes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Country TEXT NOT NULL DEFAULT '',
                Instructions TEXT NOT NULL DEFAULT '',
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                SourceUrl TEXT NOT NULL DEFAULT '',
                ImageData BLOB NULL,
                CookingTimeMinutes INTEGER NOT NULL DEFAULT 0,
                Servings INTEGER NOT NULL DEFAULT 4
            );

            CREATE TABLE IF NOT EXISTS Ingredients (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RecipeId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Quantity REAL NULL,
                Unit TEXT NOT NULL DEFAULT '',
                SortOrder INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (RecipeId) REFERENCES Recipes(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Tools (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RecipeId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (RecipeId) REFERENCES Recipes(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Ingredients_RecipeId ON Ingredients(RecipeId);
            CREATE INDEX IF NOT EXISTS IX_Tools_RecipeId ON Tools(RecipeId);

            CREATE TABLE IF NOT EXISTS AppMetadata (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS IngredientLibrary (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                PluralName TEXT NOT NULL DEFAULT '',
                Season TEXT NOT NULL DEFAULT ''
            );
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "Recipes", "SourceUrl", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Recipes", "ImageData", "BLOB NULL");
        EnsureColumn(connection, "Recipes", "CookingTimeMinutes", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Recipes", "Servings", "INTEGER NOT NULL DEFAULT 4");
        EnsureColumn(connection, "Ingredients", "Quantity", "REAL NULL");
        EnsureColumn(connection, "Ingredients", "Unit", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "IngredientLibrary", "Season", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "IngredientLibrary", "PluralName", "TEXT NOT NULL DEFAULT ''");
        SeedIngredientLibrary(connection);
    }

    public List<Recipe> GetRecipes()
    {
        using var connection = OpenConnection();
        var recipes = new List<Recipe>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Id, Title, Country, Instructions, IsFavorite, SourceUrl, ImageData, CookingTimeMinutes, Servings FROM Recipes ORDER BY IsFavorite DESC, Title COLLATE NOCASE";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                recipes.Add(new Recipe
                {
                    Id = reader.GetInt64(0),
                    Title = reader.GetString(1),
                    Cuisine = reader.GetString(2),
                    Instructions = reader.GetString(3),
                    IsFavorite = reader.GetBoolean(4),
                    SourceUrl = reader.GetString(5),
                    ImageData = reader.IsDBNull(6) ? null : (byte[])reader[6],
                    CookingTimeMinutes = reader.GetInt32(7),
                    Servings = Math.Max(1, reader.GetInt32(8))
                });
            }
        }

        foreach (var recipe in recipes)
        {
            recipe.Ingredients = GetIngredients(connection, recipe.Id);
            recipe.Tools = GetChildItems(connection, "Tools", recipe.Id);
        }

        return recipes;
    }

    public List<IngredientDefinition> GetIngredientLibrary()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, PluralName, Season FROM IngredientLibrary ORDER BY Name COLLATE NOCASE";
        using var reader = command.ExecuteReader();
        var ingredients = new List<IngredientDefinition>();
        while (reader.Read())
        {
            ingredients.Add(new IngredientDefinition
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                PluralName = reader.GetString(2),
                Season = reader.GetString(3)
            });
        }
        return ingredients;
    }

    public void AddIngredient(string name, string pluralName, string season)
    {
        name = name.Trim();
        if (name.Length == 0) throw new ArgumentException("Enter an ingredient name.");
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO IngredientLibrary (Name, PluralName, Season) VALUES ($name, $pluralName, $season)";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$pluralName", pluralName.Trim());
        command.Parameters.AddWithValue("$season", season.Trim());
        try { command.ExecuteNonQuery(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("That ingredient already exists.", ex);
        }
    }

    public void RenameIngredient(long id, string newName, string pluralName, string season)
    {
        newName = newName.Trim();
        if (newName.Length == 0) throw new ArgumentException("Enter an ingredient name.");
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        string oldName;
        using (var lookup = connection.CreateCommand())
        {
            lookup.Transaction = transaction;
            lookup.CommandText = "SELECT Name FROM IngredientLibrary WHERE Id=$id";
            lookup.Parameters.AddWithValue("$id", id);
            oldName = lookup.ExecuteScalar() as string ?? throw new InvalidOperationException("The ingredient no longer exists.");
        }

        try
        {
            using var updateLibrary = connection.CreateCommand();
            updateLibrary.Transaction = transaction;
            updateLibrary.CommandText = "UPDATE IngredientLibrary SET Name=$newName, PluralName=$pluralName, Season=$season WHERE Id=$id";
            updateLibrary.Parameters.AddWithValue("$newName", newName);
            updateLibrary.Parameters.AddWithValue("$pluralName", pluralName.Trim());
            updateLibrary.Parameters.AddWithValue("$season", season.Trim());
            updateLibrary.Parameters.AddWithValue("$id", id);
            updateLibrary.ExecuteNonQuery();

            using var updateRecipes = connection.CreateCommand();
            updateRecipes.Transaction = transaction;
            updateRecipes.CommandText = "UPDATE Ingredients SET Name=$newName WHERE Name=$oldName COLLATE NOCASE";
            updateRecipes.Parameters.AddWithValue("$newName", newName);
            updateRecipes.Parameters.AddWithValue("$oldName", oldName);
            updateRecipes.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("That ingredient name is already in the library.", ex);
        }
    }

    public int DeleteIngredient(long id)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        string name;
        using (var lookup = connection.CreateCommand())
        {
            lookup.Transaction = transaction;
            lookup.CommandText = "SELECT Name FROM IngredientLibrary WHERE Id=$id";
            lookup.Parameters.AddWithValue("$id", id);
            name = lookup.ExecuteScalar() as string ?? string.Empty;
        }

        using (var usage = connection.CreateCommand())
        {
            usage.Transaction = transaction;
            usage.CommandText = "SELECT COUNT(DISTINCT RecipeId) FROM Ingredients WHERE Name=$name COLLATE NOCASE";
            usage.Parameters.AddWithValue("$name", name);
            var count = Convert.ToInt32(usage.ExecuteScalar());
            if (count > 0) return count;
        }

        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM IngredientLibrary WHERE Id=$id";
        delete.Parameters.AddWithValue("$id", id);
        delete.ExecuteNonQuery();
        transaction.Commit();
        return 0;
    }

    public int SeedSampleRecipes()
    {
        using (var connection = OpenConnection())
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM AppMetadata WHERE Key='SampleRecipesV1'";
            if (Convert.ToInt32(check.ExecuteScalar()) > 0) return 0;
        }

        var existingTitles = GetRecipes().Select(x => x.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var recipe in SampleRecipes.Create().Where(x => !existingTitles.Contains(x.Title)))
        {
            Save(recipe);
            added++;
        }

        using var markerConnection = OpenConnection();
        using var marker = markerConnection.CreateCommand();
        marker.CommandText = "INSERT OR REPLACE INTO AppMetadata (Key, Value) VALUES ('SampleRecipesV1', $value)";
        marker.Parameters.AddWithValue("$value", DateTime.UtcNow.ToString("O"));
        marker.ExecuteNonQuery();
        return added;
    }

    public void ApplySampleCookingTimes()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var item in SampleRecipes.CookingTimes)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE Recipes SET CookingTimeMinutes=$minutes WHERE Title=$title COLLATE NOCASE AND CookingTimeMinutes=0";
            command.Parameters.AddWithValue("$minutes", item.Value);
            command.Parameters.AddWithValue("$title", item.Key);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public long Save(Recipe recipe)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        if (recipe.Id == 0)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Recipes (Title, Country, Instructions, IsFavorite, SourceUrl, ImageData, CookingTimeMinutes, Servings) VALUES ($title, $cuisine, $instructions, $favorite, $url, $image, $cookingTime, $servings); SELECT last_insert_rowid();";
            AddRecipeParameters(insert, recipe);
            recipe.Id = (long)(insert.ExecuteScalar() ?? 0L);
        }
        else
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE Recipes SET Title=$title, Country=$cuisine, Instructions=$instructions, IsFavorite=$favorite, SourceUrl=$url, ImageData=$image, CookingTimeMinutes=$cookingTime, Servings=$servings WHERE Id=$id";
            AddRecipeParameters(update, recipe);
            update.Parameters.AddWithValue("$id", recipe.Id);
            update.ExecuteNonQuery();

            DeleteChildren(connection, transaction, recipe.Id);
        }

        InsertIngredients(connection, transaction, recipe.Id, recipe.Ingredients);
        InsertChildren(connection, transaction, "Tools", recipe.Id, recipe.Tools);
        transaction.Commit();
        return recipe.Id;
    }

    public void Delete(long recipeId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Recipes WHERE Id=$id";
        command.Parameters.AddWithValue("$id", recipeId);
        command.ExecuteNonQuery();
    }

    public void SetFavorite(long recipeId, bool isFavorite)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Recipes SET IsFavorite=$favorite WHERE Id=$id";
        command.Parameters.AddWithValue("$favorite", isFavorite);
        command.Parameters.AddWithValue("$id", recipeId);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void SeedIngredientLibrary(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO IngredientLibrary (Name)
            SELECT DISTINCT TRIM(Name)
            FROM Ingredients
            WHERE LENGTH(TRIM(Name)) > 0;
            """;
        command.ExecuteNonQuery();
    }

    private static List<string> GetChildItems(SqliteConnection connection, string tableName, long recipeId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Name FROM {tableName} WHERE RecipeId=$id ORDER BY SortOrder, Id";
        command.Parameters.AddWithValue("$id", recipeId);
        using var reader = command.ExecuteReader();
        var items = new List<string>();
        while (reader.Read()) items.Add(reader.GetString(0));
        return items;
    }

    private static List<RecipeIngredient> GetIngredients(SqliteConnection connection, long recipeId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name, Quantity, Unit FROM Ingredients WHERE RecipeId=$id ORDER BY SortOrder, Id";
        command.Parameters.AddWithValue("$id", recipeId);
        using var reader = command.ExecuteReader();
        var items = new List<RecipeIngredient>();
        while (reader.Read())
        {
            items.Add(new RecipeIngredient
            {
                Name = reader.GetString(0),
                Quantity = reader.IsDBNull(1) ? null : reader.GetDouble(1),
                Unit = reader.GetString(2)
            });
        }
        return items;
    }

    private static void InsertIngredients(SqliteConnection connection, SqliteTransaction transaction, long recipeId, IEnumerable<RecipeIngredient> items)
    {
        var index = 0;
        foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Ingredients (RecipeId, Name, Quantity, Unit, SortOrder) VALUES ($recipeId, $name, $quantity, $unit, $sortOrder)";
            command.Parameters.AddWithValue("$recipeId", recipeId);
            command.Parameters.AddWithValue("$name", item.Name.Trim());
            command.Parameters.AddWithValue("$quantity", item.Quantity.HasValue ? item.Quantity.Value : DBNull.Value);
            command.Parameters.AddWithValue("$unit", item.Unit.Trim());
            command.Parameters.AddWithValue("$sortOrder", index++);
            command.ExecuteNonQuery();

            using var libraryCommand = connection.CreateCommand();
            libraryCommand.Transaction = transaction;
            libraryCommand.CommandText = "INSERT OR IGNORE INTO IngredientLibrary (Name) VALUES ($name)";
            libraryCommand.Parameters.AddWithValue("$name", item.Name.Trim());
            libraryCommand.ExecuteNonQuery();
        }
    }

    private static void AddRecipeParameters(SqliteCommand command, Recipe recipe)
    {
        command.Parameters.AddWithValue("$title", recipe.Title.Trim());
        command.Parameters.AddWithValue("$cuisine", recipe.Cuisine.Trim());
        command.Parameters.AddWithValue("$instructions", recipe.Instructions.Trim());
        command.Parameters.AddWithValue("$favorite", recipe.IsFavorite);
        command.Parameters.AddWithValue("$url", recipe.SourceUrl.Trim());
        command.Parameters.AddWithValue("$image", (object?)recipe.ImageData ?? DBNull.Value);
        command.Parameters.AddWithValue("$cookingTime", recipe.CookingTimeMinutes);
        command.Parameters.AddWithValue("$servings", Math.Max(1, recipe.Servings));
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase)) return;
        }
        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}";
        alter.ExecuteNonQuery();
    }

    private static void DeleteChildren(SqliteConnection connection, SqliteTransaction transaction, long recipeId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM Ingredients WHERE RecipeId=$id; DELETE FROM Tools WHERE RecipeId=$id;";
        command.Parameters.AddWithValue("$id", recipeId);
        command.ExecuteNonQuery();
    }

    private static void InsertChildren(SqliteConnection connection, SqliteTransaction transaction, string tableName, long recipeId, IEnumerable<string> items)
    {
        var index = 0;
        foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"INSERT INTO {tableName} (RecipeId, Name, SortOrder) VALUES ($recipeId, $name, $sortOrder)";
            command.Parameters.AddWithValue("$recipeId", recipeId);
            command.Parameters.AddWithValue("$name", item.Trim());
            command.Parameters.AddWithValue("$sortOrder", index++);
            command.ExecuteNonQuery();

        }
    }
}
