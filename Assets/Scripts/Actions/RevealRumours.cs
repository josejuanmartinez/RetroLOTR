using System;
using UnityEngine;

public class RevealRumours : EmmissaryAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            int enemyRumours = (int) Math.Max(1, Math.Floor(c.GetEmmissary() * UnityEngine.Random.Range(0.1f, 0.5f)));
            int friendlyRumours = (int) Math.Max(2, Math.Floor(c.GetEmmissary() * UnityEngine.Random.Range(0.25f, 0.75f)));
            int totalRumours = RumoursManager.GetRumours(c.GetAlignment(), enemyRumours, friendlyRumours);
            if (totalRumours > 0)
            {
                MessageDisplay.ShowMessage($"New rumours available: {totalRumours}", Color.green);
            }
            else
            {
                MessageDisplay.ShowMessage($"No new rumours available", Color.red);
            }
            
            return true; 
        };
        condition = (c) => {
            return originalCondition == null || originalCondition(c); 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

