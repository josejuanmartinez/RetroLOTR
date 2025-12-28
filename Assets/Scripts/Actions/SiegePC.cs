using System;
using UnityEngine;

public class SiegePC : CommanderEnemyPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        int baseDifficulty = difficulty;
        if (c != null && c.GetArmy() != null)
        {
            int catapults = Math.Max(0, c.GetArmy().ca);
            int reduction = Math.Min(40, catapults * 10);
            difficulty = Math.Max(5, baseDifficulty - reduction);
        }

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null) return false;
            if (pc.citySize <= PCSizeEnum.camp) return false;

            pc.DecreaseSize();
            MessageDisplayNoUI.ShowMessage(pc.hex, c, $"{pc.pcName} suffers siege damage!", Color.red);
            return true;
        };
        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            PC pc = c.hex.GetPC();
            return pc != null && pc.citySize > PCSizeEnum.camp;
        };
        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
