using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public class SearcherByName : MonoBehaviour
{
    public string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string sanitized = Regex.Replace(RemoveDiacritics(name), "[^A-Za-z0-9]", string.Empty);
        return sanitized.ToLowerInvariant();
    }
}
