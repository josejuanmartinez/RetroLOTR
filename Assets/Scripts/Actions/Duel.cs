using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

    private void HandleRefusal(Character challenger, Character defender)
    {
        if (challenger == null || defender == null) return;
        bool playerInvolved = challenger.isPlayerControlled || defender.isPlayerControlled;
        string message = $"{defender.characterName} refuses the duel with {challenger.characterName}.";
        if (defender.IsArmyCommander())
        {
            defender.AddCommander(-1);
            message += $" {defender.characterName} loses commander XP.";
        }

        if (playerInvolved || PlayerCanSeeHex(challenger.hex))
        {
            PopupManager.Show(
                "Duel Refused",
                FindFirstObjectByType<Illustrations>().GetIllustrationByName(challenger.characterName),
                FindFirstObjectByType<Illustrations>().GetIllustrationByName(defender.characterName),
                message,
                false);
        }
        else
        {
            MessageDisplayNoUI.ShowMessage(challenger.hex, challenger, message, Color.yellow);
        }
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
        int loserHealthBefore = loser.health;

        loser.Wounded(winner.GetOwner(), wound);

        bool playerInvolved = attacker.isPlayerControlled || defender.isPlayerControlled;
        bool shouldShowPopup = playerInvolved || PlayerCanSeeHex(attacker.hex);
        string narration = BuildDuelNarration(attacker, defender, winner, loser, wound, attackerScore, defenderScore, defenseBonus, loserHealthBefore, defenderAutoWins);

        if (shouldShowPopup)
        {
            PopupManager.Show(
                $"Duel: {attacker.characterName} vs {defender.characterName}",
                FindFirstObjectByType<Illustrations>().GetIllustrationByName(attacker.characterName),
                FindFirstObjectByType<Illustrations>().GetIllustrationByName(defender.characterName),
                narration,
                true);
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
        int loserHealthBefore = loser.health;

        loser.Wounded(winner.GetOwner(), wound);

        bool playerInvolved = attacker.isPlayerControlled || defender.isPlayerControlled;
        bool shouldShowPopup = playerInvolved || PlayerCanSeeHex(attacker.hex);
        string narration = BuildDuelNarration(attacker, defender, winner, loser, wound, attackerScore, defenderScore, defenseBonus, loserHealthBefore, true);

        if (shouldShowPopup)
        {
            PopupManager.Show(
                $"Duel: {attacker.characterName} vs {defender.characterName}",
                FindFirstObjectByType<Illustrations>().GetIllustrationByName(attacker.characterName),
                FindFirstObjectByType<Illustrations>().GetIllustrationByName(defender.characterName),
                narration,
                true);
        }
        else
        {
            MessageDisplayNoUI.ShowMessage(attacker.hex, attacker, $"{winner.characterName} wins the duel.", Color.yellow);
        }
    }

    private string BuildDuelNarration(Character attacker, Character defender, Character winner, Character loser, int wound, float attackerScore, float defenderScore, int defenseBonus, int loserHealthBefore, bool defenderAutoWins)
    {
        StringBuilder sb = new();
        int template = UnityEngine.Random.Range(0, 4);
        string attackerName = attacker.characterName;
        string defenderName = defender.characterName;
        string winnerName = winner.characterName;
        string loserName = loser.characterName;
        bool fatal = wound >= loserHealthBefore;
        int winnerArtifactAttack = GetArtifactAttack(winner, loser);
        int loserArtifactDefense = GetArtifactDefense(loser, winner);

        switch (template)
        {
            case 0:
                sb.AppendLine($"{attackerName} challenges {defenderName}, steel ringing in a tight circle.");
                sb.AppendLine($"{winnerName} finds the opening and drives the exchange.");
                break;
            case 1:
                sb.AppendLine($"{attackerName} and {defenderName} trade quick feints and hard cuts.");
                sb.AppendLine($"{winnerName} forces a stumble and presses the advantage.");
                break;
            case 2:
                sb.AppendLine($"{attackerName} steps in without hesitation, the duel drawing a hush.");
                sb.AppendLine($"{winnerName} lands the telling strike as the tempo rises.");
                break;
            default:
                sb.AppendLine($"{attackerName} squares off against {defenderName}, blades flashing.");
                sb.AppendLine($"{winnerName} takes control and turns the fight.");
                break;
        }

        sb.AppendLine($"Strength: {attackerName} {attackerScore:0.0} vs {defenderName} {defenderScore:0.0}.");
        if (defenderAutoWins)
        {
            sb.AppendLine($"{defenderName}'s riddle turns the challenge back and decides the duel instantly.");
        }
        if (winnerArtifactAttack > 0)
        {
            sb.AppendLine($"{winnerName}'s relics add their bite to the strike.");
        }
        if (loserArtifactDefense > 0)
        {
            sb.AppendLine($"{loserName}'s wards blunt the blow.");
        }
        if (defenseBonus > 0 && wound == 0)
        {
            sb.AppendLine($"{loserName} escapes the worst of the wound.");
        }
        else
        {
            sb.AppendLine($"{loserName} suffers {wound} wounds.");
        }
        if (fatal)
        {
            sb.AppendLine($"{loserName} falls, the duel ending in death.");
        }
        else
        {
            sb.AppendLine($"{winnerName} stands over the field, the duel decided.");
        }

        return sb.ToString();
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

    private int GetArtifactAttack(Character character, Character opponent)
    {
        if (character == null || character.artifacts == null) return 0;
        int atk = character.artifacts.Sum(a => Mathf.Max(0, a.bonusAttack));
        if (opponent != null)
        {
            atk += character.artifacts.Sum(a => a != null ? a.GetAttackBonusVsRace(opponent.race) : 0);
        }
        return atk;
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
