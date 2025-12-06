using System;
using System.Linq;
using UnityEngine;

public class ReturnToCapital: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Hex capitalHex = FindFirstObjectByType<Board>().GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == c.GetOwner() && x.GetPC().isCapital);
            if (capitalHex == null) return false;
            FindFirstObjectByType<Board>().MoveCharacterOneHex(c, c.hex, capitalHex, true);
            MessageDisplay.ShowMessage($"{c.characterName} returned to capital", Color.green);
            FindFirstObjectByType<Board>().SelectCharacter(c);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return !c.IsArmyCommander();
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

