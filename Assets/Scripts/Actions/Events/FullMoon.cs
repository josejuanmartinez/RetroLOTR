using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deck: dark only (sauron_base)
// 4-part: Nazgul hasted + dark agents hasted | dark non-Nazgul in open slowed 1 | FP emissaries gain Hope | FP near Nazgul revealed + feared + DS +10% atk
public class FullMoon : EventAction
{
    private const int TeleportRadius = 5;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) env.DarkServantsArmyAttackFactor = 1.10f;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed).Distinct().ToList();

        int nazgulHasted = 0, agentHasted = 0, revealed = 0, feared = 0, dsSlowed = 0, fpHoped = 0;
        foreach (Character ch in allChars)
        {
            if (ch.race == RacesEnum.Nazgul)
            { ch.ClearStatusEffect(StatusEffectEnum.Halted); ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); nazgulHasted++; } // big bonus own
            else if (ch.GetAlignment() == AlignmentEnum.darkServants && ch.GetAgent() > 0)
            { ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); agentHasted++; }                   // big bonus own
            else if (ch.GetAlignment() == AlignmentEnum.darkServants)
            {
                bool isOpen = ch.hex != null && (ch.hex.terrainType == TerrainEnum.plains || ch.hex.terrainType == TerrainEnum.grasslands);
                if (isOpen) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); dsSlowed++; } // small debuff own
            }
            else if (ch.GetAlignment() == AlignmentEnum.freePeople)
            {
                if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); fpHoped++; } // small bonus other

                bool nearNazgul = allChars.Any(n => n.race == RacesEnum.Nazgul && n.hex != null && ch.hex != null && n.hex.GetHexesInRadius(2).Contains(ch.hex));
                if (nearNazgul) { ch.ClearStatusEffect(StatusEffectEnum.Hidden); ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1); revealed++; feared++; } // big debuff other
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Full Moon (ongoing): {nazgulHasted} Nazgul hasted; {agentHasted} dark agents hasted; {revealed} FP near Nazgul revealed and feared; {fpHoped} FP emissaries hopeful.",
            Color.magenta);
    }

    private static void MoveCharacterToHex(Character character, Hex targetHex)
    {
        if (character == null || targetHex == null || character.hex == targetHex) return;
        Hex prev = character.hex;
        if (prev != null)
        {
            prev.characters.Remove(character);
            if (character.IsArmyCommander() && prev.armies != null && character.GetArmy() != null) prev.armies.Remove(character.GetArmy());
            prev.RedrawCharacters(); prev.RedrawArmies();
            Character.RefreshArtifactPcVisibilityForHex(prev);
        }
        if (!targetHex.characters.Contains(character)) targetHex.characters.Add(character);
        if (character.IsArmyCommander() && targetHex.armies != null && character.GetArmy() != null && !targetHex.armies.Contains(character.GetArmy()))
            targetHex.armies.Add(character.GetArmy());
        character.hex = targetHex;
        character.RefreshKidnappedCharactersPosition();
        Character.RefreshArtifactPcVisibilityForHex(targetHex);
        targetHex.RedrawCharacters(); targetHex.RedrawArmies();
        if (character.GetOwner() == UnityEngine.Object.FindFirstObjectByType<Game>()?.player) targetHex.RevealArea(1, true);
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
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var nazguls = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul && ch.hex != null).Distinct().ToList();
            if (nazguls.Count == 0) return false;
            int teleported = 0, moved = 0;
            foreach (var nazgul in nazguls)
            {
                if (nazgul.hex == null) continue;
                var target = nazgul.hex.GetHexesInRadius(TeleportRadius).FirstOrDefault(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople));
                if (target != null) { foreach (var fp in target.characters.Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople).ToList()) fp.ClearStatusEffect(StatusEffectEnum.Hidden); MoveCharacterToHex(nazgul, target); nazgul.ApplyStatusEffect(StatusEffectEnum.Haste, 1); teleported++; }
                else { nazgul.moved = Math.Max(0, nazgul.moved - 2); moved++; }
            }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Full Moon: {teleported} Nazgul descend on Free People; {moved} advance.", Color.magenta);
            return true;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
