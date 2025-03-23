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
    [SerializeField] public PCSizeEnum citySize = PCSizeEnum.NONE;
    [SerializeField] public FortSizeEnum fortSize = FortSizeEnum.NONE;
    [SerializeField] public bool hasPort;
    [SerializeField] public bool hiddenButRevealed;
    [SerializeField] public bool isCapital;


    public PC(Leader owner, string pcName, PCSizeEnum citySize, FortSizeEnum fortSize, bool hasPort, bool isHidden, Hex hex, bool isCapital = false, int loyalty = 75)
    {
        this.owner = owner;
        this.hex = hex;
        this.pcName = pcName;
        this.citySize = citySize;
        this.fortSize = fortSize;
        this.hasPort = hasPort;
        this.isHidden = isHidden;
        this.loyalty = loyalty;
        this.isCapital = isCapital;
        hiddenButRevealed = false;

        TerrainEnum terrain = hex.terrainType;
        switch (terrain)
        {
            case TerrainEnum.mountains:
                mithril += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                iron += UnityEngine.Random.Range(2, Mathf.Max(2, ((PCSizeEnum.city) - citySize)));
                break;
            case TerrainEnum.hills:
                iron += UnityEngine.Random.Range(1, Mathf.Max(1,  ((PCSizeEnum.city) - citySize)));
                timber += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                mounts += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                break;
            case TerrainEnum.plains:
            case TerrainEnum.shore:
                mounts += UnityEngine.Random.Range(1, Mathf.Max(2, ((PCSizeEnum.city) - citySize)));
                leather += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                break;
            case TerrainEnum.grasslands:
                mounts += UnityEngine.Random.Range(2, Mathf.Max(2, ((PCSizeEnum.city) - citySize)));
                timber += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                break;
            case TerrainEnum.forest:
                timber += UnityEngine.Random.Range(2, Mathf.Max(2, ((PCSizeEnum.city) - citySize)));
                mounts += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                leather += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                break;
            case TerrainEnum.shallowWater:
            case TerrainEnum.deepWater:
                break;
            case TerrainEnum.swamp:
                leather += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                timber += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                break;
            case TerrainEnum.desert:
                mounts += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                leather += UnityEngine.Random.Range(1, Mathf.Max(2, ((PCSizeEnum.city) - citySize)));
                break;
            case TerrainEnum.wastelands:
                iron += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                leather += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
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

    /**
     * This constructor is used to create a new PC in the starting city of a leader
     */
    public PC(Leader leader): this(leader, leader.biome.startingCityName, leader.biome.startingCitySize, leader.biome.startingCityFortSize, leader.biome.startsWithPort, leader.biome.startingCityIsHidden, leader.hex, true)
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
        if (hex.encounters.Contains(EncountersEnum.Disloyal)) hex.encounters.Remove(EncountersEnum.Disloyal);
        owner.hex.RedrawPC();

        if (owner.controlledPcs.Count < 1) owner.Killed(leader);

        MessageDisplay.ShowMessage($"{pcName} was captured!", Color.red);
    }

    public void CheckHighLoyalty()
    {
        if (loyalty >= 50 && hex.encounters.Contains(EncountersEnum.Disloyal))
        {
            hex.encounters.Remove(EncountersEnum.Disloyal);
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

        MessageDisplay.ShowMessage($"Population in {pcName} grow!", Color.green);

        owner.hex.RedrawPC();
    }

    public void IncreaseFort()
    {
        if (fortSize >= FortSizeEnum.citadel) return;

        // This will also increase gold per turn
        fortSize++;

        MessageDisplay.ShowMessage($"{pcName} fort was upgraded", Color.green);

        owner.hex.RedrawPC();
    }


    public void DecreaseFort()
    {
        if (fortSize <= FortSizeEnum.NONE) return;

        // This will also increase gold per turn
        fortSize--;

        MessageDisplay.ShowMessage($"{pcName} fort was downgraded", Color.red);

        owner.hex.RedrawPC();
    }

    public void CheckLowLoyalty(Leader leader)
    {
        if (loyalty >= 50) return;

        MessageDisplay.ShowMessage($"{pcName} suffers the effects of an unhappy population!", Color.red);

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
            if (!hex.encounters.Contains(EncountersEnum.Disloyal)) hex.encounters.Add(EncountersEnum.Disloyal);
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

        if (hex.encounters.Contains(EncountersEnum.Disloyal)) hex.encounters.Remove(EncountersEnum.Disloyal);

        owner.hex.RedrawPC();
        
        MessageDisplay.ShowMessage($"{pcName} population flee!", Color.red);

    }

    public void IncreaseLoyalty(int loyalty)
    {
        this.loyalty = Math.Min(100, this.loyalty + loyalty);
        MessageDisplay.ShowMessage($"{pcName} population is happier now", Color.green);
        if (loyalty >= 50 && hex.encounters.Contains(EncountersEnum.Disloyal)) hex.encounters.Remove(EncountersEnum.Disloyal);
    }

    public void DecreaseLoyalty(int loyalty, Leader decreasedBy)
    {
        this.loyalty = Math.Max(0, this.loyalty - loyalty);
        MessageDisplay.ShowMessage($"{pcName} population discontent grows", Color.red);
        CheckLowLoyalty(decreasedBy);
    }
}
