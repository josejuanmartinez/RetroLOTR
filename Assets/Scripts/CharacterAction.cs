using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GraphicRaycaster))]
public class CharacterAction : MonoBehaviour
{
    [Header("IDs")]
    public string actionName;
    public int actionId;
    [HideInInspector] public Character character;

    [Header("Rendering")]
    [HideInInspector]
    public string actionInitials;
    public Sprite actionSprite;
    public Color actionColor;

    [Header("Game Objects")]
    public Image background;
    public Button button;
    public TextMeshProUGUI textUI;

    [Header("Tooltip")]
    public GameObject hoverPrefab;

    [Header("Required skill")]
    public int difficulty = 0;
    public int commanderSkillRequired;
    public int agentSkillRequired;
    public int emissarySkillRequired;
    public int mageSkillRequired;

    [Header("Cost")]
    public int leatherCost;
    public int mountsCost;
    public int timberCost;
    public int ironCost;
    public int mithrilCost;
    public int goldCost;

    [Header("XP")]
    public int commanderXP;
    public int agentXP;
    public int emmissaryXP;
    public int mageXP;

    [Header("Reward")]
    public int reward = 1;

    [Header("Availability")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> condition;

    [Header("Effect")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> effect;

    private Game game;
    void Awake()
    {
        game = FindAnyObjectByType<Game>();
        GameObject hoverInstance = Instantiate(hoverPrefab, button.transform);
        hoverInstance.GetComponent<Hover>().Initialize(actionName, Vector2.one * 40, 35, TextAlignmentOptions.Center);

        actionInitials = gameObject.name.ToUpper();
        background.color = actionColor;
        if (actionSprite)
        {
            background.sprite = actionSprite;
            textUI.text = "";
        } else
        {
            textUI.text = actionInitials.ToUpper();
        }

        button.gameObject.SetActive(false);

    }


    public virtual void Initialize(Character character, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalCondition = condition;
        this.character = character;
        this.condition = (character) => { return ResourcesAvailable() && (originalCondition == null || originalCondition(character)); };
        this.effect = effect;
        button.gameObject.SetActive(this.condition(character));
    }

    public bool FulfillsConditions()
    {
        return condition(character);
    }

    public void Reset()
    {
        character = null;
        condition = null;
        effect = null;
        button.gameObject.SetActive(false);
    }

    public bool ResourcesAvailable()
    {
        if (character.hasActionedThisTurn) return false;
        if (commanderSkillRequired > 0 && character.GetCommander() < commanderSkillRequired) return false;
        if (agentSkillRequired > 0 && character.GetAgent() < agentSkillRequired) return false;
        if (emissarySkillRequired > 0 && character.GetEmmissary() < emissarySkillRequired) return false;
        if (mageSkillRequired > 0 && character.GetMage() < mageSkillRequired) return false;

        if (leatherCost > 0 && character.GetOwner().leatherAmount < leatherCost) return false;
        if (timberCost > 0 && character.GetOwner().timberAmount < timberCost) return false;
        if (mountsCost > 0 && character.GetOwner().mountsAmount < mountsCost) return false;
        if (ironCost > 0 && character.GetOwner().ironAmount < ironCost) return false;
        if (mithrilCost > 0 && character.GetOwner().mithrilAmount < mithrilCost) return false;
        if (goldCost > 0 && character.GetOwner().goldAmount < goldCost) return false;

        return true;
    }
    public void Execute()
    {
        bool isAI = character.isPlayerControlled;
        try
        {
            // All characters
            character.hasActionedThisTurn = true;
            if (!isAI) FindFirstObjectByType<ActionsManager>().Refresh(character);

            if (UnityEngine.Random.Range(0, 100) < difficulty || !effect(character))
            {
                if (!isAI) MessageDisplay.ShowMessage($"{actionName} failed", Color.red);
                return;
            }

            character.AddCommander(UnityEngine.Random.Range(0, 100) < commanderXP ? 1 : 0);
            character.AddAgent(UnityEngine.Random.Range(0, 100) < agentXP ? 1 : 0);
            character.AddEmmissary(UnityEngine.Random.Range(0, 100) < emmissaryXP ? 1 : 0);
            character.AddMage(UnityEngine.Random.Range(0, 100) < mageXP ? 1 : 0);

            FindFirstObjectByType<SelectedCharacterIcon>().Refresh(character);

            character.GetOwner().RemoveLeather(leatherCost);
            character.GetOwner().RemoveTimber(timberCost);
            character.GetOwner().RemoveMounts(mountsCost);
            character.GetOwner().RemoveIron(ironCost);
            character.GetOwner().RemoveMithril(mithrilCost);
            character.GetOwner().RemoveGold(goldCost);

            game.MoveToNextCharacterToAction();

            FindFirstObjectByType<StoresManager>().RefreshStores();

            if (character.GetOwner() is not PlayableLeader) return;

            // Now check influence in NPCs
            FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).Where(x => x != character.GetOwner()).ToList().ForEach(x =>
            {
                x.CheckActionConditionAnywhere(character.GetOwner(), this);
            });

            if (character.hex.GetPC() != null && character.hex.GetPC().owner is NonPlayableLeader && character.hex.GetPC().owner != character.GetOwner())
            {
                NonPlayableLeader nonPlayableLeader = character.hex.GetPC().owner as NonPlayableLeader;
                if (nonPlayableLeader == null) return;
                nonPlayableLeader.CheckActionConditionAtCapital(character.GetOwner(), this);
            }
            return;
        }
        catch (Exception e)
        {
            Debug.LogError($"{character.characterName} was unable to Execute action {actionName} {actionId} {actionInitials} {e}");
            character.hasActionedThisTurn = true;
            return;
        }
        
    }

    public bool ExecuteAI()
    {
        character.hasActionedThisTurn = true;

        if (UnityEngine.Random.Range(0, 100) < difficulty || !effect(character))
        {
            return false;
        }

        character.AddCommander(UnityEngine.Random.Range(0, 100) < commanderXP ? 1 : 0);
        character.AddAgent(UnityEngine.Random.Range(0, 100) < agentXP ? 1 : 0);
        character.AddEmmissary(UnityEngine.Random.Range(0, 100) < emmissaryXP ? 1 : 0);
        character.AddMage(UnityEngine.Random.Range(0, 100) < mageXP ? 1 : 0);

        character.GetOwner().RemoveLeather(leatherCost);
        character.GetOwner().RemoveTimber(timberCost);
        character.GetOwner().RemoveMounts(mountsCost);
        character.GetOwner().RemoveIron(ironCost);
        character.GetOwner().RemoveMithril(mithrilCost);
        character.GetOwner().RemoveGold(goldCost);

        if (character.GetOwner() is not PlayableLeader) return true;
        FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).Where(x => x != character.GetOwner()).ToList().ForEach(x =>
        {
            x.CheckActionConditionAnywhere(character.GetOwner(), this);
        });

        if (character.hex.GetPC() != null && character.hex.GetPC().owner is NonPlayableLeader && character.hex.GetPC().owner != character.GetOwner())
        {
            NonPlayableLeader nonPlayableLeader = character.hex.GetPC().owner as NonPlayableLeader;
            if (nonPlayableLeader == null) return true;
            nonPlayableLeader.CheckActionConditionAtCapital(character.GetOwner(), this);
        }

        return true;
    }

    protected Character FindEnemyCharacterTargetAtHex(Character assassin)
    {
        Character target = FindEnemyNonNeutralCharactersAtHexNoLeader(assassin);
        if (target) return target;
        target = FindEnemyCharactersAtHexNoLeaders(assassin);
        if (target) return target;
        target = FindEnemyNonNeutralCharactersAtHex(assassin);
        if (target) return target;
        target = FindEnemyCharactersAtHex(assassin);
        return target;
    }
    protected Character FindEnemyNonNeutralCharactersAtHexNoLeader(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            x.GetAlignment() != c.GetAlignment() &&
            x.GetAlignment() != AlignmentEnum.neutral &&
            x is not PlayableLeader
        );
    }
    protected Character FindEnemyCharactersAtHexNoLeaders(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment()) && (x is not PlayableLeader)
        );
    }

    protected Character FindEnemyNonNeutralCharactersAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            x.GetAlignment() != c.GetAlignment() &&
            x.GetAlignment() != AlignmentEnum.neutral
        );
    }

    protected Character FindEnemyCharactersAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
    }

    protected Army FindEnemyArmyNotNeutralAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        Character commander = c.hex.characters.Find(
            x => x.IsArmyCommander() && x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() != AlignmentEnum.neutral && x.GetAlignment() != c.GetAlignment())
        );
        if (commander != null && commander.GetArmy() != null) return commander.GetArmy();
        return null;
    }

    protected PC FindEnemyTargetPCInRange(List<Hex> hexes, Character character)
    {
        // Get potential targets from each category and track their priority
        List<TargetScore> allCandidates = new List<TargetScore>();

        // Category 1: Non-neutral PCs that aren't capitals (highest priority)
        PC target1 = FindNonNeutralEnemyTargetPCNoCapitalInRange(hexes, character);
        if (target1 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target1.hex.v2);
            float defense = target1.GetDefense();
            float score = distance + defense;
            allCandidates.Add(new TargetScore
            {
                Target = target1,
                TotalScore = score,
                CategoryPriority = 1 // Highest priority
            });
        }

        // Category 2: Any PC that isn't a capital
        PC target2 = FindEnemyTargetPCNoCapitalsInRange(hexes, character);
        if (target2 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target2.hex.v2);
            float defense = target2.GetDefense();
            float score = distance + defense;
            allCandidates.Add(new TargetScore
            {
                Target = target2,
                TotalScore = score,
                CategoryPriority = 2
            });
        }

        // Category 3: Non-neutral PCs
        PC target3 = FindEnemyTargetNonNeutralPCInRange(hexes, character);
        if (target3 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target3.hex.v2);
            float defense = target3.GetDefense();
            float score = distance + defense;
            allCandidates.Add(new TargetScore
            {
                Target = target3,
                TotalScore = score,
                CategoryPriority = 3
            });
        }

        // Category 4: Any PC
        PC target4 = FindEnemyTargetPCNoRestrictionsInRange(hexes, character);
        if (target4 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target4.hex.v2);
            float defense = target4.GetDefense();
            float score = distance + defense;
            allCandidates.Add(new TargetScore
            {
                Target = target4,
                TotalScore = score,
                CategoryPriority = 4 // Lowest priority
            });
        }

        // Return null if no targets found
        if (allCandidates.Count == 0)
        {
            return null;
        }

        // Sort by score (lowest is best)
        allCandidates = allCandidates.OrderBy(c => c.TotalScore).ToList();

        // Get best candidate
        TargetScore bestCandidate = allCandidates[0];
        float bestScore = bestCandidate.TotalScore;

        // Check if any scores are within 10% of the best score
        // If so, use category priority as a tiebreaker
        List<TargetScore> closeScoreCandidates = new List<TargetScore>();
        closeScoreCandidates.Add(bestCandidate);

        for (int i = 1; i < allCandidates.Count; i++)
        {
            float scoreDifference = allCandidates[i].TotalScore - bestScore;
            float percentDifference = bestScore > 0 ? (scoreDifference / bestScore) * 100 : 100;

            // If score is within 10% of best score, add to close candidates
            if (percentDifference <= 10)
            {
                closeScoreCandidates.Add(allCandidates[i]);
            }
        }

        // If we have multiple candidates with close scores, sort by category priority
        if (closeScoreCandidates.Count > 1)
        {
            return closeScoreCandidates.OrderBy(c => c.CategoryPriority).First().Target;
        }

        // Otherwise, return the candidate with the best score
        return bestCandidate.Target;
    }

    // Helper class to track potential targets with their scores
    private class TargetScore
    {
        public PC Target { get; set; }
        public float DistanceScore { get; set; }
        public float DefenseScore { get; set; }
        public float TotalScore { get; set; }
        public int CategoryPriority { get; set; } // 1-4, lower is higher priority
    }

    private PC FindNonNeutralEnemyTargetPCNoCapitalInRange(List<Hex> hexes, Character character)
    {
        // Prioritize non-neutral PCs that aren't capitals
        List<TargetScore> potentialTargets = new List<TargetScore>();

        foreach (Hex hex in hexes)
        {
            PC pc = hex.GetPC();
            if (pc != null &&
                pc.owner != character.GetOwner() &&
                pc.owner.GetAlignment() != character.GetAlignment() &&
                pc.owner.GetAlignment() != AlignmentEnum.neutral &&
                !pc.isCapital)
            {
                // Calculate scores (lower is better)
                float distance = Vector2.Distance(character.hex.v2, hex.v2);
                float distanceScore = distance; // Lower distance = better score
                float defenseScore = pc.GetDefense(); // Lower defense = better score

                // Combine scores (lower total = better target)
                float totalScore = distanceScore + defenseScore;

                potentialTargets.Add(new TargetScore
                {
                    Target = pc,
                    DistanceScore = distanceScore,
                    DefenseScore = defenseScore,
                    TotalScore = totalScore
                });
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private PC FindEnemyTargetPCNoCapitalsInRange(List<Hex> hexes, Character character)
    {
        // Find any PC that isn't a capital, including neutral ones
        List<TargetScore> potentialTargets = new List<TargetScore>();

        foreach (Hex hex in hexes)
        {
            PC pc = hex.GetPC();
            if (pc != null &&
                pc.owner != character.GetOwner() &&
                (pc.owner.GetAlignment() == AlignmentEnum.neutral || pc.owner.GetAlignment() != character.GetAlignment()) &&
                !pc.isCapital)
            {
                // Calculate scores (lower is better)
                float distance = Vector2.Distance(character.hex.v2, hex.v2);
                float distanceScore = distance; // Lower distance = better score
                float defenseScore = pc.GetDefense(); // Lower defense = better score

                // Combine scores (lower total = better target)
                float totalScore = distanceScore + defenseScore;

                potentialTargets.Add(new TargetScore
                {
                    Target = pc,
                    DistanceScore = distanceScore,
                    DefenseScore = defenseScore,
                    TotalScore = totalScore
                });
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private PC FindEnemyTargetNonNeutralPCInRange(List<Hex> hexes, Character character)
    {
        // Prioritize non-neutral PCs, including capitals
        List<TargetScore> potentialTargets = new List<TargetScore>();

        foreach (Hex hex in hexes)
        {
            PC pc = hex.GetPC();
            if (pc != null &&
                pc.owner != character.GetOwner() &&
                pc.owner.GetAlignment() != character.GetAlignment() &&
                pc.owner.GetAlignment() != AlignmentEnum.neutral)
            {
                // Calculate scores (lower is better)
                float distance = Vector2.Distance(character.hex.v2, hex.v2);
                float distanceScore = distance; // Lower distance = better score
                float defenseScore = pc.GetDefense(); // Lower defense = better score

                // Combine scores (lower total = better target)
                float totalScore = distanceScore + defenseScore;

                potentialTargets.Add(new TargetScore
                {
                    Target = pc,
                    DistanceScore = distanceScore,
                    DefenseScore = defenseScore,
                    TotalScore = totalScore
                });
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private PC FindEnemyTargetPCNoRestrictionsInRange(List<Hex> hexes, Character character)
    {
        // Find any PC, including neutral ones and capitals
        List<TargetScore> potentialTargets = new List<TargetScore>();

        foreach (Hex hex in hexes)
        {
            PC pc = hex.GetPC();
            if (pc != null &&
                pc.owner != character.GetOwner() &&
                (pc.owner.GetAlignment() == AlignmentEnum.neutral || pc.owner.GetAlignment() != character.GetAlignment()))
            {
                // Calculate scores (lower is better)
                float distance = Vector2.Distance(character.hex.v2, hex.v2);
                float distanceScore = distance; // Lower distance = better score
                float defenseScore = pc.GetDefense(); // Lower defense = better score

                // Combine scores (lower total = better target)
                float totalScore = distanceScore + defenseScore;

                potentialTargets.Add(new TargetScore
                {
                    Target = pc,
                    DistanceScore = distanceScore,
                    DefenseScore = defenseScore,
                    TotalScore = totalScore
                });
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    protected Army FindTargetEnemyArmyInRange(List<Hex> hexes, Character character)
    {
        // Get potential targets from each category
        List<ArmyTargetScore> allCandidates = new List<ArmyTargetScore>();

        // Category 1: Non-neutral Armies not in PCs (highest priority)
        Army target1 = FindTargetNonNeutralArmyNotInPCInRange(hexes, character);
        if (target1 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target1.commander.hex.v2);
            float strength = CalculateArmyStrength(target1);
            float score = distance + strength;
            allCandidates.Add(new ArmyTargetScore
            {
                Target = target1,
                TotalScore = score,
                CategoryPriority = 1 // Highest priority
            });
        }

        // Category 2: Any Army not in PC
        Army target2 = FindTargetAnyArmyNotInPCInRange(hexes, character);
        if (target2 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target2.commander.hex.v2);
            float strength = CalculateArmyStrength(target2);
            float score = distance + strength;
            allCandidates.Add(new ArmyTargetScore
            {
                Target = target2,
                TotalScore = score,
                CategoryPriority = 2
            });
        }

        // Category 3: Non-neutral Army (including in PCs)
        Army target3 = FindTargetNonNeutralArmyInRange(hexes, character);
        if (target3 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target3.commander.hex.v2);
            float strength = CalculateArmyStrength(target3);
            float score = distance + strength;
            allCandidates.Add(new ArmyTargetScore
            {
                Target = target3,
                TotalScore = score,
                CategoryPriority = 3
            });
        }

        // Category 4: Any Army
        Army target4 = FindTargetAnyEnemyArmyInRange(hexes, character);
        if (target4 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target4.commander.hex.v2);
            float strength = CalculateArmyStrength(target4);
            float score = distance + strength;
            allCandidates.Add(new ArmyTargetScore
            {
                Target = target4,
                TotalScore = score,
                CategoryPriority = 4 // Lowest priority
            });
        }

        // Return null if no targets found
        if (allCandidates.Count == 0)
        {
            return null;
        }

        // Sort by score (lowest is best)
        allCandidates = allCandidates.OrderBy(c => c.TotalScore).ToList();

        // Get best candidate
        ArmyTargetScore bestCandidate = allCandidates[0];
        float bestScore = bestCandidate.TotalScore;

        // Check if any scores are within 10% of the best score
        // If so, use category priority as a tiebreaker
        List<ArmyTargetScore> closeScoreCandidates = new List<ArmyTargetScore>();
        closeScoreCandidates.Add(bestCandidate);

        for (int i = 1; i < allCandidates.Count; i++)
        {
            float scoreDifference = allCandidates[i].TotalScore - bestScore;
            float percentDifference = bestScore > 0 ? (scoreDifference / bestScore) * 100 : 100;

            // If score is within 10% of best score, add to close candidates
            if (percentDifference <= 10)
            {
                closeScoreCandidates.Add(allCandidates[i]);
            }
        }

        // If we have multiple candidates with close scores, sort by category priority
        if (closeScoreCandidates.Count > 1)
        {
            return closeScoreCandidates.OrderBy(c => c.CategoryPriority).First().Target;
        }

        // Otherwise, return the candidate with the best score
        return bestCandidate.Target;
    }

    // Helper class to track potential army targets with their scores
    private class ArmyTargetScore
    {
        public Army Target { get; set; }
        public float DistanceScore { get; set; }
        public float StrengthScore { get; set; }
        public float TotalScore { get; set; }
        public int CategoryPriority { get; set; } // 1-4, lower is higher priority
    }

    // Helper method to calculate army strength (lower is better for attacker)
    private float CalculateArmyStrength(Army army)
    {
        return army.GetDefence();
    }

    private Army FindTargetNonNeutralArmyNotInPCInRange(List<Hex> hexes, Character character)
    {
        // Prioritize non-neutral Armies that aren't in PCs
        List<ArmyTargetScore> potentialTargets = new List<ArmyTargetScore>();

        foreach (Hex hex in hexes)
        {
            if (hex.armies != null && hex.armies.Count > 0 && hex.GetPC() == null)
            {
                foreach (Army army in hex.armies)
                {
                    if (army.commander != null &&
                        army.commander.GetOwner() != character.GetOwner() &&
                        army.commander.GetOwner().GetAlignment() != character.GetAlignment() &&
                        army.commander.GetOwner().GetAlignment() != AlignmentEnum.neutral)
                    {
                        // Calculate scores (lower is better)
                        float distance = Vector2.Distance(character.hex.v2, hex.v2);
                        float strengthScore = CalculateArmyStrength(army);

                        // Combine scores (lower total = better target)
                        float totalScore = distance + strengthScore;

                        potentialTargets.Add(new ArmyTargetScore
                        {
                            Target = army,
                            DistanceScore = distance,
                            StrengthScore = strengthScore,
                            TotalScore = totalScore
                        });
                    }
                }
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private Army FindTargetAnyArmyNotInPCInRange(List<Hex> hexes, Character character)
    {
        // Find any Army that isn't in a PC, including neutral ones
        List<ArmyTargetScore> potentialTargets = new List<ArmyTargetScore>();

        foreach (Hex hex in hexes)
        {
            if (hex.armies != null && hex.armies.Count > 0 && hex.GetPC() == null)
            {
                foreach (Army army in hex.armies)
                {
                    if (army.commander != null &&
                        army.commander.GetOwner() != character.GetOwner())
                    {
                        // Calculate scores (lower is better)
                        float distance = Vector2.Distance(character.hex.v2, hex.v2);
                        float strengthScore = CalculateArmyStrength(army);

                        // Combine scores (lower total = better target)
                        float totalScore = distance + strengthScore;

                        potentialTargets.Add(new ArmyTargetScore
                        {
                            Target = army,
                            DistanceScore = distance,
                            StrengthScore = strengthScore,
                            TotalScore = totalScore
                        });
                    }
                }
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private Army FindTargetNonNeutralArmyInRange(List<Hex> hexes, Character character)
    {
        // Prioritize non-neutral Armies, including those in PCs
        List<ArmyTargetScore> potentialTargets = new List<ArmyTargetScore>();

        foreach (Hex hex in hexes)
        {
            if (hex.armies != null && hex.armies.Count > 0)
            {
                foreach (Army army in hex.armies)
                {
                    if (army.commander != null &&
                        army.commander.GetOwner() != character.GetOwner() &&
                        army.commander.GetOwner().GetAlignment() != character.GetAlignment() &&
                        army.commander.GetOwner().GetAlignment() != AlignmentEnum.neutral)
                    {
                        // Calculate scores (lower is better)
                        float distance = Vector2.Distance(character.hex.v2, hex.v2);
                        float strengthScore = CalculateArmyStrength(army);

                        // Combine scores (lower total = better target)
                        float totalScore = distance + strengthScore;

                        potentialTargets.Add(new ArmyTargetScore
                        {
                            Target = army,
                            DistanceScore = distance,
                            StrengthScore = strengthScore,
                            TotalScore = totalScore
                        });
                    }
                }
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private Army FindTargetAnyEnemyArmyInRange(List<Hex> hexes, Character character)
    {
        // Find any Army, including neutral ones and those in PCs
        List<ArmyTargetScore> potentialTargets = new List<ArmyTargetScore>();

        foreach (Hex hex in hexes)
        {
            if (hex.armies != null && hex.armies.Count > 0)
            {
                foreach (Army army in hex.armies)
                {
                    if (army.commander != null &&
                        army.commander.GetOwner() != character.GetOwner())
                    {
                        // Calculate scores (lower is better)
                        float distance = Vector2.Distance(character.hex.v2, hex.v2);
                        float strengthScore = CalculateArmyStrength(army);

                        // Combine scores (lower total = better target)
                        float totalScore = distance + strengthScore;

                        potentialTargets.Add(new ArmyTargetScore
                        {
                            Target = army,
                            DistanceScore = distance,
                            StrengthScore = strengthScore,
                            TotalScore = totalScore
                        });
                    }
                }
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }
}
