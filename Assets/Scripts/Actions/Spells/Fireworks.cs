using System;
using System.Linq;

public class Fireworks: FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null || pc.owner == null) return false;            
            int loyalty = UnityEngine.Random.Range(0, 10) * c.GetMage();
            loyalty = Math.Max(0, ApplySpellEffectMultiplier(c, loyalty));
            if (pc.owner.GetAlignment() == c.GetAlignment())
            {
                c.hex.GetPC().IncreaseLoyalty(loyalty, c);
            } else
            {
                c.hex.GetPC().DecreaseLoyalty(loyalty, c);
            }            
            
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            PC pc = c.hex.GetPC();
            return pc != null && pc.owner != null;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

