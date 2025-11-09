using System;
using UnityEngine;

public class ReturnToCapital: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Hex capitalHex = FindFirstObjectByType<Board>().GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == c.GetOwner() && x.GetPC().isCapital);
            if (capitalHex == null) return false;
            FindFirstObjectByType<Board>().MoveCharacterOneHex(c, c.hex, capitalHex, true);
            MessageDisplay.ShowMessage($"{c.characterName} returned to capital", Color.green);
            FindFirstObjectByType<Board>().SelectCharacter(c);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.artifacts.Find(x => x.providesSpell == actionName) != null && !c.IsArmyCommander() && (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
