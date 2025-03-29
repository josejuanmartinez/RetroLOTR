using System;
using UnityEngine;

public class Teleport: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Hex randomHex = FindFirstObjectByType<Board>().GetHexes().Find(x => !c.GetOwner().visibleHexes.Contains(x));
            if (randomHex == null) return false;
            randomHex.RevealArea(c.GetMage());
            FindFirstObjectByType<Board>().MoveCharacter(c, c.hex, randomHex, true);
            MessageDisplay.ShowMessage($"{c.characterName} warped to an unkown place", Color.green);
            FindFirstObjectByType<Board>().SelectCharacter(c);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.artifacts.Find(x => x.providesSpell == "Teleport") != null && !c.IsArmyCommander() && (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
