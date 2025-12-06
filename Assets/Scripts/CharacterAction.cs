using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GraphicRaycaster))]
public class CharacterAction : SearcherByName
{
    [Header("IDs")]
    public string actionName;
    public int actionId;
    [HideInInspector] public Character character;
        
    [Header("Rendering")]
    [HideInInspector]
    public string actionInitials;
    public Image background;
    public Sprite actionSprite;
    public Color actionColor;

    [Header("Game Objects")]
    public Image spriteImage;
    public Button button;
    public TextMeshProUGUI textUI;

    [Header("Hover")]
    public GameObject hoverPrefab;

    [Header("Failure rate (0-100) %")]
    public int difficulty = 0;
    [Header("Required skill")]
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

    [Header("XP Chance (0-100) %")]
    public int commanderXP;
    public int agentXP;
    public int emmissaryXP;
    public int mageXP;

    [Header("XP Rewarded")]
    public int reward = 1;

    [Header("Availability")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> condition;

    [Header("AI")]
    public AdvisorType advisorType = AdvisorType.None;

    [Header("Effect")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> effect;
    // Optional async effect for actions that require awaiting UI/input
    public Func<Character, Task<bool>> asyncEffect;
    
    private Game game;
    private bool initialized = false;
    void Awake()
    {
        game = FindAnyObjectByType<Game>();
    }

    protected virtual AdvisorType DefaultAdvisorType => AdvisorType.None;

    public AdvisorType GetAdvisorType()
    {
        return advisorType == AdvisorType.None ? DefaultAdvisorType : advisorType;
    }


    public virtual void Initialize(Character character, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        try
        {
            var originalCondition = condition;
            this.character = character;
            if (advisorType == AdvisorType.None) advisorType = DefaultAdvisorType;

            this.condition = (character) => { return 
                character != null && !character.killed
                && (!character.hasActionedThisTurn || this.actionName == FindFirstObjectByType<ActionsManager>().DEFAULT.actionName) 
                &&  ResourcesAvailable() 
                && (originalCondition == null || originalCondition(character)); 
            };
            this.effect = effect;
            this.asyncEffect = asyncEffect;

            bool activate = this.condition(character);
            button.gameObject.SetActive(activate);

            if (activate && !initialized)
            {
                GameObject hoverInstance = Instantiate(hoverPrefab, button.transform);
                hoverInstance.GetComponent<Hover>().Initialize(actionName, Vector2.one * 40, 35, TextAlignmentOptions.Center);

                actionInitials = gameObject.name.ToUpper();
                spriteImage.color = actionColor;
                if (actionSprite && spriteImage)
                {
                    spriteImage.sprite = actionSprite;
                    textUI.text = "";
                }
                else
                {
                    Debug.LogWarning($"Action {actionName} does not have action sprite or the spriteImage reference is not set");
                    textUI.text = actionInitials.ToUpper();
                }
                initialized = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Unable to Initialize CharacterAction {actionName}");
            Debug.LogError(e.ToString());
        }        
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
        asyncEffect = null;
        button.gameObject.SetActive(false);
    }

    public bool IsProvidedByArtifact()
    {
        return character.artifacts.Find(x => Normalize(x.providesSpell) == Normalize(actionName)) != null;
    }

    public bool ResourcesAvailable()
    {
        if (commanderSkillRequired > 0 && !IsProvidedByArtifact() && character.GetCommander() < commanderSkillRequired) return false;
        if (agentSkillRequired > 0 && !IsProvidedByArtifact() && character.GetAgent() < agentSkillRequired) return false;
        if (emissarySkillRequired > 0 && !IsProvidedByArtifact() && character.GetEmmissary() < emissarySkillRequired) return false;
        if (mageSkillRequired > 0 && !IsProvidedByArtifact() && character.GetMage() < mageSkillRequired) return false;

        if (leatherCost > 0 && character.GetOwner().leatherAmount < leatherCost) return false;
        if (timberCost > 0 && character.GetOwner().timberAmount < timberCost) return false;
        if (mountsCost > 0 && character.GetOwner().mountsAmount < mountsCost) return false;
        if (ironCost > 0 && character.GetOwner().ironAmount < ironCost) return false;
        if (mithrilCost > 0 && character.GetOwner().mithrilAmount < mithrilCost) return false;
        if (goldCost > 0 && character.GetOwner().goldAmount < goldCost) return false;

        return true;
    }

    public void Fail(bool isAI)
    {
        string message = $"{actionName} failed";
        Debug.Log(message);
        if (!isAI) MessageDisplayNoUI.ShowMessage(character.hex, character,  message, Color.red);
        if (!isAI) game.PointToCharacterWithMissingActions();
        if (!isAI) FindFirstObjectByType<Layout>().GetActionsManager().Refresh(character);
        if (!isAI) FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(character);
        return;
    }

    public async Task Execute()
    {
            bool isAI = !character.isPlayerControlled;
        try
        {
            // All characters
            character.hasActionedThisTurn = true;

            bool failed = false;
            if (UnityEngine.Random.Range(0, 100) < difficulty) failed = true;

            // Should be impossible as the button will show not show up but just in case
            if(!ResourcesAvailable()) failed = true;

            if (failed)
            {
                Fail(isAI);
                return;
            }

            if (effect != null) failed = !effect(character);

            if (!failed && asyncEffect != null) failed = !await asyncEffect(character);

            if (failed)
            {
                Fail(isAI);
                return;
            }

            string message = actionName;            
            string rumourMessage = $"{character.characterName} succeeds on {message}";
            Debug.Log(rumourMessage);
            bool isDoubledByPlayer = character.doubledBy.Contains(game.player);
            if (isAI)
            {
                // Always record AI actions privately; if doubled by the player, also promote to public immediately
                Rumour rumour = new Rumour() { leader = character.GetOwner(), rumour = rumourMessage, v2 = character.hex.v2 };
                RumoursManager.AddRumour(rumour, false);
                if (isDoubledByPlayer)
                {
                    RumoursManager.PromoteRumourToPublic(rumour);
                }
            }
            
            if(UnityEngine.Random.Range(0, 100) < commanderXP)
            {
                character.AddCommander(1);
                Debug.Log($"{character.characterName} gets +1 to commander XP");
                if (!isAI) MessageDisplayNoUI.ShowMessage(character.hex, character,  "<sprite name=\"commander\"/> +1", Color.green);
            }

            if (UnityEngine.Random.Range(0, 100) < agentXP)
            {
                character.AddAgent(1);
                Debug.Log($"{character.characterName} gets +1 to agent XP");
                if (!isAI) MessageDisplayNoUI.ShowMessage(character.hex, character,  "<sprite name=\"agent\"/> +1", Color.green);
            }

            if (UnityEngine.Random.Range(0, 100) < emmissaryXP)
            {
                character.AddEmmissary(1);
                Debug.Log($"{character.characterName} gets +1 to emmissary XP");
                if (!isAI) MessageDisplayNoUI.ShowMessage(character.hex, character,  "<sprite name=\"emmissary\"/> +1", Color.green);
            }

            if (UnityEngine.Random.Range(0, 100) < mageXP)
            {
                character.AddMage(1);
                Debug.Log($"{character.characterName} gets +1 to commander XP");
                if (!isAI) MessageDisplayNoUI.ShowMessage(character.hex, character,  "<sprite name=\"mage\"/> +1", Color.green);
            }

            if (!isAI) FindFirstObjectByType<Layout>().GetActionsManager().Refresh(character);
            if (!isAI) FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(character);

            if(leatherCost > 0)
            {
                character.GetOwner().RemoveLeather(leatherCost);
                Debug.Log($"{character.characterName} spends {leatherCost} leather");
                if (!isAI) MessageDisplay.ShowMessage($"<sprite name=\"leather\"/> -{leatherCost}", Color.red);
            }
            if (timberCost > 0)
            {
                character.GetOwner().RemoveTimber(timberCost);
                Debug.Log($"{character.characterName} spends {timberCost} timberCost");
                if (!isAI) MessageDisplay.ShowMessage($"<sprite name=\"timber\"/> -{timberCost}", Color.red);
            }
            if (mountsCost > 0)
            {
                character.GetOwner().RemoveMounts(mountsCost);
                Debug.Log($"{character.characterName} spends {mountsCost} mounts");
                if (!isAI) MessageDisplay.ShowMessage($"<sprite name=\"mounts\"/> -{mountsCost}", Color.red);
            }

            if (ironCost > 0)
            {
                character.GetOwner().RemoveIron(ironCost);
                Debug.Log($"{character.characterName} spends {ironCost} iron");
                if (!isAI) MessageDisplay.ShowMessage($"<sprite name=\"iron\"/> -{ironCost}", Color.red);
            }

            if (mithrilCost > 0)
            {
                character.GetOwner().RemoveMithril(mithrilCost);
                Debug.Log($"{character.characterName} spends {mithrilCost} mithril");
                if (!isAI) MessageDisplay.ShowMessage($"<sprite name=\"mithril\"/> -{mithrilCost}", Color.red);
            }

            if (goldCost > 0)
            {
                character.GetOwner().RemoveLeather(goldCost);
                Debug.Log($"{character.characterName} spends {goldCost} gold");
                if (!isAI) MessageDisplay.ShowMessage($"<sprite name=\"gold\"/> -{goldCost}", Color.red);
            }

            if (!isAI) FindFirstObjectByType<StoresManager>().RefreshStores();

            if (!isAI) game.PointToCharacterWithMissingActions();

            if (character.GetOwner() is not PlayableLeader) return;

            // Now check influence in NPCs
            FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).Where(x => x != character.GetOwner()).ToList().ForEach(x =>
            {
                x.CheckJoiningCondition(character, this);
            });

            return;
        }
        catch (Exception e)
        {
            if (!isAI) FindFirstObjectByType<Layout>().GetActionsManager().Refresh(character);
            if (!isAI) FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(character);

            Debug.LogError($"{character.characterName} was unable to Execute action {actionName} {actionId} {actionInitials} {e}");
            character.hasActionedThisTurn = true;
            return;
        }
        
    }

    // Wrapper so Unity UI Buttons (which require void return) can trigger this async action
    public async void ExecuteFromButton()
    {
        await Execute();
    }

    protected Character FindEnemyCharacterTargetAtHex(Character c)
    {
        Character target = FindEnemyNonNeutralCharactersAtHexNoLeader(c);
        if (target) return target;
        target = FindEnemyCharactersAtHexNoLeaders(c);
        if (target) return target;
        target = FindEnemyNonNeutralCharactersAtHex(c);
        if (target) return target;
        target = FindEnemyCharacterAtHex(c);
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

    protected Character FindEnemyCharacterAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
    }
    protected List<Character> FindEnemyCharactersAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.FindAll(
            x => x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
    }
    protected List<Character> FindEnemyCharactersNotArmyCommandersAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.FindAll(
            x => !x.IsArmyCommander() && x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
    }

    protected Army FindEnemyArmyNotNeutralAtHex(Character c)
    {
        Character commander = c.hex.characters.Find(
            x => x.IsArmyCommander() && x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() != AlignmentEnum.neutral && x.GetAlignment() != c.GetAlignment())
        );
        if (commander != null && commander.GetArmy() != null) return commander.GetArmy();
        return null;
    }
    protected Army FindEnemyArmyAtHex(Character c)
    {
        Character commander = c.hex.characters.Find(
            x => x.IsArmyCommander() && x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() != c.GetAlignment())
        );
        if (commander != null && commander.GetArmy() != null) return commander.GetArmy();
        return null;
    }

    protected Army FindFriendlyArmyAtHex(Character c)
    {
        Character commander = c.hex.characters.Find(
            x => x.IsArmyCommander() && (x.GetOwner() == c.GetOwner() || (x.GetAlignment() == c.GetAlignment() && x.GetAlignment() != AlignmentEnum.neutral))
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

    public int GetGoldCost()
    {
        return goldCost 
        + ironCost*StoresManager.IronSellValue 
        + mountsCost*StoresManager.MountsSellValue
        + timberCost*StoresManager.TimberSellValue
        + mithrilCost*StoresManager.MithrilSellValue
        + leatherCost*StoresManager.LeatherSellValue;
    }
}
