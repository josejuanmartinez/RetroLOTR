using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SituationCardsUI : MonoBehaviour
{
    public static SituationCardsUI Instance { get; private set; }

    private CanvasGroup overlayGroup;
    private GameObject cardContainer;
    private Coroutine showCoroutine;
    private readonly List<GameObject> cardInstances = new();

    private const float FadeInDuration  = 0.35f;
    private const float FadeOutDuration = 0.3f;
    private const float CardScale       = 1.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
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
        hLayout.spacing = 40f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childForceExpandWidth  = false;
        hLayout.childForceExpandHeight = false;
        hLayout.childControlWidth  = false;
        hLayout.childControlHeight = false;
        var fitter = cardContainer.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
    }

    public void Show(List<CardData> cards, Character character)
    {
        if (cards == null || cards.Count == 0) return;
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        showCoroutine = StartCoroutine(ShowCoroutine(cards, character));
    }

    private void Dismiss()
    {
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        showCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator ShowCoroutine(List<CardData> cards, Character character)
    {
        ClearCards();

        GameObject template = DeckManager.Instance?.GetCardPrefabTemplate();
        if (template == null) yield break;

        Transform overlayTransform = cardContainer.transform.parent.gameObject.transform;
        overlayTransform.gameObject.SetActive(true);
        overlayGroup.alpha = 0f;

        Vector2 cardSize = DeckManager.Instance != null ? DeckManager.Instance.GetCardSize() : new Vector2(120f, 170f);
        PlayableLeader leader = character?.GetOwner() as PlayableLeader;

        foreach (CardData card in cards)
        {
            GameObject go = Instantiate(template, cardContainer.transform);
            go.SetActive(true);

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = cardSize;
                rt.localScale = Vector3.one * CardScale;
            }

            var le = go.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.ignoreLayout = false;
                le.preferredWidth  = cardSize.x;
                le.preferredHeight = cardSize.y;
            }

            var cardComp = go.GetComponent<Card>();
            cardComp?.Initialize(card);

            // Disable card's own raycasts so drag/hover don't fire
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) { cg.blocksRaycasts = false; cg.interactable = false; }

            AddGoldenGlow(go);
            AddCardClickButton(go, card, leader);
            cardInstances.Add(go);
        }

        // Fade in
        float t = 0f;
        while (t < FadeInDuration)
        {
            overlayGroup.alpha = t / FadeInDuration;
            t += Time.deltaTime;
            yield return null;
        }
        overlayGroup.alpha = 1f;
        showCoroutine = null;
    }

    private void AddCardClickButton(GameObject cardGo, CardData cardData, PlayableLeader leader)
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

        var btn = btnGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => OnCardClicked(cardData, leader));
    }

    private void OnCardClicked(CardData cardData, PlayableLeader leader)
    {
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        if (leader != null && DeckManager.Instance != null)
            DeckManager.Instance.TryAddCardToHand(leader, cardData);
        showCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float start = overlayGroup != null ? overlayGroup.alpha : 1f;
        float t = 0f;
        while (t < FadeOutDuration)
        {
            if (overlayGroup != null) overlayGroup.alpha = Mathf.Lerp(start, 0f, t / FadeOutDuration);
            t += Time.deltaTime;
            yield return null;
        }
        if (overlayGroup != null) overlayGroup.alpha = 0f;

        Transform overlayTransform = cardContainer?.transform.parent;
        if (overlayTransform != null) overlayTransform.gameObject.SetActive(false);

        ClearCards();
        showCoroutine = null;
    }

    private void ClearCards()
    {
        foreach (var go in cardInstances)
            if (go != null) Destroy(go);
        cardInstances.Clear();
    }

    private void AddGoldenGlow(GameObject cardGo)
    {
        var glowGo = new GameObject("GoldenGlow");
        glowGo.transform.SetParent(cardGo.transform, false);
        glowGo.transform.SetAsFirstSibling();

        var rt = glowGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = new Vector2(20f, 20f);
        rt.anchoredPosition = Vector2.zero;

        var img = glowGo.AddComponent<Image>();
        img.color = new Color(1f, 0.82f, 0.1f, 0.5f);
        img.raycastTarget = false;

        glowGo.AddComponent<SituationCardGoldenPulse>();
    }
}

public class SituationCardGoldenPulse : MonoBehaviour
{
    private Image img;

    private void Awake() { img = GetComponent<Image>(); }

    private void Update()
    {
        if (img == null) return;
        float a = 0.25f + 0.25f * Mathf.Sin(Time.time * 2.8f);
        img.color = new Color(1f, 0.82f, 0.1f, a);
    }
}
