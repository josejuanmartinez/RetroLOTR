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

            PC pc = character?.hex != null ? character.hex.GetPCData() : null;
            if (pc == null) return false;
            Leader actorOwner = character.GetOwner();
            if (actorOwner == null) return false;

            if (pc.owner == null)
            {
                return pc.ClaimUnowned(actorOwner);
            }

            if (pc.owner == actorOwner || pc.owner.GetAlignment() == actorOwner.GetAlignment())
            {
                if (pc.isCapital && pc.owner is NonPlayableLeader nonPlayableLeader)
                {
                    if (nonPlayableLeader.joined || nonPlayableLeader.killed) return false;
                    if (actorOwner is not PlayableLeader playableLeader) return false;
                    if (!nonPlayableLeader.CanJoinWithStateAllegiance(playableLeader)) return false;

                    return nonPlayableLeader.Joined(playableLeader);
                }

                int loyalty = UnityEngine.Random.Range(1, 5);
                pc.IncreaseLoyalty(loyalty, character);
                return true;
            }

            if (pc.owner.GetAlignment() != actorOwner.GetAlignment())
            {
                int loyalty = UnityEngine.Random.Range(1, 5);
                pc.DecreaseLoyalty(loyalty, character);
                return true;
            }

            return false;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;

            return character != null
                && character.hex != null
                && character.hex.GetPCData() != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
