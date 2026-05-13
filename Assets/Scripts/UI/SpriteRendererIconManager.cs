using System;
using TMPro;
using UnityEngine;

public class SpriteRendererIconManager : MonoBehaviour
{
    public TextMeshPro nationText;
    public SpriteRenderer armySprite;
    private Illustrations illustrations;
    public Character character;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        illustrations = FindFirstObjectByType<Illustrations>();
    }

    public static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        System.Text.StringBuilder sb = new();
        bool newWord = true;
        foreach (char c in name)
        {
            if (char.IsLetter(c))
            {
                if (newWord)
                {
                    sb.Append(char.ToUpperInvariant(c));
                    newWord = false;
                }
            }
            else
            {
                newWord = true;
            }
        }
        return sb.ToString();
    }

    public void Initialize(Character character)
    {
        this.character = character;
        illustrations = FindFirstObjectByType<Illustrations>();
        AlignmentEnum alignmentEnum = character.GetOwner().GetAlignment();
        Sprite sprite = illustrations != null ? illustrations.GetIllustrationByName(alignmentEnum.ToString()) : null;
        if (armySprite != null) armySprite.sprite = sprite;
        var biome = character.GetOwner().GetBiome();
        string initials = !string.IsNullOrEmpty(biome?.nationInitials) ? biome.nationInitials : GetInitials(biome?.nationName);
        if (nationText != null) nationText.text = initials;
    }

    public void Initialize(Character character, string spriteName)
    {
        this.character = character;
        illustrations = FindFirstObjectByType<Illustrations>();
        Sprite sprite = illustrations != null ? illustrations.GetIllustrationByName(spriteName) : null;
        if (armySprite != null) armySprite.sprite = sprite;
        var biome = character.GetOwner()?.GetBiome();
        string initials = !string.IsNullOrEmpty(biome?.nationInitials) ? biome.nationInitials : GetInitials(biome?.nationName);
        if (nationText != null) nationText.text = initials;
    }
}
