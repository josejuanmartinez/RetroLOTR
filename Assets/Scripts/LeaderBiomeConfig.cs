using System;
using System.Collections.Generic;

[Serializable]
public class LeaderBiomeConfigCollection
{
	public List<LeaderBiomeConfig> biomes = new ();
}

[Serializable]
public class LeaderBiomeConfig: BiomeConfig
{
    public string description;
    public string joinedText;
    public TerrainEnum terrain;
    public string startingCityName;
    public PCSizeEnum startingCitySize;
    public FortSizeEnum startingCityFortSize;
    public int startingArmySize;
    public TroopsTypeEnum preferedTroopType;
    public bool startingCityIsHidden;
    public bool startsWithPort;
    public int startingWarships;

    public List<BiomeConfig> startingCharacters = new();
}