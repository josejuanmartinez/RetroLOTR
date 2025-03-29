using System;
using UnityEditor.Experimental;
using UnityEngine;

public class FoundPC : EmmissaryAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            string nextPcName = c.GetOwner().GetBiome().pcNames[UnityEngine.Random.Range(0, c.GetOwner().GetBiome().pcNames.Count)];
            c.GetOwner().GetBiome().pcNames.Remove(nextPcName);
            PC pc = new (c.GetOwner(), nextPcName, PCSizeEnum.camp, FortSizeEnum.NONE, false, false, c.hex);
            c.hex.SetPC(pc);
            c.hex.RedrawPC();
            MessageDisplay.ShowMessage($"Camp found: {nextPcName}", Color.green);
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.GetOwner().controlledPcs.Count < FindFirstObjectByType<Game>().maxPcsPerPlayer &&
            c.hex.GetPC() == null && 
            c.hex.terrainType != TerrainEnum.shallowWater && 
            c.hex.terrainType != TerrainEnum.deepWater && 
            (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
