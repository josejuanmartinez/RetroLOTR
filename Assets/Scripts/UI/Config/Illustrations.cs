using System.Collections.Generic;
using UnityEngine;


public class Illustrations : SearcherByName
{
    public List<Sprite> illustrations;

    public Sprite GetIllustrationByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || illustrations == null) return null;
        Sprite spr = illustrations.Find(x => x != null && Normalize(x.name) == Normalize(name));
        if (!spr) Debug.LogWarning($"Sprite for {name} is not registered. Typo? Forgot to add it to Illustrations?");
        return spr;
    }

    public Sprite GetIllustrationByName(Character character)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.characterName) || illustrations == null) return null;
        Sprite spr = illustrations.Find(x => x != null && Normalize(x.name) == Normalize(character.characterName));
        if (!spr) Debug.LogWarning($"Sprite for {character.characterName} is not registered. Typo? Forgot to add it to Illustrations?");
        return spr;
    }
}
