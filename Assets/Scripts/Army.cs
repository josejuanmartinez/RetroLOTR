using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
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

    [SerializeField] public bool startingArmy = false;

    public bool killed = false;

    public Army(Character commander, bool startingArmy = false, int ma = 0, int ar = 0, int li = 0, int hi = 0, int lc = 0, int hc = 0, int ca = 0, int ws = 0)
    {
        this.commander = commander;
        this.startingArmy = startingArmy;
        this.ma = ma;
        this.ar = ar;
        this.li = li;
        this.hi = hi;
        this.lc = lc;
        this.hc = hc;
        this.ca = ca;
        this.ws = ws;
    }

    public Army(Character commander, TroopsTypeEnum troopsType, int amount, bool startingArmy, int ws = 0)
    {
        this.commander = commander;
        this.startingArmy = startingArmy;

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
    public void Recruit(TroopsTypeEnum troopsType, int amount)
    {
        if (troopsType == TroopsTypeEnum.ma)
        {
            MessageDisplay.ShowMessage($"+{amount} Men-at-arms were hired", Color.green);
            ma += amount;
        }
        if (troopsType == TroopsTypeEnum.ar)
        {
            MessageDisplay.ShowMessage($"+{amount} Archers were hired", Color.green);
            ar += amount;
        }
        if (troopsType == TroopsTypeEnum.li)
        {
            MessageDisplay.ShowMessage($"+{amount} Light Infrantry was hired", Color.green);
            li += amount;
        }
        if (troopsType == TroopsTypeEnum.hi)
        {
            MessageDisplay.ShowMessage($"+{amount} Heavy Infantry was hired", Color.green);
            hi += amount;
        }
        if (troopsType == TroopsTypeEnum.lc)
        {
            MessageDisplay.ShowMessage($"+{amount} Light Cavalry was hired", Color.green);
            lc += amount;
        }
        if (troopsType == TroopsTypeEnum.hc)
        {
            MessageDisplay.ShowMessage($"+{amount} Heavy Cavalry was hired", Color.green);
            hc += amount;
        }
        if (troopsType == TroopsTypeEnum.ca)
        {
            MessageDisplay.ShowMessage($"+{amount} Catapults were built", Color.green);
            ca += amount;
        }
        if (troopsType == TroopsTypeEnum.ws)
        {
            MessageDisplay.ShowMessage($"+{amount} Warships were built", Color.green);
            ws += amount;
        }
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

    public void Killed(Leader killedBy)
    {
        if (killed) return;
        killed = true;
        int wound = UnityEngine.Random.Range(0, 100);
        MessageDisplay.ShowMessage($"{commander.characterName} army was killed and {commander.characterName} wounded by {wound}", Color.red);
        if(commander.hex.armies.Contains(this)) commander.hex.armies.Remove(this);
        commander.hex.RedrawArmies();
        commander = null;
        ma = 0;
        ar = 0;
        li = 0;
        hi = 0;
        lc = 0;
        hc = 0;
        ca = 0;
        ws = 0;
        commander.Wounded(killedBy, wound);
        commander = null;
    }

    public int GetStrength()
    {
        int strength = 0;
        if (commander.hex.IsWaterTerrain())
        {
            strength += (ma + ar + li + hi + lc + hc + ca) * ArmyData.transportedStrength;
            strength += ws * ArmyData.warshipStrength;
        }
        else
        {
            strength += ma * ArmyData.troopsStrength[TroopsTypeEnum.ma];
            strength += ar * ArmyData.troopsStrength[TroopsTypeEnum.ar];
            strength += li * ArmyData.troopsStrength[TroopsTypeEnum.li];
            strength += hi * ArmyData.troopsStrength[TroopsTypeEnum.hi];
            strength += lc * ArmyData.troopsStrength[TroopsTypeEnum.lc];
            strength += hc * ArmyData.troopsStrength[TroopsTypeEnum.hc];
            strength += (commander.hex.GetPC() != null && commander.hex.GetPC().owner.GetAlignment() != GetAlignment()) ? ca * ArmyData.troopsStrength[TroopsTypeEnum.ca] : ca * ArmyData.troopsStrength[TroopsTypeEnum.ca] * ArmyData.catapultStrengthMultiplierInPC;
        }
        if (commander.GetOwner().biome.terrain == commander.hex.terrainType) strength *= ArmyData.biomeTerrainMultiplier;

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
        Leader attackerLeader = commander.GetOwner();
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
        Leader defenderLeader = null;
        AlignmentEnum defenderAlignment = AlignmentEnum.neutral;

        foreach (Army army in targetHex.armies)
        {
            if (army.GetAlignment() != attackerAlignment && army.GetAlignment() != AlignmentEnum.neutral)
            {
                defenderArmy = army;
                defenderLeader = army.commander.GetOwner();
                defenderAlignment = army.GetAlignment();
                break;
            }
        }

        int defenderDefense = 0;
        int defenderStrength = 0;
        float attackerDamage = 0;
        float defenderDamage = 0;
        float attackerCasualtyPercent = 0;
        int attackerTotalLosses = 0;
        bool anyAttackerCasualties = false;
        // If no enemy army found, check if there's an enemy PC to attack
        if (defenderArmy == null)
        {
            // Handle case where there's no defender army but maybe there's a PC
            if (targetHex.GetPC() != null && targetHex.GetPC().owner.GetAlignment() != attackerAlignment)
            {
                // Set defender alignment to the PC's alignment
                defenderAlignment = targetHex.GetPC().owner.GetAlignment();

                // Calculate defender's defense based on PC only
                int fortSize = (int)targetHex.GetPC().fortSize;
                int citySize = (int)targetHex.GetPC().citySize;
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

                // Check if attacker will actually lose any units
                attackerTotalLosses = CalculateTotalCasualties(attackerCasualtyPercent);
                anyAttackerCasualties = attackerTotalLosses > 0;

                // Apply casualties to attacker
                ReceiveCasualties(attackerCasualtyPercent, defenderLeader, !anyAttackerCasualties);

                if (attackerDamage > 0)
                {
                    if (targetHex.GetPC().fortSize > FortSizeEnum.NONE)
                    {
                        // Reduce fort size first
                        targetHex.GetPC().DecreaseFort();
                    }
                    else
                    {
                        targetHex.GetPC().CapturePC(commander.GetOwner());
                    }
                }
                else
                {
                    MessageDisplay.ShowMessage($"{targetHex.GetPC().pcName} defences resisted the attack", Color.red);
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
        if (targetHex.GetPC() != null && targetHex.GetPC().owner.GetAlignment() == defenderAlignment)
        {
            int fortSize = (int)targetHex.GetPC().fortSize;
            int citySize = (int)targetHex.GetPC().citySize;
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
        if (targetHex.GetPC() != null && targetHex.GetPC().owner.GetAlignment() == defenderAlignment)
        {
            int fortSize = (int)targetHex.GetPC().fortSize;
            int citySize = (int)targetHex.GetPC().citySize;
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

        // Calculate actual casualties that would occur
        attackerTotalLosses = CalculateTotalCasualties(attackerCasualtyPercent);
        int defenderTotalLosses = defenderArmy.CalculateTotalCasualties(defenderCasualtyPercent);

        // Check if either side will actually lose units
        anyAttackerCasualties = attackerTotalLosses > 0;
        bool anyDefenderCasualties = defenderTotalLosses > 0;

        // If no one is taking casualties, decide who should lose one unit
        bool forceCasualtySide = false;
        if (!anyAttackerCasualties && !anyDefenderCasualties)
        {
            // Compare strengths to decide who loses one unit
            forceCasualtySide = attackerStrength >= defenderStrength;
        }

        // Apply casualties to attacker troops
        ReceiveCasualties(attackerCasualtyPercent, defenderLeader, !anyAttackerCasualties && !forceCasualtySide);

        // Apply casualties to all defending armies based on their alignment
        ApplyCasualtiesToDefenders(targetHex, defenderCasualtyPercent, attackerAlignment, !anyDefenderCasualties && forceCasualtySide, attackerLeader);

        // Check if attacker army was eliminated
        if (!killed && GetSize(true) < 1) Killed(defenderLeader);

        // Redraw visuals
        targetHex.RedrawArmies();
        targetHex.RedrawPC();
    }

    // Helper method to apply casualties to all defending armies in a hex
    public void ApplyCasualtiesToDefenders(Hex targetHex, float casualtyPercent, AlignmentEnum attackerAlignment, bool forceOneUnitCasualty, Leader attacker)
    {
        // If we need to force one unit casualty, find the primary defender
        Army primaryDefender = null;
        if (forceOneUnitCasualty)
        {
            foreach (Army army in targetHex.armies)
            {
                if (army.GetAlignment() != attackerAlignment || (army.GetAlignment() == AlignmentEnum.neutral && army.commander.owner != commander.owner))
                {
                    if (primaryDefender == null || army.GetSize(true) > primaryDefender.GetSize(true))
                    {
                        primaryDefender = army;
                    }
                }
            }
        }

        int armiesNum = targetHex.armies.Count;

        // Use a for loop with index going backwards to avoid collection modification issues
        for (int i = armiesNum - 1; i >= 0; i--)
        {
            // Check if index is still valid (in case armies were removed)
            if (i >= targetHex.armies.Count) continue;

            Army army = targetHex.armies[i];

            if (army == this || army == null || army.GetSize() < 1 || army.GetCommander() == null || army.killed) continue;

            // Skip armies with the same alignment as the attacker and neutral armies
            if (army.GetAlignment() != attackerAlignment || (army.GetAlignment() == AlignmentEnum.neutral && army.commander.owner != commander.owner))
            {
                // Force one unit casualty only on the primary defender
                bool forceUnit = forceOneUnitCasualty && army == primaryDefender;
                army.ReceiveCasualties(casualtyPercent, attacker, forceUnit);
            }
        }

        if ((casualtyPercent > 0.4f || UnityEngine.Random.Range(0f, 1f) >= 0.75f) && targetHex.GetPC() != null && (targetHex.GetPC().owner.GetAlignment() != attackerAlignment || targetHex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral))
        {
            targetHex.GetPC().DecreaseFort();
        }
    }

    // Helper method to calculate total casualties without applying them
    // Uses the same rounding function as the actual casualty calculation
    public int CalculateTotalCasualties(float casualtyPercent)
    {
        // Calculate casualties for each unit type using the chosen rounding function
        // You can replace Math.Floor with Math.Ceiling or Math.Round as needed
        int maCasualties = (int)Math.Round(this.ma * casualtyPercent);
        int arCasualties = (int)Math.Round(this.ar * casualtyPercent);
        int liCasualties = (int)Math.Round(this.li * casualtyPercent);
        int hiCasualties = (int)Math.Round(this.hi * casualtyPercent);
        int lcCasualties = (int)Math.Round(this.lc * casualtyPercent);
        int hcCasualties = (int)Math.Round(this.hc * casualtyPercent);
        int caCasualties = (int)Math.Round(this.ca * casualtyPercent);
        int wsCasualties = commander?.hex?.IsWaterTerrain() ?? false ? (int)Math.Round(this.ws * casualtyPercent) : 0;

        return maCasualties + arCasualties + liCasualties + hiCasualties +
               lcCasualties + hcCasualties + caCasualties + wsCasualties;
    }

    public void ReceiveCasualties(float casualtyPercent, Leader defenderLeader, bool forceOneUnitCasualty = false)
    {
        // Calculate casualties for each unit type
        // You can replace Math.Floor with Math.Ceiling or Math.Round depending on desired behavior
        // Math.Floor - Minimum losses (current behavior)
        // Math.Ceiling - Maximum losses (more aggressive)
        // Math.Round - Balanced approach (rounds 0.5 up, otherwise follows normal rounding rules)
        int maCasualties = (int)Math.Round(this.ma * casualtyPercent);
        int arCasualties = (int)Math.Round(this.ar * casualtyPercent);
        int liCasualties = (int)Math.Round(this.li * casualtyPercent);
        int hiCasualties = (int)Math.Round(this.hi * casualtyPercent);
        int lcCasualties = (int)Math.Round(this.lc * casualtyPercent);
        int hcCasualties = (int)Math.Round(this.hc * casualtyPercent);
        int caCasualties = (int)Math.Round(this.ca * casualtyPercent);
        int wsCasualties = commander.hex.IsWaterTerrain() ? (int)Math.Round(this.ws * casualtyPercent) : 0;

        // Check if there are any casualties at all
        bool anyCasualties = maCasualties > 0 || arCasualties > 0 || liCasualties > 0 ||
                            hiCasualties > 0 || lcCasualties > 0 || hcCasualties > 0 ||
                            caCasualties > 0 || wsCasualties > 0;

        // If no casualties and we need to force one, find the weakest unit type and remove one
        if (!anyCasualties && forceOneUnitCasualty && GetSize(false) > 0)
        {
            // Check each unit type in order of weakness precedence
            if (ma > 0)
            {
                maCasualties = 1;
            }
            else if (ar > 0)
            {
                arCasualties = 1;
            }
            else if (li > 0)
            {
                liCasualties = 1;
            }
            else if (lc > 0)
            {
                lcCasualties = 1;
            }
            else if (hi > 0)
            {
                hiCasualties = 1;
            }
            else if (hc > 0)
            {
                hcCasualties = 1;
            }
            else if (ca > 0)
            {
                caCasualties = 1;
            }
            else if (ws > 0 && commander.hex.IsWaterTerrain())
            {
                wsCasualties = 1;
            }
        }

        // Apply casualties
        this.ma = Math.Max(0, this.ma - maCasualties);
        this.ar = Math.Max(0, this.ar - arCasualties);
        this.li = Math.Max(0, this.li - liCasualties);
        this.hi = Math.Max(0, this.hi - hiCasualties);
        this.lc = Math.Max(0, this.lc - lcCasualties);
        this.hc = Math.Max(0, this.hc - hcCasualties);
        this.ca = Math.Max(0, this.ca - caCasualties);

        // Only apply casualties to warships if in water
        if (commander.hex.IsWaterTerrain())
        {
            this.ws = Math.Max(0, this.ws - wsCasualties);
        }

        // Check if the army was eliminated
        if (this.GetSize(true) < 1)
        {
            this.Killed(defenderLeader);
        }
        else if (maCasualties > 0 || arCasualties > 0 || liCasualties > 0 ||
                hiCasualties > 0 || lcCasualties > 0 || hcCasualties > 0 ||
                caCasualties > 0 || wsCasualties > 0)
        {
            MessageDisplay.ShowMessage($"{commander.characterName} army receives casualties", Color.red);
        }
    }

    public Character GetCommander()
    {
        return commander;
    }

    public int GetMaintenanceCost()
    {
        float cost = 0f;
        cost += ma / 4;
        cost += ar / 4;
        cost += li / 3;
        cost += hi / 2;
        cost += lc / 2;
        cost += hc;

        return Mathf.FloorToInt(cost);
    }
}