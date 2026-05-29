using System;
using UnityEngine;

public class Clouds : EventAction
{
    public override void ApplyOngoingEffect()
    {
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null)
            env.GlobalArmyAttackFactor = 0.90f;

        MessageDisplayNoUI.ShowMessage(null, null,
            "Clouds (ongoing): overcast skies hamper archery and tactics — all armies suffer -10% attack.",
            Color.grey);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
