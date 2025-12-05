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
    public bool isIsland = false;
    public string startingCityName;
    public PCSizeEnum startingCitySize;

    public string pcFeature = "";
    public string fortFeature = "";
    public FortSizeEnum startingCityFortSize;
    public bool startingCityIsHidden;
    public bool startsWithPort;

    public List<BiomeConfig> startingCharacters = new();

    public string hcDescription = "Heavy Cavalry";
    public string lcDescription = "Light Cavalry";
    public string hiDescription = "Heavy Infantry";
    public string liDescription = "Light Infantry";
    public string arDescription = "Archers";
    public string maDescription = "Men-at-arms";
    public string wsDescription = "War ships";
    public string caDescription = "Catapults";

}