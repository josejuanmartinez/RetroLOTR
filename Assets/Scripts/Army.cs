using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    [SerializeField] public int xp = 25;

    [SerializeField] public bool startingArmy = false;

    public bool killed = false;

    public Army(Character commander, bool startingArmy = false, int ma = 0, int ar = 0, int li = 0, int hi = 0, int lc = 0, int hc = 0, int ca = 0, int ws = 0, int xp = 25)
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
        this.xp = Mathf.Clamp(xp, 0, 100);
    }

    public Army(Character commander, TroopsTypeEnum troopsType, int amount, bool startingArmy, int ws = 0, int xp = 25)
    {
        this.commander = commander;
        this.startingArmy = startingArmy;
        this.xp = Mathf.Clamp(xp, 0, 100);

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
        int totalSize = GetSize() + otherArmy.GetSize();
        if (totalSize > 0)
        {
            xp = Mathf.Clamp(Mathf.RoundToInt((xp * GetSize() + otherArmy.xp * otherArmy.GetSize()) / (float)totalSize), 0, 100);
        }
    }
    public void Recruit(TroopsTypeEnum troopsType, int amount)
    {
        MessageDisplayNoUI.ShowMessage(commander.hex, commander, $"+{amount} <sprite name=\"{troopsType.ToString().ToLower()}\"/>", Color.green);
        if (troopsType == TroopsTypeEnum.ma) ma += amount;
        if (troopsType == TroopsTypeEnum.ar) ar += amount;
        if (troopsType == TroopsTypeEnum.li) li += amount;
        if (troopsType == TroopsTypeEnum.hi) hi += amount;
        if (troopsType == TroopsTypeEnum.lc) lc += amount;
        if (troopsType == TroopsTypeEnum.hc) hc += amount;
        if (troopsType == TroopsTypeEnum.ca) ca += amount;
        if (troopsType == TroopsTypeEnum.ws) ws += amount;
    }

    public int GetSize(bool withoutWs = false)
    {
        int result = ma + ar + li + hi + lc + hc + ca;
        result += withoutWs ? 0 : ws;
        return result;
    }

    public string GetHoverText(bool withHealth = true)
    {
        LeaderBiomeConfig biomeConfig = commander.GetOwner().GetBiome();
        List<string> result = new() { };
    
        if (ma > 0) result.Add($"<sprite name=\"ma\">[{ma}] {biomeConfig.maDescription}");
        if (ar > 0) result.Add($"<sprite name=\"ar\">[{ar}] {biomeConfig.arDescription}");
        if (li > 0) result.Add($"<sprite name=\"li\">[{li}] {biomeConfig.liDescription}");
        if (hi > 0) result.Add($"<sprite name=\"hi\">[{hi}] {biomeConfig.hiDescription}");
        if (lc > 0) result.Add($"<sprite name=\"lc\">[{lc}] {biomeConfig.lcDescription}");
        if (hc > 0) result.Add($"<sprite name=\"hc\">[{hc}] {biomeConfig.hcDescription}");
        if (ca > 0) result.Add($"<sprite name=\"ca\">[{ca}] {biomeConfig.caDescription}");
        if (ws > 0) result.Add($"<sprite name=\"ws\">[{ws}] {biomeConfig.wsDescription}");

        string xpText = GetXpHoverText();
        string healthText = withHealth && commander != null ? commander.GetHealthHoverText() : "";

        return $" leading {string.Join(',', result)}{xpText}{healthText}";
    }

    private string GetXpHoverText()
    {
        string color = xp < 25 ? "#ff4d4d" : xp < 50 ? "#ffb347" : xp < 75 ? "#8fd14f" : "#00c853";
        return $" XP[<color={color}>{xp}</color>]";
    }

    public string GetTrainingLabel()
    {
        if (xp < 25) return "low trained";
        if (xp < 50) return "skilled";
        if (xp < 75) return "well trained";
        return "elite";
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

    public void AddXp(int amount, string reason = null)
    {
        int before = xp;
        xp = Mathf.Clamp(xp + amount, 0, 100);
        if (xp == before) return;

        if (commander != null && commander.isPlayerControlled && commander.hex != null)
        {
            string text = $"Army XP {(xp > before ? "+" : "")}{xp - before}";
            if (!string.IsNullOrWhiteSpace(reason)) text += $" ({reason})";
            Color color = xp > before ? Color.green : Color.red;
            MessageDisplayNoUI.ShowMessage(commander.hex, commander, text, color);
        }
    }

    public void Killed(Leader killedBy, bool onlyMark = false)
    {
        killed = true;
        int wound = UnityEngine.Random.Range(0, 100);
        MessageDisplayNoUI.ShowMessage(commander.hex,commander, $"{commander.characterName} army was killed and {commander.characterName} wounded by {wound}", Color.red);
        if(!onlyMark && commander.hex.armies.Contains(this)) commander.hex.armies.Remove(this);        
        commander.hex.RedrawCharacters();
        commander.hex.RedrawArmies();
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
            // Catapults hit harder when attacking an enemy PC
            bool attackingEnemyPc = commander.hex.GetPC() != null && commander.hex.GetPC().owner.GetAlignment() != GetAlignment();
            strength += attackingEnemyPc
                ? ca * ArmyData.troopsStrength[TroopsTypeEnum.ca] * ArmyData.catapultStrengthMultiplierInPC
                : ca * ArmyData.troopsStrength[TroopsTypeEnum.ca];
        }
        if (commander.GetOwner().GetBiome().terrain == commander.hex.terrainType) strength *= ArmyData.biomeTerrainMultiplier;

        strength = ApplyCommanderBonus(strength);
        strength = ApplyTrainingBonus(strength);
        strength = ApplyArtifactAttackBonus(strength);
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

        defence = ApplyCommanderBonus(defence);
        defence = ApplyTrainingBonus(defence);
        defence = ApplyArtifactDefenseBonus(defence);
        return defence;
    }

    private int ApplyCommanderBonus(int value)
    {
        int commanderLevel = commander != null ? commander.GetCommander() : 0;
        // Each commander level adds 5% to both offence and defence for this army (max +50%).
        float bonusMultiplier = 1f + Mathf.Clamp(commanderLevel, 0, 10) * 0.05f;
        return Mathf.Max(0, Mathf.RoundToInt(value * bonusMultiplier));
    }

    private int ApplyTrainingBonus(int value)
    {
        // Training scales 0.75x at 0 XP to 1.25x at 100 XP
        float trainingMultiplier = 0.75f + Mathf.Clamp(xp, 0, 100) / 200f;
        return Mathf.Max(0, Mathf.RoundToInt(value * trainingMultiplier));
    }

    private int ApplyArtifactAttackBonus(int value)
    {
        if (commander == null) return value;
        int bonus = commander.artifacts.Sum(a => Mathf.Max(0, a.bonusAttack)) * 3;
        return Mathf.Max(0, value + bonus);
    }

    private int ApplyArtifactDefenseBonus(int value)
    {
        if (commander == null) return value;
        int bonus = commander.artifacts.Sum(a => Mathf.Max(0, a.bonusDefense)) * 3;
        return Mathf.Max(0, value + bonus);
    }

    public int GetArtifactAttackBonusTotal()
    {
        if (commander == null) return 0;
        return commander.artifacts.Sum(a => Mathf.Max(0, a.bonusAttack)) * 3;
    }

    public int GetArtifactDefenseBonusTotal()
    {
        if (commander == null) return 0;
        return commander.artifacts.Sum(a => Mathf.Max(0, a.bonusDefense)) * 3;
    }

    private void GrantCombatXp(Army army, string reason)
    {
        if (army == null || army.killed || army.GetSize(true) < 1) return;
        int commanderLevel = army.commander != null ? army.commander.GetCommander() : 0;
        int gain = Mathf.Clamp(UnityEngine.Random.Range(1, 6) + commanderLevel, 1, 10);
        army.AddXp(gain, reason);
    }

    public void Attack(Hex targetHex)
    {
        Leader attackerLeader = commander.GetOwner();
        if(this == null || this.commander == null || killed || this.commander.killed || attackerLeader == null || attackerLeader.killed) return;
        // Get the attacker's alignment
        AlignmentEnum attackerAlignment = commander.GetAlignment();

        // Calculate attacker's base strength
        int attackerStrength = GetStrength();

        // Calculate attacker's defense for counter-attack
        int attackerDefence = GetDefence();
        int attackerAlliesJoined = 0;
        int attackerAlliesStrength = 0;
        int attackerAlliesDefence = 0;

        // Add strength of allied armies in the same hex
        if (commander != null && commander.hex != null)
        {
            foreach (Army ally in commander.hex.armies)
            {
                // Skip the attacker itself
                if (ally != null && ally != this && !ally.killed && ally.commander != null && !ally.commander.killed)
                {
                    // For non-neutral alignments: include armies with same alignment
                    // For neutral alignment: include only armies with same owner
                    if ((attackerAlignment != AlignmentEnum.neutral && ally.GetAlignment() == attackerAlignment) ||
                        (attackerAlignment == AlignmentEnum.neutral && ally.GetAlignment() == AlignmentEnum.neutral && ally.commander.GetOwner() == commander.GetOwner()))
                    {
                        int allyStrength = ally.GetStrength();
                        int allyDefence = ally.GetDefence();
                        attackerStrength += allyStrength;
                        attackerDefence += allyDefence;
                        attackerAlliesJoined++;
                        attackerAlliesStrength += allyStrength;
                        attackerAlliesDefence += allyDefence;
                    }
                }
            }
        }

        // Check if there are any armies to attack in the target hex
        bool foundDefenders = false;

        // First, handle all enemy armies in the hex using a snapshot to avoid collection modification issues
        List<Army> defenderSnapshot = new List<Army>(targetHex.armies);
        foreach (Army defenderArmy in defenderSnapshot)
        {
            if(defenderArmy == null || defenderArmy.commander == null || defenderArmy.killed || defenderArmy.commander.killed || defenderArmy.killed) continue;
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
                ProcessCombat(targetHex, defenderArmy, attackerStrength, attackerDefence, attackerLeader, attackerAlliesJoined, attackerAlliesStrength, attackerAlliesDefence);
                foundDefenders = true;
            }
        }

        // If no enemy armies were found, check if there's a PC to attack
        if (!foundDefenders && targetHex.GetPC() != null){
            Leader defenderLeader = targetHex.GetPC().owner;
            AlignmentEnum pcAlignment = defenderLeader != null ? defenderLeader.GetAlignment() : AlignmentEnum.neutral;

            // Don't attack your own PC (check both alignment and owner)
            bool isOwnPC = commander && !commander.killed ? defenderLeader == commander.GetOwner() : false;

            // Attack PC if it has different alignment OR if attacker is neutral (attacks all)
            // OR if PC is neutral (everyone attacks neutrals)
            // BUT never attack your own PC
            if (!isOwnPC && (defenderLeader == null || defenderLeader.killed ||
                pcAlignment != attackerAlignment ||
                attackerAlignment == AlignmentEnum.neutral ||
                pcAlignment == AlignmentEnum.neutral))
            {
                AttackPopulationCenter(targetHex, attackerStrength, attackerDefence, attackerLeader);
            }
        }


        // Redraw visuals
        targetHex.RedrawCharacters();
        targetHex.RedrawArmies();
        // Not needed: if captured or decreased or something, it will refresh automatically
        // targetHex.RedrawPC();
    }

    // Helper method to process combat between attacker and a specific defender
    private void ProcessCombat(Hex targetHex, Army defenderArmy, int attackerStrength, int attackerDefence, Leader attackerLeader, int attackerAlliesJoined, int attackerAlliesStrength, int attackerAlliesDefence)
    {
        if(defenderArmy == null || defenderArmy.commander == null || defenderArmy.killed || defenderArmy.commander.killed || attackerLeader == null || attackerLeader.killed) return;
        AlignmentEnum attackerAlignment = commander.GetAlignment();
        string attackerName = commander != null ? commander.characterName : attackerLeader.characterName;
        string defenderName = defenderArmy.GetCommander().characterName;
        int attackerXpBefore = xp;
        int defenderXpBefore = defenderArmy.xp;
        Character hudActor = commander;

        Leader defenderLeader = defenderArmy.commander.GetOwner();
        AlignmentEnum defenderAlignment = defenderArmy.GetAlignment();

        // Calculate defender's total defense and strength
        int defenderDefense = defenderArmy.GetDefence();
        int defenderStrength = defenderArmy.GetStrength();
        int attackerArtifactAttack = GetArtifactAttackBonusTotal();
        int attackerArtifactDefense = GetArtifactDefenseBonusTotal();
        int defenderArtifactAttack = defenderArmy.GetArtifactAttackBonusTotal();
        int defenderArtifactDefense = defenderArmy.GetArtifactDefenseBonusTotal();
        int defenderAlliesJoined = 0;
        int defenderAlliesStrength = 0;
        int defenderAlliesDefence = 0;
        int pcDefenseContribution = 0;

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
                    int allyDefence = ally.GetDefence();
                    int allyStrength = ally.GetStrength();
                    defenderDefense += allyDefence;
                    defenderStrength += allyStrength;
                    defenderAlliesJoined++;
                    defenderAlliesStrength += allyStrength;
                    defenderAlliesDefence += allyDefence;
                }
            }
        }

        // Add defense from Population Center if it exists and is aligned with defender
        if (targetHex.GetPC() != null && targetHex.GetPC().owner.GetAlignment() == defenderAlignment)
        {
            pcDefenseContribution = targetHex.GetPC().GetDefense();
            defenderDefense += pcDefenseContribution;
            defenderStrength += pcDefenseContribution; // PC contributes to counter-attack
        }

        int attackerCommanderLevel = commander != null ? commander.GetCommander() : 0;
        int defenderCommanderLevel = defenderArmy.commander != null ? defenderArmy.commander.GetCommander() : 0;
        int attackerBonusPercent = Mathf.RoundToInt(Mathf.Clamp(attackerCommanderLevel, 0, 10) * 10f);
        int defenderBonusPercent = Mathf.RoundToInt(Mathf.Clamp(defenderCommanderLevel, 0, 10) * 10f);

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
        int attackerPercent = Mathf.RoundToInt(attackerCasualtyPercent * 100f);
        int defenderPercent = Mathf.RoundToInt(defenderCasualtyPercent * 100f);
        int attackerReportedLosses = attackerTotalLosses;
        int defenderReportedLosses = defenderTotalLosses;
        string stalemateNote = null;

        if (!anyAttackerCasualties && !anyDefenderCasualties)
        {
            if (forceCasualtySide)
            {
                defenderReportedLosses = 1;
                stalemateNote = $"{defenderArmy.GetCommander().characterName} suffers a token casualty after a stalemate.";
            }
            else
            {
                attackerReportedLosses = 1;
                stalemateNote = $"{attackerName} suffers a token casualty after a stalemate.";
            }
        }

        string battleLocation = targetHex.HasAnyPC() && targetHex.IsPCRevealed() ? targetHex.GetPC().pcName : targetHex.GetHoverV2();
        string title = $"Attack at {battleLocation}";
        string text = BuildBattleDescription(
            battleLocation,
            attackerName,
            defenderArmy.GetCommander().characterName,
            attackerAlliesJoined,
            attackerAlliesStrength,
            attackerAlliesDefence,
            defenderAlliesJoined,
            defenderAlliesStrength,
            defenderAlliesDefence,
            pcDefenseContribution,
            attackerBonusPercent,
            defenderBonusPercent,
            GetTrainingLabel(),
            defenderArmy.GetTrainingLabel(),
            xp,
            defenderArmy.xp,
            attackerArtifactAttack,
            attackerArtifactDefense,
            defenderArtifactAttack,
            defenderArtifactDefense,
            attackerStrength,
            defenderDefense,
            defenderStrength,
            attackerDefence,
            attackerReportedLosses,
            attackerPercent,
            defenderReportedLosses,
            defenderPercent,
            stalemateNote);
        Illustrations illustrations = GameObject.FindFirstObjectByType<Illustrations>();
        List<(string message, Color color)> hudMessages = new()
        {
            ($"{attackerName} loses {attackerReportedLosses} ({attackerPercent}%).", Color.red),
            ($"{defenderName} loses {defenderReportedLosses} ({defenderPercent}%).", Color.red)
        };
        Action onPopupClose = () => ShowHudMessagesAfterPopup(targetHex, hudActor, hudMessages);
        PopupManager.Show(
            title,
            illustrations.GetIllustrationByName(attackerName),
            illustrations.GetIllustrationByName(defenderArmy.GetCommander().characterName),
            text,
            true,
            onClose: onPopupClose);

        // Apply casualties to attacker troops
        ReceiveCasualties(attackerCasualtyPercent, defenderLeader, !anyAttackerCasualties && !forceCasualtySide);

        // Apply casualties to all defending armies based on their alignment
        ApplyCasualtiesToDefenders(targetHex, defenderCasualtyPercent, attackerAlignment, !anyDefenderCasualties && forceCasualtySide, attackerLeader);

        GrantCombatXp(this, "Combat");
        GrantCombatXp(defenderArmy, "Combat");

        // Check if attacker army was eliminated
        if (!killed && GetSize(true) < 1) Killed(defenderLeader);

        bool attackerAlive = !killed && GetSize(true) > 0;
        bool defendersRemain = AreDefendersStillAlive(targetHex, attackerAlignment, attackerLeader);
        string outcome = attackerAlive && !defendersRemain
            ? $"{attackerName} holds the field."
            : (!attackerAlive && defendersRemain
                ? $"{defenderName} repels the attack."
                : "The battle grinds on with no clear victor.");
        Color outcomeColor = attackerAlive && !defendersRemain ? Color.green : Color.yellow;
        hudMessages.Add((outcome, outcomeColor));

        int attackerXpDelta = xp - attackerXpBefore;
        int defenderXpDelta = defenderArmy.xp - defenderXpBefore;
        hudMessages.Add(($"XP gained: {attackerName} {FormatDelta(attackerXpDelta)}, {defenderName} {FormatDelta(defenderXpDelta)}", Color.cyan));
    }


    private string BuildBattleDescription(
        string location,
        string attackerName,
        string defenderName,
        int attackerAlliesJoined,
        int attackerAlliesStrength,
        int attackerAlliesDefence,
        int defenderAlliesJoined,
        int defenderAlliesStrength,
        int defenderAlliesDefence,
        int pcDefenseContribution,
        int attackerBonusPercent,
        int defenderBonusPercent,
        string attackerTrainingLabel,
        string defenderTrainingLabel,
        int attackerXp,
        int defenderXp,
        int attackerArtifactAttack,
        int attackerArtifactDefense,
        int defenderArtifactAttack,
        int defenderArtifactDefense,
        int attackerStrength,
        int defenderDefense,
        int defenderStrength,
        int attackerDefence,
        int attackerReportedLosses,
        int attackerPercent,
        int defenderReportedLosses,
        int defenderPercent,
        string stalemateNote)
    {
        StringBuilder sb = new StringBuilder();
        int template = UnityEngine.Random.Range(0, 5);

        void Allies(bool attackerSide)
        {
            if (attackerSide && attackerAlliesJoined > 0)
            {
                sb.AppendLine($"Allies surge in: {attackerAlliesJoined} host(s) add +{attackerAlliesStrength} STR / +{attackerAlliesDefence} DEF to {attackerName}'s line.");
            }
            if (!attackerSide && defenderAlliesJoined > 0)
            {
                sb.AppendLine($"Reinforcements rally to {defenderName}: {defenderAlliesJoined} contingent(s) lend +{defenderAlliesStrength} STR / +{defenderAlliesDefence} DEF.");
            }
        }

        void Artifacts()
        {
            if (attackerArtifactAttack + attackerArtifactDefense > 0 || defenderArtifactAttack + defenderArtifactDefense > 0)
            {
                sb.AppendLine($"Relics blaze: attacker +{attackerArtifactAttack} ATK / +{attackerArtifactDefense} DEF, defender +{defenderArtifactAttack} ATK / +{defenderArtifactDefense} DEF.");
            }
        }

        void Casualties()
        {
            sb.AppendLine($"Casualties mount: {attackerName} loses {attackerReportedLosses} ({attackerPercent}%), {defenderName} loses {defenderReportedLosses} ({defenderPercent}%).");
            if (!string.IsNullOrEmpty(stalemateNote)) sb.AppendLine(stalemateNote);
        }

        switch (template)
        {
            case 0:
                sb.AppendLine($"Under {attackerName}'s banner the host marches on {location}, while {defenderName} braces the shieldwall.");
                Allies(true);
                Allies(false);
                if (pcDefenseContribution > 0) sb.AppendLine($"Battlements thunder from {location}, adding {pcDefenseContribution} to defense and reply.");
                sb.AppendLine($"Command bites: {attackerName} drives +{attackerBonusPercent}% with {attackerTrainingLabel} troops (XP {attackerXp}); {defenderName} counters with +{defenderBonusPercent}% and {defenderTrainingLabel} (XP {defenderXp}).");
                Artifacts();
                sb.AppendLine($"Steel and muscle: {attackerStrength} attack slams {defenderDefense} defense, while {defenderStrength} hammers back at {attackerDefence}.");
                Casualties();
                break;
            case 1:
                sb.AppendLine($"{location} erupts as {attackerName} advances; {defenderName} locks ranks and answers.");
                if (pcDefenseContribution > 0) sb.AppendLine($"Stones and arrows rain from the walls (+{pcDefenseContribution} DEF).");
                Allies(true);
                Allies(false);
                sb.AppendLine($"Veterans steady the lines: attacker {attackerTrainingLabel} (XP {attackerXp}) +{attackerBonusPercent}%, defender {defenderTrainingLabel} (XP {defenderXp}) +{defenderBonusPercent}%.");
                Artifacts();
                sb.AppendLine($"Clash of totals: {attackerStrength} vs {defenderDefense}; riposte {defenderStrength} vs {attackerDefence}.");
                Casualties();
                break;
            case 2:
                sb.AppendLine($"{attackerName} presses into {location}, horns sounding. {defenderName} signals to hold the line.");
                Allies(true);
                if (pcDefenseContribution > 0) sb.AppendLine($"The settlement stiffens the defense (+{pcDefenseContribution}).");
                Allies(false);
                sb.AppendLine($"Leadership weighs heavy: +{attackerBonusPercent}% for {attackerName}, +{defenderBonusPercent}% for {defenderName}.");
                sb.AppendLine($"Training shows: {attackerTrainingLabel} (XP {attackerXp}) vs {defenderTrainingLabel} (XP {defenderXp}).");
                Artifacts();
                sb.AppendLine($"Totals collide: attack {attackerStrength} onto {defenderDefense}, counter {defenderStrength} into {attackerDefence}.");
                Casualties();
                break;
            case 3:
                sb.AppendLine($"{attackerName} drives a wedge toward {location}; {defenderName} pivots to meet the charge.");
                Allies(true);
                Allies(false);
                if (pcDefenseContribution > 0) sb.AppendLine($"Walls and militia strain, adding {pcDefenseContribution} to the stand.");
                sb.AppendLine($"Edge in command: attacker +{attackerBonusPercent}%, defender +{defenderBonusPercent}%.");
                Artifacts();
                sb.AppendLine($"Blades find gaps: attacker {attackerStrength} vs defense {defenderDefense}. Counterblow {defenderStrength} vs {attackerDefence}.");
                Casualties();
                break;
            default:
                sb.AppendLine($"Battle for {location}: {attackerName} leads the assault, {defenderName} anchors the defense.");
                Allies(true);
                Allies(false);
                if (pcDefenseContribution > 0) sb.AppendLine($"Local defenses join the fray (+{pcDefenseContribution}).");
                sb.AppendLine($"Morale and drill: {attackerTrainingLabel} (XP {attackerXp}) vs {defenderTrainingLabel} (XP {defenderXp}); commanders push +{attackerBonusPercent}% / +{defenderBonusPercent}%.");
                Artifacts();
                sb.AppendLine($"Numbers clash: {attackerStrength} onto {defenderDefense}; return strike {defenderStrength} into {attackerDefence}.");
                Casualties();
                break;
        }

        return sb.ToString();
    }

    private string BuildSiegeDescription(
        string location,
        string attackerName,
        string defenderName,
        int attackerStrength,
        int defenderDefense,
        int attackerDefence,
        int attackerBonusPercent,
        string attackerTrainingLabel,
        int attackerXp,
        int attackerArtifactAttack,
        int attackerArtifactDefense,
        int attackerTotalLosses,
        int attackerPercent,
        float attackerDamage,
        bool fortStands)
    {
        StringBuilder sb = new StringBuilder();
        int template = UnityEngine.Random.Range(0, 5);

        switch (template)
        {
            case 0:
                sb.AppendLine($"{attackerName} drives siege lines toward {location}, drums rolling as ladders rise.");
                sb.AppendLine($"The assault throws {attackerStrength} strength at defenses of {defenderDefense}; arrows and oil bite back against {attackerDefence}.");
                break;
            case 1:
                sb.AppendLine($"Siege of {location}: rams grind forward under {attackerName}'s shout while {defenderName} braces the parapet.");
                sb.AppendLine($"Pressure: {attackerStrength} vs {defenderDefense}; missiles lash the attackers (DEF {attackerDefence}).");
                break;
            case 2:
                sb.AppendLine($"{location} shakes as {attackerName} orders the climb. {defenderName} answers with volleys.");
                sb.AppendLine($"Strength meets stone: {attackerStrength} attacking, {defenderDefense} resisting; defenders strike back into {attackerDefence}.");
                break;
            case 3:
                sb.AppendLine($"{attackerName} hurls the host at {location}; mantlets and ladders grind ahead.");
                sb.AppendLine($"Force on the walls: {attackerStrength} against {defenderDefense}. Counterfire tests {attackerDefence}.");
                break;
            default:
                sb.AppendLine($"At {location}, {attackerName} raises a storm of iron while {defenderName} commands the ramparts.");
                sb.AppendLine($"Numbers collide: {attackerStrength} vs {defenderDefense}; the defense punishes {attackerDefence}.");
                break;
        }

        sb.AppendLine($"Command and drill hold: {attackerName} pushes a +{attackerBonusPercent}% edge with {attackerTrainingLabel} troops (XP {attackerXp}).");
        if (attackerArtifactAttack + attackerArtifactDefense > 0)
        {
            sb.AppendLine($"Relics flare at the breach: +{attackerArtifactAttack} ATK / +{attackerArtifactDefense} DEF lend their might.");
        }
        sb.AppendLine($"Losses bite hard: {attackerName} loses {attackerTotalLosses} ({attackerPercent}%).");
        if (attackerDamage > 0)
        {
            sb.AppendLine(fortStands ? "The walls crack and the defenders fall back a step." : "Gates burst and the population center falls to the attackers.");
        }
        else
        {
            sb.AppendLine("The gate holds; the attackers recoil beneath a storm of stones.");
        }

        return sb.ToString();
    }

    // Helper method to attack a Population Center

    private void AttackPopulationCenter(Hex targetHex, int attackerStrength, int attackerDefence, Leader attackerLeader)
    {
        if(targetHex.GetPC() == null) return;
        // No defending army, only a PC
        Leader defenderLeader = targetHex.GetPC().owner;
        bool defenderDeadOrMissing = defenderLeader == null || defenderLeader.killed;
        if(attackerLeader == null || attackerLeader.killed) return;
        string attackerName = commander != null ? commander.characterName : attackerLeader.characterName;
        int attackerXpBefore = xp;
        Character hudActor = commander;

        // Don't attack your own PC (check both alignment and owner)
        bool isOwnPC = commander && !commander.killed ? defenderLeader == commander.GetOwner() : false;
        if (isOwnPC) return;

        // If the defending leader is gone (null or killed), capture immediately
        if (defenderDeadOrMissing)
        {
            targetHex.GetPC().CapturePC(attackerLeader);
            MessageDisplayNoUI.ShowMessage(commander.hex, commander, $"{targetHex.GetPC().pcName} was captured", Color.green);
            return;
        }

        AlignmentEnum defenderAlignment = defenderLeader.GetAlignment();

        if(defenderAlignment == attackerLeader.GetAlignment()) return;

        int defenderDefense = targetHex.GetPC().GetDefense();
        int defenderStrength = defenderDefense; // PC defense is its strength for counter-attack
        int attackerCommanderLevel = commander != null ? commander.GetCommander() : 0;
        int attackerBonusPercent = Mathf.RoundToInt(Mathf.Clamp(attackerCommanderLevel, 0, 10) * 10f);

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

        int attackerPercent = Mathf.RoundToInt(attackerCasualtyPercent * 100f);
        int attackerArtifactAttack = GetArtifactAttackBonusTotal();
        int attackerArtifactDefense = GetArtifactDefenseBonusTotal();
        string pcTitle = $"Siege of {targetHex.GetPC().pcName}";
        string pcText = BuildSiegeDescription(
            targetHex.GetPC().pcName,
            attackerName,
            defenderLeader.characterName,
            attackerStrength,
            defenderDefense,
            attackerDefence,
            attackerBonusPercent,
            GetTrainingLabel(),
            xp,
            attackerArtifactAttack,
            attackerArtifactDefense,
            attackerTotalLosses,
            attackerPercent,
            attackerDamage,
            targetHex.GetPC().fortSize > FortSizeEnum.NONE);
        Illustrations pcIllustrations = GameObject.FindFirstObjectByType<Illustrations>();
        List<(string message, Color color)> hudMessages = new()
        {
            ($"{attackerName} loses {attackerTotalLosses} ({attackerPercent}%).", Color.red)
        };
        Action onPopupClose = () => ShowHudMessagesAfterPopup(targetHex, hudActor, hudMessages);
        PopupManager.Show(
            pcTitle,
            pcIllustrations.GetIllustrationByName(attackerName),
            pcIllustrations.GetIllustrationByName(defenderLeader.characterName),
            pcText,
            true,
            onClose: onPopupClose);

        // Apply casualties to attacker
        ReceiveCasualties(attackerCasualtyPercent, defenderLeader, !anyAttackerCasualties);

        // Process damage to the PC
        if (attackerDamage > 0)
        {
            if (targetHex.GetPC().fortSize > FortSizeEnum.NONE)
            {
                // Reduce fort size first
                targetHex.GetPC().DecreaseFort();
                MessageDisplayNoUI.ShowMessage(commander.hex, commander, $"Defenses at {targetHex.GetPC().pcName} were damaged", Color.yellow);
            }
            else
            {
                targetHex.GetPC().CapturePC(attackerLeader);
            }
        }
        else
        {
            MessageDisplayNoUI.ShowMessage(commander.hex, commander, $"{targetHex.GetPC().pcName} defenses resisted the attack", Color.red);
        }

        GrantCombatXp(this, attackerDamage > 0 ? "Siege success" : "Assault");

        // Check if attacker army was eliminated
        if (!killed && GetSize(true) < 1) Killed(defenderLeader);

        string outcome = attackerDamage > 0 ? $"{attackerName} seizes the walls." : $"{targetHex.GetPC().pcName} holds fast.";
        hudMessages.Add((outcome, attackerDamage > 0 ? Color.green : Color.yellow));

        int attackerXpDelta = xp - attackerXpBefore;
        hudMessages.Add(($"XP gained: {attackerName} {FormatDelta(attackerXpDelta)}", Color.cyan));
    }

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
            StringBuilder casualties = new StringBuilder();
            if (maCasualties > 0) casualties.Append($"<sprite name=\"ma\"/>[{maCasualties}]");
            if (arCasualties > 0) casualties.Append($"<sprite name=\"ar\"/>[{arCasualties}]");
            if (liCasualties > 0) casualties.Append($"<sprite name=\"li\"/>[{liCasualties}]");
            if (hiCasualties > 0) casualties.Append($"<sprite name=\"hi\"/>[{hiCasualties}]");
            if (lcCasualties > 0) casualties.Append($"<sprite name=\"lc\"/>[{lcCasualties}]");
            if (hcCasualties > 0) casualties.Append($"<sprite name=\"hc\"/>[{hcCasualties}]");
            if (caCasualties > 0) casualties.Append($"<sprite name=\"ca\"/>[{caCasualties}]");
            if (wsCasualties > 0) casualties.Append($"<sprite name=\"ws\"/>[{wsCasualties}]");
            MessageDisplayNoUI.ShowMessage(commander.hex, commander, $"{commander.characterName} army casualties: {casualties}", Color.red);
        }
    }

    private void ShowHudMessagesAfterPopup(Hex hex, Character actor, List<(string message, Color color)> messages)
    {
        if (hex == null || actor == null || messages == null) return;
        foreach (var entry in messages)
        {
            MessageDisplayNoUI.ShowMessage(hex, actor, entry.message, entry.color);
        }
    }

    private bool AreDefendersStillAlive(Hex targetHex, AlignmentEnum attackerAlignment, Leader attackerLeader)
    {
        return targetHex.armies.Any(army =>
        {
            if (army == null || army.killed || army.GetSize(true) < 1 || army.commander == null) return false;
            bool isOwnArmy = army.commander.GetOwner() == attackerLeader;
            if (isOwnArmy) return false;
            return army.GetAlignment() != attackerAlignment || attackerAlignment == AlignmentEnum.neutral || army.GetAlignment() == AlignmentEnum.neutral;
        });
    }

    private string FormatDelta(int delta)
    {
        if (delta > 0) return $"+{delta}";
        if (delta < 0) return delta.ToString();
        return "0";
    }

    public Character GetCommander()
    {
        if(commander.killed) return null;
        return commander;
    }

    public int GetMaintenanceCost()
    {
        if (startingArmy) return 0;
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
