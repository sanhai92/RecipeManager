using System.Globalization;
using System.Text;

namespace RecipeManager.Services;

public static class BilingualSearchService
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["paddenstoel"] = "mushroom", ["paddenstoelen"] = "mushroom", ["champignon"] = "mushroom", ["champignons"] = "mushroom", ["mushrooms"] = "mushroom",
        ["appel"] = "apple", ["appels"] = "apple", ["apples"] = "apple",
        ["aardappel"] = "potato", ["aardappelen"] = "potato", ["potatoes"] = "potato",
        ["ui"] = "onion", ["uien"] = "onion", ["onions"] = "onion",
        ["knoflook"] = "garlic", ["wortel"] = "carrot", ["wortels"] = "carrot", ["carrots"] = "carrot",
        ["tomaat"] = "tomato", ["tomaten"] = "tomato", ["tomatoes"] = "tomato",
        ["komkommer"] = "cucumber", ["komkommers"] = "cucumber", ["cucumbers"] = "cucumber",
        ["paprika"] = "bellpepper", ["paprikas"] = "bellpepper", ["pepper"] = "bellpepper", ["peppers"] = "bellpepper",
        ["ei"] = "egg", ["eieren"] = "egg", ["eggs"] = "egg",
        ["melk"] = "milk", ["boter"] = "butter", ["bloem"] = "flour", ["meel"] = "flour",
        ["suiker"] = "sugar", ["zout"] = "salt", ["peper"] = "blackpepper",
        ["kaas"] = "cheese", ["brood"] = "bread", ["room"] = "cream",
        ["kip"] = "chicken", ["kipfilet"] = "chicken", ["kipfilets"] = "chicken", ["kippenborst"] = "chicken",
        ["rundvlees"] = "beef", ["rundergehakt"] = "beef", ["varkensvlees"] = "pork",
        ["vis"] = "fish", ["garnalen"] = "shrimp", ["garnaal"] = "shrimp",
        ["rijst"] = "rice", ["pasta"] = "pasta", ["noedels"] = "noodle", ["noodle"] = "noodle", ["noodles"] = "noodle",
        ["linzen"] = "lentil", ["linze"] = "lentil", ["lentils"] = "lentil",
        ["kikkererwten"] = "chickpea", ["kikkererwt"] = "chickpea", ["chickpeas"] = "chickpea",
        ["spinazie"] = "spinach", ["boerenkool"] = "kale", ["broccoli"] = "broccoli",
        ["prei"] = "leek", ["selderij"] = "celery", ["bloemkool"] = "cauliflower", ["kool"] = "cabbage",
        ["erwt"] = "pea", ["erwten"] = "pea", ["mais"] = "corn", ["maïs"] = "corn",
        ["courgette"] = "zucchini", ["aubergine"] = "eggplant",
        ["citroen"] = "lemon", ["citroenen"] = "lemon", ["limoen"] = "lime",
        ["banaan"] = "banana", ["bananen"] = "banana", ["bananas"] = "banana",
        ["aardbei"] = "strawberry", ["aardbeien"] = "strawberry", ["strawberries"] = "strawberry",
        ["sinaasappel"] = "orange", ["sinaasappels"] = "orange", ["ananas"] = "pineapple",
        ["bosbes"] = "blueberry", ["bosbessen"] = "blueberry", ["framboos"] = "raspberry", ["frambozen"] = "raspberry"
    };

    public static bool IsLooseMatch(string candidate, string search)
    {
        var normalizedCandidate = Canonicalize(candidate);
        var normalizedSearch = Canonicalize(search);
        if (normalizedSearch.Length == 0 || normalizedCandidate.Contains(normalizedSearch, StringComparison.Ordinal))
            return true;

        var candidateParts = normalizedCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var searchParts = normalizedSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return searchParts.All(searchPart => candidateParts.Any(candidatePart =>
            candidatePart.Contains(searchPart, StringComparison.Ordinal)
            || searchPart.Contains(candidatePart, StringComparison.Ordinal)
            || LevenshteinDistance(candidatePart, searchPart) <= (searchPart.Length >= 7 ? 2 : searchPart.Length >= 4 ? 1 : 0)));
    }

    public static bool AreEquivalent(string left, string right)
    {
        var normalizedLeft = Canonicalize(left);
        var normalizedRight = Canonicalize(right);
        return normalizedLeft.Equals(normalizedRight, StringComparison.Ordinal)
            || normalizedLeft.Contains(normalizedRight, StringComparison.Ordinal)
            || normalizedRight.Contains(normalizedLeft, StringComparison.Ordinal);
    }

    private static string Canonicalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(' ', builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => Aliases.GetValueOrDefault(word, word)));
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}
