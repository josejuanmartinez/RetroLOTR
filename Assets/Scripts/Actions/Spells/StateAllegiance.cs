using System;
using System.Threading.Tasks;
using UnityEngine;

public class StateAllegiance : EmmissaryAction
{
    override public void Initialize(
        Character c,
        Func<Character, bool> condition = null,
        Func<Character, bool> effect = null,
        Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || pc.owner is not NonPlayableLeader nonPlayableLeader || !pc.isCapital) return false;

            Leader leader = character.GetOwner();
            if (!nonPlayableLeader.MeetsJoiningRequirements(leader)) return false;

            return nonPlayableLeader.AttemptJoin(leader);
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || pc.owner is not NonPlayableLeader nonPlayableLeader || !pc.isCapital) return false;
            if (nonPlayableLeader.joined || nonPlayableLeader.killed) return false;

            return nonPlayableLeader.MeetsJoiningRequirements(character.GetOwner());
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
