using System;
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
        if (commander.GetOwner().GetBiome().terrain == commander.hex.terrainType) strength *= ArmyData.biomeTerrainMultiplier;

        return strength;
    }

    public int GetOffence()
    {
        return GetStrength();
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
        if (commander.GetOwner().GetBiome().terrain == commander.hex.terrainType) defence *= ArmyData.biomeTerrainMultiplier;

        return defence;
    }

    public void Attack(Hex targetHex)
    {
        Leader attackerLeader = commander.GetOwner();
        // Get the attacker's alignment
        AlignmentEnum attackerAlignment = commander.GetAlignment();

        // Calculate attacker's base strength
        int attackerStrength = GetStrength();

        // Add strength of allied armies in the same hex
        if (commander != null && commander.hex != null)
        {
            foreach (Army ally in commander.hex.armies)
            {
                // Skip the attacker itself
                if (ally != this)
                {
                    // For non-neutral alignments: include armies with same alignment
                    // For neutral alignment: include only armies with same owner
                    if ((attackerAlignment != AlignmentEnum.neutral && ally.GetAlignment() == attackerAlignment) ||
                        (attackerAlignment == AlignmentEnum.neutral && ally.GetAlignment() == AlignmentEnum.neutral && ally.commander.GetOwner() == commander.GetOwner()))
                    {
                        attackerStrength += ally.GetStrength();
                    }
                }
            }
        }

        // Calculate attacker's defense for counter-attack
        int attackerDefence = GetDefence();

        // Add defense from allied armies in the attacker's hex
        if (commander != null && commander.hex != null)
        {
            foreach (Army ally in commander.hex.armies)
            {
                // Skip the attacker itself
                if (ally != this)
                {
                    // For non-neutral alignments: include armies with same alignment
                    // For neutral alignment: include only armies with same owner
                    if ((attackerAlignment != AlignmentEnum.neutral && ally.GetAlignment() == attackerAlignment) ||
                        (attackerAlignment == AlignmentEnum.neutral && ally.GetAlignment() == AlignmentEnum.neutral && ally.commander.GetOwner() == commander.GetOwner()))
                    {
                        attackerDefence += ally.GetDefence();
                    }
                }
            }
        }

        // Check if there are any armies to attack in the target hex
        bool foundDefenders = false;

        // First, handle all enemy armies in the hex
        foreach (Army defenderArmy in targetHex.armies)
        {
            // Don't attack your own armies (check ownership)
            bool isOwnArmy = defenderArmy.commander.GetOwner() == commander.GetOwner();

            // Attack if defender has different alignment OR if attacker is neutral (attacks everyone)
            // OR if defender is neutral (everyone attacks neutrals)
            // BUT never attack your own armies
            if (!isOwnArmy && (defenderArmy.GetAlignment() != attackerAlignment ||
                attackerAlignment == AlignmentEnum.neutral ||
                defenderArmy.GetAlignment() == AlignmentEnum.neutral))
            {
                // Process combat against this defender
                ProcessCombat(targetHex, defenderArmy, attackerStrength, attackerDefence, attackerLeader);
                foundDefenders = true;
            }
        }

        // If no enemy armies were found, check if there's a PC to attack
        if (!foundDefenders && targetHex.GetPC() != null)
        {
            Leader defenderLeader = targetHex.GetPC().owner;
            AlignmentEnum pcAlignment = defenderLeader.GetAlignment();

            // Don't attack your own PC (check both alignment and owner)
            bool isOwnPC = defenderLeader == commander.GetOwner();

            // Attack PC if it has different alignment OR if attacker is neutral (attacks all)
            // OR if PC is neutral (everyone attacks neutrals)
            // BUT never attack your own PC
            if (!isOwnPC && (pcAlignment != attackerAlignment ||
                attackerAlignment == AlignmentEnum.neutral ||
                pcAlignment == AlignmentEnum.neutral))
            {
                AttackPopulationCenter(targetHex, attackerStrength, attackerDefence, attackerLeader);
            }
        }

        // Redraw visuals
        commander.hex.RedrawArmies();
        targetHex.RedrawArmies();
        targetHex.RedrawPC();
    }

    // Helper method to process combat between attacker and a specific defender
    private void ProcessCombat(Hex targetHex, Army defenderArmy, int attackerStrength, int attackerDefence, Leader attackerLeader)
    {
        AlignmentEnum attackerAlignment = commander.GetAlignment();

        Leader defenderLeader = defenderArmy.commander.GetOwner();
        AlignmentEnum defenderAlignment = defenderArmy.GetAlignment();

        // Calculate defender's total defense and strength
        int defenderDefense = defenderArmy.GetDefence();
        int defenderStrength = defenderArmy.GetStrength();

        // Add defense from allied armies in the defender's hex
        foreach (Army ally in targetHex.armies)
        {
            // Skip the primary defender itself
            if (ally != defenderArmy)
            {
                // For non-neutral alignments: include armies with same alignment
                // For neutral alignment: include only armies with same owner
                if ((defenderAlignment != AlignmentEnum.neutral && ally.GetAlignment() == defenderAlignment) ||
                    (defenderAlignment == AlignmentEnum.neutral && ally.GetAlignment() == AlignmentEnum.neutral && ally.commander.GetOwner() == defenderArmy.commander.GetOwner()))
                {
                    defenderDefense += ally.GetDefence();
                    defenderStrength += ally.GetStrength();
                }
            }
        }

        // Add defense from Population Center if it exists and is aligned with defender
        if (targetHex.GetPC() != null && targetHex.GetPC().owner.GetAlignment() == defenderAlignment)
        {
            defenderDefense += targetHex.GetPC().GetDefense();
            defenderStrength += targetHex.GetPC().GetDefense(); // PC contributes to counter-attack
        }

        // Calculate raw damage values
        float attackerDamage = Math.Max(0, attackerStrength - defenderDefense);
        float defenderDamage = Math.Max(0, defenderStrength - attackerDefence);

        // Calculate casualty percentages (as decimals)
        float attackerCasualtyPercent = defenderDamage / (attackerStrength * 10);
        float defenderCasualtyPercent = attackerDamage / (defenderStrength * 10);

        // Clamp casualty percentages between 0 and 1
        attackerCasualtyPercent = Math.Clamp(attackerCasualtyPercent, 0, 1);
        defenderCasualtyPercent = Math.Clamp(defenderCasualtyPercent, 0, 1);

        // Calculate actual casualties that would occur
        int attackerTotalLosses = CalculateTotalCasualties(attackerCasualtyPercent);
        int defenderTotalLosses = defenderArmy.CalculateTotalCasualties(defenderCasualtyPercent);

        // Check if either side will actually lose units
        bool anyAttackerCasualties = attackerTotalLosses > 0;
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
    }

    // Helper method to attack a Population Center
    private void AttackPopulationCenter(Hex targetHex, int attackerStrength, int attackerDefence, Leader attackerLeader)
    {
        // No defending army, only a PC
        Leader defenderLeader = targetHex.GetPC().owner;
        AlignmentEnum defenderAlignment = defenderLeader.GetAlignment();

        int defenderDefense = targetHex.GetPC().GetDefense();
        int defenderStrength = defenderDefense; // PC defense is its strength for counter-attack

        // Calculate raw damage values
        float attackerDamage = Math.Max(0, attackerStrength - defenderDefense);
        float defenderDamage = Math.Max(0, defenderStrength - attackerDefence);

        // Calculate casualties for attacker (PC can still cause casualties)
        float attackerCasualtyPercent = defenderDamage / (attackerStrength * 10);

        // Clamp casualty percentage between 0 and 1
        attackerCasualtyPercent = Math.Clamp(attackerCasualtyPercent, 0, 1);

        // Check if attacker will actually lose any units
        int attackerTotalLosses = CalculateTotalCasualties(attackerCasualtyPercent);
        bool anyAttackerCasualties = attackerTotalLosses > 0;

        // Apply casualties to attacker
        ReceiveCasualties(attackerCasualtyPercent, defenderLeader, !anyAttackerCasualties);

        // Process damage to the PC
        if (attackerDamage > 0)
        {
            if (targetHex.GetPC().fortSize > FortSizeEnum.NONE)
            {
                // Reduce fort size first
                targetHex.GetPC().DecreaseFort();
                MessageDisplay.ShowMessage($"Defenses at {targetHex.GetPC().pcName} were damaged", Color.yellow);
            }
            else
            {
                targetHex.GetPC().CapturePC(attackerLeader);
            }
        }
        else
        {
            MessageDisplay.ShowMessage($"{targetHex.GetPC().pcName} defenses resisted the attack", Color.red);
        }

        // Check if attacker army was eliminated
        if (!killed && GetSize(true) < 1) Killed(defenderLeader);
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
                // Skip attacker's own armies
                bool isOwnArmy = army.commander.GetOwner() == commander.GetOwner();

                // Consider armies that should be attacked
                if (!isOwnArmy && (army.GetAlignment() != attackerAlignment ||
                    attackerAlignment == AlignmentEnum.neutral ||
                    army.GetAlignment() == AlignmentEnum.neutral))
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

            // Skip invalid armies, the attacker itself, or dead armies
            if (army == this || army == null || army.GetSize() < 1 ||
                army.GetCommander() == null || army.killed) continue;

            // Skip the attacker's own armies
            bool isOwnArmy = army.commander.GetOwner() == commander.GetOwner();
            if (isOwnArmy) continue;

            // Apply casualties to armies that should be attacked:
            // Different alignment OR attacker is neutral OR defender is neutral
            if (army.GetAlignment() != attackerAlignment ||
                attackerAlignment == AlignmentEnum.neutral ||
                army.GetAlignment() == AlignmentEnum.neutral)
            {
                // Force one unit casualty only on the primary defender
                bool forceUnit = forceOneUnitCasualty && army == primaryDefender;
                army.ReceiveCasualties(casualtyPercent, attacker, forceUnit);
            }
        }

        // Handle damage to population center
        if ((casualtyPercent > 0.4f || UnityEngine.Random.Range(0f, 1f) >= 0.75f) &&
            targetHex.GetPC() != null)
        {
            // Don't damage own PC
            bool isOwnPC = targetHex.GetPC().owner == commander.GetOwner();

            // Damage PC if it belongs to a different alignment OR attacker is neutral OR PC is neutral
            // BUT never damage your own PC
            if (!isOwnPC && (targetHex.GetPC().owner.GetAlignment() != attackerAlignment ||
                attackerAlignment == AlignmentEnum.neutral ||
                targetHex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral))
            {
                targetHex.GetPC().DecreaseFort();
            }
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