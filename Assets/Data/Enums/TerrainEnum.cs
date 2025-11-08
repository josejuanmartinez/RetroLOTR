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
    wastelands,
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
