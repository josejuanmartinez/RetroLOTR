using System;

public class InfluenceUpPC : EmmissaryPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c.hex.GetPC() == null) return false;
            PC pc = c.hex.GetPC();
            int loyalty = UnityEngine.Random.Range(1, 3) * c.GetEmmissary();
            pc.IncreaseLoyalty(loyalty, c);

            // MessageDisplayNoUI.ShowMessage(pc.hex, c, $"{pc.pcName} +{loyalty} loyalty!", Color.green);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.hex.GetPC() != null && c.hex.GetPC().loyalty < 100;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

