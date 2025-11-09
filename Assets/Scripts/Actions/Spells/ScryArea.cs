using System;
using System.Linq;

public class ScryArea : Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
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
                return originalEffect == null || originalEffect(c);
            } else return false;
        };
        condition = (c) => {
            return c.GetOwner() == FindFirstObjectByType<Game>().player && c.artifacts.Find(x => x.providesSpell == actionName) != null && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
