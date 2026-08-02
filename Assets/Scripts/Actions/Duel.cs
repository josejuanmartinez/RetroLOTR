using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class Duel : CharacterAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.IsRefusingDuels()) return false;
            return FindEnemyCharactersAtHex(character).Any(x => x != null && !x.IsHidden() && !x.IsRefusingDuels() && !x.IsArmyCommander());
        };

        async Task<bool> duelAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(character)
                .Where(x => x != null && !x.IsHidden() && !x.IsRefusingDuels() && !x.IsArmyCommander())
                .ToList();
            if (enemies.Count < 1) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;
            if (!isAI)
            {
                string targetName = await SelectionDialog.Ask("Select enemy character", "Ok", "Cancel", enemies.Select(x => x.characterName).ToList(), isAI, SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null);
                if (string.IsNullOrWhiteSpace(targetName)) return false;
                target = enemies.Find(x => x.characterName == targetName);
            }
            else
            {
                target = PickBestTarget(enemies);
            }

            if (target == null) return false;
            ResolveDuel(character, target);
            return true;
        }

        base.Initialize(c, condition, effect, duelAsync);
    }

    private Character PickBestTarget(List<Character> enemies)
    {
        if (enemies == null || enemies.Count == 0) return null;
        return enemies
            .OrderByDescending(GetDuelScore)
            .FirstOrDefault();
    }

    private void ResolveDuel(Character attacker, Character defender)
    {
        if (attacker == null || defender == null) return;

        if (defender.HasStatusEffect(StatusEffectEnum.DuelSupremacy))
        {
            defender.ClearStatusEffect(StatusEffectEnum.DuelSupremacy);
            ResolveGuaranteedDefenderWin(attacker, defender);
            return;
        }

        float attackerScore = GetDuelScore(attacker, defender);
        float defenderScore = GetDuelScore(defender, attacker);
        bool defenderAutoWins = defender.HasDuelSupremacy();
        bool attackerWins = !defenderAutoWins && attackerScore > defenderScore;
        if (!defenderAutoWins && Mathf.Approximately(attackerScore, defenderScore))
        {
            attackerWins = UnityEngine.Random.Range(0, 2) == 0;
        }

        Character winner = attackerWins ? attacker : defender;
        Character loser = attackerWins ? defender : attacker;

        float diff = Mathf.Abs(attackerScore - defenderScore);
        int baseWound = Mathf.Clamp(Mathf.RoundToInt(diff * 10f), 0, 100);
        if (baseWound == 0) baseWound = UnityEngine.Random.Range(5, 16);

        int defenseBonus = GetArtifactDefense(loser, winner);
        int wound = Mathf.Max(0, baseWound - defenseBonus * 5);

        loser.Wounded(winner.GetOwner(), wound);

        bool playerInvolved = attacker.isPlayerControlled || defender.isPlayerControlled;
        bool shouldShowPopup = playerInvolved || PlayerCanSeeHex(attacker.hex);

        if (shouldShowPopup)
        {
            ShowCombatBanner(attacker, defender, loser, wound);
        }
        else
        {
            MessageDisplayNoUI.ShowMessage(attacker.hex, attacker, $"{winner.characterName} wins the duel.", Color.yellow);
        }
    }

    private void ResolveGuaranteedDefenderWin(Character attacker, Character defender)
    {
        float attackerScore = GetDuelScore(attacker, defender);
        float defenderScore = GetDuelScore(defender, attacker);
        Character winner = defender;
        Character loser = attacker;

        float diff = Mathf.Abs(attackerScore - defenderScore);
        int baseWound = Mathf.Clamp(Mathf.RoundToInt(diff * 10f), 0, 100);
        if (baseWound == 0) baseWound = UnityEngine.Random.Range(5, 16);

        int defenseBonus = GetArtifactDefense(loser, winner);
        int wound = Mathf.Max(0, baseWound - defenseBonus * 5);

        loser.Wounded(winner.GetOwner(), wound);

        bool playerInvolved = attacker.isPlayerControlled || defender.isPlayerControlled;
        bool shouldShowPopup = playerInvolved || PlayerCanSeeHex(attacker.hex);

        if (shouldShowPopup)
        {
            ShowCombatBanner(attacker, defender, loser, wound);
        }
        else
        {
            MessageDisplayNoUI.ShowMessage(attacker.hex, attacker, $"{winner.characterName} wins the duel.", Color.yellow);
        }
    }

    // Only the loser ever takes damage in this duel model, so wounded/killed for each side
    // reduces to "is this side the loser, and did the wound/Wounded() call actually land."
    private void ShowCombatBanner(Character attacker, Character defender, Character loser, int wound)
    {
        bool attackerIsLoser = attacker == loser;
        bool defenderIsLoser = defender == loser;
        CombatBanner.Show(
            "Duel", "duels",
            attacker, defender,
            attackerIsLoser && wound > 0, attackerIsLoser && attacker.killed,
            defenderIsLoser && wound > 0, defenderIsLoser && defender.killed,
            attacker.hex.GetBattleLocationLabel(),
            attackerExistingStatusEffects: attacker.statusEffects,
            defenderExistingStatusEffects: defender.statusEffects);
    }

    private float GetDuelScore(Character character)
    {
        return GetDuelScore(character, null);
    }

    private float GetDuelScore(Character character, Character opponent)
    {
        if (character == null) return 0f;
        float baseScore = character.GetBaseCommander() * 1f
                          + character.GetBaseMage() * 1f
                          + character.GetBaseAgent() * 0.5f
                          + character.GetBaseEmmissary() * 0.25f;

        float score = baseScore + GetArtifactCombatScore(character, opponent);

        if (character.HasStatusEffect(StatusEffectEnum.Strengthened))
        {
            score *= 1.10f;
        }

        if (character.HasStatusEffect(StatusEffectEnum.Fortified))
        {
            score *= 1.10f;
        }

        return score;
    }

    private int GetArtifactCombatScore(Character character, Character opponent)
    {
        if (character == null || character.artifacts == null) return 0;
        int score = character.artifacts.Sum(a => Mathf.Max(0, a.bonusAttack) + Mathf.Max(0, a.bonusDefense));
        if (opponent != null)
        {
            score += character.artifacts.Sum(a => a != null ? a.GetAttackBonusVsRace(opponent.race) + a.GetDefenseBonusVsRace(opponent.race) : 0);
        }
        return score;
    }

    private int GetArtifactDefense(Character character, Character opponent)
    {
        if (character == null || character.artifacts == null) return 0;
        int def = character.artifacts.Sum(a => Mathf.Max(0, a.bonusDefense));
        if (opponent != null)
        {
            def += character.artifacts.Sum(a => a != null ? a.GetDefenseBonusVsRace(opponent.race) : 0);
        }
        return def;
    }

    private bool PlayerCanSeeHex(Hex hex)
    {
        if (hex == null) return false;
        Game g = FindFirstObjectByType<Game>();
        if (g == null || g.player == null) return false;
        return g.player.visibleHexes.Contains(hex) && hex.IsHexSeen();
    }
}
