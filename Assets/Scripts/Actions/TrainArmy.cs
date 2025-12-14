using System;
using UnityEngine;

public class TrainArmy : CommanderAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.Militaristic;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            Army army = character.GetArmy();
            if (army == null) return false;

            int commanderLevel = character.GetCommander();
            int gain = Mathf.Clamp(UnityEngine.Random.Range(1, 7) + Mathf.Max(0, commanderLevel - 1), 1, 10);
            army.AddXp(gain, "Training");
            return true;
        };

        condition = (character) =>
        {
            if (character == null || character.GetArmy() == null) return false;
            PC pc = character.hex != null ? character.hex.GetPC() : null;
            if (pc != null && pc.owner != null && pc.owner.GetAlignment() != character.GetAlignment()) return false;
            if (originalCondition != null && !originalCondition(character)) return false;
            return true;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
