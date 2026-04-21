// Auto-generated wrapper actions for new deck cards

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static CustomDeckExpansionActionHelpers;

public static class CustomDeckExpansionActionHelpers
{
    public static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }
}


public class KindleDawnfire : Dawn { }
public class WardAgainsttheEye : VisionsOfTolEressea { }
public class StewardsMuster : RalliedMen { }
public class HallowtheRoad : GoingOnAnAdventure { }
public class OathoftheWestfold : FirstLightOnTheThirdDay { }
public class MorgulTithe : ReachOfBaradUngol { }
public class EyesTribute : WhatDoesMordorCommand { }
public class BlackGateSortie : RaidFromTheMountains { }
public class ShroudofGorgoroth : DoorsOfNight { }
public class ChainsoftheLidlessEye : TheNineRideAgain { }
public class WormtonguesWhisper : UnderTheWhiteHand { }
public class FurnacesofIsengard : ChoppingTheTrees { }
public class UrukVanguard : WellEquipedArmy { }
public class FalseParley : RestlessEast { }
public class StormfromOrthanc : Caradhras { }
public class ThroughMirkwoodShadowsAction : EventAction
{
    private const int RevealCount = 5;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Leader owner = character.GetOwner();
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (owner == null || board == null || board.hexes == null) return false;

            var forestHexes = board.hexes.Values
                .Where(h => h != null && h.terrainType == TerrainEnum.forest && !h.IsScoutedBy(owner))
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(RevealCount)
                .ToList();

            if (forestHexes.Count == 0) return false;

            owner.AddTemporarySeenHexes(forestHexes);

            if (owner == UnityEngine.Object.FindFirstObjectByType<Game>()?.player)
            {
                owner.RefreshVisibleHexesImmediate();
            }

            for (int i = 0; i < forestHexes.Count; i++)
            {
                forestHexes[i]?.RefreshVisibilityRendering();
            }

            forestHexes[0]?.LookAt();
            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Through Mirkwood Shadows: {forestHexes.Count} unseen forest hex(es) are revealed for 1 turn.",
                new UnityEngine.Color(0.38f, 0.62f, 0.42f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Leader owner = character.GetOwner();
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (owner == null || board == null || board.hexes == null) return false;

            return board.hexes.Values.Any(h => h != null && h.terrainType == TerrainEnum.forest && !h.IsScoutedBy(owner));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class EreborBeckonsAction : EventAction
{
    private const int Duration = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment()
                    && (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Haste, Duration);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Erebor Beckons: {allies.Count} Hobbit/Dwarf unit(s) in this hex gain Haste for {Duration} turns.",
                new UnityEngine.Color(0.78f, 0.66f, 0.32f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            return character.hex.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment()
                && (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class UnderMountainBannersAction : EventAction
{
    private const int Radius = 1;
    private const int Duration = 1;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.race == RacesEnum.Dwarf && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].ApplyStatusEffect(StatusEffectEnum.Strengthened, Duration);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Under Mountain Banners: {targets.Count} allied Dwarf commander(s) gain Strengthened for {Duration} turn.",
                new UnityEngine.Color(0.74f, 0.68f, 0.4f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.race == RacesEnum.Dwarf && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class HiddenTreasureAction : EventAction
{
    private const int GoldAmount = 5;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            owner.AddGold(GoldAmount);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Hidden Treasure yields +{GoldAmount} <sprite name=\"gold\">.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.GetOwner() != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class TrollsHoardGrantArtifactAction : EventAction
{
    private static Artifact CloneArtifact(Artifact source)
    {
        if (source == null) return null;
        return new Artifact
        {
            artifactName = source.artifactName,
            hidden = source.hidden,
            alignment = source.alignment,
            commanderBonus = source.commanderBonus,
            agentBonus = source.agentBonus,
            emmissaryBonus = source.emmissaryBonus,
            mageBonus = source.mageBonus,
            bonusAttack = source.bonusAttack,
            bonusDefense = source.bonusDefense,
            passiveEffectId = source.passiveEffectId,
            passiveEffectValue = source.passiveEffectValue,
            transferable = source.transferable,
            spriteString = source.spriteString
        };
    }

    private static HashSet<string> GetOwnedOrHiddenArtifactNames(Game game, Board board)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        if (board != null && board.hexes != null)
        {
            foreach (Hex hex in board.hexes.Values)
            {
                if (hex?.hiddenArtifacts == null) continue;
                foreach (Artifact artifact in hex.hiddenArtifacts)
                {
                    if (artifact != null && !string.IsNullOrWhiteSpace(artifact.artifactName))
                    {
                        names.Add(artifact.artifactName);
                    }
                }
            }
        }

        if (game != null)
        {
            foreach (Leader leader in UnityEngine.Object.FindObjectsByType<Leader>(FindObjectsSortMode.None))
            {
                if (leader?.controlledCharacters == null) continue;
                foreach (Character ch in leader.controlledCharacters)
                {
                    if (ch?.artifacts == null) continue;
                    foreach (Artifact artifact in ch.artifacts)
                    {
                        if (artifact != null && !string.IsNullOrWhiteSpace(artifact.artifactName))
                        {
                            names.Add(artifact.artifactName);
                        }
                    }
                }
            }
        }

        return names;
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
            if (character.artifacts.Count >= Character.MAX_ARTIFACTS) return false;

            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (game == null || game.artifacts == null) return false;

            HashSet<string> unavailable = GetOwnedOrHiddenArtifactNames(game, board);
            foreach (Artifact owned in character.artifacts)
            {
                if (owned != null && !string.IsNullOrWhiteSpace(owned.artifactName)) unavailable.Add(owned.artifactName);
            }

            List<Artifact> candidates = game.artifacts
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.artifactName) && !unavailable.Contains(a.artifactName))
                .ToList();

            if (candidates.Count == 0) return false;

            Artifact chosen = CloneArtifact(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
            if (chosen == null) return false;

            character.artifacts.Add(chosen);
            character.ApplyOppositeAlignmentArtifactPenalty(chosen);
            Character.RefreshArtifactPcVisibilityForHex(character.hex);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Troll's Hoard yields {chosen.artifactName}.", Color.yellow);
            Sounds.Instance?.PlayArtifactFound();
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.artifacts.Count >= Character.MAX_ARTIFACTS) return false;

            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (game == null || game.artifacts == null) return false;

            HashSet<string> unavailable = GetOwnedOrHiddenArtifactNames(game, board);
            foreach (Artifact owned in character.artifacts)
            {
                if (owned != null && !string.IsNullOrWhiteSpace(owned.artifactName)) unavailable.Add(owned.artifactName);
            }

            return game.artifacts.Any(a => a != null && !string.IsNullOrWhiteSpace(a.artifactName) && !unavailable.Contains(a.artifactName));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class VeinOfTrueSilverAction : EventAction
{
    private const int MithrilAmount = 3;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Leader owner = character.GetOwner();
            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            if (owner == null || game == null || deckManager == null) return false;

            PlayableLeader player = game.player;
            if (player == null || owner != player || !deckManager.HasDeckFor(player)) return false;

            var hand = deckManager.GetHand(player);
            CardData discardTarget = hand.FirstOrDefault(card => card != null && !card.IsEncounterCard() && !string.Equals(card.name, "Vein of True Silver", StringComparison.OrdinalIgnoreCase));
            if (discardTarget == null) return false;

            CardData balrog = deckManager.FindCardByNameForLeader(player, "Balrog");
            if (balrog == null || !balrog.IsEncounterCard()) return false;

            owner.AddMithril(MithrilAmount);
            if (!deckManager.TryDiscardCard(player, discardTarget.name, out _)) return false;
            if (!deckManager.TryAddCardToHand(player, balrog)) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Vein of True Silver yields +{MithrilAmount} <sprite name=\"mithril\">, but the Balrog enters your hand.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Leader owner = character.GetOwner();
            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            if (owner == null || game == null || deckManager == null || game.player == null) return false;
            if (owner != game.player || !deckManager.HasDeckFor(game.player)) return false;

            bool hasReplaceableCard = deckManager.GetHand(game.player)
                .Any(card => card != null && !card.IsEncounterCard() && !string.Equals(card.name, "Vein of True Silver", StringComparison.OrdinalIgnoreCase));
            CardData balrog = deckManager.FindCardByNameForLeader(game.player, "Balrog");
            return hasReplaceableCard && balrog != null && balrog.IsEncounterCard();
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class SealTheLowerGatesAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            List<Character> enemies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Seal the Lower Gates halts {enemies.Count} enemy unit(s) in this hex.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment());
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class HallOfOathsAction : EventAction
{
    private const int LoyaltyGain = 15;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;
            PC pc = character.hex.GetPC();
            if (pc == null) return false;
            pc.IncreaseLoyalty(LoyaltyGain, character);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Hall of Oaths grants {pc.pcName} +{LoyaltyGain} loyalty.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.GetPC() != null && character.hex.GetPC().loyalty < 100;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class AshenBreathRepaidAction : EventAction
{
    private const int Radius = 1;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> allies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            int burningCleared = 0;
            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i].HasStatusEffect(StatusEffectEnum.Burning))
                {
                    allies[i].ClearStatusEffect(StatusEffectEnum.Burning);
                    burningCleared++;
                }
                allies[i].ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Ashen Breath Repaid clears Burning from {burningCleared} allied Dwarf unit(s) and strengthens {allies.Count}.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class ForgefireKeptAction : EventAction
{
    private const int Duration = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;
            Leader owner = character.GetOwner();
            if (owner == null) return false;

            Character target = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf && IsAllied(character, ch))
                .OrderByDescending(ch => ch.IsArmyCommander() ? 1 : 0)
                .ThenByDescending(ch => ch.GetCommander() + ch.GetAgent() + ch.GetEmmissary() + ch.GetMage())
                .FirstOrDefault();

            if (target == null) return false;

            target.ApplyStatusEffect(StatusEffectEnum.Fortified, Duration);
            owner.AddIron(1);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Forgefire Kept fortifies {target.characterName} for {Duration} turns and yields +1 <sprite name=\"iron\">.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters != null && character.GetOwner() != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class WallOfOakAndIronAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf && (ch.GetOwner() == character.GetOwner() || (character.GetAlignment() != AlignmentEnum.neutral && ch.GetAlignment() == character.GetAlignment())))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Wall of Oak and Iron fortifies {allies.Count} allied Dwarf unit(s).", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf && (ch.GetOwner() == character.GetOwner() || (character.GetAlignment() != AlignmentEnum.neutral && ch.GetAlignment() == character.GetAlignment())));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class PitchFromTheMurderHolesAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            List<Character> enemies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].ApplyStatusEffect(StatusEffectEnum.Burning, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Pitch from the Murder-Holes sets {enemies.Count} enemy unit(s) Burning.", Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment());
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class RaiseTheInnerBastionAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || pc.owner != character.GetOwner() || pc.fortSize >= FortSizeEnum.citadel) return false;
            pc.IncreaseFort();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Raise the Inner Bastion increases the fortifications of {pc.pcName} by 1 level.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.GetPC() != null
                && character.hex.GetPC().owner == character.GetOwner()
                && character.hex.GetPC().fortSize < FortSizeEnum.citadel;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class PalantirGlimpseAction : EventAction
{
    private const int RevealCount = 3;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
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

            Leader owner = character.GetOwner();
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (owner == null || board == null) return false;

            List<Hex> chosen = board.GetHexes()
                .Where(hex => hex != null && !hex.IsHexRevealed())
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(RevealCount)
                .ToList();

            if (chosen.Count == 0) return false;

            owner.AddTemporarySeenHexes(chosen);
            if (owner == UnityEngine.Object.FindFirstObjectByType<Game>()?.player)
            {
                owner.RefreshVisibleHexesImmediate();
            }

            for (int i = 0; i < chosen.Count; i++)
            {
                chosen[i]?.RefreshVisibilityRendering();
            }

            character.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
            chosen[0]?.LookAt();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Palantir Glimpse reveals {chosen.Count} unseen hex(es) for 1 turn and grants Arcane Insight.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            return character != null && character.GetOwner() != null && board != null && board.GetHexes().Any(hex => hex != null && !hex.IsHexRevealed());
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class WordsOfWardingAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            Character target = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .OrderByDescending(ch => ch.GetMage() + ch.GetEmmissary())
                .FirstOrDefault();
            if (target == null) return false;

            target.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
            target.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Words of Warding shelters {target.characterName} with Fortified and Arcane Insight.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class TheHiddenScriptAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (game == null || deckManager == null || board == null) return false;

            Hex artifactHex = board.GetHexes().FirstOrDefault(h => h != null && h.hiddenArtifacts != null && h.hiddenArtifacts.Count > 0);
            if (artifactHex == null) return false;

            artifactHex.RevealArtifact();

            int drawn = 0;
            if (character.GetOwner() == game.player && deckManager.TryDrawCard(game.player, out _))
            {
                drawn = 1;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Hidden Script reveals an artifact site{(drawn > 0 ? " and draws 1 card." : ".")}", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            return character != null && board != null && board.GetHexes().Any(h => h != null && h.hiddenArtifacts != null && h.hiddenArtifacts.Count > 0);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class LightThroughCloudAction : EventAction
{
    private const int Radius = 1;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && !IsAllied(character, ch))
                .Distinct()
                .ToList();

            int fearCleared = 0;
            int hiddenCleared = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].HasStatusEffect(StatusEffectEnum.Fear))
                {
                    enemies[i].ClearStatusEffect(StatusEffectEnum.Fear);
                    fearCleared++;
                }
                if (enemies[i].HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    enemies[i].ClearStatusEffect(StatusEffectEnum.Hidden);
                    hiddenCleared++;
                }
            }

            if (fearCleared == 0 && hiddenCleared == 0) return false;
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Light Through Cloud strips Hidden from {hiddenCleared} and clears Fear from {fearCleared} enemy unit(s).", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && !IsAllied(character, ch)
                    && (ch.HasStatusEffect(StatusEffectEnum.Fear) || ch.HasStatusEffect(StatusEffectEnum.Hidden)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class RuneOfTheWestAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            Character target = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .OrderByDescending(ch => ch.GetMage() + ch.GetEmmissary())
                .FirstOrDefault();
            if (target == null) return false;

            target.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
            target.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Rune of the West veils {target.characterName} in Hidden and Arcane Insight.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class FarSpeakingThoughtAction : EventAction
{
    private const int MaxTargets = 2;
    private const int Radius = 3;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && !IsAllied(character, ch))
                .OrderByDescending(ch => ch.HasStatusEffect(StatusEffectEnum.Hidden) ? 1 : 0)
                .ThenBy(_ => UnityEngine.Random.value)
                .Take(MaxTargets)
                .ToList();

            if (targets.Count == 0) return false;

            HashSet<Hex> revealed = new();
            int hiddenCleared = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    targets[i].ClearStatusEffect(StatusEffectEnum.Hidden);
                    hiddenCleared++;
                }
                if (targets[i].hex != null && revealed.Add(targets[i].hex))
                {
                    targets[i].hex.RevealArea(0, true, owner);
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Far-Speaking Thought reveals {revealed.Count} enemy hex(es) and strips Hidden from {hiddenCleared} target(s).", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && !IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
