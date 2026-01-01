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
            return FindEnemyCharactersAtHex(character).Count > 0;
        };

        async Task<bool> duelAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(character);
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

            if (target.IsRefusingDuels())
            {
                HandleRefusal(character, target);
                return true;
            }

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

        float attackerScore = GetDuelScore(attacker);
        float defenderScore = GetDuelScore(defender);
        bool attackerWins = attackerScore > defenderScore;
        if (Mathf.Approximately(attackerScore, defenderScore))
        {
            attackerWins = UnityEngine.Random.Range(0, 2) == 0;
        }

        Character winner = attackerWins ? attacker : defender;
        Character loser = attackerWins ? defender : attacker;

        float diff = Mathf.Abs(attackerScore - defenderScore);
        int baseWound = Mathf.Clamp(Mathf.RoundToInt(diff * 10f), 0, 100);
        if (baseWound == 0) baseWound = UnityEngine.Random.Range(5, 16);

        int defenseBonus = GetArtifactDefense(loser);
        int wound = Mathf.Max(0, baseWound - defenseBonus * 5);
        int loserHealthBefore = loser.health;

        loser.Wounded(winner.GetOwner(), wound);

        bool playerInvolved = attacker.isPlayerControlled || defender.isPlayerControlled;
        bool shouldShowPopup = playerInvolved || PlayerCanSeeHex(attacker.hex);
        string narration = BuildDuelNarration(attacker, defender, winner, loser, wound, attackerScore, defenderScore, defenseBonus, loserHealthBefore);

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

    private string BuildDuelNarration(Character attacker, Character defender, Character winner, Character loser, int wound, float attackerScore, float defenderScore, int defenseBonus, int loserHealthBefore)
    {
        StringBuilder sb = new();
        int template = UnityEngine.Random.Range(0, 4);
        string attackerName = attacker.characterName;
        string defenderName = defender.characterName;
        string winnerName = winner.characterName;
        string loserName = loser.characterName;
        bool fatal = wound >= loserHealthBefore;
        int winnerArtifactAttack = GetArtifactAttack(winner);
        int loserArtifactDefense = GetArtifactDefense(loser);

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
        if (character == null) return 0f;
        float baseScore = character.GetBaseCommander() * 1f
                          + character.GetBaseMage() * 1f
                          + character.GetBaseAgent() * 0.5f
                          + character.GetBaseEmmissary() * 0.25f;
        return baseScore + GetArtifactCombatScore(character);
    }

    private int GetArtifactCombatScore(Character character)
    {
        if (character == null || character.artifacts == null) return 0;
        return character.artifacts.Sum(a => Mathf.Max(0, a.bonusAttack) + Mathf.Max(0, a.bonusDefense));
    }

    private int GetArtifactAttack(Character character)
    {
        if (character == null || character.artifacts == null) return 0;
        return character.artifacts.Sum(a => Mathf.Max(0, a.bonusAttack));
    }

    private int GetArtifactDefense(Character character)
    {
        if (character == null || character.artifacts == null) return 0;
        return character.artifacts.Sum(a => Mathf.Max(0, a.bonusDefense));
    }

    private bool PlayerCanSeeHex(Hex hex)
    {
        if (hex == null) return false;
        Game g = FindFirstObjectByType<Game>();
        if (g == null || g.player == null) return false;
        return g.player.visibleHexes.Contains(hex) && hex.IsHexSeen();
    }
}
