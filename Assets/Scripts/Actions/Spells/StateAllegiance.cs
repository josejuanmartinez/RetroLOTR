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
            if (pc == null) return false;

            if (pc.owner == null)
            {
                return pc.ClaimUnowned(character.GetOwner());
            }

            if (!pc.isCapital) return false;
            if (pc.owner is not NonPlayableLeader nonPlayableLeader) return false;

            Leader leader = character.GetOwner();
            if (nonPlayableLeader.joined || nonPlayableLeader.killed) return false;
            if (leader is not PlayableLeader playableLeader) return false;
            if (!nonPlayableLeader.CanJoinWithStateAllegiance(playableLeader)) return false;

            return nonPlayableLeader.Joined(playableLeader);
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;

            PC pc = character.hex.GetPC();
            if (pc == null) return false;
            if (pc.owner == null) return true;
            if (!pc.isCapital) return false;
            if (pc.owner is not NonPlayableLeader nonPlayableLeader) return false;
            if (nonPlayableLeader.joined || nonPlayableLeader.killed) return false;
            if (character.GetOwner() is not PlayableLeader playableLeader) return false;
            return nonPlayableLeader.CanJoinWithStateAllegiance(playableLeader);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
