using RecipeManager.Models;

namespace RecipeManager.Data;

internal static class SampleRecipes
{
    private static readonly IReadOnlyList<Recipe> Recipes =
    [
        R("Spaghetti Aglio e Olio", "Italian", 20,
            I(("spaghetti", 320, "g"), ("garlic", 4, "cloves"), ("olive oil", 4, "tbsp"), ("chili flakes", 1, "tsp"), ("parsley", 15, "g")),
            ["large pot", "frying pan"],
            "Cook the spaghetti until al dente. Gently fry sliced garlic and chili flakes in olive oil. Toss with the drained pasta, a splash of cooking water, and chopped parsley."),
        R("Creamy Chickpea Curry", "Indian", 35,
            I(("chickpeas", 480, "g"), ("coconut milk", 400, "ml"), ("tomato", 400, "g"), ("onion", 1, ""), ("garlic", 3, "cloves"), ("ginger", 15, "g"), ("curry powder", 2, "tbsp")),
            ["large pot", "wooden spoon"],
            "Soften the onion, garlic, and ginger. Stir in curry powder, tomato, chickpeas, and coconut milk. Simmer until rich and creamy.", true),
        R("Rainbow Vegetable Stir-Fry", "Chinese-inspired", 25,
            I(("broccoli", 250, "g"), ("bell pepper", 2, ""), ("carrot", 2, ""), ("mushrooms", 200, "g"), ("soy sauce", 3, "tbsp"), ("garlic", 2, "cloves"), ("sesame oil", 1, "tbsp")),
            ["wok", "knife"],
            "Cut the vegetables into bite-sized pieces. Stir-fry the garlic and vegetables over high heat. Add soy sauce and sesame oil and cook until crisp-tender."),
        R("Fluffy Banana Pancakes", "American", 20,
            I(("banana", 2, ""), ("flour", 180, "g"), ("oat milk", 250, "ml"), ("baking powder", 2, "tsp"), ("cinnamon", 1, "tsp"), ("vegetable oil", 1, "tbsp")),
            ["mixing bowl", "whisk", "frying pan"],
            "Mash the bananas and whisk with oat milk. Fold in flour, baking powder, and cinnamon. Cook small ladlefuls in a lightly oiled pan until golden."),
        R("Smoky Tofu Shakshuka", "North African-inspired", 35,
            I(("tofu", 300, "g"), ("tomato", 800, "g"), ("bell pepper", 1, ""), ("onion", 1, ""), ("garlic", 3, "cloves"), ("cumin", 1, "tsp"), ("smoked paprika", 1, "tsp")),
            ["large frying pan"],
            "Soften the onion and pepper. Add garlic, spices, and tomato and simmer until thick. Crumble in the tofu and cook for another five minutes."),
        R("Black Bean Tacos", "Mexican-inspired", 30,
            I(("black beans", 480, "g"), ("tortillas", 8, ""), ("avocado", 1, ""), ("tomato", 2, ""), ("red onion", 1, ""), ("lime", 1, ""), ("cumin", 1, "tsp")),
            ["frying pan", "knife"],
            "Warm the beans with cumin. Heat the tortillas and fill with beans, avocado, tomato, and onion. Finish with lime juice.", true),
        R("Mediterranean Chickpea Salad", "Mediterranean", 15,
            I(("chickpeas", 480, "g"), ("cucumber", 1, ""), ("tomato", 3, ""), ("red onion", 1, ""), ("olives", 100, "g"), ("lemon", 1, ""), ("olive oil", 2, "tbsp")),
            ["knife", "salad bowl"],
            "Chop the vegetables and combine with chickpeas and olives. Dress with lemon juice, olive oil, salt, and pepper."),
        R("Caramelized Onion Soup", "French-inspired", 70,
            I(("onion", 6, ""), ("vegetable stock", 1, "l"), ("olive oil", 3, "tbsp"), ("bread", 4, "slices"), ("thyme", 1, "tsp"), ("nutritional yeast", 2, "tbsp")),
            ["large pot", "oven-safe bowls"],
            "Slowly caramelize the sliced onions in olive oil. Add stock and thyme and simmer. Top each bowl with toasted bread and nutritional yeast."),
        R("Peanut Tofu Noodles", "Thai-inspired", 35,
            I(("rice noodles", 300, "g"), ("tofu", 300, "g"), ("bean sprouts", 150, "g"), ("peanut butter", 3, "tbsp"), ("soy sauce", 2, "tbsp"), ("lime", 1, ""), ("peanuts", 50, "g")),
            ["wok", "large pot"],
            "Cook the noodles. Brown the tofu, then add noodles and a sauce of peanut butter, soy sauce, lime, and a little water. Fold in sprouts and garnish with peanuts."),
        R("Creamy Mushroom Risotto", "Italian", 45,
            I(("arborio rice", 300, "g"), ("mushrooms", 350, "g"), ("onion", 1, ""), ("vegetable stock", 1, "l"), ("white wine", 150, "ml"), ("nutritional yeast", 3, "tbsp")),
            ["saucepan", "large frying pan", "ladle"],
            "Brown the mushrooms. Soften the onion, toast the rice, and add wine. Stir in hot stock one ladle at a time. Finish with mushrooms and nutritional yeast.", true),
        R("Dutch Kale Stamppot", "Dutch", 40,
            I(("potatoes", 1, "kg"), ("kale", 400, "g"), ("oat milk", 150, "ml"), ("vegan sausage", 4, ""), ("mustard", 2, "tbsp")),
            ["large pot", "potato masher", "frying pan"],
            "Boil the potatoes and kale until tender. Brown the vegan sausages separately. Mash with oat milk and mustard and serve with the sausages."),
        R("Red Lentil Dal", "Indian", 35,
            I(("red lentils", 300, "g"), ("coconut milk", 400, "ml"), ("tomato", 400, "g"), ("onion", 1, ""), ("garlic", 3, "cloves"), ("turmeric", 1, "tsp"), ("cumin", 1, "tsp")),
            ["large pot"],
            "Cook the onion and garlic with the spices. Add lentils, tomato, coconut milk, and water. Simmer until the lentils are soft."),
        R("Swedish Lentil Balls", "Swedish-inspired", 50,
            I(("green lentils", 400, "g"), ("breadcrumbs", 100, "g"), ("onion", 1, ""), ("mushrooms", 200, "g"), ("oat cream", 250, "ml"), ("vegetable stock", 250, "ml")),
            ["mixing bowl", "frying pan", "food processor"],
            "Pulse cooked lentils, mushrooms, onion, and breadcrumbs. Shape into balls and brown. Simmer oat cream and stock into a sauce and return the balls to the pan."),
        R("Tomato Basil Bruschetta", "Italian", 15,
            I(("bread", 8, "slices"), ("tomato", 4, ""), ("garlic", 2, "cloves"), ("basil", 20, "g"), ("olive oil", 2, "tbsp")),
            ["oven", "knife", "mixing bowl"],
            "Toast the bread. Mix chopped tomato with basil and olive oil. Rub the toast with garlic and spoon the tomato mixture on top."),
        R("Kombu Miso Soup", "Japanese", 20,
            I(("miso paste", 4, "tbsp"), ("tofu", 250, "g"), ("wakame", 10, "g"), ("spring onion", 2, ""), ("kombu stock", 1, "l")),
            ["saucepan"],
            "Warm the kombu stock and add tofu and wakame. Dissolve miso in a little warm stock, stir it into the pan without boiling, and garnish with spring onion."),
        R("Vegan Banana Bread", "American", 65,
            I(("banana", 3, ""), ("flour", 250, "g"), ("brown sugar", 100, "g"), ("vegetable oil", 80, "ml"), ("oat milk", 80, "ml"), ("baking soda", 1, "tsp")),
            ["mixing bowl", "loaf pan", "oven"],
            "Mash the bananas and mix with sugar, oil, and oat milk. Fold in flour and baking soda. Bake in a loaf pan until a skewer comes out clean."),
        R("Spanish Chickpea Tortilla", "Spanish-inspired", 45,
            I(("potatoes", 600, "g"), ("chickpea flour", 150, "g"), ("onion", 1, ""), ("water", 250, "ml"), ("olive oil", 3, "tbsp")),
            ["frying pan", "mixing bowl"],
            "Gently cook sliced potatoes and onion. Whisk chickpea flour with water, fold in the vegetables, and cook in a pan. Flip and cook until firm."),
        R("Hearty Lentil Soup", "Middle Eastern-inspired", 45,
            I(("lentils", 300, "g"), ("onion", 1, ""), ("carrot", 2, ""), ("celery", 2, "stalks"), ("garlic", 3, "cloves"), ("vegetable stock", 1, "l"), ("cumin", 1, "tsp")),
            ["large pot", "blender"],
            "Soften the vegetables and garlic with cumin. Add lentils and stock and simmer until tender. Blend a small portion for a thicker texture."),
        R("Roasted Vegetable Pizza", "Italian-inspired", 35,
            I(("pizza dough", 1, ""), ("tomato sauce", 200, "g"), ("zucchini", 1, ""), ("bell pepper", 1, ""), ("mushrooms", 150, "g"), ("olives", 60, "g")),
            ["oven", "baking tray"],
            "Stretch the dough and spread with tomato sauce. Add sliced vegetables and olives. Bake in a very hot oven until the crust is browned."),
        R("Apple Cinnamon Crumble", "British", 50,
            I(("apples", 6, ""), ("flour", 180, "g"), ("vegan butter", 100, "g"), ("brown sugar", 100, "g"), ("cinnamon", 2, "tsp")),
            ["baking dish", "mixing bowl", "oven"],
            "Slice the apples into a baking dish and sprinkle with cinnamon. Rub vegan butter into flour and sugar, scatter over the apples, and bake until golden.")
    ];

    public static IReadOnlyDictionary<string, int> CookingTimes { get; } = Recipes
        .ToDictionary(recipe => recipe.Title, recipe => recipe.CookingTimeMinutes, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Recipe> Create() => Recipes.Select(Clone).ToList();

    public static IEnumerable<IngredientDefinition> DefinitionsFor(Recipe recipe) => recipe.Ingredients
        .Select(ingredient => ingredient.Name)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(name => new IngredientDefinition { Name = name, Category = CategoryFor(name) });

    private static Recipe R(string title, string cuisine, int minutes, RecipeIngredient[] ingredients,
        string[] tools, string instructions, bool favorite = false) => new()
    {
        Title = title,
        Cuisine = cuisine,
        CookingTimeMinutes = minutes,
        Servings = 4,
        Ingredients = [.. ingredients],
        Tools = [.. tools],
        Instructions = instructions,
        IsFavorite = favorite
    };

    private static RecipeIngredient[] I(params (string Name, double Quantity, string Unit)[] items) => items
        .Select(item => new RecipeIngredient { Name = item.Name, Quantity = item.Quantity, Unit = item.Unit })
        .ToArray();

    private static Recipe Clone(Recipe recipe) => new()
    {
        Title = recipe.Title,
        Cuisine = recipe.Cuisine,
        CookingTimeMinutes = recipe.CookingTimeMinutes,
        Servings = recipe.Servings,
        Ingredients = recipe.Ingredients.Select(item => new RecipeIngredient
            { Name = item.Name, Quantity = item.Quantity, Unit = item.Unit }).ToList(),
        Tools = [.. recipe.Tools],
        Instructions = recipe.Instructions,
        IsFavorite = recipe.IsFavorite
    };

    private static string CategoryFor(string name) => name.ToLowerInvariant() switch
    {
        "banana" or "apples" or "avocado" or "lemon" or "lime" => "Fruit",
        "broccoli" or "bell pepper" or "carrot" or "mushrooms" or "tomato" or "onion" or "garlic"
            or "ginger" or "cucumber" or "red onion" or "kale" or "potatoes" or "spring onion"
            or "celery" or "zucchini" or "bean sprouts" => "Vegetable",
        "chickpeas" or "black beans" or "red lentils" or "green lentils" or "lentils" or "tofu" => "Legume",
        "spaghetti" or "rice noodles" or "arborio rice" or "flour" or "chickpea flour" or "bread"
            or "breadcrumbs" or "pizza dough" => "Grain & Pasta",
        "parsley" or "basil" or "thyme" or "cumin" or "turmeric" or "cinnamon" or "chili flakes"
            or "curry powder" or "smoked paprika" => "Herb & Spice",
        "olive oil" or "vegetable oil" or "sesame oil" or "soy sauce" or "mustard" or "tomato sauce"
            or "miso paste" => "Condiment & Sauce",
        "oat milk" or "oat cream" or "coconut milk" or "vegan butter" => "Dairy Alternative",
        "baking powder" or "baking soda" or "brown sugar" or "nutritional yeast" => "Baking",
        _ => "Other"
    };
}
