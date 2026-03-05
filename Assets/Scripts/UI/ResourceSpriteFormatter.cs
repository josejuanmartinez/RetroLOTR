using System.Text.RegularExpressions;

public static class ResourceSpriteFormatter
{
    private static readonly Regex ResourceWordRegex = new(
        @"\b(leather|timber|mounts?|iron|steel|mithril|gold)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public static string ReplaceResourceWordsWithSprites(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        return ResourceWordRegex.Replace(text, match =>
        {
            string token = match.Value.ToLowerInvariant();
            if (token == "mount") token = "mounts";
            return $"<sprite name=\"{token}\">";
        });
    }
}
