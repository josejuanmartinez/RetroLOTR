using System;
using UnityEngine;

[Serializable]
public class PC
{
    [SerializeField] public Leader owner;
    [SerializeField] public Hex hex;
    [SerializeField] public string pcName;

    [SerializeField] public int leather = 0;
    [SerializeField] public int mounts = 0;
    [SerializeField] public int timber = 0;
    [SerializeField] public int iron = 0;
    [SerializeField] public int mithril = 0;

    [SerializeField] public int initialLeather = 0;
    [SerializeField] public int initialMounts = 0;
    [SerializeField] public int initialTimber = 0;
    [SerializeField] public int initialIron = 0;
    [SerializeField] public int initialMithril = 0;

    [SerializeField] public int loyalty = 100;

    [SerializeField] public bool isHidden;
    [SerializeField] public PCSizeEnum citySize;
    [SerializeField] public FortSizeEnum fortSize;
    [SerializeField] public bool hasPort;
    [SerializeField] public bool hiddenButRevealed;

    public PC(Leader owner, string pcName, PCSizeEnum citySize, FortSizeEnum fortSize, bool hasPort, bool isHidden, Hex hex, int loyalty = 100)
    {
        this.owner = owner;
        this.hex = hex;
        this.pcName = pcName;
        this.citySize = citySize;
        this.fortSize = fortSize;
        this.hasPort = hasPort;
        this.isHidden = isHidden;
        this.loyalty = loyalty;
        hiddenButRevealed = false;

        TerrainEnum terrain = hex.terrainType;
        switch (terrain)
        {
            case TerrainEnum.mountains:
                mithril += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                iron += UnityEngine.Random.Range(2, Mathf.Max(2, ((PCSizeEnum.city + 1) - citySize)));
                break;
            case TerrainEnum.hills:
                iron += UnityEngine.Random.Range(1, Mathf.Max(1,  ((PCSizeEnum.city + 1) - citySize)));
                timber += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                mounts += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                break;
            case TerrainEnum.plains:
            case TerrainEnum.shore:
                mounts += UnityEngine.Random.Range(1, Mathf.Max(2, ((PCSizeEnum.city + 1) - citySize)));
                leather += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                break;
            case TerrainEnum.grasslands:
                mounts += UnityEngine.Random.Range(2, Mathf.Max(2, ((PCSizeEnum.city + 1) - citySize)));
                timber += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                break;
            case TerrainEnum.forest:
                timber += UnityEngine.Random.Range(2, Mathf.Max(2, ((PCSizeEnum.city + 1) - citySize)));
                mounts += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                leather += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                break;
            case TerrainEnum.shallowWater:
            case TerrainEnum.deepWater:
                break;
            case TerrainEnum.swamp:
                leather += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                timber += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                break;
            case TerrainEnum.desert:
                mounts += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                leather += UnityEngine.Random.Range(1, Mathf.Max(2, ((PCSizeEnum.city + 1) - citySize)));
                break;
            case TerrainEnum.wastelands:
                iron += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                leather += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city + 1) - citySize)));
                break;
        }

        initialLeather = leather;
        initialMounts = mounts;
        initialTimber = timber;
        initialIron = iron;
        initialMithril = mithril;

        owner.controlledPcs.Add(this);
        owner.visibleHexes.Add(hex);
    }

    public PC(Leader leader): this(leader, leader.biome.startingCityName, leader.biome.startingCitySize, leader.biome.startingCityFortSize, leader.biome.startsWithPort, leader.biome.startingCityIsHidden, leader.hex)
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

    public void CapturePC(Leader leader)
    {
        owner.controlledPcs.Remove(this);
        owner.visibleHexes.Remove(hex);
        owner = leader;
        owner.controlledPcs.Add(this);
        owner.visibleHexes.Add(hex);
        loyalty = UnityEngine.Random.Range(50, 75);
        if (hex.encounterEnum == EncountersEnum.LowLoyalty) hex.encounterEnum = EncountersEnum.NONE;
        owner.hex.RedrawPC();

        if (owner.controlledPcs.Count < 1) owner.Killed(leader);
    }

    public void CheckHighLoyalty()
    {
        if (loyalty >= 50 && hex.encounterEnum == EncountersEnum.LowLoyalty)
        {
            hex.encounterEnum = EncountersEnum.NONE;
            owner.hex.RedrawEncounters();
        }

        if (loyalty < 100 || citySize == PCSizeEnum.city) return;

        if (UnityEngine.Random.Range(1, 5) >= ((int)citySize) + 1) IncreaseSize();
    }

    public void IncreaseSize()
    {
        if (citySize >= PCSizeEnum.city) return;

        // This will also increase gold per turn
        citySize++;
        // But reduce produces
        if (leather > 1) leather -= 1;
        if (iron > 1) iron -= 1;
        if (timber > 1) timber -= 1;
        if (mounts > 1) mounts -= 1;
        if (mithril > 1) mithril -= 1;

        // Reduce a little the loyalty
        loyalty = 70;

        owner.hex.RedrawPC();
    }

    public void IncreaseFort()
    {
        if (fortSize >= FortSizeEnum.citadel) return;

        // This will also increase gold per turn
        fortSize++;

        owner.hex.RedrawPC();
    }

    public void CheckLowLoyalty(Leader leader)
    {
        if (loyalty >= 50) return;


        if ( UnityEngine.Random.Range(0, 50) > loyalty)
        {
            if(owner is NonPlayableLeader || citySize == PCSizeEnum.camp)
            {
                CapturePC(leader);
            }
            else
            {
                DecreaseSize();
            }

            owner.hex.RedrawPC();
        } 
        
        if(loyalty <= 50)
        {
            hex.encounterEnum = EncountersEnum.LowLoyalty;
            owner.hex.RedrawEncounters();
        }

        if (owner.controlledPcs.Count < 1) owner.Killed(leader);
    }


    public void DecreaseSize()
    {
        // This will also decrease gold per turn
        if (citySize > PCSizeEnum.camp) citySize--;

        // But increases produces
        if (initialLeather > 0) leather += leather < initialLeather ? 1 : 0;
        if (initialIron > 0) iron += iron < initialIron ? 1 : 0;
        if (initialTimber > 0) timber += timber < initialTimber ? 1 : 0;
        if (initialMounts > 0) mounts += mounts < initialMounts ? 1 : 0;
        if (initialMithril > 0) mithril += mithril < initialMithril ? 1 : 0;

        // Increase loyalty to avoid immediate decrease
        loyalty = 60;
        if (hex.encounterEnum == EncountersEnum.LowLoyalty) hex.encounterEnum = EncountersEnum.NONE;

        owner.hex.RedrawPC();

    }
}
