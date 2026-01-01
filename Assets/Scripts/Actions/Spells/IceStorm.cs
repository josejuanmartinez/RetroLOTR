using System;
using System.Collections.Generic;
using System.Linq;

public class IceStorm : DarkNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => true;
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyArmyAtHex(c) != null;
        };
        async System.Threading.Tasks.Task<bool> iceStormAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> enemyCommanders = c.hex.GetEnemyArmies(c.GetOwner());
            if (enemyCommanders.Count < 1) return false;

            bool isAI = !c.isPlayerControlled;
            Character selectedCommander = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask("Select enemy army", "Ok", "Cancel", enemyCommanders.Select(x => x.characterName).ToList(), isAI, SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null);
                if (string.IsNullOrEmpty(targetCharacter)) return false;
                selectedCommander = enemyCommanders.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                selectedCommander = enemyCommanders[UnityEngine.Random.Range(0, enemyCommanders.Count)];
            }

            Army army = selectedCommander != null ? selectedCommander.GetArmy() : null;
            if (army == null) return false;

            army.commander.Halt();
            if (c.hex != null) c.hex.PlayIceParticles();
            return true;
        }
        base.Initialize(c, condition, effect, iceStormAsync);
    }
}
