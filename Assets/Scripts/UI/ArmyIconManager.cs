using System;
using TMPro;
using UnityEngine;

public class ArmyIconManager : MonoBehaviour
{
    public TextMeshPro nationText;
    public SpriteRenderer armySprite;
    private Illustrations illustrations;

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
        illustrations = FindFirstObjectByType<Illustrations>();
        AlignmentEnum alignmentEnum = character.GetOwner().GetAlignment();
        Sprite sprite = illustrations != null ? illustrations.GetIllustrationByName(alignmentEnum.ToString()) : null;
        if (armySprite != null) armySprite.sprite = sprite;
        if (nationText != null) nationText.text = GetInitials(character.GetOwner().GetBiome().nationName);
    }
}
