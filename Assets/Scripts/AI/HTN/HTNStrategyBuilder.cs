// ---------------------------------------------------------------------------
// Hardcoded fallback strategy, used when no authored Strategies.json applies.
// Matches the default strategy shape shown in the AI Widget's Strategies tab.
//
// Situations-first shape: the root's highest-priority branches are cross-cutting situations
// (ImmediateDanger, Danger, Economic distress, low NPLs, low artifacts) with per-domain response
// Methods underneath, rather than per-domain "Viability" aggregates gating everything. Since
// HTNPlanner.Decompose runs independently per character (each controlled Character gets its own
// UtilityAIContext/CharacterBlackboard, built from that character's own position) and backtracks
// past a Method whose leaf has no role-eligible card, a shared situation like ImmediateDanger
// naturally routes a commander-character to the Militaristic response and an agent-character to
// the Intelligence response for free — no per-character dispatch logic needed.
// ---------------------------------------------------------------------------

public static class HTNStrategyBuilder
{
    public static HTNCompoundTask BuildDefault()
    {
        HTNRegistry.TryGetPredicate("Global.ImmediateDanger", out var immediateDanger);
        HTNRegistry.TryGetPredicate("Militaristic.Danger", out var danger);
        HTNRegistry.TryGetPredicate("Economic.Critical", out var economyCritical);
        HTNRegistry.TryGetPredicate("Economic.Weak", out var economyWeak);
        HTNRegistry.TryGetPredicate("Diplomatic.LowNplsReady", out var lowNplsReady);
        HTNRegistry.TryGetPredicate("Artifacts.ArtifactScarcityReady", out var artifactScarcityReady);
        HTNRegistry.TryGetPredicate("Militaristic.OffenseWinRatioReady", out var offenseWinRatioReady);
        HTNRegistry.TryGetPredicate("Intelligence.HighValueEnemyCharacterReady", out var highValueEnemyCharacterReady);
        HTNRegistry.TryGetPredicate("Intelligence.EnemyPcVulnerabilityReady", out var enemyPcVulnerabilityReady);
        HTNRegistry.TryGetPredicate("Diplomatic.NplsNearReady", out var nplsNearReady);
        HTNRegistry.TryGetPredicate("Diplomatic.NplsMidReady", out var nplsMidReady);
        HTNRegistry.TryGetPredicate("Diplomatic.EnemyPcOpportunityNearReady", out var enemyPcOpportunityNearReady);
        HTNRegistry.TryGetPredicate("Diplomatic.EnemyPcOpportunityMidReady", out var enemyPcOpportunityMidReady);
        HTNRegistry.TryGetPredicate("Diplomatic.OwnPcLoyaltyRiskReady", out var ownPcLoyaltyRiskReady);
        HTNRegistry.TryGetPredicate("Artifacts.ArtifactTransferReady", out var artifactTransferReady);
        HTNRegistry.TryGetPredicate("Logistics.HealingNeedReady", out var healingNeedReady);
        HTNRegistry.TryGetPredicate("Global.Always", out var always);
        HTNRegistry.TryGetPredicate("Global.Never", out var never);

        // Danger-tier pick predicates, shared by both root.immediatedanger and root.danger below.
        HTNRegistry.TryGetPredicate("Militaristic.OwnPcFortificationNeedReady", out var fortificationNeedReady);
        HTNRegistry.TryGetPredicate("Intelligence.EnemyCharacterReady", out var enemyCharacterReady);
        HTNRegistry.TryGetPredicate("Militaristic.DuelOpportunityReady", out var duelOpportunityReady);
        HTNRegistry.TryGetPredicate("Militaristic.SongDuelOpportunityReady", out var songDuelOpportunityReady);

        HTNRegistry.TryGetPredicate("Disruption.EnemyPressureReady", out var disruptionPressureReady);

        HTNRegistry.TryGetPredicate("Economic.MithrilReady", out var mithrilReady);
        HTNRegistry.TryGetPredicate("Economic.SteelReady", out var steelReady);
        HTNRegistry.TryGetPredicate("Economic.IronReady", out var ironReady);
        HTNRegistry.TryGetPredicate("Economic.MountsReady", out var mountsReady);
        HTNRegistry.TryGetPredicate("Economic.TimberReady", out var timberReady);
        HTNRegistry.TryGetPredicate("Economic.LeatherReady", out var leatherReady);

        // ---------------------------------------------------------------------------------
        // Shared danger-tier pick, built once and reused (with distinct TaskIds) for both
        // root.immediatedanger and root.danger — a specific under-fortified own PC takes
        // priority over a generic response; then Intelligence's response to the same threat
        // (wound/assassinate whoever is bearing down); then personal combat (Duel/BattleOfSongs)
        // ONLY when the win-probability margin is comfortable (never a suicidal last stand);
        // finally a generic emergency conscript. "Specific opportunity before generic fallback",
        // same shape as every other pick below.
        // ---------------------------------------------------------------------------------
        HTNCompoundTask BuildDangerPick(string prefix)
        {
            HTNPrimitiveTask fortifyLeaf = new()
            {
                TaskId = $"{prefix}.fortify.leaf",
                Precondition = always,
                CompletionCondition = never,
                PreferredParameters = new() { UtilityAIParameters.MilitaristicOwnPcFortificationNeed }
            };
            HTNMethod fortifyMethod = new() { TaskId = $"{prefix}.fortify", Precondition = fortificationNeedReady };
            fortifyMethod.Subtasks.Add(fortifyLeaf);

            HTNPrimitiveTask intelligenceLeaf = new()
            {
                TaskId = $"{prefix}.intelligence.leaf",
                Precondition = always,
                CompletionCondition = never,
                PreferredParameters = new() { UtilityAIParameters.IntelligenceEnemyCharacter, UtilityAIParameters.IntelligenceIndirectSafety, UtilityAIParameters.LogisticsReachEnemyCharacter }
            };
            HTNMethod intelligenceMethod = new() { TaskId = $"{prefix}.intelligence", Precondition = enemyCharacterReady };
            intelligenceMethod.Subtasks.Add(intelligenceLeaf);

            HTNPrimitiveTask duelLeaf = new()
            {
                TaskId = $"{prefix}.duel.leaf",
                Precondition = always,
                CompletionCondition = never,
                PreferredParameters = new() { UtilityAIParameters.MilitaristicDuelAdvantage }
            };
            HTNMethod duelMethod = new() { TaskId = $"{prefix}.duel", Precondition = duelOpportunityReady };
            duelMethod.Subtasks.Add(duelLeaf);

            HTNPrimitiveTask songDuelLeaf = new()
            {
                TaskId = $"{prefix}.songduel.leaf",
                Precondition = always,
                CompletionCondition = never,
                PreferredParameters = new() { UtilityAIParameters.MilitaristicSongDuelAdvantage }
            };
            HTNMethod songDuelMethod = new() { TaskId = $"{prefix}.songduel", Precondition = songDuelOpportunityReady };
            songDuelMethod.Subtasks.Add(songDuelLeaf);

            HTNPrimitiveTask conscriptLeaf = new()
            {
                TaskId = $"{prefix}.conscript.leaf",
                Precondition = always,
                CompletionCondition = never,
                PreferredParameters = new() { UtilityAIParameters.MilitaristicOwnPcDefenderNeed }
            };
            HTNMethod conscriptMethod = new() { TaskId = $"{prefix}.conscript", Precondition = always };
            conscriptMethod.Subtasks.Add(conscriptLeaf);

            HTNCompoundTask pick = new() { TaskId = prefix };
            pick.Methods.Add(fortifyMethod);
            pick.Methods.Add(intelligenceMethod);
            pick.Methods.Add(duelMethod);
            pick.Methods.Add(songDuelMethod);
            pick.Methods.Add(conscriptMethod);
            return pick;
        }

        // 1. root.immediatedanger: tightest-radius, highest-priority tier — see
        // UtilityAIContext.IsImmediateDanger / Targeting.ImmediateDangerDistance.
        HTNMethod immediateDangerMethod = new() { TaskId = "root.immediatedanger", Precondition = immediateDanger };
        immediateDangerMethod.Subtasks.Add(BuildDangerPick("root.immediatedanger.pick"));

        // 2. root.danger: wider-radius tier, today's existing Militaristic.Danger formula.
        HTNMethod dangerMethod = new() { TaskId = "root.danger", Precondition = danger };
        dangerMethod.Subtasks.Add(BuildDangerPick("root.danger.pick"));

        // 3. root.recover: verbatim, unchanged — one mission per tradeable material, insufficient
        // stock biases toward that material's Buy{X} card, surplus biases toward Sell{X} (both via
        // PreferredParameters on the same leaf; the underlying utility math, not branch priority,
        // decides which of the two actually wins). Ordered by StoresManager trade value descending.
        HTNPrimitiveTask recoverMithrilLeaf = new()
        {
            TaskId = "root.recover.pick.mithril.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.EconomicMithrilInsufficient, UtilityAIParameters.EconomicMithrilSurplus }
        };
        HTNMethod recoverMithrilMethod = new() { TaskId = "root.recover.pick.mithril", Precondition = mithrilReady };
        recoverMithrilMethod.Subtasks.Add(recoverMithrilLeaf);

        HTNPrimitiveTask recoverSteelLeaf = new()
        {
            TaskId = "root.recover.pick.steel.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.EconomicSteelInsufficient, UtilityAIParameters.EconomicSteelSurplus }
        };
        HTNMethod recoverSteelMethod = new() { TaskId = "root.recover.pick.steel", Precondition = steelReady };
        recoverSteelMethod.Subtasks.Add(recoverSteelLeaf);

        HTNPrimitiveTask recoverIronLeaf = new()
        {
            TaskId = "root.recover.pick.iron.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.EconomicIronInsufficient, UtilityAIParameters.EconomicIronSurplus }
        };
        HTNMethod recoverIronMethod = new() { TaskId = "root.recover.pick.iron", Precondition = ironReady };
        recoverIronMethod.Subtasks.Add(recoverIronLeaf);

        HTNPrimitiveTask recoverMountsLeaf = new()
        {
            TaskId = "root.recover.pick.mounts.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.EconomicMountsInsufficient, UtilityAIParameters.EconomicMountsSurplus }
        };
        HTNMethod recoverMountsMethod = new() { TaskId = "root.recover.pick.mounts", Precondition = mountsReady };
        recoverMountsMethod.Subtasks.Add(recoverMountsLeaf);

        HTNPrimitiveTask recoverTimberLeaf = new()
        {
            TaskId = "root.recover.pick.timber.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.EconomicTimberInsufficient, UtilityAIParameters.EconomicTimberSurplus }
        };
        HTNMethod recoverTimberMethod = new() { TaskId = "root.recover.pick.timber", Precondition = timberReady };
        recoverTimberMethod.Subtasks.Add(recoverTimberLeaf);

        HTNPrimitiveTask recoverLeatherLeaf = new()
        {
            TaskId = "root.recover.pick.leather.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.EconomicLeatherInsufficient, UtilityAIParameters.EconomicLeatherSurplus }
        };
        HTNMethod recoverLeatherMethod = new() { TaskId = "root.recover.pick.leather", Precondition = leatherReady };
        recoverLeatherMethod.Subtasks.Add(recoverLeatherLeaf);

        HTNPrimitiveTask recoverFallbackLeaf = new()
        {
            TaskId = "root.recover.pick.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.EconomicLiquidWealth }
        };
        HTNMethod recoverFallbackMethod = new() { TaskId = "root.recover.pick.fallback", Precondition = always };
        recoverFallbackMethod.Subtasks.Add(recoverFallbackLeaf);

        HTNCompoundTask recoverPick = new() { TaskId = "root.recover.pick" };
        recoverPick.Methods.Add(recoverMithrilMethod);
        recoverPick.Methods.Add(recoverSteelMethod);
        recoverPick.Methods.Add(recoverIronMethod);
        recoverPick.Methods.Add(recoverMountsMethod);
        recoverPick.Methods.Add(recoverTimberMethod);
        recoverPick.Methods.Add(recoverLeatherMethod);
        recoverPick.Methods.Add(recoverFallbackMethod);

        HTNMethod recoverMethod = new() { TaskId = "root.recover", Precondition = HTNRegistry.Or(economyCritical, economyWeak) };
        recoverMethod.Subtasks.Add(recoverPick);

        // 4. root.diplomacy.lownpls: board-wide NPL scarcity — a wide-radius recruit push,
        // regardless of proximity, when few same-alignment NPLs remain to recruit at all.
        HTNPrimitiveTask lowNplsLeaf = new()
        {
            TaskId = "root.diplomacy.lownpls.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.DiplomaticNplRecruitment, UtilityAIParameters.DiplomaticNplScarcity }
        };
        HTNMethod lowNplsMethod = new() { TaskId = "root.diplomacy.lownpls", Precondition = lowNplsReady };
        lowNplsMethod.Subtasks.Add(lowNplsLeaf);

        // 5. root.artifacts.lowartifacts: relocation of the old root.magic.pick.retrieve —
        // artifact scarcity + hidden-artifact search, mage-hire bias.
        HTNPrimitiveTask lowArtifactsLeaf = new()
        {
            TaskId = "root.artifacts.lowartifacts.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.ArtifactsArtifactScarcity, UtilityAIParameters.ArtifactsHiddenArtifacts }
        };
        HTNMethod lowArtifactsMethod = new() { TaskId = "root.artifacts.lowartifacts", Precondition = artifactScarcityReady };
        lowArtifactsMethod.Subtasks.Add(lowArtifactsLeaf);

        // 6. root.offense: hard win-probability gate (Militaristic.OffenseWinRatioReady) replaces
        // the old fuzzy Militaristic.Viable — a losing/marginal matchup never reaches Attack.
        // fortify (specific opportunity) -> disrupt (deny the enemy, folded in from the old
        // standalone Disruption domain) -> attack (generic fallback). Disrupt MUST precede
        // attack: attack's leaf is Global.Always, so Decompose would never try disrupt otherwise.
        HTNPrimitiveTask offenseFortifyLeaf = new()
        {
            TaskId = "root.offense.pick.fortify.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.MilitaristicOwnPcFortificationNeed }
        };
        HTNMethod offenseFortifyMethod = new() { TaskId = "root.offense.pick.fortify", Precondition = fortificationNeedReady };
        offenseFortifyMethod.Subtasks.Add(offenseFortifyLeaf);

        HTNPrimitiveTask offenseDisruptLeaf = new()
        {
            TaskId = "root.offense.pick.disrupt.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.DisruptionEnemyPressure }
        };
        HTNMethod offenseDisruptMethod = new() { TaskId = "root.offense.pick.disrupt", Precondition = disruptionPressureReady };
        offenseDisruptMethod.Subtasks.Add(offenseDisruptLeaf);

        HTNPrimitiveTask offenseAttackLeaf = new()
        {
            TaskId = "root.offense.pick.attack.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.MilitaristicMilitaryEdge, UtilityAIParameters.MilitaristicEnemyPressure, UtilityAIParameters.LogisticsInterceptEnemy }
        };
        HTNMethod offenseAttackMethod = new() { TaskId = "root.offense.pick.attack", Precondition = always };
        offenseAttackMethod.Subtasks.Add(offenseAttackLeaf);

        HTNCompoundTask offensePick = new() { TaskId = "root.offense.pick" };
        offensePick.Methods.Add(offenseFortifyMethod);
        offensePick.Methods.Add(offenseDisruptMethod);
        offensePick.Methods.Add(offenseAttackMethod);

        HTNMethod offenseMethod = new() { TaskId = "root.offense", Precondition = offenseWinRatioReady };
        offenseMethod.Subtasks.Add(offensePick);

        // 7. root.intelligence.offense: today's highvalue/sabotage picks, promoted out from under
        // the old fuzzy Intelligence.Viable gate to an explicit "not in danger and a qualifying
        // target exists" gate (Or(...) composed inline, same idiom root.recover uses above).
        HTNPrimitiveTask intelligenceHighValueLeaf = new()
        {
            TaskId = "root.intelligence.offense.pick.highvalue.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.IntelligenceHighValueEnemyCharacter, UtilityAIParameters.LogisticsReachEnemyCharacter }
        };
        HTNMethod intelligenceHighValueMethod = new() { TaskId = "root.intelligence.offense.pick.highvalue", Precondition = highValueEnemyCharacterReady };
        intelligenceHighValueMethod.Subtasks.Add(intelligenceHighValueLeaf);

        HTNPrimitiveTask intelligenceSabotageLeaf = new()
        {
            TaskId = "root.intelligence.offense.pick.sabotage.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.IntelligenceEnemyPcVulnerability, UtilityAIParameters.LogisticsInterceptEnemy }
        };
        HTNMethod intelligenceSabotageMethod = new() { TaskId = "root.intelligence.offense.pick.sabotage", Precondition = enemyPcVulnerabilityReady };
        intelligenceSabotageMethod.Subtasks.Add(intelligenceSabotageLeaf);

        HTNPrimitiveTask intelligenceOffenseFallbackLeaf = new()
        {
            TaskId = "root.intelligence.offense.pick.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
        };
        HTNMethod intelligenceOffenseFallbackMethod = new() { TaskId = "root.intelligence.offense.pick.fallback", Precondition = always };
        intelligenceOffenseFallbackMethod.Subtasks.Add(intelligenceOffenseFallbackLeaf);

        HTNCompoundTask intelligenceOffensePick = new() { TaskId = "root.intelligence.offense.pick" };
        intelligenceOffensePick.Methods.Add(intelligenceHighValueMethod);
        intelligenceOffensePick.Methods.Add(intelligenceSabotageMethod);
        intelligenceOffensePick.Methods.Add(intelligenceOffenseFallbackMethod);

        HTNMethod intelligenceOffenseMethod = new() { TaskId = "root.intelligence.offense", Precondition = HTNRegistry.Or(highValueEnemyCharacterReady, enemyPcVulnerabilityReady) };
        intelligenceOffenseMethod.Subtasks.Add(intelligenceOffensePick);

        // 8-11. root.diplomacy.nplsnear/nplsmid/enemiesnear/enemiesmid: near/mid distance banding
        // of the existing continuous DiplomaticNplRecruitment/DiplomaticEnemyPcOpportunity
        // proximity signals — two discrete HTN priority tiers instead of one fading score.
        HTNPrimitiveTask nplsNearLeaf = new()
        {
            TaskId = "root.diplomacy.nplsnear.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.DiplomaticNplRecruitment }
        };
        HTNMethod nplsNearMethod = new() { TaskId = "root.diplomacy.nplsnear", Precondition = nplsNearReady };
        nplsNearMethod.Subtasks.Add(nplsNearLeaf);

        HTNPrimitiveTask nplsMidLeaf = new()
        {
            TaskId = "root.diplomacy.nplsmid.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.DiplomaticNplRecruitment }
        };
        HTNMethod nplsMidMethod = new() { TaskId = "root.diplomacy.nplsmid", Precondition = nplsMidReady };
        nplsMidMethod.Subtasks.Add(nplsMidLeaf);

        HTNPrimitiveTask enemiesNearLeaf = new()
        {
            TaskId = "root.diplomacy.enemiesnear.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.DiplomaticEnemyPcOpportunity }
        };
        HTNMethod enemiesNearMethod = new() { TaskId = "root.diplomacy.enemiesnear", Precondition = enemyPcOpportunityNearReady };
        enemiesNearMethod.Subtasks.Add(enemiesNearLeaf);

        HTNPrimitiveTask enemiesMidLeaf = new()
        {
            TaskId = "root.diplomacy.enemiesmid.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.DiplomaticEnemyPcOpportunity }
        };
        HTNMethod enemiesMidMethod = new() { TaskId = "root.diplomacy.enemiesmid", Precondition = enemyPcOpportunityMidReady };
        enemiesMidMethod.Subtasks.Add(enemiesMidLeaf);

        // 12. root.diplomacy.shore: relocation of the old root.diplomacy.pick.shore, unchanged.
        HTNPrimitiveTask shoreLeaf = new()
        {
            TaskId = "root.diplomacy.shore.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.DiplomaticOwnPcLoyaltyRisk }
        };
        HTNMethod shoreMethod = new() { TaskId = "root.diplomacy.shore", Precondition = ownPcLoyaltyRiskReady };
        shoreMethod.Subtasks.Add(shoreLeaf);

        // 13. root.artifacts.surplus: "mages have many artifacts" — consolidate/protect via
        // TransferArtifact. Artifacts.ArtifactTransferReady existed in HTNRegistry before this
        // change but was never wired to a Method — promoted from orphaned to a real gate here.
        HTNPrimitiveTask artifactSurplusLeaf = new()
        {
            TaskId = "root.artifacts.surplus.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.ArtifactsArtifactTransfer }
        };
        HTNMethod artifactSurplusMethod = new() { TaskId = "root.artifacts.surplus", Precondition = artifactTransferReady };
        artifactSurplusMethod.Subtasks.Add(artifactSurplusLeaf);

        // 14. root.militaristic.build: no danger, no offense-ready — proactively build up
        // instead of only ever reacting. Also home for the folded-in Logistics healing-support
        // leaf (the other two Logistics "reach" signals ride along on the offense/build leaves'
        // own PreferredParameters above/below instead of a standalone Logistics domain).
        HTNPrimitiveTask buildHealLeaf = new()
        {
            TaskId = "root.militaristic.build.pick.heal.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.LogisticsHealingNeed }
        };
        HTNMethod buildHealMethod = new() { TaskId = "root.militaristic.build.pick.heal", Precondition = healingNeedReady };
        buildHealMethod.Subtasks.Add(buildHealLeaf);

        HTNPrimitiveTask buildFortifyLeaf = new()
        {
            TaskId = "root.militaristic.build.pick.fortify.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.MilitaristicOwnPcFortificationNeed }
        };
        HTNMethod buildFortifyMethod = new() { TaskId = "root.militaristic.build.pick.fortify", Precondition = fortificationNeedReady };
        buildFortifyMethod.Subtasks.Add(buildFortifyLeaf);

        HTNPrimitiveTask buildConscriptLeaf = new()
        {
            TaskId = "root.militaristic.build.pick.conscript.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.MilitaristicOwnPcDefenderNeed }
        };
        HTNMethod buildConscriptMethod = new() { TaskId = "root.militaristic.build.pick.conscript", Precondition = always };
        buildConscriptMethod.Subtasks.Add(buildConscriptLeaf);

        HTNCompoundTask militaristicBuildPick = new() { TaskId = "root.militaristic.build.pick" };
        militaristicBuildPick.Methods.Add(buildHealMethod);
        militaristicBuildPick.Methods.Add(buildFortifyMethod);
        militaristicBuildPick.Methods.Add(buildConscriptMethod);

        HTNMethod militaristicBuildMethod = new() { TaskId = "root.militaristic.build", Precondition = always };
        militaristicBuildMethod.Subtasks.Add(militaristicBuildPick);

        // 15. root.intelligence.build: generic recon fallback — also where the orphaned
        // LogisticsReachNpc parameter lands (scouting toward a still-hidden NPC is distinct from
        // DiplomaticNplRecruitment's "recruit an already-visible capital" target).
        HTNPrimitiveTask intelligenceBuildLeaf = new()
        {
            TaskId = "root.intelligence.build.leaf",
            Precondition = always,
            CompletionCondition = never,
            PreferredParameters = new() { UtilityAIParameters.IntelligenceEnemyCharacter, UtilityAIParameters.LogisticsReachNpc }
        };
        HTNMethod intelligenceBuildMethod = new() { TaskId = "root.intelligence.build", Precondition = always };
        intelligenceBuildMethod.Subtasks.Add(intelligenceBuildLeaf);

        // 16. root.fallback: unchanged.
        HTNPrimitiveTask fallbackLeaf = new()
        {
            TaskId = "root.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
        };
        HTNMethod fallbackMethod = new() { TaskId = "root.fallback", Precondition = always };
        fallbackMethod.Subtasks.Add(fallbackLeaf);

        HTNCompoundTask root = new() { TaskId = "root" };
        root.Methods.Add(immediateDangerMethod);
        root.Methods.Add(dangerMethod);
        root.Methods.Add(recoverMethod);
        root.Methods.Add(lowNplsMethod);
        root.Methods.Add(lowArtifactsMethod);
        root.Methods.Add(offenseMethod);
        root.Methods.Add(intelligenceOffenseMethod);
        root.Methods.Add(nplsNearMethod);
        root.Methods.Add(nplsMidMethod);
        root.Methods.Add(enemiesNearMethod);
        root.Methods.Add(enemiesMidMethod);
        root.Methods.Add(shoreMethod);
        root.Methods.Add(artifactSurplusMethod);
        root.Methods.Add(militaristicBuildMethod);
        root.Methods.Add(intelligenceBuildMethod);
        root.Methods.Add(fallbackMethod);
        return root;
    }
}
