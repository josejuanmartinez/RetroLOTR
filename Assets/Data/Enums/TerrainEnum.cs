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
}
