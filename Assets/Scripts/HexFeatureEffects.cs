using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the gameplay effects of the landmark features depicted on a hex's tile art
/// (see <see cref="HexFeatureEnum"/> / <see cref="HexFeatureData"/>).
///
/// Two execution points:
///  - <see cref="ProcessRest"/> runs once per character at the START of its NewTurn, i.e. on the
///    hex where it ended last turn ("rests / stays here"). This is where heals, resource trickles,
///    hazards, scouting and the on-rest buffs fire.
///  - The pass-by movement features (Road / Bridge cost, River stop) live in Hex.GetTerrainCost and
///    Board.MoveCharacterOneHex respectively, not here.
///
/// All numbers below are deliberately gathered as constants so they are easy to tune.
/// </summary>
public static class HexFeatureEffects
{
    // Healing
    private const int PondHeal = 8;       // minor
    private const int FountainHeal = 25;  // major

    // Hazards
    private const int LavaDamage = 10;
    private const int ChasmDamage = 15;
    private const int ChasmChance = 10;     // %
    private const int BlightPoisonChance = 10; // %
    private const int BlightCurseChance = 5;   // %
    private const int PoisonTurns = 2;
    private const int CurseTurns = 2;

    // Rewards
    private const int VillageResourceChance = 25; // %
    private const int MineResourceChance = 25;    // %
    private const int RuinsArtifactChance = 5;    // %

    /// <summary>Process the features of the hex the character is resting on, at the start of its turn.</summary>
    public static void ProcessRest(Character character)
    {
        if (character == null || character.killed) return;
        Hex hex = character.hex;
        if (hex == null || hex.features == HexFeatureEnum.None) return;

        Leader owner = character.GetOwner();
        bool isCommander = character.IsArmyCommander();

        // ---- Restorative ----
        if (hex.HasFeature(HexFeatureEnum.Fountain)) Heal(character, FountainHeal);
        else if (hex.HasFeature(HexFeatureEnum.Pond)) Heal(character, PondHeal);

        // ---- On-rest buffs ----
        if (hex.HasFeature(HexFeatureEnum.Watchtower))
        {
            if (owner != null) hex.RevealArea(1, false, owner);
            if (isCommander)
            {
                character.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                MessageDisplayNoUI.ShowMessage(hex, character, "The watchtower fortifies the army.", Color.cyan);
            }
        }

        if (hex.HasFeature(HexFeatureEnum.Lighthouse) && owner != null)
            RevealWater(hex, owner, 3);

        if (hex.HasFeature(HexFeatureEnum.StandingStones) && character.GetMage() > 0)
        {
            character.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
            MessageDisplayNoUI.ShowMessage(hex, character, "The standing stones grant Arcane Insight.", Color.magenta);
        }

        if (hex.HasFeature(HexFeatureEnum.Monument) && isCommander)
        {
            character.Encourage(1);
            MessageDisplayNoUI.ShowMessage(hex, character, "The monument inspires Courage.", Color.green);
        }

        // ---- Resource trickles ----
        if (hex.HasFeature(HexFeatureEnum.Village) && owner != null && Roll(VillageResourceChance))
            GrantRandomResource(owner, hex, character, mineralsOnly: false);

        if (hex.HasFeature(HexFeatureEnum.Mine) && owner != null && Roll(MineResourceChance))
            GrantRandomResource(owner, hex, character, mineralsOnly: true);

        // ---- Discovery ----
        if (hex.HasFeature(HexFeatureEnum.Ruins) && Roll(RuinsArtifactChance))
            TryUnearthArtifact(character);

        // ---- Hazards (can kill — keep last) ----
        if (hex.HasFeature(HexFeatureEnum.Lava) && character.GetAlignment() != AlignmentEnum.darkServants)
        {
            MessageDisplayNoUI.ShowMessage(hex, character, "Volcanic heat scorches the camp.", Color.red);
            character.Wounded(null, LavaDamage);
            if (character.killed) return;
        }

        if (hex.HasFeature(HexFeatureEnum.Blighted))
        {
            if (Roll(BlightPoisonChance))
            {
                character.ApplyStatusEffect(StatusEffectEnum.Poisoned, PoisonTurns);
                MessageDisplayNoUI.ShowMessage(hex, character, "The blighted ground sickens them (Poisoned).", Color.green);
            }
            if (Roll(BlightCurseChance))
            {
                character.ApplyStatusEffect(StatusEffectEnum.MorgulTouch, CurseTurns);
                MessageDisplayNoUI.ShowMessage(hex, character, "A curse clings to them (Morgul Touch).", Color.magenta);
            }
        }

        if (hex.HasFeature(HexFeatureEnum.Chasm) && Roll(ChasmChance))
        {
            MessageDisplayNoUI.ShowMessage(hex, character, "The ground gives way!", Color.red);
            character.Wounded(null, ChasmDamage);
            if (character.killed) return;
            TeleportToAnotherChasm(character, hex);
        }
    }

    private static void Heal(Character character, int amount)
    {
        if (character.health >= 100) return;
        int before = character.health;
        character.health = Mathf.Min(100, character.health + amount);
        int healed = character.health - before;
        if (healed > 0)
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"+{healed} <sprite name=\"health\">health", Color.green);
    }

    private static void RevealWater(Hex center, Leader owner, int radius)
    {
        List<Hex> inRange = center.GetHexesInRadius(radius);
        for (int i = 0; i < inRange.Count; i++)
        {
            Hex h = inRange[i];
            if (h != null && h.IsWaterTerrain()) h.Reveal(owner);
        }
    }

    private static void GrantRandomResource(Leader owner, Hex hex, Character character, bool mineralsOnly)
    {
        // Minerals = the ore a mine yields; villages can also hand over food-tier goods or coin.
        int pick = mineralsOnly ? Random.Range(0, 3) : Random.Range(0, 6);
        string label;
        switch (pick)
        {
            case 0: owner.AddIron(1, false); label = "iron"; break;
            case 1: owner.AddSteel(1, false); label = "steel"; break;
            case 2: owner.AddMithril(1, false); label = "mithril"; break;
            case 3: owner.AddLeather(1, false); label = "leather"; break;
            case 4: owner.AddTimber(1, false); label = "timber"; break;
            default: owner.AddMounts(1, false); label = "mounts"; break;
        }
        MessageDisplayNoUI.ShowMessage(hex, character, $"+1 <sprite name=\"{label}\">{label}", Color.yellow);
    }

    private static void TryUnearthArtifact(Character character)
    {
        if (character.artifacts != null && character.artifacts.Count >= Character.MAX_ARTIFACTS) return;

        Board board = Object.FindFirstObjectByType<Board>();
        if (board == null) return;

        // Pull a not-yet-discovered artifact from wherever it lies hidden on the map.
        List<Hex> sources = new();
        List<Hex> all = board.GetHexes();
        for (int i = 0; i < all.Count; i++)
        {
            Hex h = all[i];
            if (h != null && h.hiddenArtifacts != null && h.hiddenArtifacts.Count > 0) sources.Add(h);
        }
        if (sources.Count == 0) return;

        Hex sourceHex = sources[Random.Range(0, sources.Count)];
        Artifact artifact = sourceHex.hiddenArtifacts[0];
        if (artifact == null) return;

        character.artifacts.Add(artifact);
        sourceHex.hiddenArtifacts.Remove(artifact);
        sourceHex.UpdateArtifactVisibility();
        Character.RefreshArtifactPcVisibilityForHex(sourceHex);
        character.ApplyOppositeAlignmentArtifactPenalty(artifact);

        MessageDisplayNoUI.ShowMessage(character.hex, character,
            $"The ruins yield a hidden <sprite name=\"artifact\">artifact: {artifact.GetHoverText()}!", Color.green);
        Sounds.Instance?.PlayArtifactFound();
    }

    private static void TeleportToAnotherChasm(Character character, Hex from)
    {
        Board board = Object.FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Hex> chasms = new();
        List<Hex> all = board.GetHexes();
        for (int i = 0; i < all.Count; i++)
        {
            Hex h = all[i];
            if (h != null && h != from && h.HasFeature(HexFeatureEnum.Chasm) && !h.IsWaterTerrain()) chasms.Add(h);
        }
        if (chasms.Count == 0) return;

        Hex destination = chasms[Random.Range(0, chasms.Count)];
        board.MoveCharacterOneHex(character, from, destination, true, false);
        MessageDisplayNoUI.ShowMessage(destination, character, "Swallowed by the earth and cast out elsewhere!", Color.red);
    }

    private static bool Roll(int percentChance) => Random.Range(0, 100) < percentChance;
}
