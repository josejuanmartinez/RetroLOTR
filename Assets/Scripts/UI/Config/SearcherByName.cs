using System.Globalization;
using System.Text;
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
        return RemoveDiacritics(name).Replace(" ", "").ToLower();
    }
}
