using System;
using System.Linq;
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

            var board = FindFirstObjectByType<Board>();
            var unseenHexes = board.GetHexes().Where(x => c.GetOwner().visibleHexes.Contains(x)).ToList();
            if (unseenHexes.Count == 0) return false;

            Hex randomHex = unseenHexes[UnityEngine.Random.Range(0, unseenHexes.Count)];
            int radius = Math.Max(0, ApplySpellEffectMultiplier(c, c.GetMage()));
            randomHex.RevealArea(radius);
            board.MoveCharacterOneHex(c, c.hex, randomHex, true);
            randomHex.LookAt();
            MessageDisplay.ShowMessage($"{c.characterName} warped to an unknown place", Color.green);
            if (FindFirstObjectByType<Game>().currentlyPlaying == FindFirstObjectByType<Game>().player) board.SelectCharacter(c);
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
