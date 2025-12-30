using System;
using TMPro;
using UnityEngine;

[Serializable]
public class PC
{
    [SerializeField] public Leader owner;
    [SerializeField] public Hex hex;
    [SerializeField] public string pcName;
    [SerializeField] public Leader initialOwner;
    [SerializeField] public PCOriginType originType = PCOriginType.Unknown;
    [SerializeField] public PCAcquisitionType acquisitionType = PCAcquisitionType.StartingOwned;

    [SerializeField] public int leather = 0;
    [SerializeField] public int mounts = 0;
    [SerializeField] public int timber = 0;
    [SerializeField] public int iron = 0;
    [SerializeField] public int steel = 0;
    [SerializeField] public int mithril = 0;

    [SerializeField] public int initialLeather = 0;
    [SerializeField] public int initialMounts = 0;
    [SerializeField] public int initialTimber = 0;
    [SerializeField] public int initialIron = 0;
    [SerializeField] public int initialSteel = 0;
    [SerializeField] public int initialMithril = 0;

    [SerializeField] public int loyalty = 100;

    [SerializeField] public bool isHidden;
    [SerializeField] public PCSizeEnum citySize = PCSizeEnum.NONE;
    [SerializeField] public FortSizeEnum fortSize = FortSizeEnum.NONE;
    [SerializeField] public bool hasPort;
    [SerializeField] public bool hiddenButRevealed;
    [SerializeField] public bool isCapital;
    [SerializeField] public int temporaryHiddenTurns;
    [SerializeField] public int temporaryRevealTurns;


    public PC(Leader owner, string pcName, PCSizeEnum citySize, FortSizeEnum fortSize, bool hasPort, bool isHidden, Hex hex, bool isCapital = false, int loyalty = 75)
    {
        this.owner = owner;
        initialOwner = owner;
        this.hex = hex;
        this.pcName = pcName;
        this.citySize = citySize;
        this.fortSize = fortSize;
        this.hasPort = hasPort;
        this.isHidden = isHidden;
        this.loyalty = loyalty;
        this.isCapital = isCapital;
        hiddenButRevealed = false;
        originType = owner != null ? GetOriginType(owner) : PCOriginType.Unknown;
        acquisitionType = PCAcquisitionType.StartingOwned;

        TerrainEnum terrain = hex.terrainType;
        switch (terrain)
        {
            case TerrainEnum.mountains:
                mithril += UnityEngine.Random.Range(0, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
                steel += UnityEngine.Random.Range(2, Mathf.Max(3, ((PCSizeEnum.city) - citySize)));
                iron += UnityEngine.Random.Range(2, Mathf.Max(2, ((PCSizeEnum.city) - citySize)));
                break;
            case TerrainEnum.hills:
                steel += UnityEngine.Random.Range(1, Mathf.Max(1, ((PCSizeEnum.city) - citySize)));
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
        initialSteel = steel;
        initialMithril = mithril;

        if (owner != null)
        {
            owner.controlledPcs.Add(this);
            owner.visibleHexes.Add(hex);
        }

        hex.SetPC(this);
    }

    private static PCOriginType GetOriginType(Leader leader)
    {
        if (leader is PlayableLeader) return PCOriginType.PlayableLeader;
        if (leader is NonPlayableLeader) return PCOriginType.NonPlayableLeader;
        return PCOriginType.Unknown;
    }

    /**
     * This constructor is used to create a new PC in the starting city of a leader
     */
    public PC(Leader leader, Hex hex): this(leader, leader.GetBiome().startingCityName, leader.GetBiome().startingCitySize, leader.GetBiome().startingCityFortSize, leader.GetBiome().startsWithPort, leader.GetBiome().startingCityIsHidden, hex, true)
    {
        
    }

    public bool IsRevealed(PlayableLeader overrideLeader = null)
    {
        var l = overrideLeader ? overrideLeader : GameObject.FindFirstObjectByType<Game>().player;
        if (l == null) return false;

        if (IsTemporarilyRevealed()) return true;
        if (IsTemporarilyHidden(l)) return false;
        if (!isHidden || hiddenButRevealed) return true;

        var pcOwner = owner;
        if (pcOwner == null) return false;
        if (pcOwner == l) return true;

        var pcAlign = pcOwner.GetAlignment();
        var lAlign = l.GetAlignment();
        return pcAlign != AlignmentEnum.neutral && pcAlign == lAlign;
    }

    public bool IsTemporarilyRevealed() => temporaryRevealTurns > 0;

    public bool IsTemporarilyHidden(Leader viewer = null)
    {
        if (temporaryHiddenTurns <= 0) return false;
        if (viewer != null && owner != null && viewer == owner) return false;
        return true;
    }

    public void SetTemporaryHidden(int turns)
    {
        if (turns <= 0) return;
        temporaryHiddenTurns = Mathf.Max(temporaryHiddenTurns, turns);
        temporaryRevealTurns = 0;
    }

    public void SetTemporaryReveal(int turns)
    {
        if (turns <= 0) return;
        temporaryRevealTurns = Mathf.Max(temporaryRevealTurns, turns);
        temporaryHiddenTurns = 0;
    }

    public void TickTemporaryVisibility()
    {
        if (temporaryHiddenTurns > 0) temporaryHiddenTurns--;
        if (temporaryRevealTurns > 0) temporaryRevealTurns--;
    }

    public string GetLoyaltyText()
    {
        // Brighter palette for readability in hover
        string color = loyalty <= 25 ? "#ff4d4d" : loyalty <= 50 ? "#ffb347" : loyalty <= 65 ? "#8fd14f" : "#00c853";
        return $"Loyalty [<color={color}>{Math.Max(0, loyalty)}</color>]";
    }

    public string GetProducesHoverText()
    {
        string result = "";
        
        if (leather > 0) result += $"<sprite name=\"leather\"/>[{leather}]";
        if (mounts > 0) result += $"<sprite name=\"mounts\"/>[{mounts}]";
        if (timber > 0) result += $"<sprite name=\"timber\"/>[{timber}]";
        if (iron > 0) result += $"<sprite name=\"iron\"/>[{iron}]";
        if (steel > 0) result += $"<sprite name=\"steel\"/>[{steel}]";
        if (mithril > 0) result += $"<sprite name=\"mithril\"/>[{mithril}]";

        return result;
    }

    public void CapturePC(Leader leader)
    {
        Leader previousOwner = owner;
        if (previousOwner != null)
        {
            previousOwner.controlledPcs.Remove(this);
            previousOwner.visibleHexes.Remove(hex);
        }

        owner = leader;
        acquisitionType = PCAcquisitionType.CapturedByForce;
        owner.controlledPcs.Add(this);
        owner.visibleHexes.Add(hex);
        loyalty = UnityEngine.Random.Range(50, 75);
        owner.hex.RedrawPC();
        if (owner is NonPlayableLeader && hex != null)
        {
            hex.EnsurePersistentScouting(owner);
        }

        if (previousOwner != null && previousOwner.controlledPcs.Count < 1) previousOwner.Killed(leader);

        MessageDisplayNoUI.ShowMessage(hex, owner,  $"{pcName} was captured!", Color.red);
    }

    public bool ClaimUnowned(Leader leader)
    {
        if (leader == null || owner != null) return false;
        owner = leader;
        acquisitionType = PCAcquisitionType.Joined;
        leader.controlledPcs.Add(this);
        leader.visibleHexes.Add(hex);
        loyalty = UnityEngine.Random.Range(50, 75);
        hex.RedrawPC();
        if (owner is NonPlayableLeader && hex != null)
        {
            hex.EnsurePersistentScouting(owner);
        }
        MessageDisplayNoUI.ShowMessage(hex, owner, $"{pcName} swore allegiance!", Color.green);
        return true;
    }

    public void CheckHighLoyalty()
    {
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
        if (steel > 1) steel -= 1;
        if (timber > 1) timber -= 1;
        if (mounts > 1) mounts -= 1;
        if (mithril > 1) mithril -= 1;

        // Reduce a little the loyalty
        loyalty = 70;

        MessageDisplayNoUI.ShowMessage(hex, owner,  $"Population in {pcName} grow!", Color.green);

        owner.hex.RedrawPC();
    }

    public void IncreaseFort()
    {
        if (fortSize >= FortSizeEnum.citadel) return;

        // This will also increase gold per turn
        fortSize++;

        MessageDisplayNoUI.ShowMessage(hex, owner,  $"{pcName} fort was upgraded", Color.green);

        owner.hex.RedrawPC();
    }


    public void DecreaseFort()
    {
        if (fortSize <= FortSizeEnum.NONE) return;

        // This will also increase gold per turn
        fortSize--;

        MessageDisplayNoUI.ShowMessage(hex, owner,  $"{pcName} fort was downgraded", Color.red);

        owner.hex.RedrawPC();
    }

    public int GetFortSize()
    {
        return (int) fortSize;
    }

    public void CheckLowLoyalty(Leader leader)
    {
        if (loyalty >= 50) return;

        MessageDisplayNoUI.ShowMessage(hex, owner,  $"{pcName} suffers the effects of an unhappy population!", Color.red);

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
        
        if (owner.controlledPcs.Count < 1) owner.Killed(leader);
    }


    public void DecreaseSize()
    {
        // This will also decrease gold per turn
        if (citySize > PCSizeEnum.camp) citySize--;

        // But increases produces
        if (initialLeather > 0) leather += leather < initialLeather ? 1 : 0;
        if (initialIron > 0) iron += iron < initialIron ? 1 : 0;
        if (initialSteel > 0) steel += steel < initialSteel ? 1 : 0;
        if (initialTimber > 0) timber += timber < initialTimber ? 1 : 0;
        if (initialMounts > 0) mounts += mounts < initialMounts ? 1 : 0;
        if (initialMithril > 0) mithril += mithril < initialMithril ? 1 : 0;

        // Increase loyalty to avoid immediate decrease
        loyalty = 60;

        owner.hex.RedrawPC();
        
        MessageDisplayNoUI.ShowMessage(hex, owner,  $"{pcName} population flee!", Color.red);

    }

    public void IncreaseLoyalty(int loyalty, Character c)
    {
        this.loyalty = Math.Min(100, this.loyalty + loyalty);
        MessageDisplayNoUI.ShowMessage(hex, c,  $"{pcName} loyalty increased by {loyalty}", Color.green);
        hex.RedrawPC();
    }

    public void DecreaseLoyalty(int loyalty, Character decreasedBy)
    {
        this.loyalty = Math.Max(0, this.loyalty - loyalty);
        MessageDisplayNoUI.ShowMessage(hex, decreasedBy,  $"{pcName} loyalty decreased by {loyalty}", Color.red);
        hex.RedrawPC();
        CheckLowLoyalty(decreasedBy.GetOwner());
    }

    public int GetDefense()
    {
        // Core PC defense is scaled by loyalty: low loyalty = weaker defensive contribution.
        int staticDefense = (int)citySize
            + (int)fortSize * FortSizeData.defensePerFortSizeLevel
            + (hasPort ? 1 : 0)
            + (isCapital ? 1 : 0);

        float loyaltyFactor = Mathf.Clamp01(loyalty / 100f);
        int defense = Mathf.RoundToInt(staticDefense * loyaltyFactor);

        // Armies stationed in the PC defend at full strength if aligned.
        hex.armies.ForEach(army =>
        {
            bool aligned = owner != null &&
                army.commander != null &&
                army.commander.GetAlignment() == owner.GetAlignment() &&
                owner.GetAlignment() != AlignmentEnum.neutral;
            if (aligned) defense += army.GetDefence();
        });

        return defense;
    }

    public int GetProduction()
    {
        return leather + timber + steel + mithril + iron + mounts;
    }

    public int GetProductionPoints()
    {
        return leather + timber*2 + steel*4 + mithril*5 + iron*3 + mounts*2;
    }

    public void Reveal()
    {
        if(isHidden) hiddenButRevealed = true;
    }
}
