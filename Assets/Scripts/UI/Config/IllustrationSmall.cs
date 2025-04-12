using System.Collections.Generic;
using UnityEngine;

public class IllustrationsSmall : MonoBehaviour
{
    public List<Sprite> illustrationsSmall;
    public Sprite GetIllustrationByName(string characterName)
    {
        return illustrationsSmall.Find(x => x.name.ToLower() == characterName.ToLower());
    }
    public Sprite GetIllustrationByName(Character character)
    {
        string characterName = character.characterName.ToLower();
       
        Sprite sprite = GetIllustrationByName(characterName);
        if(sprite == null)
        {
            if (character.GetAgent() > 0) return GetIllustrationByName("agent");
            if (character.GetCommander() > 0) return GetIllustrationByName("commander");
            if (character.GetEmmissary() > 0) return GetIllustrationByName("emmissary");
            if (character.GetMage() > 0) return GetIllustrationByName("mage");
            return GetIllustrationByName("commander");
        }
        else
        {
            return sprite;
        }   
    }
}
