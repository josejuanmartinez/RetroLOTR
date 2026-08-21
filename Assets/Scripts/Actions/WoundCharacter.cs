using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WoundCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => true;
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyCharacterTargetAtHex(c) != null;
        };
        async System.Threading.Tasks.Task<bool> woundCharacterAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(c);
            if (enemies.Count < 1) return false;

            bool isAI = !c.isPlayerControlled;
            Character enemy = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.AskImmediate(
                    "Select enemy character",
                    "Ok",
                    "Cancel",
                    enemies.Select(x => x.characterName).ToList(),
                    null,
                    isAI,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null,
                    EventIconType.MultiChoice,
                    actionName,
                    SelectionDialog.CharacterIconNames(enemies));
                if (string.IsNullOrEmpty(targetCharacter)) return false;
                enemy = enemies.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                enemy = FindEnemyCharacterTargetAtHex(c);
            }

            if (enemy == null) return false;

            int wound = UnityEngine.Random.Range(0, 20) * c.GetAgent();
            Hex capitalHex = Board.Instance.GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == c.GetOwner() && x.GetPC().isCapital);
            if (capitalHex == null) return false;
            int random = UnityEngine.Random.Range(0, 5);
            string message = $"Agent returned to capital";
            Color color = Color.green;
            if (random > c.GetAgent())
            {
                message += " wounded";
                c.Wounded(c.hex.GetPC().owner, random * 10);
                color = Color.red;
            }
            Board.Instance.MoveCharacterOneHex(c, c.hex, capitalHex, true);
            MessageDisplayNoUI.ShowMessage(c.hex, c, message, color);

            Hex victimHex = enemy.hex;
            List<StatusEffectEnum> victimStatusEffects = enemy.statusEffects;
            enemy.Wounded(c.GetOwner(), wound);

            if (c.isPlayerControlled || enemy.isPlayerControlled || PlayerCanSeeHex(victimHex))
            {
                CombatBanner.Show(
                    "Wound", "wounds",
                    c, enemy,
                    false, false, wound > 0, enemy.killed,
                    victimHex.GetBattleLocationLabel(),
                    attackerExistingStatusEffects: c.statusEffects,
                    defenderExistingStatusEffects: victimStatusEffects);
            }

            return true;
        }
        base.Initialize(c, condition, effect, woundCharacterAsync);
    }

    private bool PlayerCanSeeHex(Hex hex)
    {
        if (hex == null) return false;
        Game g = Game.Instance;
        if (g == null || g.player == null) return false;
        return g.player.visibleHexes.Contains(hex) && hex.IsHexSeen();
    }
}
