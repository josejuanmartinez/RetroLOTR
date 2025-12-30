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
    public List<Artifact> tutorialArtifacts = new();
    public List<string> tutorialAnchors = new();

    public string hcDescription = "Heavy Cavalry";
    public string lcDescription = "Light Cavalry";
    public string hiDescription = "Heavy Infantry";
    public string liDescription = "Light Infantry";
    public string arDescription = "Archers";
    public string maDescription = "Men-at-arms";
    public string wsDescription = "War ships";
    public string caDescription = "Catapults";

    public List<string> newCharacters = new();
    public List<string> newPCs = new();

}
