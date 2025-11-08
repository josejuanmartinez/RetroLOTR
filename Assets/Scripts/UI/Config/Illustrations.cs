using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;


public class Illustrations : SearcherByName
{
    public List<Sprite> illustrations;

    public Sprite GetIllustrationByName(string name)
    {
        Sprite spr =  illustrations.Find(x => Normalize(x.name) == Normalize(name));
        if (!spr) Debug.LogWarning($"Sprite for {name} is not registered. Typo? Forgot to add it to Illustrations?");
        return spr;
    }

    public Sprite GetIllustrationByName(Character character)
    {
        Sprite spr= illustrations.Find(x => Normalize(x.name) == Normalize(character.characterName));
        if (!spr) Debug.LogWarning($"Sprite for {character.characterName} is not registered. Typo? Forgot to add it to Illustrations?");
        return spr;
    }
}
