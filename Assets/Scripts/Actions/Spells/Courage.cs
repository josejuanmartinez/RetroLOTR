using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Courage : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => true;
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindFriendlyArmyAtHex(c) != null;
        };
        async System.Threading.Tasks.Task<bool> courageAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> characters = c.hex.GetFriendlyArmies(c.GetOwner());
            if (characters.Count < 1) return false;

            bool isAI = !c.isPlayerControlled;
            Character commander = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask("Select friendly army", "Ok", "Cancel", characters.Select(x => x.characterName).ToList(), isAI);
                if (string.IsNullOrEmpty(targetCharacter)) return false;
                commander = characters.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                commander = characters[UnityEngine.Random.Range(0, characters.Count)];
            }

            Army army = commander != null ? commander.GetArmy() : null;
            if (army == null) return false;

            int turns = 1 + c.GetMage() * Mathf.FloorToInt(UnityEngine.Random.Range(0.0f, 0.5f));
            turns = Math.Max(1, ApplySpellEffectMultiplier(c, turns));
            army.commander.Encourage(turns);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Courage for {turns} turns!", Color.green);
            return true;
        }
        base.Initialize(c, condition, effect, courageAsync);
    }
}
