using System;

public class Pass : CharacterAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.None;

    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            c.moved = c.GetMaxMovement();
            return true; 
        };
        condition = (c) => {
            return originalCondition == null || originalCondition(c); 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            if (c != null && c.isPlayerControlled)
            {
                Game game = FindFirstObjectByType<Game>();
                if (game != null) game.SelectNextCharacterOrFinishTurnPrompt();
            }
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

