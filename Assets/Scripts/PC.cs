using System;
using UnityEngine;

[Serializable]
public class PC
{
    [SerializeField] public Leader owner;
    [SerializeField] public string pcName;

    [SerializeField] public int leather = 0;
    [SerializeField] public int mounts = 0;
    [SerializeField] public int timber = 0;
    [SerializeField] public int iron = 0;
    [SerializeField] public int mithril = 0;

    [SerializeField] public bool isHidden;
    [SerializeField] public PCSizeEnum citySize;
    [SerializeField] public FortSizeEnum fortSize;
    [SerializeField] public bool hasPort;
    [SerializeField] public bool hiddenButRevealed;

    public PC(Leader owner, string pcName, PCSizeEnum citySize, FortSizeEnum fortSize, bool hasPort, bool isHidden, TerrainEnum terrain)
    {
        this.owner = owner;
        this.pcName = pcName;
        this.citySize = citySize;
        this.fortSize = fortSize;
        this.hasPort = hasPort;
        this.isHidden = isHidden;
        hiddenButRevealed = false;

        switch (terrain)
        {
            case TerrainEnum.mountains:
                mithril += UnityEngine.Random.Range(0, 3);
                iron += UnityEngine.Random.Range(1, 2);
                break;
            case TerrainEnum.hills:
                iron += UnityEngine.Random.Range(1, 2);
                timber += UnityEngine.Random.Range(0, 2);
                mounts += UnityEngine.Random.Range(0, 2);
                break;
            case TerrainEnum.plains:
            case TerrainEnum.shore:
                mounts += UnityEngine.Random.Range(1, 2);
                leather += UnityEngine.Random.Range(1, 2);
                break;
            case TerrainEnum.grasslands:
                mounts += UnityEngine.Random.Range(1, 3);
                timber += UnityEngine.Random.Range(0, 2);
                leather += UnityEngine.Random.Range(0, 2);
                break;
            case TerrainEnum.forest:
                timber += UnityEngine.Random.Range(1, 3);
                mounts += UnityEngine.Random.Range(0, 2);
                leather += UnityEngine.Random.Range(0, 2);
                break;
            case TerrainEnum.shallowWater:
            case TerrainEnum.deepWater:
                break;
            case TerrainEnum.swamp:
                leather += UnityEngine.Random.Range(1, 2);
                timber += UnityEngine.Random.Range(0, 1);
                break;
            case TerrainEnum.desert:
                leather += UnityEngine.Random.Range(1, 2);
                mounts += UnityEngine.Random.Range(0, 1);
                break;
            case TerrainEnum.wastelands:
                iron += UnityEngine.Random.Range(0, 1);
                leather += UnityEngine.Random.Range(1, 3);
                break;
        }
        owner.controlledPcs.Add(this);
    }

    public PC(Leader leader): this(leader, leader.biome.startingCityName, leader.biome.startingCitySize, leader.biome.startingCityFortSize, leader.biome.startsWithPort, leader.biome.startingCityIsHidden, leader.biome.terrain)
    {
        
    }

    public string GetProducesHoverText()
    {
        string result = "";
        
        if (leather > 0) result += $"<sprite name=\"leather\"/>{leather}";
        if (mounts > 0) result += $"<sprite name=\"mounts\"/>{mounts}";
        if (timber > 0) result += $"<sprite name=\"timber\"/>{timber}";
        if (iron > 0) result += $"<sprite name=\"iron\"/>{iron}";
        if (mithril > 0) result += $"<sprite name=\"mithril\"/>{mithril}";

        return result;
    }
}
