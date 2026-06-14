using System.Collections.Generic;

public enum TerrainEnum
{
    mountains = 0,
    hills = 1,
    plains = 2,
    grasslands = 3,
    shore = 4,
    forest = 5,
    shallowWater = 6,
    deepWater = 7,
    swamp = 8,
    desert = 9,
    wastelands = 10,
    MAX
}

public static class TerrainData
{
    public static Dictionary<TerrainEnum, int> terrainCosts = new ()
    {
        { TerrainEnum.mountains, 5 },
        { TerrainEnum.hills, 3 },
        { TerrainEnum.plains, 1 },
        { TerrainEnum.grasslands, 1 },
        { TerrainEnum.shore, 1 },
        { TerrainEnum.forest, 3 },
        { TerrainEnum.shallowWater, 5 },
        { TerrainEnum.deepWater, 1 },
        { TerrainEnum.swamp, 4 },
        { TerrainEnum.desert, 3 },
        { TerrainEnum.wastelands, 2 }
    };

    public static string GetDisplayName(TerrainEnum terrain)
    {
        return terrain switch
        {
            TerrainEnum.mountains => "Mountains",
            TerrainEnum.hills => "Hills",
            TerrainEnum.plains => "Plains",
            TerrainEnum.grasslands => "Grasslands",
            TerrainEnum.shore => "Shore",
            TerrainEnum.forest => "Forest",
            TerrainEnum.shallowWater => "Shallow Water",
            TerrainEnum.deepWater => "Deep Water",
            TerrainEnum.swamp => "Swamp",
            TerrainEnum.desert => "Desert",
            TerrainEnum.wastelands => "Wastelands",
            _ => "Unknown"
        };
    }

    /// <summary>Short description of what the terrain gives, shown in the hex hover tooltip.</summary>
    public static string GetDescription(TerrainEnum terrain)
    {
        int cost = terrainCosts.TryGetValue(terrain, out int c) ? c : 1;
        string note = terrain switch
        {
            TerrainEnum.mountains => "Rugged peaks, very slow to cross.",
            TerrainEnum.hills => "Broken high ground.",
            TerrainEnum.plains => "Open, easy ground.",
            TerrainEnum.grasslands => "Open, easy ground.",
            TerrainEnum.shore => "Coastline where land meets sea.",
            TerrainEnum.forest => "Dense woodland that slows movement.",
            TerrainEnum.shallowWater => "Coastal shallows, hard to wade.",
            TerrainEnum.deepWater => "Open sea, only ships may cross.",
            TerrainEnum.swamp => "Boggy ground that mires travellers.",
            TerrainEnum.desert => "Arid wastes under a harsh sun.",
            TerrainEnum.wastelands => "Barren, blasted land.",
            _ => "Unknown terrain."
        };
        return $"{note}\nMovement cost: {cost}";
    }
}
