using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LeaderBiomeConfig: BiomeConfig
{
    [Header("Starting")]
    public TerrainEnum terrain;
    public string startingCityName;
    public PCSizeEnum startingCitySize;
    public FortSizeEnum startingCityFortSize;
    public int startingArmySize;
    public TroopsTypeEnum preferedTroopType;
    public bool startingCityIsHidden;
    public bool startsWithPort;
    public int startingWarships;

    [Header("Characters")]
    public List<LeaderBiomeConfig> startingCharacters;

    [Header("New")]
    public List<string> pcNames;
    public List<string> characterNames;
}