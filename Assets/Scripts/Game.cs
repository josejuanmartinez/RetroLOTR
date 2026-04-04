using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System.Threading.Tasks;

public class Game : MonoBehaviour
{
    private const int AlliedTradeChance = 25;
    private const int AlliedTradeGoldAsk = 5;

    [Header("Sound")]
    public AudioSource soundPlayer;
    public AudioSource musicPlayer;

    [Header("Playable Leader (Player)")]
    public PlayableLeader player;
    [Header("Other Playable Leaders")]
    public List<PlayableLeader> competitors;
    [Header("Non Playable Leaders")]
    public List<NonPlayableLeader> npcs;
    [Header("Currently Playing")]
    public PlayableLeader currentlyPlaying;
    [Header("Artifacts")]
    public List<Artifact> artifacts = new();

    [Header("Movement")]
    public int characterMovement = 5;
    public int armyMovement = 5;
    public int cavalryMovement = 7;

    [Header("Caps")]
    public static int MAX_BOARD_WIDTH = 25;
    public static int MAX_BOARD_HEIGHT = 75;
    public static int MAX_ARTIFACTS = 100;
    public static int MAX_CHARACTERS = 100;
    public static int MAX_PCS = 100;
    public static int MAX_TURNS = 999;

    [Header("References")]
    public StoresManager storesManager;
    public Board board;
    public CharacterIcons icons;
    public CanvasGroup selectedCharacterIconCanvasGroup;
    public CanvasGroup actionsCanvasGroup;
    public Button nextTurnButton;


    [Header("Starting info")]
    public int turn = 0;
    public bool started = false;
    public bool skipTutorial = false;

    public event Action<int> NewTurnStarted;

    private bool skipNextTurnPrompt = false;
    private readonly List<NpcFocusEntry> npcFocusEntries = new();
    private bool blockLookAtUntilStartupPopupCloses;
    private bool startupPopupShown;
    void Awake()
    {
        if (!board) board = FindAnyObjectByType<Board>();
        if (!storesManager) storesManager = FindAnyObjectByType<StoresManager>();
        if (AIContextCacheManager.Instance == null) gameObject.AddComponent<AIContextCacheManager>();
        if (FindFirstObjectByType<NonPlayableLeaderEventManager>() == null)
        {
            gameObject.AddComponent<NonPlayableLeaderEventManager>();
        }
        if (FindFirstObjectByType<TutorialManager>() == null)
        {
            gameObject.AddComponent<TutorialManager>();
        }
    }

    private void AssignAIandHumans()
    {
        // Find all ML-Agents in the scene
        List<Character> allCharacters = FindObjectsByType<Character>(FindObjectsSortMode.None).ToList();
        foreach(Character character in allCharacters)
        {
            character.isPlayerControlled = character.GetOwner() == player;
        }
    }

    public void SelectPlayer(PlayableLeader playableLeader)
    {
        competitors = new();
        npcs = new();
        player = playableLeader;
        foreach (PlayableLeader otherPlayableLeader in FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None))
        {
            if (otherPlayableLeader == playableLeader) continue;
            competitors.Add(otherPlayableLeader);
        }
        foreach (NonPlayableLeader nonPlayableLeader in FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None))
        {
            npcs.Add(nonPlayableLeader);
        }
    }

    public void StartGame()
    {
        StartGame(this.skipTutorial);
    }

    public void StartGame(bool skipTutorial)
    {
        FindFirstObjectByType<LeaderSelector>()?.ApplyCurrentSelection();
        FindFirstObjectByType<Initialize>()?.UndoInitialState();

        turn = 0;
        started = true;
        this.skipTutorial = skipTutorial;
        blockLookAtUntilStartupPopupCloses = true;
        startupPopupShown = false;

        currentlyPlaying = player;
        MessageDisplay.ClearPersistent();
        MessageDisplay.ShowPersistent("Game starting...", Color.yellow);

        InitializePlayableLeaderIcons();
        InitializeNonPlayableLeaderIcons();
        board.StartGame();
        AssignAIandHumans();
        VictoryPoints.RecalculateAndAssign(this);
        RefreshPlayableLeaderIconVictoryPoints();
        AIContextCacheManager.Instance?.BeginPlayerTurnPrecompute(this);
        NewTurnStarted?.Invoke(turn);
        if (this.skipTutorial)
        {
            TutorialManager.Instance?.Skip(player);
        }
        else
        {
            TutorialManager.Instance?.InitializeForLeader(player);
        }
        currentlyPlaying.NewTurn();
        BuildPlayerCharacterIcons();
        SelectFirstPlayerCharacter();
        StartCoroutine(RefreshDeckUiAfterStartup());
        MessageDisplay.ClearPersistent();

    }

    private IEnumerator RefreshDeckUiAfterStartup()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        if (deckManager == null) yield break;

        deckManager.InitializeHandsForCurrentGame();
        deckManager.RefreshHumanPlayerHandUI();
        FindFirstObjectByType<ActionsManager>()?.RefreshInteractableState();
        ShowHumanPlayerWidgetsWidgets();
    }

    private void InitializePlayableLeaderIcons()
    {
        PlayableLeaderIcons leaderIcons = FindFirstObjectByType<PlayableLeaderIcons>();
        if (leaderIcons == null) return;

        if (player != null) leaderIcons.Instantiate(player);
        if (competitors == null) return;

        foreach (PlayableLeader competitor in competitors)
        {
            if (competitor != null) leaderIcons.Instantiate(competitor);
        }
    }

    private void InitializeNonPlayableLeaderIcons()
    {
        foreach (NonPlayableLeader nonPlayableLeader in FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None))
        {
            if (nonPlayableLeader != null) nonPlayableLeader.InitializeIcons();
        }
    }

    private void RefreshPlayableLeaderIconVictoryPoints()
    {
        PlayableLeaderIcons leaderIcons = FindFirstObjectByType<PlayableLeaderIcons>();
        if (leaderIcons == null) return;
        leaderIcons.RefreshVictoryPointsForAll();
        leaderIcons.UpdateVictoryPointColors();
    }

    public bool PointToCharacterWithMissingActions()
    {
        // Make sure all characters have actioned
        Character stillNotActioned = player.controlledCharacters.Find(x => !x.hasActionedThisTurn && !x.killed && board.selectedCharacter != x);
        if ( stillNotActioned != null) board.SelectCharacter(stillNotActioned, true, 1.0f, 2.0f);
        return stillNotActioned != null;
    }

    public async void SelectNextCharacterOrFinishTurnPrompt()
    {
        if (!IsPlayerCurrentlyPlaying() || player == null || board == null) return;

        List<Character> characters = player.controlledCharacters;
        if (characters == null || characters.Count == 0) return;

        await WaitForNoUiMessagesAsync();
        if (!IsPlayerCurrentlyPlaying() || player == null || board == null) return;

        Character current = board.selectedCharacter;
        Character nextCharacter = null;

        // Try to find the next character without an action, cycling from current selection
        int startIndex = characters.IndexOf(current);
        if (startIndex < 0) startIndex = -1;
        for (int offset = 1; offset <= characters.Count; offset++)
        {
            int i = (startIndex + offset) % characters.Count;
            var c = characters[i];
            if (c != null && !c.killed && !c.hasActionedThisTurn)
            {
                nextCharacter = c;
                break;
            }
        }

        if (nextCharacter != null)
        {
            board.SelectCharacter(nextCharacter);
            return;
        }

        // No characters with free actions; offer to finish turn
        bool finish = await ConfirmationDialog.Ask(
            "No more characters with free actions are available. You may still have movement left. Finish the turn?",
            "Finish Turn",
            "Cancel");
        if (finish)
        {
            skipNextTurnPrompt = true;
            NextPlayer();
        }
    }

    private async Task WaitForNoUiMessagesAsync()
    {
        if (!MessageDisplayNoUI.IsBusy()) return;
        int safety = 0;
        while (MessageDisplayNoUI.IsBusy() && safety < 200)
        {
            await Task.Delay(50);
            safety++;
        }
    }

    public void SelectNextCharacterInPriorityCycle()
    {
        if (!IsPlayerCurrentlyPlaying() || player == null || board == null) return;

        List<Character> characters = player.controlledCharacters;
        if (characters == null || characters.Count == 0) return;

        List<Character> ordered = new();
        ordered.AddRange(characters.Where(c => c != null && !c.killed && !c.hasActionedThisTurn));
        ordered.AddRange(characters.Where(c => c != null && !c.killed && c.hasActionedThisTurn && c.moved < c.GetMaxMovement()));
        ordered.AddRange(characters.Where(c => c != null && !c.killed && c.hasActionedThisTurn && c.moved >= c.GetMaxMovement()));

        if (ordered.Count == 0) return;

        Character current = board.selectedCharacter;
        int currentIndex = ordered.IndexOf(current);
        int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % ordered.Count : 0;
        Character next = ordered[nextIndex];
        if (next != null) board.SelectCharacter(next);
    }

    public async void NextPlayer()
    {
        PopupManager.CloseAll();
        ConfirmationDialog.CloseAll();
        SelectionDialog.CloseAll();

        bool shouldPrompt = currentlyPlaying == player && !skipNextTurnPrompt;
        skipNextTurnPrompt = false;

        if (shouldPrompt)
        {
            bool hasPendingActions = player.controlledCharacters.Any(x => !x.killed && !x.hasActionedThisTurn);
            string message = hasPendingActions
                ? "Some characters have not actioned yet. End turn?"
                : "End turn?";

            bool finishTurn = await ConfirmationDialog.Ask(message, "Finish Turn", "Cancel");
            if (!finishTurn)
            {
                Character nextCharacter = player.controlledCharacters.Find(x => !x.killed && !x.hasActionedThisTurn);
                if (nextCharacter != null)
                {
                    board.SelectCharacter(nextCharacter, true, 1.0f, 0.0f);
                }
                else
                {
                    Character firstAlive = player.controlledCharacters.Find(x => !x.killed);
                    if (firstAlive != null) board.SelectCharacter(firstAlive, true, 1.0f, 0.0f);
                }
                return;
            }
        }

        PlayableLeader next = FindNextTurnLeader(currentlyPlaying);

        // If no one else alive and player alive, victory; otherwise defeat
        if (next == null)
        {
            EndGame(player != null && !player.killed);
            return;
        }

        // If the only remaining leader is the player and there are no competitors left, end game as win
        if (next == player && !player.killed && competitors.All(c => c == null || c.killed))
        {
            EndGame(true);
            return;
        }

        currentlyPlaying = next;

        if (currentlyPlaying == player)
        {
            StartCoroutine(BeginPlayerTurnSequence());
        }
        else
        {
            HideHumanPlayerWidgetsWidgets();
            MessageDisplay.ShowPersistent($"{currentlyPlaying.characterName} is playing", Color.yellow);
            MessageDisplayNoUI.SetPaused(true);
            board.RefreshRelevantHexes();
            currentlyPlaying.NewTurn();
        }
    }

    private void NewTurn()
    {
        MessageDisplay.ClearPersistent();
        PopupManager.CloseAll();
        ConfirmationDialog.CloseAll();
        SelectionDialog.CloseAll();
        turn++;
        if (turn >= MAX_TURNS)
        {
            EndGame(false);
            return;
        }
        AdvanceTemporaryPcVisibility();
        board?.ClearAllScouting();
        AnnounceScoutingStatus();
        MessageDisplay.ShowMessage($"Turn {turn}", Color.green);
        NewTurnStarted?.Invoke(turn);
        AIContextCacheManager.Instance?.BeginPlayerTurnPrecompute(this);
        storesManager.AdvanceTurn();
    }

    public void QueueNpcFocus(Leader leader, Hex hex)
    {
        if (leader == null || hex == null) return;
        if (player != null && leader == player) return;
        npcFocusEntries.Add(new NpcFocusEntry { leader = leader, hex = hex });
    }

    private IEnumerator BeginPlayerTurnSequence()
    {
        HideHumanPlayerWidgetsWidgets();
        MessageDisplayNoUI.SetPaused(true);

        yield return PlayNpcFocusSequence();

        MessageDisplayNoUI.SetPaused(false);
        MessageDisplay.ClearPersistent();

        NewTurn();
        board.RefreshRelevantHexes();

        yield return WaitForNpcEvents();
        yield return TryOfferAlliedTradeToPlayer();

        currentlyPlaying.NewTurn();

        yield return WaitForCameraAndMessages();

        ShowHumanPlayerWidgetsWidgets();
        SelectFirstPlayerCharacter();
    }

    private IEnumerator WaitForNpcEvents()
    {
        NonPlayableLeaderEventManager manager = FindFirstObjectByType<NonPlayableLeaderEventManager>();
        if (manager == null) yield break;
        while (manager.IsProcessingTurn)
        {
            yield return null;
        }
    }

    private IEnumerator TryOfferAlliedTradeToPlayer()
    {
        if (player == null || player.killed) yield break;
        if (UnityEngine.Random.Range(0, AlliedTradeChance) != 0) yield break;

        AlliedTradeOffer offer = BuildAlliedTradeOffer(player);
        if (offer == null) yield break;

        Task<bool> answerTask = ConfirmationDialog.AskYesNo(offer.BuildPrompt());
        while (!answerTask.IsCompleted)
        {
            yield return null;
        }

        if (!answerTask.Result)
        {
            MessageDisplay.ShowMessage($"{offer.Ally.characterName}'s trade offer was declined.", Color.yellow);
            yield break;
        }

        ApplyAlliedTradeOffer(offer);
    }

    private AlliedTradeOffer BuildAlliedTradeOffer(PlayableLeader buyer)
    {
        List<Leader> allies = GetAlliedTradePartners(buyer);
        if (allies.Count < 1) return null;

        List<AlliedTradeOffer> offers = new();
        for (int i = 0; i < allies.Count; i++)
        {
            Leader ally = allies[i];
            AlliedTradeOffer buyOffer = BuildGoldForResourcesOffer(buyer, ally);
            if (buyOffer != null) offers.Add(buyOffer);

            AlliedTradeOffer sellOffer = BuildResourceForGoldOffer(buyer, ally);
            if (sellOffer != null) offers.Add(sellOffer);
        }

        if (offers.Count < 1) return null;
        return offers[UnityEngine.Random.Range(0, offers.Count)];
    }

    private List<Leader> GetAlliedTradePartners(PlayableLeader buyer)
    {
        List<Leader> allies = new();
        if (buyer == null) return allies;

        AlignmentEnum alignment = buyer.GetAlignment();
        if (competitors != null)
        {
            for (int i = 0; i < competitors.Count; i++)
            {
                PlayableLeader competitor = competitors[i];
                if (competitor == null || competitor == buyer || competitor.killed) continue;
                if (competitor.GetAlignment() != alignment) continue;
                allies.Add(competitor);
            }
        }

        if (npcs != null)
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                NonPlayableLeader npc = npcs[i];
                if (npc == null || npc.killed) continue;
                if (npc.GetAlignment() != alignment) continue;
                allies.Add(npc);
            }
        }

        return allies;
    }

    private AlliedTradeOffer BuildGoldForResourcesOffer(Leader buyer, Leader ally)
    {
        if (buyer == null || ally == null) return null;
        if (buyer.goldAmount < AlliedTradeGoldAsk) return null;

        Dictionary<ProducesEnum, int> offered = BuildBestResourceBundle(ally, AlliedTradeGoldAsk, false, buyer);
        if (offered.Count < 1) return null;

        return new AlliedTradeOffer
        {
            Ally = ally,
            Buyer = buyer,
            GoldFromBuyer = AlliedTradeGoldAsk,
            ResourcesFromBuyer = new Dictionary<ProducesEnum, int>(),
            GoldFromAlly = 0,
            ResourcesFromAlly = offered
        };
    }

    private AlliedTradeOffer BuildResourceForGoldOffer(Leader buyer, Leader ally)
    {
        if (buyer == null || ally == null) return null;
        if (ally.goldAmount < 1) return null;

        int maxGold = Mathf.Min(AlliedTradeGoldAsk, ally.goldAmount);
        Dictionary<ProducesEnum, int> requested = BuildBestResourceBundle(buyer, maxGold, true, ally);
        if (requested.Count < 1) return null;

        int goldOffer = GetBundleValue(requested);
        if (goldOffer < 1) return null;

        return new AlliedTradeOffer
        {
            Ally = ally,
            Buyer = buyer,
            GoldFromBuyer = 0,
            ResourcesFromBuyer = requested,
            GoldFromAlly = goldOffer,
            ResourcesFromAlly = new Dictionary<ProducesEnum, int>()
        };
    }

    private Dictionary<ProducesEnum, int> BuildBestResourceBundle(Leader source, int maxValue, bool requireZeroStockInReceiver, Leader receiver)
    {
        Dictionary<ProducesEnum, int> bundle = new();
        if (source == null || maxValue < 1) return bundle;

        ProducesEnum[] ordered = new[]
        {
            ProducesEnum.steel,
            ProducesEnum.mounts,
            ProducesEnum.iron,
            ProducesEnum.timber,
            ProducesEnum.leather
        };

        int remaining = maxValue;
        for (int i = 0; i < ordered.Length; i++)
        {
            ProducesEnum resource = ordered[i];
            if (requireZeroStockInReceiver && receiver != null && receiver.GetResourceAmount(resource) > 0) continue;

            int available = source.GetResourceAmount(resource);
            if (available < 1) continue;

            int price = GetResourceTradeValue(resource);
            if (price > remaining) continue;

            int maxUnits = Mathf.Min(available, remaining / price);
            if (maxUnits < 1) continue;

            bundle[resource] = maxUnits;
            remaining -= maxUnits * price;
            if (remaining <= 0) break;
        }

        return bundle;
    }

    private int GetBundleValue(Dictionary<ProducesEnum, int> bundle)
    {
        if (bundle == null || bundle.Count < 1) return 0;

        int total = 0;
        foreach (KeyValuePair<ProducesEnum, int> entry in bundle)
        {
            total += GetResourceTradeValue(entry.Key) * Mathf.Max(0, entry.Value);
        }

        return total;
    }

    private int GetResourceTradeValue(ProducesEnum resource)
    {
        return resource switch
        {
            ProducesEnum.leather => StoresManager.LeatherSellValue,
            ProducesEnum.timber => StoresManager.TimberSellValue,
            ProducesEnum.iron => StoresManager.IronSellValue,
            ProducesEnum.steel => StoresManager.SteelSellValue,
            ProducesEnum.mounts => StoresManager.MountsSellValue,
            ProducesEnum.mithril => StoresManager.MithrilSellValue,
            ProducesEnum.gold => 1,
            _ => 0
        };
    }

    private void ApplyAlliedTradeOffer(AlliedTradeOffer offer)
    {
        if (offer == null || offer.Ally == null || offer.Buyer == null) return;

        if (offer.GoldFromBuyer > 0)
        {
            offer.Buyer.RemoveGold(offer.GoldFromBuyer, offer.Buyer == player);
            offer.Ally.AddGold(offer.GoldFromBuyer);
        }

        if (offer.GoldFromAlly > 0)
        {
            offer.Ally.RemoveGold(offer.GoldFromAlly, false);
            offer.Buyer.AddGold(offer.GoldFromAlly);
        }

        TransferResources(offer.Buyer, offer.Ally, offer.ResourcesFromBuyer, true);
        TransferResources(offer.Ally, offer.Buyer, offer.ResourcesFromAlly, false);

        storesManager?.RefreshStores();
        MessageDisplay.ShowMessage($"{offer.Ally.characterName} completed a trade with {offer.Buyer.characterName}.", Color.green);
    }

    private void TransferResources(Leader from, Leader to, Dictionary<ProducesEnum, int> resources, bool showPlayerCost)
    {
        if (from == null || to == null || resources == null) return;

        foreach (KeyValuePair<ProducesEnum, int> entry in resources)
        {
            int amount = Mathf.Max(0, entry.Value);
            if (amount < 1) continue;

            from.RemoveResource(entry.Key, amount, showPlayerCost && from == player);
            to.AddResource(entry.Key, amount);
        }
    }

    private IEnumerator PlayNpcFocusSequence()
    {
        if (npcFocusEntries.Count == 0) yield break;
        BoardNavigator navigator = BoardNavigator.Instance;
        if (navigator == null)
        {
            npcFocusEntries.Clear();
            yield break;
        }

        List<Leader> leaderOrder = new();
        for (int i = 0; i < npcFocusEntries.Count; i++)
        {
            Leader leader = npcFocusEntries[i].leader;
            if (leader != null && !leaderOrder.Contains(leader)) leaderOrder.Add(leader);
        }

        for (int i = 0; i < leaderOrder.Count; i++)
        {
            Leader leader = leaderOrder[i];
            for (int j = 0; j < npcFocusEntries.Count; j++)
            {
                if (npcFocusEntries[j].leader != leader) continue;
                navigator.EnqueueNpcPlaybackFocus(npcFocusEntries[j].hex);
            }
            while (navigator.HasPendingFocus())
            {
                yield return null;
            }
        }

        npcFocusEntries.Clear();
    }

    private IEnumerator WaitForCameraAndMessages()
    {
        BoardNavigator navigator = BoardNavigator.Instance;
        while (MessageDisplayNoUI.IsBusy() || MessageDisplay.IsBusy() || (navigator != null && navigator.HasPendingFocus()))
        {
            yield return null;
        }
    }

    private struct NpcFocusEntry
    {
        public Leader leader;
        public Hex hex;
    }

    private void AnnounceScoutingStatus()
    {
        if (board == null || player == null || board.hexes == null) return;

        foreach (Hex hex in board.hexes.Values)
        {
            if (hex == null) continue;
            int scoutedTurns = hex.GetScoutedTurnsRemaining(player);
            if (scoutedTurns <= 0) continue;

            PC pc = hex.GetPCData();
            string message = null;
            if (pc != null && pc.temporaryRevealTurns > 0)
            {
                message = $"<sprite name=\"light\"> Light fades ({pc.temporaryRevealTurns} turn{(pc.temporaryRevealTurns == 1 ? "" : "s")} left)";
            }
            else if (pc != null && pc.temporaryHiddenTurns > 0)
            {
                message = $"<sprite name=\"darkness\"> Darkness fades ({pc.temporaryHiddenTurns} turn{(pc.temporaryHiddenTurns == 1 ? "" : "s")} left)";
            }
            else
            {
                message = $"<sprite name=\"scout\"> Scouted: {scoutedTurns} turn{(scoutedTurns == 1 ? "" : "s")} left";
            }

            MessageDisplayNoUI.ShowMessage(hex, player, message, Color.yellow, false);
        }
    }

    private void AdvanceTemporaryPcVisibility()
    {
        List<Leader> leaders = new();
        if (player != null) leaders.Add(player);
        if (competitors != null) leaders.AddRange(competitors.Where(c => c != null));
        if (npcs != null) leaders.AddRange(npcs.Where(n => n != null));

        foreach (Leader leader in leaders)
        {
            foreach (PC pc in leader.controlledPcs)
            {
                if (pc == null) continue;
                pc.TickTemporaryVisibility();
                if (pc.hex != null) pc.hex.RedrawPC();
            }
        }
    }

    private void HideHumanPlayerWidgetsWidgets()
    {
        SetCanvasGroupVisible(selectedCharacterIconCanvasGroup, false);
        SetCanvasGroupVisible(actionsCanvasGroup, false);
        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        deckManager?.SetHumanHandVisible(false);
        nextTurnButton.enabled = false;
    }
    private void ShowHumanPlayerWidgetsWidgets()
    {
        SetCanvasGroupVisible(selectedCharacterIconCanvasGroup, true);
        SetCanvasGroupVisible(actionsCanvasGroup, true);
        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        deckManager?.SetHumanHandVisible(true);
        nextTurnButton.enabled = true;
    }

    private static void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool visible)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void BuildPlayerCharacterIcons()
    {
        if (player == null) return;
        icons?.BuildIconsForPlayer(player);
    }

    private void SelectFirstPlayerCharacter()
    {
        if (player == null || board == null) return;

        if (!startupPopupShown)
        {
            blockLookAtUntilStartupPopupCloses = false;
        }

        Character firstAlive = player.controlledCharacters
            .FirstOrDefault(c => c != null && !c.killed);

        if (firstAlive != null)
        {
            if (firstAlive.hex != null)
            {
                if (!player.visibleHexes.Contains(firstAlive.hex)) player.visibleHexes.Add(firstAlive.hex);
                firstAlive.hex.RevealArea(1, true, null);
            }
            ShowHumanPlayerWidgetsWidgets();
            board.SelectCharacter(firstAlive, true, 1.0f, 0.0f);
        }
        else
        {
            HideHumanPlayerWidgetsWidgets();
        }
    }

    // Helper method to find the next alive leader in turn order
    private PlayableLeader FindNextTurnLeader(PlayableLeader current)
    {
        List<PlayableLeader> order = new();
        if (player != null && !player.killed) order.Add(player);
        if (competitors != null) order.AddRange(competitors.Where(c => c != null && !c.killed));

        if (order.Count == 0) return null;

        int currentIndex = order.IndexOf(current);
        int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % order.Count : 0;
        return order[nextIndex];
    }

    // Add this method to handle game ending
    public void EndGame(bool win)
    {
        HideHumanPlayerWidgetsWidgets();
        if (win) MessageDisplay.ShowMessage("Victory!", Color.green); else MessageDisplay.ShowMessage("Defeat!", Color.red);

        Application.Quit();
        Debug.Log("Game Ended!");
    }

    public bool IsPlayerCurrentlyPlaying()
    {
        return currentlyPlaying == player;
    }

    public bool ShouldBlockLookAtUntilStartupPopupCloses()
    {
        return started && blockLookAtUntilStartupPopupCloses;
    }

    public void NotifyStartupPopupShown()
    {
        if (!blockLookAtUntilStartupPopupCloses || startupPopupShown) return;
        startupPopupShown = true;
    }

    public void NotifyStartupPopupClosed()
    {
        if (!blockLookAtUntilStartupPopupCloses || !startupPopupShown) return;
        blockLookAtUntilStartupPopupCloses = false;
    }

    private sealed class AlliedTradeOffer
    {
        public Leader Ally;
        public Leader Buyer;
        public int GoldFromBuyer;
        public Dictionary<ProducesEnum, int> ResourcesFromBuyer;
        public int GoldFromAlly;
        public Dictionary<ProducesEnum, int> ResourcesFromAlly;

        public string BuildPrompt()
        {
            if (GoldFromBuyer > 0)
            {
                return $"{Ally.characterName} asks for 5 <sprite name=\"gold\"> and offers {FormatResources(ResourcesFromAlly)}.\nAccept this allied trade?";
            }

            return $"{Ally.characterName} asks for {FormatResources(ResourcesFromBuyer)} and offers {GoldFromAlly} <sprite name=\"gold\">.\nAccept this allied trade?";
        }

        private static string FormatResources(Dictionary<ProducesEnum, int> resources)
        {
            if (resources == null || resources.Count < 1) return "nothing";

            List<string> parts = new();
            foreach (KeyValuePair<ProducesEnum, int> entry in resources)
            {
                if (entry.Value < 1) continue;
                parts.Add($"{entry.Value} <sprite name=\"{entry.Key}\">");
            }

            return string.Join(", ", parts);
        }
    }
}
