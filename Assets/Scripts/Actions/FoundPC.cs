using System;

public class FoundPC : EmmissaryAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            string nextPcName = c.GetOwner().biome.pcNames[UnityEngine.Random.Range(0, c.GetOwner().biome.pcNames.Count)];
            c.GetOwner().biome.pcNames.Remove(nextPcName);
            PC pc = new (c.GetOwner(), nextPcName, PCSizeEnum.camp, FortSizeEnum.NONE, false, false, c.hex);
            c.hex.pc = pc;

            c.hex.RedrawPC();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.GetOwner().controlledPcs.Count < FindFirstObjectByType<Game>().maxPcsPerPlayer &&
            c.hex.pc == null && 
            c.hex.terrainType != TerrainEnum.shallowWater && 
            c.hex.terrainType != TerrainEnum.deepWater && 
            (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
