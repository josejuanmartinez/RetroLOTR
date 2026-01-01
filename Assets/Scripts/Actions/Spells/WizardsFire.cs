using System;
using System.Collections.Generic;
using System.Linq;

public class WizardsFire : FreeSpell
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
        async System.Threading.Tasks.Task<bool> wizardsFireAsync(Character c)
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
                Army priority = FindEnemyArmyNotNeutralAtHex(c);
                selectedCommander = priority != null ? priority.commander : enemyCommanders.First();
            }

            Army army = selectedCommander != null ? selectedCommander.GetArmy() : null;
            if (army == null) return false;

            float casualties = Math.Clamp(UnityEngine.Random.Range(0.05f, 0.25f) * c.GetMage(), 0.1f, 1f);
            casualties = Math.Clamp(ApplySpellEffectMultiplier(c, casualties), 0.1f, 1f);
            army.ReceiveCasualties(casualties, c.GetOwner());
            if (c.hex != null) c.hex.PlayFireParticles();
            return true;
        }
        base.Initialize(c, condition, effect, wizardsFireAsync);
    }
}
