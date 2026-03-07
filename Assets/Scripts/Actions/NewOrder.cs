using System;
using System.Linq;
using UnityEngine;

public class NewOrder : CharacterAction
{
    private const int TimberGain = 5;
    private const int IronGain = 5;
    private const int SteelGain = 5;
    private const int FortUpgrades = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            Hex capitalHex = board.GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == owner && x.GetPC().isCapital);
            PC capital = capitalHex?.GetPC();
            if (capital == null) return false;

            int goldSpent = Mathf.Max(0, owner.goldAmount);
            if (goldSpent > 0)
            {
                owner.RemoveGold(goldSpent);
            }

            owner.AddTimber(TimberGain);
            owner.AddIron(IronGain);
            owner.AddSteel(SteelGain);

            int fortBefore = capital.GetFortSize();
            for (int i = 0; i < FortUpgrades; i++)
            {
                capital.IncreaseFort();
            }
            int fortGained = Mathf.Max(0, capital.GetFortSize() - fortBefore);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"New Order converts {goldSpent} Gold into +{TimberGain} Timber, +{IronGain} Iron, +{SteelGain} Steel, and +{fortGained} Fortification at {capital.pcName}.",
                Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            Hex capitalHex = board.GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == owner && x.GetPC().isCapital);
            return capitalHex?.GetPC() != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
