using System;
using UnityEngine;

public class DestroyFortifications : CommanderEnemyPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null) return false;
            if (pc.fortSize <= FortSizeEnum.NONE) return false;

            pc.DecreaseFort();
            MessageDisplayNoUI.ShowMessage(pc.hex, c, $"Fortifications at {pc.pcName} were damaged!", Color.red);
            return true;
        };
        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            PC pc = c.hex.GetPC();
            return pc != null && pc.fortSize > FortSizeEnum.NONE;
        };
        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
