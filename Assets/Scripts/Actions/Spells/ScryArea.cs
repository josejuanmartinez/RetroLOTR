using System;
using System.Linq;
using UnityEngine;

public class ScryArea : Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            // Get all hexes that meet your criteria
            var eligibleHexes = FindFirstObjectByType<Board>().GetHexes().Where(x => !c.GetOwner().visibleHexes.Contains(x)).ToList();

            // Check if there are any eligible hexes
            if (eligibleHexes.Count > 0)
            {
                // Select a random hex from the list
                int randomIndex = UnityEngine.Random.Range(0, eligibleHexes.Count);
                Hex randomHex = eligibleHexes[randomIndex];
                randomHex.RevealArea(c.GetMage());
                randomHex.LookAt();
                MessageDisplayNoUI.ShowMessage(randomHex, c, $"Area scried!", Color.green);
                return true;
            } else return false;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.GetOwner() == FindFirstObjectByType<Game>().player && c.artifacts.Find(x => x.providesSpell == actionName) != null; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

