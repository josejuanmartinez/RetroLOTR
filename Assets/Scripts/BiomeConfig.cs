using System.Collections.Generic;
using UnityEngine;

public class BiomeConfig: MonoBehaviour
{
    public AlignmentEnum alignment;
    public TerrainEnum terrain;
    public string startingCityName;
    public PCSizeEnum startingCitySize;
    public FortSizeEnum startingCityFortSize;
    public int startingArmySize;
    public TroopsTypeEnum preferedTroopType;
    public bool startingCityIsHidden;
    public bool startsWithPort;
    public int startingWarships;

    public List<string> pcNames;
}