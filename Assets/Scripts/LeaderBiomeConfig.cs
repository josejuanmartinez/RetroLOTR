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
    public string joinedTitle;
    public string joinedText;
    public string introActor1;
    public string introActor2;
    public TerrainEnum terrain;
    public FeaturesEnum feature = FeaturesEnum.noFeature;
    public string startingCityName;
    public PCSizeEnum startingCitySize;
    public FortSizeEnum startingCityFortSize;
    public bool startingCityIsHidden;
    public bool startsWithPort;

    public List<BiomeConfig> startingCharacters = new();
}