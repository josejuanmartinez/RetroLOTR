using System;

public class PerceiveAllegiance : Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null) return false;
            if (pc.owner is not NonPlayableLeader) return false;            
            NonPlayableLeader leader = pc.owner as NonPlayableLeader;
            PopupManager.Show(
                "Revealed conditions", 
                FindFirstObjectByType<Illustrations>().GetIllustrationByName(leader.characterName),
                FindFirstObjectByType<Illustrations>().GetIllustrationByName(c.GetOwner().characterName),
                leader.GetJoiningConditionsText(c.owner.GetAlignment()),
                true);
            return true; 
        };
        condition = (c) => { 
            if (originalCondition != null && !originalCondition(c)) return false;
            PC pc = c.hex.GetPC();
            return pc != null && pc.IsRevealed() && pc.owner is NonPlayableLeader;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

