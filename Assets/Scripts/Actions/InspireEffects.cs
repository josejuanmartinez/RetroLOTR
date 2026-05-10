using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// --- Shared helpers ---

internal static class InspireEffectHelpers
{
    internal static List<PC> GetEnemyPCs(Leader leader)
    {
        return leader.visibleHexes
            .Where(h => h != null)
            .Select(h => h.GetPC())
            .Where(pc => pc != null && pc.owner != null
                      && pc.owner != leader
                      && pc.owner.GetAlignment() != leader.GetAlignment())
            .ToList();
    }

    internal static PC GetNearestEnemyPC(Leader leader)
    {
        List<PC> enemies = GetEnemyPCs(leader);
        if (enemies.Count == 0) return null;

        List<Vector2Int> friendlyPositions = leader.controlledCharacters
            .Where(c => c != null && !c.killed && c.hex != null)
            .Select(c => c.hex.v2)
            .ToList();

        if (friendlyPositions.Count == 0)
            return enemies[Random.Range(0, enemies.Count)];

        return enemies
            .OrderBy(pc => friendlyPositions.Min(fp => Vector2Int.Distance(fp, pc.hex.v2)))
            .First();
    }

    internal static PC GetRandomEnemyPC(Leader leader)
    {
        List<PC> enemies = GetEnemyPCs(leader);
        return enemies.Count == 0 ? null : enemies[Random.Range(0, enemies.Count)];
    }

    internal static void RemoveWarshipsFromPC(PC pc)
    {
        if (pc?.hex == null || pc.owner == null) return;
        foreach (Army army in pc.hex.armies)
        {
            if (army?.commander == null) continue;
            if (army.commander.GetOwner() != pc.owner) continue;
            army.ws = 0;
        }
    }
}

// --- Enums used to configure inspire effects ---

public enum InspireResourceType { Gold, Leather, Mounts, Timber, Iron, Steel, Mithril }

public enum InspireSkillType { Commander, Agent, Emmissary, Mage }

[System.Serializable]
public class InspireEffectData
{
    public string type;
    public string statusEffect;
    public int turns;
    public int amount;
    public string troopType;
    public string resourceType;
    public string skillType;
    public bool allCharacters = true;
    public bool nearest = true;
}

public static class InspireEffectFactory
{
    public static InspireEffect CreateFromCardData(CardData card)
    {
        if (card?.inspireEffectData == null) return null;
        var d = card.inspireEffectData;
        switch (d.type)
        {
            case "ApplyStatusEffect":
                if (System.Enum.TryParse<StatusEffectEnum>(d.statusEffect, out var se))
                    return new ApplyStatusEffectInspireEffect(se, d.turns, d.allCharacters);
                return null;
            case "ClearStatusEffect":
                if (System.Enum.TryParse<StatusEffectEnum>(d.statusEffect, out var cse))
                    return new ClearStatusEffectInspireEffect(cse);
                return null;
            case "Heal":
                return new HealInspireEffect(d.amount, d.allCharacters);
            case "IncreaseSkill":
                if (System.Enum.TryParse<InspireSkillType>(d.skillType, out var sk))
                    return new IncreaseSkillInspireEffect(sk, d.amount, d.allCharacters);
                return null;
            case "GainResource":
                if (System.Enum.TryParse<InspireResourceType>(d.resourceType, out var res))
                    return new GainResourceInspireEffect(res, d.amount);
                return null;
            case "RecruitTroops":
                if (System.Enum.TryParse<TroopsTypeEnum>(d.troopType, out var tt))
                    return new RecruitTroopsInspireEffect(tt, d.amount);
                return null;
            case "ArmyXp":
                return new ArmyXpInspireEffect(d.amount);
            case "RevealHexes":
                return new RevealHexesInspireEffect(d.amount);
            case "RevealArtifact":
                return new RevealArtifactInspireEffect();
            case "IncreaseLoyalty":
                return new IncreaseLoyaltyInspireEffect(d.amount);
            case "DecreaseLoyalty":
                return new DecreaseLoyaltyInspireEffect(d.amount);
            case "ResetMovement":
                return new ResetMovementInspireEffect(d.allCharacters);
            case "ResetAction":
                return new ResetActionInspireEffect(d.allCharacters);
            case "ResetMovementAndAction":
                return new ResetMovementAndActionInspireEffect(d.allCharacters);
            case "Encourage":
                return new EncourageInspireEffect(d.turns, d.allCharacters);
            case "FreeCaptives":
                return new FreeCaptivesInspireEffect();
            case "IncreaseFort":
                return new IncreaseFortInspireEffect();
            case "DecreaseFortEnemy":
                return new DecreaseFortEnemyInspireEffect(d.nearest);
            case "CreatePort":
                return new CreatePortInspireEffect();
            case "SabotagePort":
                return new SabotagePortInspireEffect(d.nearest);
            case "IncreaseCitySize":
                return new IncreaseCitySizeInspireEffect();
            case "DecreaseCitySizeEnemy":
                return new DecreaseCitySizeEnemyInspireEffect(d.nearest);
            case "HidePC":
                return new HidePCInspireEffect(d.turns);
            case "RevealEnemyPC":
                return new RevealEnemyPCInspireEffect(d.turns, d.nearest);
            default:
                return null;
        }
    }
}

// ---- Status Effects ----

public class ApplyStatusEffectInspireEffect : InspireEffect
{
    private readonly StatusEffectEnum _effect;
    private readonly int _turns;
    private readonly bool _allCharacters;

    public ApplyStatusEffectInspireEffect(StatusEffectEnum effect, int turns, bool allCharacters = true)
    {
        _effect = effect;
        _turns = turns;
        _allCharacters = allCharacters;
    }

    public override string Description => _allCharacters
        ? $"Apply {_effect} ({_turns} turns) to all controlled characters."
        : $"Apply {_effect} ({_turns} turns) to a random controlled character.";

    public override void Apply(Leader leader)
    {
        List<Character> candidates = leader.controlledCharacters
            .Where(c => c != null && !c.killed)
            .ToList();

        if (candidates.Count == 0) return;

        if (_allCharacters)
        {
            foreach (Character character in candidates)
                character.ApplyStatusEffect(_effect, _turns);
        }
        else
        {
            candidates[Random.Range(0, candidates.Count)].ApplyStatusEffect(_effect, _turns);
        }
    }
}

public class ClearStatusEffectInspireEffect : InspireEffect
{
    private readonly StatusEffectEnum _effect;

    public ClearStatusEffectInspireEffect(StatusEffectEnum effect)
    {
        _effect = effect;
    }

    public override string Description => $"Clear {_effect} from all controlled characters.";

    public override void Apply(Leader leader)
    {
        foreach (Character character in leader.controlledCharacters.Where(c => c != null && !c.killed && c.HasStatusEffect(_effect)))
            character.ClearStatusEffect(_effect);
    }
}

// ---- Healing ----

public class HealInspireEffect : InspireEffect
{
    private readonly int _amount;
    private readonly bool _allCharacters;

    public HealInspireEffect(int amount, bool allCharacters = true)
    {
        _amount = amount;
        _allCharacters = allCharacters;
    }

    public override string Description => _allCharacters
        ? $"Heal all controlled characters by {_amount}."
        : $"Heal a random controlled character by {_amount}.";

    public override void Apply(Leader leader)
    {
        List<Character> candidates = leader.controlledCharacters
            .Where(c => c != null && !c.killed)
            .ToList();

        if (candidates.Count == 0) return;

        if (_allCharacters)
        {
            foreach (Character character in candidates)
                character.Heal(_amount);
        }
        else
        {
            candidates[Random.Range(0, candidates.Count)].Heal(_amount);
        }
    }
}

// ---- Skills ----

public class IncreaseSkillInspireEffect : InspireEffect
{
    private readonly InspireSkillType _skill;
    private readonly int _amount;
    private readonly bool _allCharacters;

    public IncreaseSkillInspireEffect(InspireSkillType skill, int amount, bool allCharacters = false)
    {
        _skill = skill;
        _amount = amount;
        _allCharacters = allCharacters;
    }

    public override string Description => _allCharacters
        ? $"Increase {_skill} by {_amount} on all controlled characters."
        : $"Increase {_skill} by {_amount} on a random controlled character.";

    public override void Apply(Leader leader)
    {
        List<Character> candidates = leader.controlledCharacters
            .Where(c => c != null && !c.killed)
            .ToList();

        if (candidates.Count == 0) return;

        IEnumerable<Character> targets = _allCharacters
            ? candidates
            : new[] { candidates[Random.Range(0, candidates.Count)] };

        foreach (Character character in targets)
        {
            switch (_skill)
            {
                case InspireSkillType.Commander: character.AddCommander(_amount); break;
                case InspireSkillType.Agent:     character.AddAgent(_amount);     break;
                case InspireSkillType.Emmissary: character.AddEmmissary(_amount); break;
                case InspireSkillType.Mage:      character.AddMage(_amount);      break;
            }
        }
    }
}

// ---- Resources ----

public class GainResourceInspireEffect : InspireEffect
{
    private readonly InspireResourceType _resourceType;
    private readonly int _amount;

    public GainResourceInspireEffect(InspireResourceType resourceType, int amount)
    {
        _resourceType = resourceType;
        _amount = amount;
    }

    public override string Description => $"Gain {_amount} {_resourceType}.";

    public override void Apply(Leader leader)
    {
        switch (_resourceType)
        {
            case InspireResourceType.Gold:    leader.AddGold(_amount);    break;
            case InspireResourceType.Leather: leader.AddLeather(_amount); break;
            case InspireResourceType.Mounts:  leader.AddMounts(_amount);  break;
            case InspireResourceType.Timber:  leader.AddTimber(_amount);  break;
            case InspireResourceType.Iron:    leader.AddIron(_amount);    break;
            case InspireResourceType.Steel:   leader.AddSteel(_amount);   break;
            case InspireResourceType.Mithril: leader.AddMithril(_amount); break;
        }
    }
}

// ---- Armies ----

public class RecruitTroopsInspireEffect : InspireEffect
{
    private readonly TroopsTypeEnum _troopType;
    private readonly int _amount;

    public RecruitTroopsInspireEffect(TroopsTypeEnum troopType, int amount)
    {
        _troopType = troopType;
        _amount = amount;
    }

    public override string Description => $"Recruit {_amount} {_troopType} to a controlled army.";

    public override void Apply(Leader leader)
    {
        List<Army> armies = leader.controlledCharacters
            .Where(c => c != null && !c.killed && c.GetArmy() != null)
            .Select(c => c.GetArmy())
            .ToList();

        if (armies.Count == 0) return;

        armies[Random.Range(0, armies.Count)].Recruit(_troopType, _amount);
    }
}

public class ArmyXpInspireEffect : InspireEffect
{
    private readonly int _xp;

    public ArmyXpInspireEffect(int xp)
    {
        _xp = xp;
    }

    public override string Description => $"Grant {_xp} XP to all controlled armies.";

    public override void Apply(Leader leader)
    {
        foreach (Character character in leader.controlledCharacters.Where(c => c != null && !c.killed && c.GetArmy() != null))
            character.GetArmy().AddXp(_xp, "Inspired");
    }
}

// ---- Hexes ----

public class RevealHexesInspireEffect : InspireEffect
{
    private readonly int _radius;

    public RevealHexesInspireEffect(int radius)
    {
        _radius = radius;
    }

    public override string Description => $"Reveal hexes in radius {_radius} around all controlled characters.";

    public override void Apply(Leader leader)
    {
        foreach (Character character in leader.controlledCharacters.Where(c => c != null && !c.killed && c.hex != null))
            character.hex.RevealArea(_radius, true, leader);
    }
}

public class RevealArtifactInspireEffect : InspireEffect
{
    public override string Description => "Reveal a hidden artifact on a visible hex.";

    public override void Apply(Leader leader)
    {
        List<Hex> candidates = leader.visibleHexes
            .Where(h => h != null && h.hiddenArtifacts != null && h.hiddenArtifacts.Count > 0)
            .ToList();

        if (candidates.Count == 0) return;

        candidates[Random.Range(0, candidates.Count)].RevealArtifact();
    }
}

// ---- Loyalty ----

public class IncreaseLoyaltyInspireEffect : InspireEffect
{
    private readonly int _amount;

    public IncreaseLoyaltyInspireEffect(int amount)
    {
        _amount = amount;
    }

    public override string Description => $"Increase loyalty by {_amount} on a random controlled settlement.";

    public override void Apply(Leader leader)
    {
        List<PC> candidates = leader.controlledPcs.Where(p => p != null).ToList();
        if (candidates.Count == 0) return;

        candidates[Random.Range(0, candidates.Count)].IncreaseLoyalty(_amount, null);
    }
}

public class DecreaseLoyaltyInspireEffect : InspireEffect
{
    private readonly int _amount;

    public DecreaseLoyaltyInspireEffect(int amount)
    {
        _amount = amount;
    }

    public override string Description => $"Decrease loyalty by {_amount} on a visible enemy settlement.";

    public override void Apply(Leader leader)
    {
        List<PC> enemyPcs = leader.visibleHexes
            .Where(h => h != null)
            .Select(h => h.GetPC())
            .Where(pc => pc != null && pc.owner != null && pc.owner != leader
                      && pc.owner.GetAlignment() != leader.GetAlignment())
            .ToList();

        if (enemyPcs.Count == 0) return;

        PC target = enemyPcs[Random.Range(0, enemyPcs.Count)];
        target.DecreaseLoyalty(_amount, leader);
    }
}

// ---- Movement & Actions ----

public class ResetMovementInspireEffect : InspireEffect
{
    private readonly bool _allCharacters;

    public ResetMovementInspireEffect(bool allCharacters = true)
    {
        _allCharacters = allCharacters;
    }

    public override string Description => _allCharacters
        ? "Restore full movement to all controlled characters."
        : "Restore full movement to a random controlled character.";

    public override void Apply(Leader leader)
    {
        List<Character> candidates = leader.controlledCharacters
            .Where(c => c != null && !c.killed)
            .ToList();

        if (candidates.Count == 0) return;

        IEnumerable<Character> targets = _allCharacters
            ? candidates
            : new[] { candidates[Random.Range(0, candidates.Count)] };

        foreach (Character character in targets)
        {
            character.moved = 0;
            character.RefreshActionsIfSelected();
            character.RefreshSelectedCharacterIconIfSelected();
        }
    }
}

public class ResetActionInspireEffect : InspireEffect
{
    private readonly bool _allCharacters;

    public ResetActionInspireEffect(bool allCharacters = true)
    {
        _allCharacters = allCharacters;
    }

    public override string Description => _allCharacters
        ? "Restore action to all controlled characters."
        : "Restore action to a random controlled character.";

    public override void Apply(Leader leader)
    {
        List<Character> candidates = leader.controlledCharacters
            .Where(c => c != null && !c.killed)
            .ToList();

        if (candidates.Count == 0) return;

        IEnumerable<Character> targets = _allCharacters
            ? candidates
            : new[] { candidates[Random.Range(0, candidates.Count)] };

        foreach (Character character in targets)
        {
            character.hasActionedThisTurn = false;
            character.RefreshActionsIfSelected();
        }
    }
}

public class ResetMovementAndActionInspireEffect : InspireEffect
{
    private readonly bool _allCharacters;

    public ResetMovementAndActionInspireEffect(bool allCharacters = false)
    {
        _allCharacters = allCharacters;
    }

    public override string Description => _allCharacters
        ? "Grant all controlled characters a full extra turn."
        : "Grant a random controlled character a full extra turn.";

    public override void Apply(Leader leader)
    {
        List<Character> candidates = leader.controlledCharacters
            .Where(c => c != null && !c.killed)
            .ToList();

        if (candidates.Count == 0) return;

        IEnumerable<Character> targets = _allCharacters
            ? candidates
            : new[] { candidates[Random.Range(0, candidates.Count)] };

        foreach (Character character in targets)
        {
            character.moved = 0;
            character.hasActionedThisTurn = false;
            character.RefreshActionsIfSelected();
            character.RefreshSelectedCharacterIconIfSelected();
        }
    }
}

// ---- Encouragement ----

public class EncourageInspireEffect : InspireEffect
{
    private readonly int _turns;
    private readonly bool _allCharacters;

    public EncourageInspireEffect(int turns = 1, bool allCharacters = true)
    {
        _turns = turns;
        _allCharacters = allCharacters;
    }

    public override string Description => _allCharacters
        ? $"Encourage all controlled characters for {_turns} turn(s)."
        : $"Encourage a random controlled character for {_turns} turn(s).";

    public override void Apply(Leader leader)
    {
        List<Character> candidates = leader.controlledCharacters
            .Where(c => c != null && !c.killed)
            .ToList();

        if (candidates.Count == 0) return;

        IEnumerable<Character> targets = _allCharacters
            ? candidates
            : new[] { candidates[Random.Range(0, candidates.Count)] };

        foreach (Character character in targets)
            character.Encourage(_turns);
    }
}

// ---- Liberation ----

public class FreeCaptivesInspireEffect : InspireEffect
{
    public override string Description => "Free all controlled characters from captivity.";

    public override void Apply(Leader leader)
    {
        List<Character> captives = leader.controlledCharacters
            .Where(c => c != null && !c.killed && c.IsKidnapped())
            .ToList();

        foreach (Character captive in captives)
            captive.ReleaseFromKidnap(false);
    }
}

// ---- Fortifications ----

public class IncreaseFortInspireEffect : InspireEffect
{
    public override string Description => "Upgrade fortifications on a random controlled settlement.";

    public override void Apply(Leader leader)
    {
        List<PC> candidates = leader.controlledPcs.Where(p => p != null).ToList();
        if (candidates.Count == 0) return;

        candidates[Random.Range(0, candidates.Count)].IncreaseFort();
    }
}

public class DecreaseFortEnemyInspireEffect : InspireEffect
{
    private readonly bool _nearest;

    public DecreaseFortEnemyInspireEffect(bool nearest = true)
    {
        _nearest = nearest;
    }

    public override string Description => _nearest
        ? "Downgrade fortifications on the nearest visible enemy settlement."
        : "Downgrade fortifications on a random visible enemy settlement.";

    public override void Apply(Leader leader)
    {
        PC target = _nearest
            ? InspireEffectHelpers.GetNearestEnemyPC(leader)
            : InspireEffectHelpers.GetRandomEnemyPC(leader);

        target?.DecreaseFort();
    }
}

// ---- Ports ----

public class CreatePortInspireEffect : InspireEffect
{
    public override string Description => "Build a port on a controlled settlement that lacks one.";

    public override void Apply(Leader leader)
    {
        List<PC> candidates = leader.controlledPcs
            .Where(p => p != null && !p.hasPort)
            .ToList();

        if (candidates.Count == 0) return;

        PC pc = candidates[Random.Range(0, candidates.Count)];
        pc.hasPort = true;
        pc.hex.RedrawPC();
        pc.hex.RedrawArmies();
        pc.hex.RedrawCharacters();
        MessageDisplayNoUI.ShowMessage(pc.hex, leader, $"{pc.pcName} port built!", Color.green);
    }
}

public class SabotagePortInspireEffect : InspireEffect
{
    private readonly bool _nearest;

    public SabotagePortInspireEffect(bool nearest = true)
    {
        _nearest = nearest;
    }

    public override string Description => _nearest
        ? "Sabotage the port of the nearest visible enemy settlement."
        : "Sabotage the port of a random visible enemy settlement.";

    public override void Apply(Leader leader)
    {
        PC target = _nearest
            ? InspireEffectHelpers.GetNearestEnemyPC(leader)
            : InspireEffectHelpers.GetRandomEnemyPC(leader);

        if (target == null || !target.hasPort) return;

        target.hasPort = false;
        InspireEffectHelpers.RemoveWarshipsFromPC(target);
        target.hex.RedrawPC();
        target.hex.RedrawArmies();
        target.hex.RedrawCharacters();
        MessageDisplayNoUI.ShowMessage(target.hex, leader, $"{target.pcName} port sabotaged!", Color.red);
    }
}

// ---- City Size ----

public class IncreaseCitySizeInspireEffect : InspireEffect
{
    public override string Description => "Grow a random controlled settlement.";

    public override void Apply(Leader leader)
    {
        List<PC> candidates = leader.controlledPcs.Where(p => p != null).ToList();
        if (candidates.Count == 0) return;

        candidates[Random.Range(0, candidates.Count)].IncreaseSize();
    }
}

public class DecreaseCitySizeEnemyInspireEffect : InspireEffect
{
    private readonly bool _nearest;

    public DecreaseCitySizeEnemyInspireEffect(bool nearest = true)
    {
        _nearest = nearest;
    }

    public override string Description => _nearest
        ? "Reduce the size of the nearest visible enemy settlement."
        : "Reduce the size of a random visible enemy settlement.";

    public override void Apply(Leader leader)
    {
        PC target = _nearest
            ? InspireEffectHelpers.GetNearestEnemyPC(leader)
            : InspireEffectHelpers.GetRandomEnemyPC(leader);

        target?.DecreaseSize();
    }
}

// ---- PC Visibility ----

public class HidePCInspireEffect : InspireEffect
{
    private readonly int _turns;

    public HidePCInspireEffect(int turns = 1)
    {
        _turns = turns;
    }

    public override string Description => $"Conceal a random controlled settlement from enemies for {_turns} turn(s).";

    public override void Apply(Leader leader)
    {
        List<PC> candidates = leader.controlledPcs.Where(p => p != null).ToList();
        if (candidates.Count == 0) return;

        candidates[Random.Range(0, candidates.Count)].SetTemporaryHidden(_turns);
    }
}

public class RevealEnemyPCInspireEffect : InspireEffect
{
    private readonly int _turns;
    private readonly bool _nearest;

    public RevealEnemyPCInspireEffect(int turns = 1, bool nearest = true)
    {
        _turns = turns;
        _nearest = nearest;
    }

    public override string Description => _nearest
        ? $"Reveal the nearest enemy settlement for {_turns} turn(s)."
        : $"Reveal a random enemy settlement for {_turns} turn(s).";

    public override void Apply(Leader leader)
    {
        PC target = _nearest
            ? InspireEffectHelpers.GetNearestEnemyPC(leader)
            : InspireEffectHelpers.GetRandomEnemyPC(leader);

        target?.SetTemporaryReveal(_turns);
    }
}
