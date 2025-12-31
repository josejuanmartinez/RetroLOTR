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
            if (pc == null || !pc.isCapital) return false;

            if (pc.owner == null)
            {
                return pc.ClaimUnowned(character.GetOwner());
            }

            if (pc.owner is not NonPlayableLeader nonPlayableLeader) return false;

            Leader leader = character.GetOwner();
            if (nonPlayableLeader.joined || nonPlayableLeader.killed) return false;
            if (leader == null) return false;

            if (nonPlayableLeader.MeetsJoiningRequirements(leader))
            {
                return nonPlayableLeader.AttemptJoin(leader);
            }

            if (character.isPlayerControlled)
            {
                nonPlayableLeader.RevealToPlayer();
                return true;
            }

            if (nonPlayableLeader.IsAlignmentCompatibleWith(leader))
            {
                float chance = nonPlayableLeader.GetPartialJoinChance(leader, 0.02f, 0.15f);
                if (UnityEngine.Random.Range(0f, 1f) <= chance)
                {
                    return nonPlayableLeader.Joined(leader);
                }
            }

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || !pc.isCapital) return false;
            if (pc.owner == null) return true;
            if (pc.owner is not NonPlayableLeader nonPlayableLeader) return false;
            if (nonPlayableLeader.joined || nonPlayableLeader.killed) return false;

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
