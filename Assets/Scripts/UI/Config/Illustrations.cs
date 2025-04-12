using System.Collections.Generic;
using UnityEngine;


public class Illustrations : MonoBehaviour
{
    public List<Sprite> illustrations;

    public Sprite GetIllustrationByName(string name)
    {
        return illustrations.Find(x => x.name.ToLower() == name.ToLower());
    }
}
