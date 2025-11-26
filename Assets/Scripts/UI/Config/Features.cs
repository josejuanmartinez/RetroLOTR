using UnityEngine;
using System.Collections.Generic;

public class Features : SearcherByName
{
    public List<Sprite> features;

    public Sprite GetFeatureByName(string name)
    {
        Sprite spr =  features.Find(x => Normalize(x.name) == Normalize(name));
        if (!spr) Debug.LogWarning($"Sprite for {name} is not registered. Typo? Forgot to add it to Features?");
        return spr;
    }

}
