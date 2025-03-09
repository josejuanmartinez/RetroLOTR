using System.Collections.Generic;

public enum TerrainEnum
{
    mountains,
    hills,
    plains,
    grasslands,
    shore,
    forest,
    shallowWater,
    deepWater,
    swamp,
    desert,
    wastelands
}

public static class TerrainData
{
    public static Dictionary<TerrainEnum, int> terrainCosts = new Dictionary<TerrainEnum, int>()
    {
        { TerrainEnum.mountains, 12 },
        { TerrainEnum.hills, 4 },
        { TerrainEnum.plains, 1 },
        { TerrainEnum.grasslands, 1 },
        { TerrainEnum.shore, 1 },
        { TerrainEnum.forest, 3 },
        { TerrainEnum.shallowWater, 3 },
        { TerrainEnum.deepWater, 1 },
        { TerrainEnum.swamp, 6 },
        { TerrainEnum.desert, 3 },
        { TerrainEnum.wastelands, 2 }
    };
}
