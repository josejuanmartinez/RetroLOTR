using System;

public class ConscriptArmy : CommanderPCAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.Militaristic;

    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (character) => {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (this.card == null) return false;
            if (character.GetArmy() == null)
            {
                character.CreateArmy(this.card.troopType, 1, startingArmy: false, specialAbilities: this.card.specialAbilities, showSpawnMessage: true);
            }
            else
            {
                character.GetArmy().Recruit(this.card.troopType, 1, this.card.specialAbilities);
            }
            return true;
        };
        condition = (character) => {
            return originalCondition == null || originalCondition(character);
        };
        asyncEffect = async (character) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
