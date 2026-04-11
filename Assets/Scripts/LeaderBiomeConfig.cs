using System;
using System.Collections.Generic;

[Serializable]
public class LeaderBiomeConfigCollection
{
	public List<LeaderBiomeConfig> biomes = new ();
}

[Serializable]
public class LeaderVariantConfig
{
    public string variantId;
    public string displayName;
    public string description;
    public string deckIdentity;
    public string subdeckId;
    public string banner;
}

[Serializable]
public class LeaderBiomeConfig: BiomeConfig
{
    public string description;
    public string deckIdentity;
    public string subdeckId;
    public string banner;
    public List<LeaderVariantConfig> variants = new();
    public TerrainEnum terrain;
    public FeaturesEnum feature = FeaturesEnum.noFeature;
    public bool isIsland = false;
    public string startingCityName;
    public string startingCityRegion;
    public PCSizeEnum startingCitySize;

    public string pcFeature = "";
    public string fortFeature = "";
    public FortSizeEnum startingCityFortSize;
    public bool startingCityIsHidden;
    public bool startsWithPort;

    public List<BiomeConfig> startingCharacters = new();
    public List<Artifact> tutorialArtifacts = new();
    public List<string> tutorialAnchors = new();

    public List<string> newCharacters = new();
    public List<string> newPCs = new();

}
