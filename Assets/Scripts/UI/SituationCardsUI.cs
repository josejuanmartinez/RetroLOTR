using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SituationCardsUI : MonoBehaviour
{
    public static SituationCardsUI Instance { get; private set; }

    // True from the moment the opportunity-card overlay is requested until it has fully
    // faded out. Movement and other events block on this so they don't interrupt it.
    public static bool IsShowing { get; private set; }

    [Header("Content")]
    [SerializeField] private string titleMessage = "Act now!";

    [Header("Presentation")]
    [Tooltip("When true, opportunity cards are presented via the CardBloomWheel positioned " +
        "at the acting character's hex instead of this UI's own center-screen card tray.")]
    [SerializeField] private bool bloom = false;

    [Header("Timing & Layout")]
    [SerializeField] private float fadeInDuration  = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float cardSpacing     = 40f; // also drives the HorizontalLayoutGroup spacing
    [SerializeField] private float maxCardScale    = 1.3f;

    [Header("Confetti")]
    [SerializeField] private int   confettiCount    = 56;
    [SerializeField] private float confettiDuration = 1.5f;

    [Header("Scene References (auto-built/bound when left empty)")]
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private GameObject cardContainer;
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private TextMeshProUGUI titleLabel;

    private Coroutine showCoroutine;
    private readonly List<GameObject> cardInstances = new();

    // Bloom-mode presentation state (see ShowBloomCoroutine / DismissBloom).
    private CardBloomWheel activeBloomWheel;
    private GameObject bloomDismissCatcher;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // A prefab instance already carries the UI hierarchy; only build it from
        // scratch when this was created as a bare GameObject.
        if (!BindExistingUI())
            BuildUI();

        ApplyConfiguration();
        ClearBakedTrayCards();
    }

    // The prefab can carry a design-time preview card saved inside the tray. It never goes
    // through Initialize, so it isn't locked to its real-card face (hover-out flips it to its
    // token) and shows stale content — purge anything already in the tray; runtime cards are
    // always instantiated fresh per Show.
    private void ClearBakedTrayCards()
    {
        if (cardContainer == null) return;
        foreach (Card baked in cardContainer.GetComponentsInChildren<Card>(true))
            if (baked != null) Destroy(baked.gameObject);
    }

    private void OnDestroy()
    {
        // Never leave movement blocked behind an overlay that's gone.
        if (Instance == this) { Instance = null; IsShowing = false; }
    }

    // Binds references from an already-present UI hierarchy (prefab instance),
    // filling in anything not wired in the inspector. Returns false when no UI
    // exists yet, signalling that it must be built procedurally.
    private bool BindExistingUI()
    {
        if (overlayGroup == null)
        {
            Transform overlay = transform.Find("Overlay");
            if (overlay != null) overlayGroup = overlay.GetComponent<CanvasGroup>();
        }
        if (cardContainer == null)
        {
            Transform tray = transform.Find("Overlay/CardTray");
            if (tray != null) cardContainer = tray.gameObject;
        }
        if (titleLabel == null)
        {
            Transform title = transform.Find("Overlay/Title");
            if (title != null) titleLabel = title.GetComponent<TextMeshProUGUI>();
        }
        if (titleRect == null && titleLabel != null) titleRect = titleLabel.rectTransform;

        return overlayGroup != null && cardContainer != null;
    }

    // Applies inspector-configurable values and re-wires runtime-only hooks that
    // don't survive prefab serialization (the dim-overlay dismiss listener).
    private void ApplyConfiguration()
    {
        if (titleLabel != null && !string.IsNullOrEmpty(titleMessage))
            titleLabel.text = titleMessage;

        if (cardContainer != null)
        {
            var hLayout = cardContainer.GetComponent<HorizontalLayoutGroup>();
            if (hLayout != null) hLayout.spacing = cardSpacing;
        }

        if (overlayGroup != null)
        {
            var overlayBtn = overlayGroup.GetComponent<Button>();
            if (overlayBtn != null)
            {
                overlayBtn.onClick.RemoveListener(Dismiss);
                overlayBtn.onClick.AddListener(Dismiss);
            }
            overlayGroup.alpha = 0f;
            overlayGroup.gameObject.SetActive(false);
        }
    }

    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        // Full-screen dim overlay (dismiss on click)
        var overlayGo = new GameObject("Overlay");
        overlayGo.transform.SetParent(transform, false);
        var ort = overlayGo.AddComponent<RectTransform>();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.sizeDelta = Vector2.zero;
        var dimImg = overlayGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0.02f, 0.55f);
        dimImg.raycastTarget = true;
        overlayGroup = overlayGo.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGo.SetActive(false);

        var btn = overlayGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(Dismiss);

        // Centered card tray
        cardContainer = new GameObject("CardTray");
        cardContainer.transform.SetParent(overlayGo.transform, false);
        var crt = cardContainer.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot     = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        var hLayout = cardContainer.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = cardSpacing;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childForceExpandWidth  = false;
        hLayout.childForceExpandHeight = false;
        hLayout.childControlWidth  = false;
        hLayout.childControlHeight = false;
        var fitter = cardContainer.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // "Act now!" banner across the top.
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(overlayGo.transform, false);
        titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -70f);
        titleRect.sizeDelta = new Vector2(900f, 130f);

        titleLabel = titleGo.AddComponent<TextMeshProUGUI>();
        titleLabel.text = titleMessage;
        titleLabel.fontSize = 84f;
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.color = new Color(1f, 0.92f, 0.55f);
        titleLabel.raycastTarget = false;
        titleLabel.enableVertexGradient = true;
        titleLabel.colorGradient = new VertexGradient(
            new Color(1f, 0.95f, 0.7f),
            new Color(1f, 0.95f, 0.7f),
            new Color(1f, 0.6f, 0.15f),
            new Color(1f, 0.6f, 0.15f));
        titleLabel.fontMaterial.EnableKeyword("UNDERLAY_ON");
        titleLabel.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.75f));
        titleLabel.fontMaterial.SetFloat("_UnderlayDilate", 0.4f);
        titleLabel.fontMaterial.SetFloat("_UnderlaySoftness", 0.25f);
    }

    public void Show(List<CardData> cards, Character character)
    {
        if (cards == null || cards.Count == 0) return;
        Show(cards.Select(card => new SituationCardOffer(
            card,
            SituationCardOfferSource.Situation,
            IsCardPlayable(card, character))).ToList(), character);
    }

    public void Show(List<SituationCardOffer> offers, Character character)
    {
        if (offers == null || offers.Count == 0) return;
        IsShowing = true;
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        showCoroutine = StartCoroutine(bloom ? ShowBloomCoroutine(offers, character) : ShowCoroutine(offers, character));
    }

    private void Dismiss()
    {
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        showCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator ShowCoroutine(List<SituationCardOffer> offers, Character character)
    {
        ClearCards();

        GameObject template = DeckManager.Instance?.GetCardPrefabTemplate();
        if (template == null)
        {
            Debug.LogWarning($"[SituationCards] ShowCoroutine aborted — no card template (DeckManager.Instance={DeckManager.Instance != null}).");
            IsShowing = false;
            yield break;
        }

        Transform overlayTransform = cardContainer.transform.parent.gameObject.transform;
        overlayTransform.gameObject.SetActive(true);
        overlayGroup.alpha = 0f;

        PlayableLeader leader = character?.GetOwner() as PlayableLeader;

        // First pass: instantiate each card in its RealCard (expanded) representation.
        var rects = new List<RectTransform>();
        foreach (SituationCardOffer offer in offers)
        {
            CardData card = offer.Card;
            GameObject go = Instantiate(template, cardContainer.transform);
            go.SetActive(true);

            var cardComp = go.GetComponent<Card>();
            if (cardComp != null)
            {
                // Opportunity cards are already presented face-up in a display-only tray.
                // Render the full description (including the quote) immediately instead
                // of replaying the hand card's one-time quote typewriter animation.
                cardComp.InitializePreview(card);
                // Display-only: clicks go through the CardClickArea overlay, so the card
                // itself must never react to hover (which would flip it back into a token).
                cardComp.SuppressHoverEffects = true;
                cardComp.SetUnplayableRealCardTint(!offer.IsPlayable);
            }

            // Disable card's own raycasts so drag/hover don't fire
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) { cg.blocksRaycasts = false; cg.interactable = false; }

            if (offer.IsPlayable) AddCardClickButton(go, offer, character, leader);

            cardInstances.Add(go);
            rects.Add(go.GetComponent<RectTransform>());
        }

        // The RealCard art (its border frame) overflows the prefab's root rect, so we
        // can't rely on the token-sized layout dimensions. Measure each card's actual
        // rendered footprint and reserve that much space, then scale to fit the screen
        // side by side (the HorizontalLayoutGroup reserves space from each child's
        // sizeDelta and ignores localScale).
        Canvas.ForceUpdateCanvases();

        var footprints = new List<Vector2>();
        float maxWidth = 1f;
        foreach (RectTransform rt in rects)
        {
            Vector2 size = rt != null
                ? (Vector2)RectTransformUtility.CalculateRelativeRectTransformBounds(rt).size
                : Vector2.one;
            footprints.Add(size);
            maxWidth = Mathf.Max(maxWidth, size.x);
        }

        float available = Screen.width * 0.92f;
        float perCard = (available - cardSpacing * (offers.Count - 1)) / offers.Count;
        float scale = Mathf.Clamp(perCard / maxWidth, 0.4f, maxCardScale);

        for (int i = 0; i < rects.Count; i++)
        {
            RectTransform rt = rects[i];
            if (rt == null) continue;

            Vector2 reserved = footprints[i] * scale;
            rt.sizeDelta = reserved;

            var le = rt.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.ignoreLayout = false;
                le.preferredWidth  = reserved.x;
                le.preferredHeight = reserved.y;
            }

            // One-time "boost" pop-in instead of a persistent glow.
            rt.localScale = Vector3.zero;
            StartCoroutine(PopIn(rt, scale, 0.12f + i * 0.08f));
        }

        // Title drops/pops in, then a single party-popper confetti burst.
        if (titleRect != null)
        {
            titleRect.localScale = Vector3.zero;
            StartCoroutine(PopIn(titleRect, 1f, 0f));
        }
        SpawnConfetti();

        // Fade in
        float t = 0f;
        while (t < fadeInDuration)
        {
            overlayGroup.alpha = t / fadeInDuration;
            t += Time.deltaTime;
            yield return null;
        }
        overlayGroup.alpha = 1f;
        showCoroutine = null;
    }

    // Bloom presentation: instead of this UI's own center-screen tray, opportunity cards are
    // handed to DeckManager's CardBloomWheel (its sole purpose — there is no hand display),
    // repositioned over the acting character's hex for the duration of the offer, then
    // emptied and hidden again once resolved or dismissed (see DismissBloom).
    private IEnumerator ShowBloomCoroutine(List<SituationCardOffer> offers, Character character)
    {
        CardBloomWheel wheel = DeckManager.Instance?.GetCardBloomWheel();
        // TokenCard.prefab (not Card.prefab's real-card-only template) — the two were split
        // apart in the card refactor, so a real-card instance has no token visuals to show
        // and would render as an empty rect.
        GameObject template = DeckManager.Instance?.GetTokenCardPrefabTemplate();
        if (wheel == null || template == null || character == null || character.hex == null)
        {
            // No wheel to bloom from (or nowhere to anchor it) — fall back to the normal
            // center-screen presentation rather than silently showing nothing.
            yield return ShowCoroutine(offers, character);
            yield break;
        }

        ClearCards();

        PlayableLeader leader = character.GetOwner() as PlayableLeader;

        foreach (SituationCardOffer offer in offers)
        {
            CardData card = offer.Card;
            GameObject go = Instantiate(template, wheel.transform);
            go.SetActive(true);

            var cardComp = go.GetComponent<Card>();
            if (cardComp != null)
            {
                cardComp.UseCardArtFolderOnly = true;
                cardComp.Initialize(card);
                cardComp.SuppressHoverEffects = true;
                if (!offer.IsPlayable) cardComp.SetTokenTint(0.35f, 1f);
            }

            // The wheel drives clicks via its own geometric hit test (see SetCards), not
            // Unity's event system — disable this card's own raycasts so it can't also
            // receive OnPointerClick and misroute into Card.PlayFromBloom's hand-play flow.
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) { cg.blocksRaycasts = false; cg.interactable = false; }

            cardInstances.Add(go);
        }

        activeBloomWheel = wheel;
        wheel.SetCards(cardInstances, index => OnBloomCardClicked(index, offers, character, leader));
        wheel.SetWorldAnchor(character.hex.transform.position, Camera.main);
        wheel.SetVisible(true);
        wheel.SetForcedOpen(true);

        BuildBloomDismissCatcher();
        showCoroutine = null;
    }

    // Full-screen, invisible click-catcher behind the bloom wheel: clicking anywhere that
    // isn't a card dismisses the offer, mirroring the dim overlay's click-to-dismiss in the
    // non-bloom presentation.
    private void BuildBloomDismissCatcher()
    {
        DestroyBloomDismissCatcher();

        bloomDismissCatcher = new GameObject("BloomDismissCatcher", typeof(RectTransform));
        bloomDismissCatcher.transform.SetParent(transform, false);
        bloomDismissCatcher.transform.SetAsFirstSibling();

        var rt = bloomDismissCatcher.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var img = bloomDismissCatcher.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;

        var btn = bloomDismissCatcher.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(DismissBloom);
    }

    private void DestroyBloomDismissCatcher()
    {
        if (bloomDismissCatcher != null) Destroy(bloomDismissCatcher);
        bloomDismissCatcher = null;
    }

    private void OnBloomCardClicked(int index, List<SituationCardOffer> offers, Character character, PlayableLeader leader)
    {
        if (offers == null || index < 0 || index >= offers.Count) return;
        SituationCardOffer offer = offers[index];
        if (offer == null || !offer.IsPlayable) return;
        DismissBloom();
        ResolveCardAction(offer, character, leader);
    }

    // Tears down the bloom presentation: the wheel is exclusively an opportunity-card
    // widget (there is no hand display to hand it back to), so dismissing just empties and
    // hides it. Safe to call more than once (e.g. both the clicked card and the background
    // catcher may fire on the same click).
    private void DismissBloom()
    {
        if (activeBloomWheel == null) return;

        activeBloomWheel.ClearWorldAnchor();
        activeBloomWheel.SetForcedOpen(false);
        activeBloomWheel.SetCards(null);
        activeBloomWheel.SetVisible(false);
        activeBloomWheel = null;

        DestroyBloomDismissCatcher();
        ClearCards();

        if (showCoroutine != null) { StopCoroutine(showCoroutine); showCoroutine = null; }
        IsShowing = false;
    }

    // Scale-up with an elastic overshoot (easeOutBack), after an optional delay.
    private IEnumerator PopIn(RectTransform rt, float targetScale, float delay)
    {
        if (rt == null) yield break;
        rt.localScale = Vector3.zero;

        float t = -delay;
        const float dur = 0.42f;
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        while (t < dur)
        {
            if (rt == null) yield break;
            t += Time.unscaledDeltaTime;
            if (t < 0f) { yield return null; continue; }

            float p = Mathf.Clamp01(t / dur);
            float eased = 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
            rt.localScale = Vector3.one * (targetScale * eased);
            yield return null;
        }

        if (rt != null) rt.localScale = Vector3.one * targetScale;
    }

    private void SpawnConfetti()
    {
        if (cardContainer == null) return;
        Transform overlay = cardContainer.transform.parent;
        if (overlay == null) return;

        var go = new GameObject("Confetti", typeof(RectTransform));
        go.transform.SetParent(overlay, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.SetAsLastSibling();

        go.AddComponent<SituationConfettiBurst>().Emit(confettiCount, Screen.width, confettiDuration);
        cardInstances.Add(go); // ensure cleanup if dismissed mid-burst
    }

    private void AddCardClickButton(GameObject cardGo, SituationCardOffer offer, Character character, PlayableLeader leader)
    {
        var btnGo = new GameObject("CardClickArea");
        btnGo.transform.SetParent(cardGo.transform, false);
        btnGo.transform.SetAsLastSibling();

        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // Transparent but raycast-target so it intercepts clicks over the card
        var img = btnGo.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;

        // The card root's own CanvasGroup has blocksRaycasts=false (set just before this is
        // called, to stop the card's hover/drag effects from firing) — without its own
        // CanvasGroup overriding that, this click area would inherit blocksRaycasts=false too
        // and every click would fall through to the dim background behind it (Dismiss()).
        var cg = btnGo.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;
        cg.ignoreParentGroups = true;

        var btn = btnGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => OnCardClicked(offer, character, leader));
    }

    // Center-tray click: fade the overlay out, then resolve the card via the shared path.
    private void OnCardClicked(SituationCardOffer offer, Character character, PlayableLeader leader)
    {
        if (offer == null || !offer.IsPlayable) return;
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        showCoroutine = StartCoroutine(FadeOut());
        ResolveCardAction(offer, character, leader);
    }

    // Resolves and executes the card's action right away, the same way a hand card
    // resolves on click (Card.HandleActionCardPlayed) — the "Act now!" presentation
    // promises an immediate effect, not a card quietly parked in hand for later. Shared by
    // both the center-tray click (OnCardClicked) and the bloom-wheel click (OnBloomCardClicked).
    private async void ResolveCardAction(SituationCardOffer offer, Character character, PlayableLeader leader)
    {
        CardData cardData = offer?.Card;
        if (cardData == null || character == null || leader == null || DeckManager.Instance == null)
        {
            Debug.Log($"[SituationCards] click aborted — cardData={cardData != null} character={character != null} leader={leader != null} deckManager={DeckManager.Instance != null}");
            return;
        }

        if (cardData.GetCardType() == CardTypeEnum.Character)
        {
            bool succeeded = ResolveCharacterOpportunity(cardData, character, leader);
            Debug.Log($"[SituationCards] '{cardData.name}' character opportunity resolved, succeeded={succeeded}");
            if (succeeded) leader.RecordPlayedCard(cardData);
            return;
        }

        string actionRef = cardData.GetActionRef();
        if (string.IsNullOrWhiteSpace(actionRef))
        {
            Debug.Log($"[SituationCards] click aborted — '{cardData.name}' has no actionRef");
            return;
        }

        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        CharacterAction action = actionsManager != null ? actionsManager.ResolveActionByRef(actionRef, cardData) : null;
        if (action == null)
        {
            Debug.Log($"[SituationCards] click aborted — could not resolve action '{actionRef}' for '{cardData.name}' (actionsManager={actionsManager != null})");
            return;
        }

        action.Initialize(character, cardData);
        if (!action.FulfillsConditions())
        {
            Debug.Log($"[SituationCards] click aborted — '{cardData.name}' action '{actionRef}' no longer FulfillsConditions()");
            return;
        }

        // Route through the normal hand-consume path so resource costs, discard-pile
        // bookkeeping, and card history are applied exactly as they are for a card
        // played straight out of hand.
        bool consumed = offer.Source == SituationCardOfferSource.AI
            ? DeckManager.Instance.TryConsumeActionCardFromFullDeck(leader, actionRef, cardData, out _)
            : DeckManager.Instance.TryAddCardToHand(leader, cardData)
                && DeckManager.Instance.TryConsumeActionCard(leader, actionRef, false, out _, cardData.name);
        if (!consumed)
        {
            Debug.Log($"[SituationCards] click aborted — could not consume '{cardData.name}' from {offer.Source} source (actionRef={actionRef})");
            return;
        }

        DeckManager.Instance.ApplyMapRevealForPlayedCard(leader, cardData);

        action.Initialize(character, cardData);
        await action.Execute();

        Debug.Log($"[SituationCards] '{cardData.name}' action executed, succeeded={action.LastExecutionSucceeded}");

        if (action.LastExecutionSucceeded)
        {
            leader.RecordPlayedCard(cardData);
        }
    }

    private static bool IsCardPlayable(CardData cardData, Character character)
    {
        if (cardData == null || character == null) return false;
        string actionRef = cardData.GetActionRef();
        if (string.IsNullOrWhiteSpace(actionRef)) return cardData.EvaluatePlayability(character);

        ActionsManager manager = FindFirstObjectByType<ActionsManager>();
        CharacterAction action = manager != null ? manager.ResolveActionByRef(actionRef, cardData) : null;
        if (action == null) return false;
        action.Initialize(character, cardData);
        return cardData.EvaluatePlayability(character, null, _ => action.FulfillsConditions());
    }

    private static bool ResolveCharacterOpportunity(CardData cardData, Character actor, PlayableLeader leader)
    {
        Hex hex = actor?.hex;
        PC pc = hex?.GetPCData();
        if (cardData == null || actor == null || leader == null || pc == null) return false;
        if (!CardNameUtility.Equals(pc.pcName, cardData.startingPC)) return false;
        if (!cardData.EvaluatePlayability(actor)) return false;

        Character existing = FindObjectsByType<Character>(FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null && !candidate.killed
                && (CardNameUtility.Equals(candidate.characterName, cardData.name)
                    || (!string.IsNullOrWhiteSpace(cardData.characterGroup)
                        && string.Equals(candidate.characterGroup, cardData.characterGroup, System.StringComparison.OrdinalIgnoreCase))));

        if (existing == null && !leader.HasCharacterSlot())
        {
            MessageDisplay.ShowMessage("No character slots available.", Color.red);
            return false;
        }

        CharacterInstantiator instantiator = null;
        InspireEffect inspireEffect = null;
        if (existing == null)
        {
            instantiator = FindFirstObjectByType<CharacterInstantiator>();
            if (instantiator == null) return false;
        }
        else
        {
            Leader owner = existing.GetOwner();
            bool enemy = owner != null && owner != leader && owner.GetAlignment() != leader.GetAlignment();
            if (!enemy)
            {
                inspireEffect = InspireEffectFactory.CreateFromCardData(cardData);
                if (inspireEffect == null) return false;
            }
        }

        if (!DeckManager.Instance.TryPayOpportunityCardCosts(leader, cardData)) return false;

        if (existing == null)
        {
            BiomeConfig config = new()
            {
                characterName = cardData.name,
                alignment = (AlignmentEnum)cardData.alignment,
                race = cardData.race,
                sex = SexEnum.Male,
                commander = cardData.commander,
                agent = cardData.agent,
                emmissary = cardData.emmissary,
                mage = cardData.mage,
                artifacts = cardData.artifacts != null ? new List<string>(cardData.artifacts) : new List<string>()
            };

            Character recruited = instantiator.InstantiateCharacter(leader, hex, config);
            if (recruited == null) return false;
            recruited.startingCharacter = false;
            recruited.characterGroup = cardData.characterGroup;
            recruited.hasActionedThisTurn = true;
            recruited.isPlayerControlled = true;
            leader.TryConsumeCharacterSlot();
            hex.RedrawCharacters();
            MessageDisplayNoUI.ShowMessage(hex, recruited, $"{cardData.name} has joined {leader.characterName}.", Color.green, recordRumour: false);
            return true;
        }

        Leader existingOwner = existing.GetOwner();
        bool heldByEnemy = existingOwner != null && existingOwner != leader
            && existingOwner.GetAlignment() != leader.GetAlignment();
        if (heldByEnemy)
        {
            int turns = UnityEngine.Random.value < 0.5f ? 1 : 3;
            existing.BecomeDoubleAgent(leader, turns);
            EventIconsManager.ShowHexAnchoredMessage(EventIconType.HexMessage, existing.hex, hex,
                $"{cardData.name} is doubled for {turns} turns", Color.yellow);
            return true;
        }

        inspireEffect.Apply(leader);
        MessageDisplayNoUI.ShowMessage(hex, existing,
            $"The presence of {cardData.name} inspires {pc.pcName}.", Color.cyan, recordRumour: false);
        return true;
    }

    private IEnumerator FadeOut()
    {
        float start = overlayGroup != null ? overlayGroup.alpha : 1f;
        float t = 0f;
        while (t < fadeOutDuration)
        {
            if (overlayGroup != null) overlayGroup.alpha = Mathf.Lerp(start, 0f, t / fadeOutDuration);
            t += Time.deltaTime;
            yield return null;
        }
        if (overlayGroup != null) overlayGroup.alpha = 0f;

        Transform overlayTransform = cardContainer?.transform.parent;
        if (overlayTransform != null) overlayTransform.gameObject.SetActive(false);

        ClearCards();
        showCoroutine = null;
        IsShowing = false;
    }

    private void ClearCards()
    {
        foreach (var go in cardInstances)
            if (go != null) Destroy(go);
        cardInstances.Clear();
    }

}

// One-shot party-popper confetti: spawns colored pieces that fan upward and out,
// fall under gravity, tumble and fade, then the whole burst destroys itself.
public class SituationConfettiBurst : MonoBehaviour
{
    private struct Piece
    {
        public RectTransform rt;
        public Image img;
        public Vector2 velocity;
        public float angularVelocity;
    }

    private static readonly Color[] Palette =
    {
        new Color(1f, 0.30f, 0.36f),
        new Color(1f, 0.78f, 0.22f),
        new Color(0.36f, 0.78f, 1f),
        new Color(0.52f, 0.93f, 0.45f),
        new Color(0.85f, 0.52f, 1f),
        Color.white,
    };

    private const float Gravity = -2200f;

    private readonly List<Piece> pieces = new();
    private float duration = 1.5f;
    private float life;

    public void Emit(int count, float spread, float lifetime)
    {
        duration = Mathf.Max(0.1f, lifetime);

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Piece", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(Random.Range(8f, 14f), Random.Range(12f, 22f));
            rt.anchoredPosition = new Vector2(Random.Range(-0.08f, 0.08f) * spread, 0f);
            rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));

            var img = go.GetComponent<Image>();
            img.color = Palette[Random.Range(0, Palette.Length)];
            img.raycastTarget = false;

            float angle = Random.Range(35f, 145f) * Mathf.Deg2Rad; // upward fan
            float speed = Random.Range(900f, 1900f);
            pieces.Add(new Piece
            {
                rt = rt,
                img = img,
                velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed),
                angularVelocity = Random.Range(-540f, 540f),
            });
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        life += dt;
        float fade = Mathf.Clamp01(1f - life / duration);

        for (int i = 0; i < pieces.Count; i++)
        {
            Piece p = pieces[i];
            if (p.rt == null) continue;

            Vector2 v = p.velocity;
            v.y += Gravity * dt;
            pieces[i] = new Piece { rt = p.rt, img = p.img, velocity = v, angularVelocity = p.angularVelocity };

            p.rt.anchoredPosition += v * dt;
            p.rt.Rotate(0f, 0f, p.angularVelocity * dt);
            if (p.img != null)
            {
                Color c = p.img.color;
                c.a = fade;
                p.img.color = c;
            }
        }

        if (life >= duration) Destroy(gameObject);
    }
}
