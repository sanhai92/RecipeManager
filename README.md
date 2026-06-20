# Recipe Manager for Windows

A clean, local Windows desktop recipe manager built with C# WPF and SQLite.

## Features

- Add, edit, and delete recipes
- Keep ingredients and required kitchen tools with each recipe
- Manage ingredient names centrally to prevent typos and duplicates
- Optionally assign Spring, Summer, Autumn, or Winter to ingredients
- See the current meteorological season discreetly in the main status bar
- Filter recipes that contain ingredients tagged for the current season
- Select ingredients from searchable lists when editing recipes or searching the pantry
- Attach, replace, or remove a recipe picture and website URL
- Record a cuisine, cooking time, and cooking instructions
- Store quantities and units for each recipe ingredient
- Set recipe servings and scale displayed ingredient quantities automatically
- Display countable ingredients dynamically, such as `1 apple` and `3 apples`
- Mark favorite recipes
- Enter the ingredients you have at home to find recipes you can make
- Store everything locally in SQLite (no account or internet connection required after setup)

## Requirements

- Windows 10 or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Run the app

1. Open PowerShell or Windows Terminal in this project folder.
2. Restore the SQLite package:

   ```powershell
   dotnet restore
   ```

3. Start the app:

   ```powershell
   dotnet run
   ```

You can also open `RecipeManager.csproj` in Visual Studio 2022, let NuGet restore packages, and press **F5**.

## Create a standalone Windows build

Run:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published app will be under `bin\Release\net8.0-windows\win-x64\publish`. Copy that folder anywhere on a 64-bit Windows computer and run `RecipeManager.exe`.

## Installer and updates

The project includes a per-user Windows installer and an automated GitHub Release workflow. The installer is self-contained, creates Start Menu and optional desktop shortcuts, does not require administrator rights, and preserves recipes during upgrades.

### Publish through GitHub (recommended)

1. Put this project in a public GitHub repository.
2. Push a version tag:

   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```

3. GitHub Actions builds `RecipeManager-Setup-1.0.0.exe` and attaches it to a new GitHub Release.
4. Download that installer on another Windows computer and run it.

For a later update, use a higher tag such as `v1.1.0`. Installed GitHub builds check the repository for new releases on startup and also provide **Check for updates** in the bottom status bar. The app downloads the attached installer, closes, installs silently, and reopens automatically after confirmation. The database remains under `%LOCALAPPDATA%\RecipeManager` and is not removed.

### Build an installer locally

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and [Inno Setup 6](https://jrsoftware.org/isinfo.php), then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version 1.0.0 -Repository "YOUR-GITHUB-NAME/YOUR-REPOSITORY"
```

The installer is written to `artifacts\installer`.

## Local data

Recipes are saved in:

```text
%LOCALAPPDATA%\RecipeManager\recipes.db
```

To back up your recipes, close the app and copy that file. Deleting the file resets the app to an empty recipe book.

## Ingredient search behavior

Check ingredients in the searchable **Ingredients I have** list. A recipe appears when it contains every ingredient you selected. For example, selecting `tomato` and `onion` finds recipes containing both. Use **Manage ingredients** to add or rename library entries. Renaming updates all recipes, and ingredients that are still used by recipes cannot be deleted.
