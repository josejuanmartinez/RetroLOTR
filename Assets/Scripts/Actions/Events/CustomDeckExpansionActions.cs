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
public class StormfromOrthanc : EventAction
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
                .Where(ch => ch != null && !ch.killed && (ch.GetOwner() == character.GetOwner() || (character.GetAlignment() != AlignmentEnum.neutral && ch.GetAlignment() == character.GetAlignment())))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].hasActionedThisTurn = false;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Storm from Orthanc drives {allies.Count} friendly unit(s) in this hex to act again.", Color.white);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed && (ch.GetOwner() == character.GetOwner() || (character.GetAlignment() != AlignmentEnum.neutral && ch.GetAlignment() == character.GetAlignment())));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class OrthancsSurveillanceAction : EventAction
{
    private const int Radius = 4;

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

            List<Hex> revealed = new();
            foreach (Hex hex in character.hex.GetHexesInRadius(Radius))
            {
                if (hex == null) continue;
                bool enemyPc = hex.GetPC() != null && hex.GetPC().owner != owner;
                bool enemyArmy = hex.armies != null && hex.armies.Any(a => a != null && !a.killed && a.commander != null && a.commander.GetOwner() != owner);
                if (!enemyPc && !enemyArmy) continue;

                hex.RevealArea(0, true, owner);
                revealed.Add(hex);
            }

            if (revealed.Count == 0) return false;
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Orthanc's Surveillance reveals {revealed.Count} enemy PC/army hex(es) in radius {Radius}.", Color.white);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            Leader owner = character.GetOwner();
            if (owner == null) return false;

            return character.hex.GetHexesInRadius(Radius).Any(hex => hex != null
                && ((hex.GetPC() != null && hex.GetPC().owner != owner)
                    || (hex.armies != null && hex.armies.Any(a => a != null && !a.killed && a.commander != null && a.commander.GetOwner() != owner))));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class EnginesFromIsengardAction : EventAction
{
    private const int Radius = 2;
    private const int BonusProc = 25;

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

            List<Army> armies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.armies != null)
                .SelectMany(h => h.armies)
                .Where(a => a != null && !a.killed && a.commander != null && a.commander.GetOwner() == owner)
                .Distinct()
                .ToList();

            if (armies.Count == 0) return false;

            for (int i = 0; i < armies.Count; i++)
            {
                armies[i].specialAbilityProcChance = Mathf.Clamp(armies[i].specialAbilityProcChance + BonusProc, 1, 100);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Engines from Isengard drives {armies.Count} allied arm(ies) to +{BonusProc}% proc chance this turn.", Color.white);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            Leader owner = character.GetOwner();
            if (owner == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.armies != null)
                .SelectMany(h => h.armies)
                .Any(a => a != null && !a.killed && a.commander != null && a.commander.GetOwner() == owner);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class PalantirOfOrthancAction : EventAction
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
            if (game == null || deckManager == null || board == null || game.player == null) return false;
            if (character.GetOwner() != game.player || !deckManager.HasDeckFor(game.player) || deckManager.GetHand(game.player).Count >= deckManager.GetHandSize()) return false;

            CardData peek = deckManager.GetDrawPile(game.player).Take(3).FirstOrDefault(card => card != null);
            if (peek == null) return false;
            if (!deckManager.TryAddCardToHand(game.player, peek)) return false;

            Leader owner = character.GetOwner();
            Hex nearestEnemyPc = board.GetHexes()
                .Where(h => h != null && h.GetPC() != null && h.GetPC().owner != owner)
                .OrderBy(h => Vector2.Distance(character.hex.v2, h.v2))
                .FirstOrDefault();

            if (nearestEnemyPc != null)
            {
                nearestEnemyPc.RevealArea(0, true, owner);
                MessageDisplayNoUI.ShowMessage(character.hex, character, $"Palantír of Orthanc secures {peek.name} and reveals {nearestEnemyPc.GetPC().pcName}.", Color.white);
            }
            else
            {
                MessageDisplayNoUI.ShowMessage(character.hex, character, $"Palantír of Orthanc secures {peek.name} from the top of your deck.", Color.white);
            }

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            return character != null && game != null && deckManager != null && game.player != null
                && character.GetOwner() == game.player
                && deckManager.HasDeckFor(game.player)
                && deckManager.GetHand(game.player).Count < deckManager.GetHandSize()
                && deckManager.GetDrawPile(game.player).Take(3).Any(card => card != null);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
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
    private const int RevealCount = 10;

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
                .Where(hex => hex != null && hex.terrainType == TerrainEnum.mountains)
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(RevealCount)
                .ToList();

            if (chosen.Count == 0) return false;

            owner.AddTemporarySeenHexes(chosen, 1);
            if (owner == UnityEngine.Object.FindFirstObjectByType<Game>()?.player)
            {
                owner.RefreshVisibleHexesImmediate();
            }

            for (int i = 0; i < chosen.Count; i++)
            {
                chosen[i]?.RefreshVisibilityRendering();
            }

            chosen[0]?.LookAt();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Signal Fires of Anorien reveals {chosen.Count} mountain hex(es) for 1 turn.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            return character != null && character.GetOwner() != null && board != null && board.GetHexes().Any(hex => hex != null && hex.terrainType == TerrainEnum.mountains);
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
    private const int Radius = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Hex> area = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null)
                .Distinct()
                .ToList();

            if (area.Count == 0) return false;

            for (int i = 0; i < area.Count; i++)
            {
                area[i].RevealMapOnlyArea(0, false, false);
            }

            if (character.GetOwner() == FindFirstObjectByType<Game>()?.player)
            {
                MinimapManager.RefreshMinimap();
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Wardens of the Rammas: {area.Count} hex(es) around this place are revealed as unseen for 1 turn.", new Color(0.82f, 0.76f, 0.6f));
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null
                && character.hex.GetHexesInRadius(Radius).Any(h => h != null);
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

            List<Character> unitsAtHex = character.hex.characters != null
                ? character.hex.characters.Where(ch => ch != null && !ch.killed).Distinct().ToList()
                : new List<Character>();

            int courageGranted = 0;
            int fearCleared = 0;
            int haltedEnemies = 0;

            for (int i = 0; i < unitsAtHex.Count; i++)
            {
                Character target = unitsAtHex[i];
                if (IsAllied(character, target))
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
                    courageGranted++;

                    if (target.HasStatusEffect(StatusEffectEnum.Fear))
                    {
                        target.ClearStatusEffect(StatusEffectEnum.Fear);
                        fearCleared++;
                    }
                }
                else
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
                    haltedEnemies++;
                }
            }

            if (courageGranted == 0 && fearCleared == 0 && haltedEnemies == 0) return false;
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Light Through Cloud: {courageGranted} friendly unit(s) gain Courage (1); Fear is cleared from {fearCleared}; {haltedEnemies} enemy unit(s) are Halted (1).", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed);
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

public class CounselByFirelightAction : EventAction
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
            if (game == null || deckManager == null || game.player == null) return false;
            if (character.GetOwner() != game.player || !deckManager.HasDeckFor(game.player)) return false;

            var hand = deckManager.GetHand(game.player);
            int maxHand = deckManager.GetHandSize();
            CardData discardTarget = hand.FirstOrDefault(card => card != null && !card.IsEncounterCard() && !string.Equals(card.name, "Counsel By Firelight", StringComparison.OrdinalIgnoreCase));
            if (discardTarget == null) return false;

            if (!deckManager.TryDiscardCard(game.player, discardTarget.name, out _)) return false;

            int roomAfterDiscard = Math.Max(0, maxHand - deckManager.GetHand(game.player).Count);
            int drawsAllowed = Math.Min(2, roomAfterDiscard);
            int drawn = 0;
            if (drawsAllowed >= 1 && deckManager.TryDrawCard(game.player, out _)) drawn++;
            if (drawsAllowed >= 2 && deckManager.TryDrawCard(game.player, out _)) drawn++;

            if (drawn == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Counsel by Firelight discards 1 card and draws {drawn}.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            if (character == null || game == null || deckManager == null || game.player == null) return false;
            if (character.GetOwner() != game.player || !deckManager.HasDeckFor(game.player)) return false;

            var hand = deckManager.GetHand(game.player);
            bool hasDiscardTarget = hand.Any(card => card != null && !card.IsEncounterCard() && !string.Equals(card.name, "Counsel By Firelight", StringComparison.OrdinalIgnoreCase));
            int roomAfterDiscard = Math.Max(0, deckManager.GetHandSize() - (hand.Count - 1));
            return hasDiscardTarget && roomAfterDiscard > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class StayTheDarkTaleAction : EventAction
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
            if (game == null || deckManager == null || game.player == null) return false;
            if (character.GetOwner() != game.player || !deckManager.HasDeckFor(game.player)) return false;

            var hand = deckManager.GetHand(game.player);
            int maxHand = deckManager.GetHandSize();
            CardData encounterCard = hand.FirstOrDefault(card => card != null && card.IsEncounterCard());
            if (encounterCard == null) return false;

            if (!deckManager.TryReturnCardToHand(game.player, encounterCard.name)) return false;
            if (deckManager.GetHand(game.player).Count >= maxHand) return false;
            if (!deckManager.TryDrawCard(game.player, out CardData replacement) || replacement == null) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Stay the Dark Tale sets aside {encounterCard.name} and draws a replacement.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            if (character == null || game == null || deckManager == null || game.player == null) return false;
            if (character.GetOwner() != game.player || !deckManager.HasDeckFor(game.player)) return false;

            var hand = deckManager.GetHand(game.player);
            bool hasEncounter = hand.Any(card => card != null && card.IsEncounterCard());
            return hasEncounter && hand.Count <= deckManager.GetHandSize();
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class KingUnderTheMountainAction : EventAction
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
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf && IsAllied(character, ch))
                .OrderByDescending(ch => ch.hasActionedThisTurn ? 1 : 0)
                .ThenByDescending(ch => ch.GetCommander() + ch.GetAgent() + ch.GetEmmissary() + ch.GetMage())
                .FirstOrDefault();
            if (target == null) return false;

            target.hasActionedThisTurn = false;
            target.moved = 0;
            target.lastPlayedActionClassNameThisTurn = null;
            target.lastPlayedActionNameThisTurn = null;
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"King Under the Mountain readies {target.characterName} for one more labor this turn.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters != null
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

public class DoorsOfTheDeepDelvedAction : EventAction
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
            if (game == null || deckManager == null || game.player == null) return false;
            if (character.GetOwner() != game.player || !deckManager.HasDeckFor(game.player)) return false;
            if (deckManager.GetHand(game.player).Count >= deckManager.GetHandSize()) return false;

            PlayableLeader player = game.player;
            var drawPile = deckManager.GetDrawPile(player).Take(5).ToList();
            CardData chosen = drawPile.FirstOrDefault(card => card != null &&
                ((card.race == RacesEnum.Dwarf)
                || (!string.IsNullOrWhiteSpace(card.name) && card.name.IndexOf("mithril", StringComparison.OrdinalIgnoreCase) >= 0)
                || (card.tags != null && card.tags.Any(tag => tag != null && (tag.IndexOf("treasure", StringComparison.OrdinalIgnoreCase) >= 0 || tag.IndexOf("artifact", StringComparison.OrdinalIgnoreCase) >= 0 || tag.IndexOf("dwarf", StringComparison.OrdinalIgnoreCase) >= 0)))));
            if (chosen == null) return false;

            if (!deckManager.TryAddCardToHand(player, chosen)) return false;
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Doors of the Deep Delved finds {chosen.name} among the deep ways.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            return character != null && game != null && deckManager != null && game.player != null && character.GetOwner() == game.player
                && deckManager.HasDeckFor(game.player)
                && deckManager.GetHand(game.player).Count < deckManager.GetHandSize()
                && deckManager.GetDrawPile(game.player).Take(5).Any(card => card != null);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class IllNewsBeforeDawnAction : EventAction
{
    private const int Radius = 5;

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

            var nearby = character.hex.GetHexesInRadius(Radius).Where(h => h != null).ToList();
            Hex targetHex = nearby.FirstOrDefault(h => h.GetPC() != null && h.GetPC().owner != owner)
                ?? nearby.FirstOrDefault(h => h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && !IsAllied(character, ch) && ch.IsArmyCommander()));
            if (targetHex == null) return false;

            targetHex.RevealArea(0, true, owner);
            if (targetHex.GetPC() != null && targetHex.GetPC().loyalty > 0)
            {
                targetHex.GetPC().DecreaseLoyalty(10, character);
                MessageDisplayNoUI.ShowMessage(character.hex, character, $"Ill News Before Dawn reveals {targetHex.GetPC().pcName} and lowers its loyalty.", Color.white);
            }
            else
            {
                MessageDisplayNoUI.ShowMessage(character.hex, character, $"Ill News Before Dawn exposes enemy forces before dawn.", Color.white);
            }
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.GetHexesInRadius(Radius).Any(h => h != null && ((h.GetPC() != null && h.GetPC().owner != character.GetOwner()) || (h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && !IsAllied(character, ch) && ch.IsArmyCommander()))));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class RidersSentInHasteAction : EventAction
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

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null) return false;

            Hex farthestOwnedPcHex = board.GetHexes()
                .Where(h => h != null && h.GetPC() != null && h.GetPC().owner == character.GetOwner())
                .OrderByDescending(h => Vector2.Distance(character.hex.v2, h.v2))
                .FirstOrDefault();
            if (farthestOwnedPcHex == null) return false;

            board.MoveCharacterOneHex(character, character.hex, farthestOwnedPcHex, true, false);

            PC pc = farthestOwnedPcHex.GetPC();
            if (pc != null)
            {
                pc.IncreaseLoyalty(5, character);
                MessageDisplayNoUI.ShowMessage(farthestOwnedPcHex, character, $"Riders Sent in Haste carries {character.characterName} to {pc.pcName}, where loyalty rises by 5.", Color.white);
            }

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            return character != null && character.hex != null && board != null
                && board.GetHexes().Any(h => h != null && h.GetPC() != null && h.GetPC().owner == character.GetOwner());
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class CouncilInAShutteredHallAction : EventAction
{
    private const int Radius = 2;

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
            if (game == null || deckManager == null || game.player == null) return false;
            if (character.GetOwner() != game.player || !deckManager.HasDeckFor(game.player)) return false;
            if (deckManager.GetHand(game.player).Count >= deckManager.GetHandSize()) return false;

            CardData peek = deckManager.GetDrawPile(game.player).Take(3).FirstOrDefault(card => card != null);
            if (peek == null) return false;
            if (!deckManager.TryAddCardToHand(game.player, peek)) return false;

            PC pc = character.hex.GetPC();
            bool grantedLoyalty = false;
            if (pc != null && pc.owner == character.GetOwner() && character.hex.GetHexesInRadius(Radius).Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && !IsAllied(character, ch))))
            {
                pc.IncreaseLoyalty(5, character);
                grantedLoyalty = true;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Council in a Shuttered Hall secures {peek.name}{(grantedLoyalty ? " and hardens local loyalty." : ".")}", Color.white);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            return character != null && game != null && deckManager != null && game.player != null && character.GetOwner() == game.player
                && deckManager.HasDeckFor(game.player)
                && deckManager.GetHand(game.player).Count < deckManager.GetHandSize()
                && deckManager.GetDrawPile(game.player).Take(3).Any(card => card != null);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class WhiteTowerArsenalAction : EventAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static List<Character> GetTargets(Character character)
    {
        if (character == null || character.hex == null || character.hex.characters == null) return new List<Character>();
        return character.hex.characters
            .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch) && ch.GetArmy() != null)
            .Distinct()
            .ToList();
    }

    private static int GrantShielded(Army army)
    {
        if (army == null || army.troopAbilityGroups == null) return 0;

        int shieldedTroops = 0;
        foreach (ArmyTroopAbilityGroup group in army.troopAbilityGroups)
        {
            if (group == null || group.amount <= 0) continue;
            group.abilities ??= new List<ArmySpecialAbilityEnum>();
            if (group.abilities.Contains(ArmySpecialAbilityEnum.Shielded)) continue;
            group.abilities.Add(ArmySpecialAbilityEnum.Shielded);
            shieldedTroops += group.amount;
        }

        return shieldedTroops;
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

            List<Character> targets = GetTargets(character);
            if (targets.Count == 0) return false;

            int fortifiedTargets = 0;
            int shieldedTroops = 0;
            foreach (Character target in targets)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                fortifiedTargets++;
                shieldedTroops += GrantShielded(target.GetArmy());
                target.GetArmy()?.commander?.hex?.RedrawArmies();
            }

            character.hex?.RedrawCharacters();
            character.hex?.RedrawArmies();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"White Tower Arsenal arms {fortifiedTargets} allied army commander(s) with Fortified and {shieldedTroops} shielded troop(s).", Color.cyan);
            return fortifiedTargets > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return GetTargets(character).Count > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class OsgiliathQuartermastersAction : EventAction
{
    private const int Radius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static List<Character> GetTargets(Character character)
    {
        if (character == null || character.hex == null) return new List<Character>();

        return character.hex.GetHexesInRadius(Radius)
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch) && ch.GetArmy() != null)
            .Distinct()
            .ToList();
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

            List<Character> targets = GetTargets(character);
            if (targets.Count == 0) return false;

            Character target = targets
                .OrderByDescending(ch => ch.GetCommander() + ch.GetEmmissary())
                .ThenByDescending(ch => ch.GetArmy() != null ? ch.GetArmy().GetSize() : 0)
                .FirstOrDefault();
            if (target == null || target.GetArmy() == null) return false;

            Leader owner = target.GetOwner();
            owner?.AddTimber(1, false);
            owner?.AddIron(1, false);
            owner?.AddGold(1, false);
            target.GetArmy().AddXp(1, "Quartermasters");
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Osgiliath Quartermasters replenish {target.characterName}: +1 timber, +1 iron, +1 gold, and +1 army XP.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return GetTargets(character).Count > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class RammasEchorAction : EventAction
{
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

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();
            List<Character> enemies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && !IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (allies.Count == 0 && enemies.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            }

            character.hex.RedrawCharacters();
            character.hex.RedrawArmies();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Rammas Echor braces the wall: {allies.Count} allied unit(s) gain Fortified and {enemies.Count} enemy unit(s) are Halted.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class DolAmrothShipyardAction : EventAction
{
    private static bool IsSeaAdjacent(Hex hex)
    {
        if (hex == null) return false;
        return hex.GetHexesInRadius(1)
            .Any(h => h != null && h != hex && (h.terrainType == TerrainEnum.shore || h.terrainType == TerrainEnum.shallowWater || h.IsWaterTerrain()));
    }

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static List<Character> GetTargets(Character character)
    {
        if (character == null || character.hex == null || character.hex.characters == null) return new List<Character>();
        return character.hex.characters
            .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch) && ch.GetArmy() != null)
            .Distinct()
            .ToList();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || !IsSeaAdjacent(character.hex)) return false;

            Character target = GetTargets(character)
                .OrderByDescending(ch => ch.GetCommander() + ch.GetEmmissary())
                .ThenByDescending(ch => ch.GetArmy() != null ? ch.GetArmy().GetSize() : 0)
                .FirstOrDefault();
            if (target == null || target.GetArmy() == null) return false;

            target.GetArmy().Recruit(TroopsTypeEnum.ws, 1);
            target.GetOwner()?.AddTimber(1, false);
            target.hex?.RedrawCharacters();
            target.hex?.RedrawArmies();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Dol Amroth Shipyard launches a warship for {target.characterName} and adds 1 timber to the stores.", Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && IsSeaAdjacent(character.hex) && GetTargets(character).Count > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class GondorMusteringAction : EventAction
{
    private const int Radius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static List<Character> GetTargets(Character character)
    {
        if (character == null || character.hex == null) return new List<Character>();
        return character.hex.GetHexesInRadius(Radius)
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch) && ch.GetArmy() != null)
            .Distinct()
            .ToList();
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

            Character target = GetTargets(character)
                .OrderByDescending(ch => ch.GetCommander() + ch.GetEmmissary())
                .ThenByDescending(ch => ch.GetArmy() != null ? ch.GetArmy().GetSize() : 0)
                .FirstOrDefault();
            if (target == null || target.GetArmy() == null) return false;

            target.GetArmy().Recruit(TroopsTypeEnum.li, 1);
            target.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            target.hex?.RedrawCharacters();
            target.hex?.RedrawArmies();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Gondor Mustering adds 1 Light Infantry and Courage to {target.characterName}.", Color.green);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return GetTargets(character).Count > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

public class PelargirShipCaptainAction : EventAction
{
    private static bool IsSeaAdjacent(Hex hex)
    {
        if (hex == null) return false;
        return hex.GetHexesInRadius(1)
            .Any(h => h != null && h != hex && (h.terrainType == TerrainEnum.shore || h.terrainType == TerrainEnum.shallowWater || h.IsWaterTerrain()));
    }

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static List<Character> GetTargets(Character character)
    {
        if (character == null || character.hex == null || character.hex.characters == null) return new List<Character>();
        return character.hex.characters
            .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch) && ch.GetArmy() != null)
            .Distinct()
            .ToList();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || !IsSeaAdjacent(character.hex)) return false;

            Character target = GetTargets(character)
                .OrderByDescending(ch => (ch.GetCommander() + ch.GetEmmissary(), ch.GetArmy() != null ? ch.GetArmy().GetSize() : 0))
                .FirstOrDefault();
            if (target == null || target.GetArmy() == null) return false;

            target.GetArmy().Recruit(TroopsTypeEnum.ws, 1);
            target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            target.hex?.RedrawCharacters();
            target.hex?.RedrawArmies();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Pelargir Ship Captain adds 1 Warship and Haste to {target.characterName}.", Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && IsSeaAdjacent(character.hex) && GetTargets(character).Count > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
