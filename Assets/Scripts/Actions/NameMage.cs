using System;
using UnityEngine;

public class NameMage : MageAction
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
            if (!owner.HasCharacterSlot()) return false;
            if (!HasOwnedPc(character, owner)) return false;

            string newName = owner.GetNextNewCharacterName();
            if (string.IsNullOrWhiteSpace(newName)) return false;

            if (!owner.TryConsumeCharacterSlot()) return false;

            CharacterInstantiator instantiator = GameObject.FindFirstObjectByType<CharacterInstantiator>();
            if (instantiator == null) return false;

            BiomeConfig config = new()
            {
                characterName = newName,
                alignment = owner.GetAlignment(),
                race = owner.GetBiome().race,
                commander = 0,
                agent = 0,
                emmissary = 0,
                mage = 1
            };

            Character newCharacter = instantiator.InstantiateCharacter(owner, character.hex, config);
            if (newCharacter == null) return false;
            newCharacter.startingCharacter = false;
            newCharacter.hasActionedThisTurn = true;

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"{newName} joins as a mage.", Color.green);
            CharacterIcons.RefreshForHumanPlayerOf(owner);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Leader owner = character.GetOwner();
            if (owner == null) return false;
            if (!owner.HasCharacterSlot()) return false;
            if (!HasOwnedPc(character, owner)) return false;
            return owner.GetNextNewCharacterName() != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }

    private static bool HasOwnedPc(Character character, Leader owner)
    {
        if (character == null || owner == null) return false;
        PC pc = character.hex != null ? character.hex.GetPCData() : null;
        return pc != null && pc.owner == owner;
    }
}
