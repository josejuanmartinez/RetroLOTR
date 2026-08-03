using System.Linq;
using TMPro;
using UnityEngine;

public class FontManager : SearcherByName
{
    public static FontManager Instance { get; private set; }

    [SerializeField] private TMP_FontAsset[] fonts;
    private TMP_FontAsset currentFont;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public TMP_FontAsset GetFontByName(string name)
    {
        return fonts.FirstOrDefault(font =>
            font != null && Normalize(font.name) == Normalize(name));
    }

    public TMP_FontAsset GetCurrentFont()
    {
        return currentFont != null ? currentFont : TMP_Settings.defaultFontAsset;
    }

    public void ApplyCurrentFont(TMP_Text text)
    {
        TMP_FontAsset font = GetCurrentFont();
        if (text != null && font != null) text.font = font;
    }

    public void ApplyFont(TMP_FontAsset font)
    {
        if (font == null) return;

        currentFont = font;
        TMP_Settings.defaultFontAsset = font;
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font == null || fonts.Contains(text.font))
                text.font = font;
        }
    }
}
