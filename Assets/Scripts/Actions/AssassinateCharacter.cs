using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AssassinateCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalAsyncEffect = asyncEffect;
        var originalCondition = condition;
        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyCharacterTargetAtHex(c) != null;
        };
        effect = (c) => true;
        async System.Threading.Tasks.Task<bool> assassinateAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            List<Character> characters = c.hex.GetEnemyCharacters(c.GetOwner());
            if (characters.Count < 1) return false;
            bool isAI = !c.isPlayerControlled;
            Character enemy = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask(
                    "Select enemy character",
                    "Ok",
                    "Cancel",
                    characters.Select(x => x.characterName).ToList(),
                    null,
                    isAI,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null,
                    EventIconType.MultiChoice,
                    actionName,
                    SelectionDialog.CharacterIconNames(characters));
                enemy = c.hex.characters.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                enemy = FindEnemyCharacterTargetAtHex(c);
            }

            if (enemy == null) return false;

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
            MessageDisplay.ShowMessage(message, color);

            Hex victimHex = enemy.hex;
            List<StatusEffectEnum> victimStatusEffects = enemy.statusEffects;
            enemy.Killed(c.GetOwner());
            MessageDisplayNoUI.ShowMessage(victimHex, c, $"{enemy.characterName} assassinated!", Color.green);

            if (c.isPlayerControlled || enemy.isPlayerControlled || PlayerCanSeeHex(victimHex))
            {
                CombatBanner.Show(
                    "Assassination", "assassinates",
                    c, enemy,
                    false, false, true, true,
                    victimHex.GetBattleLocationLabel(),
                    attackerExistingStatusEffects: c.statusEffects,
                    defenderExistingStatusEffects: victimStatusEffects);
            }

            return true;
        }

        base.Initialize(c, condition, effect, assassinateAsync);
    }

    private bool PlayerCanSeeHex(Hex hex)
    {
        if (hex == null) return false;
        Game g = Game.Instance;
        if (g == null || g.player == null) return false;
        return g.player.visibleHexes.Contains(hex) && hex.IsHexSeen();
    }
}
