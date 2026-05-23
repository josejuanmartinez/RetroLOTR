using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class IthilienRangerHood : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Hex> allHexes = board.GetHexes().Where(h => h != null).ToList();
        int forestAllyCount = 0, agentCount = 0, revealed = 0;

        // Free people in forest/swamp gain Hidden; all free people agents gain ArcaneInsight
        HashSet<Hex> occupiedForestHexes = new HashSet<Hex>();
        foreach (Hex hex in allHexes)
        {
            if (hex.characters == null) continue;
            bool isForestOrSwamp = hex.terrainType == TerrainEnum.forest || hex.terrainType == TerrainEnum.swamp;
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople).ToList())
            {
                if (isForestOrSwamp) { ch.Hide(1); forestAllyCount++; occupiedForestHexes.Add(hex); }
                if (ch.GetAgent() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); agentCount++; }
            }
        }

        // Reveal hidden dark servants on hexes adjacent to free-people-occupied forest/swamp hexes
        HashSet<Hex> adjHexes = new HashSet<Hex>();
        foreach (Hex fh in occupiedForestHexes)
            foreach (Hex adj in fh.GetHexesInRadius(1))
                if (adj != null) adjHexes.Add(adj);

        foreach (Hex adj in adjHexes)
        {
            if (adj.characters == null) continue;
            foreach (Character enemy in adj.characters.Where(ch => ch != null && !ch.killed
                && ch.GetAlignment() == AlignmentEnum.darkServants
                && ch.HasStatusEffect(StatusEffectEnum.Hidden)).ToList())
            {
                enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                revealed++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Ithilien Ranger Hood (ongoing): {forestAllyCount} allied forest units hidden; {agentCount} agents gain insight; {revealed} enemies revealed.",
            Color.green);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;
            character.Hide(1);
            if (character.hex != null)
                character.hex.RevealArea(1, true, character.GetOwner());
            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"{character.characterName} dons the Ithilien hood: Hidden (1 turn) and reveals enemies in radius 1.",
                Color.green);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.GetAlignment() == AlignmentEnum.freePeople;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
