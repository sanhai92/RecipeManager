using RecipeManager.Models;

namespace RecipeManager.Data;

internal static class SampleRecipes
{
    public static IReadOnlyDictionary<string, int> CookingTimes { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Spaghetti Carbonara"] = 30, ["Chicken Tikka Masala"] = 50, ["Vegetable Stir-Fry"] = 25,
        ["Classic Pancakes"] = 20, ["Shakshuka"] = 35, ["Beef Tacos"] = 30,
        ["Greek Salad"] = 15, ["French Onion Soup"] = 75, ["Pad Thai"] = 40,
        ["Mushroom Risotto"] = 45, ["Dutch Stamppot"] = 40, ["Chickpea Curry"] = 35,
        ["Swedish Meatballs"] = 50, ["Tomato Bruschetta"] = 15, ["Japanese Miso Soup"] = 15,
        ["Banana Bread"] = 70, ["Spanish Tortilla"] = 45, ["Lentil Soup"] = 45,
        ["Margherita Pizza"] = 30, ["Apple Crumble"] = 50
    };

    public static IReadOnlyList<Recipe> Create() =>
    [
        R("Spaghetti Carbonara", "Italy", ["spaghetti", "eggs", "pecorino cheese", "guanciale", "black pepper"], ["large pot", "frying pan", "mixing bowl"],
            "Boil the spaghetti. Crisp the guanciale. Whisk eggs with pecorino and pepper. Toss the hot pasta with the guanciale, remove from heat, and quickly mix in the egg mixture until creamy.", "https://en.wikipedia.org/wiki/Carbonara", true),
        R("Chicken Tikka Masala", "United Kingdom / India", ["chicken", "yogurt", "tomato", "onion", "garlic", "ginger", "garam masala", "cream"], ["mixing bowl", "large frying pan"],
            "Marinate chicken in yogurt and spices. Brown the chicken, then cook onion, garlic, ginger, tomato, and spices. Add cream and chicken and simmer until cooked through.", "https://en.wikipedia.org/wiki/Chicken_tikka_masala", true),
        R("Vegetable Stir-Fry", "China", ["broccoli", "bell pepper", "carrot", "mushrooms", "soy sauce", "garlic", "sesame oil"], ["wok", "knife"],
            "Cut the vegetables into bite-sized pieces. Stir-fry garlic and the vegetables over high heat. Add soy sauce and sesame oil and cook until crisp-tender.", "https://en.wikipedia.org/wiki/Stir_frying"),
        R("Classic Pancakes", "United States", ["flour", "milk", "egg", "butter", "sugar", "baking powder"], ["mixing bowl", "whisk", "frying pan"],
            "Whisk the dry ingredients, then mix in milk, egg, and melted butter. Cook ladlefuls in a lightly buttered pan until bubbles form, flip, and finish the other side.", "https://en.wikipedia.org/wiki/Pancake", true),
        R("Shakshuka", "Tunisia", ["eggs", "tomato", "bell pepper", "onion", "garlic", "cumin", "paprika"], ["large frying pan"],
            "Soften onion and pepper. Add garlic, spices, and tomato and simmer into a thick sauce. Make wells, crack in the eggs, cover, and cook until the whites set.", "https://en.wikipedia.org/wiki/Shakshouka"),
        R("Beef Tacos", "Mexico", ["ground beef", "tortillas", "onion", "tomato", "lettuce", "cheese", "cumin", "chili powder"], ["frying pan", "knife"],
            "Brown the beef with onion and spices. Warm the tortillas, fill with beef, and top with tomato, lettuce, and cheese.", "https://en.wikipedia.org/wiki/Taco"),
        R("Greek Salad", "Greece", ["tomato", "cucumber", "red onion", "feta cheese", "olives", "olive oil", "oregano"], ["knife", "salad bowl"],
            "Chop the vegetables into generous pieces. Add olives and feta, drizzle with olive oil, season with oregano, and toss gently.", "https://en.wikipedia.org/wiki/Greek_salad"),
        R("French Onion Soup", "France", ["onion", "butter", "beef stock", "bread", "gruyere cheese", "thyme"], ["large pot", "oven-safe bowls"],
            "Slowly caramelize sliced onions in butter. Add stock and thyme and simmer. Ladle into bowls, top with bread and cheese, and broil until bubbling.", "https://en.wikipedia.org/wiki/French_onion_soup"),
        R("Pad Thai", "Thailand", ["rice noodles", "egg", "tofu", "bean sprouts", "peanuts", "lime", "fish sauce", "tamarind"], ["wok", "large pot"],
            "Soak the noodles. Stir-fry tofu and egg, add noodles and a sauce of tamarind and fish sauce, then toss with sprouts. Serve with peanuts and lime.", "https://en.wikipedia.org/wiki/Pad_thai", true),
        R("Mushroom Risotto", "Italy", ["arborio rice", "mushrooms", "onion", "vegetable stock", "parmesan cheese", "butter", "white wine"], ["saucepan", "large frying pan", "ladle"],
            "Brown the mushrooms. Soften onion, toast the rice, and add wine. Stir in hot stock one ladle at a time. Finish with mushrooms, butter, and parmesan.", "https://en.wikipedia.org/wiki/Risotto"),
        R("Dutch Stamppot", "Netherlands", ["potatoes", "kale", "smoked sausage", "milk", "butter", "mustard"], ["large pot", "potato masher"],
            "Boil potatoes and kale together until tender. Heat the sausage separately. Drain and mash with milk, butter, and mustard, then serve with sliced sausage.", "https://en.wikipedia.org/wiki/Stamppot"),
        R("Chickpea Curry", "India", ["chickpeas", "tomato", "onion", "garlic", "ginger", "coconut milk", "curry powder"], ["large pot"],
            "Cook onion, garlic, ginger, and curry powder until fragrant. Add tomato, chickpeas, and coconut milk and simmer until thickened.", "https://en.wikipedia.org/wiki/Chana_masala"),
        R("Swedish Meatballs", "Sweden", ["ground beef", "breadcrumbs", "egg", "onion", "butter", "cream", "beef stock"], ["mixing bowl", "frying pan"],
            "Mix beef, breadcrumbs, egg, and onion and shape into balls. Brown in butter. Make a cream and stock sauce in the pan, return the meatballs, and simmer.", "https://en.wikipedia.org/wiki/Meatball#Swedish_meatballs"),
        R("Tomato Bruschetta", "Italy", ["bread", "tomato", "garlic", "basil", "olive oil"], ["oven", "knife", "mixing bowl"],
            "Toast slices of bread. Mix chopped tomato with basil and olive oil. Rub toast with garlic and spoon the tomato mixture on top.", "https://en.wikipedia.org/wiki/Bruschetta"),
        R("Japanese Miso Soup", "Japan", ["miso paste", "tofu", "wakame", "spring onion", "dashi stock"], ["saucepan"],
            "Warm the dashi and add tofu and wakame. Dissolve miso in a little hot stock, stir it into the pan without boiling, and garnish with spring onion.", "https://en.wikipedia.org/wiki/Miso_soup"),
        R("Banana Bread", "United States", ["bananas", "flour", "butter", "sugar", "egg", "baking soda"], ["mixing bowl", "loaf pan", "oven"],
            "Mash the bananas and mix with melted butter, sugar, and egg. Fold in flour and baking soda. Pour into a loaf pan and bake until a skewer comes out clean.", "https://en.wikipedia.org/wiki/Banana_bread"),
        R("Spanish Tortilla", "Spain", ["potatoes", "eggs", "onion", "olive oil", "salt"], ["frying pan", "mixing bowl"],
            "Gently fry sliced potatoes and onion in olive oil. Drain, mix with beaten eggs, then cook in a pan. Flip and cook the second side until set.", "https://en.wikipedia.org/wiki/Spanish_omelette"),
        R("Lentil Soup", "Middle East", ["lentils", "onion", "carrot", "celery", "garlic", "vegetable stock", "cumin"], ["large pot", "blender"],
            "Soften the vegetables and garlic with cumin. Add lentils and stock and simmer until tender. Blend partially for a thick but textured soup.", "https://en.wikipedia.org/wiki/Lentil_soup"),
        R("Margherita Pizza", "Italy", ["pizza dough", "tomato", "mozzarella", "basil", "olive oil"], ["oven", "baking tray"],
            "Stretch the dough, spread with crushed tomato, and add mozzarella. Bake in a very hot oven until browned, then finish with basil and olive oil.", "https://en.wikipedia.org/wiki/Pizza_Margherita", true),
        R("Apple Crumble", "United Kingdom", ["apples", "flour", "butter", "brown sugar", "cinnamon"], ["baking dish", "mixing bowl", "oven"],
            "Slice the apples into a baking dish and sprinkle with cinnamon. Rub butter into flour and sugar to form crumbs, scatter over the apples, and bake until golden.", "https://en.wikipedia.org/wiki/Apple_crisp")
    ];

    private static Recipe R(string title, string cuisine, string[] ingredients, string[] tools, string instructions, string url, bool favorite = false) => new()
    {
        Title = title,
        Cuisine = cuisine,
        CookingTimeMinutes = CookingTimes[title],
        Ingredients = ingredients.Select(x => new RecipeIngredient { Name = x }).ToList(),
        Tools = [.. tools],
        Instructions = instructions,
        SourceUrl = url,
        IsFavorite = favorite
    };
}
