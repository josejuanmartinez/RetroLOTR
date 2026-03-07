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
    public bool spawnPcWithoutOwner = false;
}
