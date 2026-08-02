using System.Linq;
using TMPro;
using UnityEngine;

public class FontManager : SearcherByName
{
    [SerializeField] private TMP_FontAsset[] fonts;

    public TMP_FontAsset GetFontByName(string name)
    {
        return fonts.FirstOrDefault(font =>
            font != null && Normalize(font.name) == Normalize(name));
    }

    public void ApplyFont(TMP_FontAsset font)
    {
        if (font == null) return;

        TMP_Settings.defaultFontAsset = font;
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font == null || fonts.Contains(text.font))
                text.font = font;
        }
    }
}
