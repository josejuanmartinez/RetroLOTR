using System;
using System.Linq;
using UnityEngine;

public class SiegePC : CommanderEnemyPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        if (c != null && c.GetArmy() != null && c.hex != null)
        {
            Army army = c.GetArmy();
            PC pc = c.hex.GetPC();
            int catapults = Math.Max(0, army.ca);
            int fortLevel = pc != null ? (int)pc.fortSize : 0;
            int commanderSkill = c.GetCommander();
            var enemyArmies = c.hex.armies?
                .Where(a => a != null && !a.killed && a.commander != null && a.commander.GetOwner() != c.GetOwner())
                .ToList();
            int enemyCa = enemyArmies?.Sum(a => a.ca) ?? 0;
            int enemyTotal = enemyArmies?.Sum(a => a.GetSize()) ?? 0;
            int enemyPenalty = enemyCa * 10 + (enemyTotal - enemyCa) * 5;

            int successChance = catapults * 10 - fortLevel * 5 + commanderSkill * 5 - enemyPenalty;
            difficulty = Math.Clamp(100 - successChance, 0, 100);
        }

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null || pc.citySize <= PCSizeEnum.camp) return false;

            pc.DecreaseSize();
            MessageDisplayNoUI.ShowMessage(pc.hex, c, $"{pc.pcName} suffers siege damage!", Color.red);
            return true;
        };
        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null || pc.citySize <= PCSizeEnum.camp) return false;
            Army army = c.GetArmy();
            return army != null && army.ca >= 1;
        };
        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
