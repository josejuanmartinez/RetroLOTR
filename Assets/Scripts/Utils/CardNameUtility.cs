using System.Linq;

public static class CardNameUtility
{
    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public static bool Equals(string name1, string name2)
    {
        return string.Equals(Normalize(name1), Normalize(name2));
    }
}
