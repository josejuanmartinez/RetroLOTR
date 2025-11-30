using System;
using UnityEngine;

public class Teleport: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Hex randomHex = FindFirstObjectByType<Board>().GetHexes().Find(x => !c.GetOwner().visibleHexes.Contains(x));
            if (randomHex == null) return false;
            randomHex.RevealArea(c.GetMage());
            FindFirstObjectByType<Board>().MoveCharacterOneHex(c, c.hex, randomHex, true);
            randomHex.LookAt();
            MessageDisplay.ShowMessage($"{c.characterName} warped to an unkown place", Color.green);
            if (FindFirstObjectByType<Game>().currentlyPlaying == FindFirstObjectByType<Game>().player) FindFirstObjectByType<Board>().SelectCharacter(c);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.artifacts.Find(x => x.providesSpell == actionName) != null && !c.IsArmyCommander();
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

