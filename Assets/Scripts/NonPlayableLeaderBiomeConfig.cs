using System;
using System.Collections.Generic;

[Serializable]
public class NonPlayableLeaderBiomeConfigCollection
{
    public List<NonPlayableLeaderBiomeConfig> biomes = new();
}

[Serializable]
public class NonPlayableLeaderBiomeConfig: LeaderBiomeConfig
{

    public List<string> artifactsToJoin = new();

    public int leatherToJoin = 0;
    public int mountsToJoin = 0;
    public int timberToJoin = 0;
    public int ironToJoin = 0;
    public int mithrilToJoin = 0;
    public int goldToJoin = 0;

    public int commanderLevelToJoin = 0;
    public int agentLevelToJoin = 0;
    public int emmissaryLevelToJoin = 0;
    public int mageLevelToJoin = 0;

    public int armiesToJoin = 0;
    public int maSizeToJoin = 0;
    public int arSizeToJoin = 0;
    public int liSizeToJoin = 0;
    public int hiSizeToJoin = 0;
    public int lcSizeToJoin = 0;
    public int hcSizeToJoin = 0;
    public int caSizeToJoin = 0;
    public int wsSizeToJoin = 0;

    public int commandersToJoin = 0;
    public int agentsToJoin = 0;
    public int emmissarysToJoin = 0;
    public int magesToJoin = 0;

    public List<string> actionsAtCapital;

    public List<string> actionsAnywhere;
}
