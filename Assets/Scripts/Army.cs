using System;
using System.Linq;
using Unity.Burst.Intrinsics;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public class Army
{
    [SerializeField] public Character commander;

    [SerializeField] public int ma = 0;
    [SerializeField] public int ar = 0;
    [SerializeField] public int li = 0;
    [SerializeField] public int hi = 0;
    [SerializeField] public int lc = 0;
    [SerializeField] public int hc = 0;
    [SerializeField] public int ca = 0;
    [SerializeField] public int ws = 0;

    public Army(Character commander, int ma=0, int ar=0, int li=0, int hi=0, int lc=0, int hc=0, int ca=0, int ws=0)
    {
        this.commander = commander;
        this.ma = ma;
        this.ar = ar;
        this.li = li;
        this.hi = hi;
        this.lc = lc;
        this.hc = hc;
        this.ca = ca;
        this.ws = ws;
    }

    public Army(Character commander, TroopsTypeEnum troopsType, int amount, int ws = 0)
    {
        this.commander = commander;

        // Use reflection to set the field based on the enum value
        string fieldName = troopsType.ToString();

        // Get field info using reflection
        var fieldInfo = GetType().GetField(fieldName);

        if (fieldInfo != null)
        {
            // Set the field value to the amount
            fieldInfo.SetValue(this, amount);
        }
        else
        {
            throw new ArgumentException($"Could not find field for troop type: {troopsType}");
        }

        if (ws > 0) this.ws += ws;
    }

    public AlignmentEnum GetAlignment()
    {
        return commander.GetAlignment();
    }

    public void Recruit(Army otherArmy)
    {
        ma += otherArmy.ma;
        ar += otherArmy.ar;
        li += otherArmy.li;
        hi += otherArmy.hi;
        lc += otherArmy.lc;
        hc += otherArmy.hc;
        ca += otherArmy.ca;
        ws += otherArmy.ws;
    }

    public int GetSize(bool withoutWs = false)
    {
        int result = ma + ar + li + hi + lc + hc + ca;
        result += withoutWs ? 0 : ws;
        return result;
    }

    public string GetHoverText()
    {
        string result = "";

        if (ma > 0) result += $"<b>MA</b>{ma} ";
        if (ar > 0) result += $"<b>AR</b>{ar} ";
        if (li > 0) result += $"<b>LI</b>{li} ";
        if (hi > 0) result += $"<b>HI</b>{hi} ";
        if (lc > 0) result += $"<b>LC</b>{lc} ";
        if (hc > 0) result += $"<b>HC</b>{hc} ";
        if (ca > 0) result += $"<b>CA</b>{ca} ";
        if (ws > 0) result += $"<b>WS</b>{ws} ";

        return result;
    }

    public bool IsCavalryOnly()
    {
        if (lc < 1 && hc < 1) return false;
        if (ma > 0) return false;
        if (ar > 0) return false;
        if (li > 0) return false;
        if (hi > 0) return false;
        if (ca > 0) return false;
        if (ws > 0) return false;
        return true;
    }

    public MovementType GetMovementType()
    {
        return IsCavalryOnly() ? MovementType.ArmyCommanderCavalryOnly : MovementType.ArmyCommander;
    }

    public void Killed()
    {
        GameObject.FindFirstObjectByType<Board>().GetHexes().FindAll(x => x.armies.Contains(this)).ToList().ForEach(x => x.armies.Remove(this));
        commander = null;
        ma = 0;
        ar = 0;
        li = 0;
        hi = 0;
        lc = 0;
        hc = 0;
        ca = 0;
        ws = 0;
    }

    public int GetStrength()
    {
        int strength = 0;
        if (commander.hex.IsWaterTerrain())
        {
            strength += (ma+ar+li+hi+lc+hc+ca) * ArmyData.transportedStrength;
            strength += ws * ArmyData.warshipStrength;            
        } else
        {
            strength += ma * ArmyData.troopsStrength[TroopsTypeEnum.ma];
            strength += ar * ArmyData.troopsStrength[TroopsTypeEnum.ar];
            strength += li * ArmyData.troopsStrength[TroopsTypeEnum.li];
            strength += hi * ArmyData.troopsStrength[TroopsTypeEnum.hi];
            strength += lc * ArmyData.troopsStrength[TroopsTypeEnum.lc];
            strength += hc * ArmyData.troopsStrength[TroopsTypeEnum.hc];
            strength += (commander.hex.pc != null && commander.hex.pc.owner.GetAlignment() != GetAlignment()) ? ca * ArmyData.troopsStrength[TroopsTypeEnum.ca] : ca * ArmyData.troopsStrength[TroopsTypeEnum.ca] * ArmyData.catapultStrengthMultiplierInPC;
        }
        if(commander.GetOwner().biome.terrain == commander.hex.terrainType) strength *= ArmyData.biomeTerrainMultiplier;

        return strength;
    }

    public int GetDefence()
    {
        int defence = 0;
        if (commander.hex.IsWaterTerrain())
        {
            defence += (ma + ar + li + hi + lc + hc + ca) * ArmyData.transportedStrength;
            defence += ws * ArmyData.warshipStrength;
        }
        else
        {
            defence += ma * ArmyData.troopsDefence[TroopsTypeEnum.ma];
            defence += ar * ArmyData.troopsDefence[TroopsTypeEnum.ar];
            defence += li * ArmyData.troopsDefence[TroopsTypeEnum.li];
            defence += hi * ArmyData.troopsDefence[TroopsTypeEnum.hi];
            defence += lc * ArmyData.troopsDefence[TroopsTypeEnum.lc];
            defence += hc * ArmyData.troopsDefence[TroopsTypeEnum.hc];
            // CA usual defence even if it's PC
            defence += ca * ArmyData.troopsDefence[TroopsTypeEnum.ca];
        }
        if (commander.GetOwner().biome.terrain == commander.hex.terrainType) defence *= ArmyData.biomeTerrainMultiplier;

        return defence;
    }

    public void Attack(Hex targetHex)
    {
        // Get the attacker's alignment
        AlignmentEnum attackerAlignment = commander.GetAlignment();

        // Calculate attacker's base strength
        int attackerStrength = GetStrength();

        // Add strength of allied armies in the same hex (if not neutral)
        if (attackerAlignment != AlignmentEnum.neutral && commander != null && commander.hex != null)
        {
            foreach (Army ally in commander.hex.armies)
            {
                // Skip the attacker itself and neutral armies
                if (ally != this && ally.GetAlignment() == attackerAlignment && ally.GetAlignment() != AlignmentEnum.neutral)
                {
                    attackerStrength += ally.GetStrength();
                }
            }
        }

        // Calculate attacker's defense for counter-attack
        int attackerDefence = GetDefence();

        // Add defense from allied armies in the attacker's hex (if not neutral)
        if (attackerAlignment != AlignmentEnum.neutral && commander != null && commander.hex != null)
        {
            foreach (Army ally in commander.hex.armies)
            {
                // Skip the attacker itself and neutral armies
                if (ally != this && ally.GetAlignment() == attackerAlignment && ally.GetAlignment() != AlignmentEnum.neutral)
                {
                    attackerDefence += ally.GetDefence();
                }
            }
        }

        // Find the main defending army (assume first army of different alignment)
        Army defenderArmy = null;
        AlignmentEnum defenderAlignment = AlignmentEnum.neutral;

        foreach (Army army in targetHex.armies)
        {
            if (army.GetAlignment() != attackerAlignment && army.GetAlignment() != AlignmentEnum.neutral)
            {
                defenderArmy = army;
                defenderAlignment = army.GetAlignment();
                break;
            }
        }

        int defenderDefense = 0;
        int defenderStrength = 0;
        float attackerDamage = 0;
        float defenderDamage = 0;
        float attackerCasualtyPercent = 0;
        // If no enemy army found, check if there's an enemy PC to attack
        if (defenderArmy == null)
        {
            // Handle case where there's no defender army but maybe there's a PC
            if (targetHex.pc != null && targetHex.pc.owner.GetAlignment() != attackerAlignment)
            {
                // Set defender alignment to the PC's alignment
                defenderAlignment = targetHex.pc.owner.GetAlignment();

                // Calculate defender's defense based on PC only
                int fortSize = (int) targetHex.pc.fortSize;
                int citySize = (int)targetHex.pc.citySize;
                defenderDefense = citySize + (fortSize * FortSizeData.defensePerFortSizeLevel);

                // Calculate defender's strength based on PC only
                defenderStrength = citySize + (fortSize * FortSizeData.defensePerFortSizeLevel);

                // Calculate raw damage values
                attackerDamage = Math.Max(0, attackerStrength - defenderDefense);
                defenderDamage = Math.Max(0, defenderStrength - attackerDefence);

                // Calculate casualties for attacker (PC can still cause casualties)
                attackerCasualtyPercent = defenderDamage / (attackerStrength * 10);

                // Clamp casualty percentage between 0 and 1
                attackerCasualtyPercent = Math.Clamp(attackerCasualtyPercent, 0, 1);

                // Apply casualties to attacker troops - ensure values stay >= 0
                ma = Math.Max(0, ma - (int)Math.Floor(ma * attackerCasualtyPercent));
                ar = Math.Max(0, ar - (int)Math.Floor(ar * attackerCasualtyPercent));
                li = Math.Max(0, li - (int)Math.Floor(li * attackerCasualtyPercent));
                hi = Math.Max(0, hi - (int)Math.Floor(hi * attackerCasualtyPercent));
                lc = Math.Max(0, lc - (int)Math.Floor(lc * attackerCasualtyPercent));
                hc = Math.Max(0, hc - (int)Math.Floor(hc * attackerCasualtyPercent));
                ca = Math.Max(0, ca - (int)Math.Floor(ca * attackerCasualtyPercent));

                // Only apply casualties to warships if in water
                if (commander.hex.IsWaterTerrain())
                {
                    ws = Math.Max(0, ws - (int)Math.Floor(ws * attackerCasualtyPercent));
                }

                // Check if attacker army was eliminated
                if (GetSize(true) < 1) Killed();

                if (attackerDamage > 0)
                {
                    if(targetHex.pc.fortSize > 0)
                    {
                        // Reduce fort size first
                        targetHex.pc.fortSize -= 1;
                    } else
                    {
                        targetHex.pc.CapturePC(commander.GetOwner());
                    }
                }

                // Redraw visuals
                commander.hex.RedrawArmies();
                commander.hex.RedrawPC();
            }
            return;
        }

        // Calculate defender's base defense
        defenderDefense = defenderArmy.GetDefence();

        // Add defense from allied armies in the defender's hex
        if (defenderAlignment != AlignmentEnum.neutral)
        {
            foreach (Army ally in targetHex.armies)
            {
                // Skip the defender itself and neutral armies
                // Also skip armies with the same alignment as attacker
                if (ally != defenderArmy && ally.GetAlignment() != AlignmentEnum.neutral && ally.GetAlignment() != attackerAlignment)
                {
                    defenderDefense += ally.GetDefence();
                }
            }
        }

        // Add defense from Population Center if it exists and is aligned with defender
        if (targetHex.pc != null && targetHex.pc.owner.GetAlignment() == defenderAlignment)
        {
            int fortSize = (int)targetHex.pc.fortSize;
            int citySize = (int)targetHex.pc.citySize;
            defenderDefense = citySize + (fortSize * FortSizeData.defensePerFortSizeLevel);
        }


        // Calculate collective defender strength for counter-attack
        defenderStrength = defenderArmy.GetStrength();

        // Add strength of allied defending armies
        if (defenderAlignment != AlignmentEnum.neutral)
        {
            foreach (Army ally in targetHex.armies)
            {
                // Skip the defender itself and neutral armies
                // Also skip armies with the same alignment as attacker
                if (ally != defenderArmy && ally.GetAlignment() != AlignmentEnum.neutral && ally.GetAlignment() != attackerAlignment)
                {
                    defenderStrength += ally.GetStrength();
                }
            }
        }

        // Add strength from Population Center if it exists and is aligned with defender
        if (targetHex.pc != null && targetHex.pc.owner.GetAlignment() == defenderAlignment)
        {
            int fortSize = (int)targetHex.pc.fortSize;
            int citySize = (int)targetHex.pc.citySize;
            defenderStrength = citySize + (fortSize * FortSizeData.defensePerFortSizeLevel);
        }

        // Calculate raw damage values
        attackerDamage = Math.Max(0, attackerStrength - defenderDefense);
        defenderDamage = Math.Max(0, defenderStrength - attackerDefence);

        // Calculate casualty percentages (as decimals)
        attackerCasualtyPercent = defenderDamage / (attackerStrength * 10);
        float defenderCasualtyPercent = attackerDamage / (defenderArmy.GetStrength() * 10);

        // Clamp casualty percentages between 0 and 1
        attackerCasualtyPercent = Math.Clamp(attackerCasualtyPercent, 0, 1);
        defenderCasualtyPercent = Math.Clamp(defenderCasualtyPercent, 0, 1);

        // Apply casualties to attacker troops - ensure values stay >= 0
        ma = Math.Max(0, ma - (int)Math.Floor(ma * attackerCasualtyPercent));
        ar = Math.Max(0, ar - (int)Math.Floor(ar * attackerCasualtyPercent));
        li = Math.Max(0, li - (int)Math.Floor(li * attackerCasualtyPercent));
        hi = Math.Max(0, hi - (int)Math.Floor(hi * attackerCasualtyPercent));
        lc = Math.Max(0, lc - (int)Math.Floor(lc * attackerCasualtyPercent));
        hc = Math.Max(0, hc - (int)Math.Floor(hc * attackerCasualtyPercent));
        ca = Math.Max(0, ca - (int)Math.Floor(ca * attackerCasualtyPercent));

        // Only apply casualties to warships if in water
        if (commander.hex.IsWaterTerrain())
        {
            ws = Math.Max(0, ws - (int)Math.Floor(ws * attackerCasualtyPercent));
        }

        // Apply casualties to all defending armies based on their alignment
        ApplyCasualties(targetHex, defenderCasualtyPercent, attackerAlignment);

        // Check if attacker army was eliminated
        if (GetSize(true) < 1) Killed();

        // Redraw visuals
        commander.hex.RedrawArmies();
        commander.hex.RedrawPC();
        targetHex.RedrawArmies();
        targetHex.RedrawPC();
    }

    // Helper method to apply casualties to all defending armies in a hex
    public void ApplyCasualties(Hex targetHex, float casualtyPercent, AlignmentEnum attackerAlignment)
    {
        foreach (Army army in targetHex.armies)
        {
            if (army == this) return;
            // Skip armies with the same alignment as the attacker and neutral armies
            if (army.GetAlignment() != attackerAlignment || (army.GetAlignment() == AlignmentEnum.neutral && army.commander.owner != commander.owner))
            {
               army.ReceiveCasualties(casualtyPercent);
            }
        }
    }

    public void ReceiveCasualties(float casualtyPercent)
    {
        // Apply casualties to this this
        this.ma = Math.Max(0, this.ma - (int)Math.Floor(this.ma * casualtyPercent));
        this.ar = Math.Max(0, this.ar - (int)Math.Floor(this.ar * casualtyPercent));
        this.li = Math.Max(0, this.li - (int)Math.Floor(this.li * casualtyPercent));
        this.hi = Math.Max(0, this.hi - (int)Math.Floor(this.hi * casualtyPercent));
        this.lc = Math.Max(0, this.lc - (int)Math.Floor(this.lc * casualtyPercent));
        this.hc = Math.Max(0, this.hc - (int)Math.Floor(this.hc * casualtyPercent));
        this.ca = Math.Max(0, this.ca - (int)Math.Floor(this.ca * casualtyPercent));

        // Only apply casualties to warships if in water
        if (this.commander.hex.IsWaterTerrain()) this.ws = Math.Max(0, this.ws - (int)Math.Floor(this.ws * casualtyPercent));

        // Check if this this was eliminated
        if (this.GetSize(true) < 1) this.Killed();
    }
}
