using System;
using UnityEngine;

public class PostCamp : CommanderAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            Leader owner = character.GetOwner();
            if (owner == null || owner.killed) return false;
            if (!owner.HasPcSlot()) return false;
            if (character.hex.GetPC() != null) return false;

            string pcName = owner.GetNextNewPcName() ?? $"Camp {owner.GetCreatedPcsCount() + 1}";
            if (!owner.TryConsumePcSlot()) return false;
            PC pc = new PC(owner, pcName, PCSizeEnum.camp, FortSizeEnum.NONE, false, false, character.hex, false, 75);

            if (pc == null) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"{pcName} was founded.", Color.green);
            character.hex.RedrawPC();
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Leader owner = character.GetOwner();
            if (owner == null) return false;
            if (!owner.HasPcSlot()) return false;
            if (character.hex.GetPC() != null) return false;
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
