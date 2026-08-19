using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Blocks input while the startup card catalog and illustration cache become ready.</summary>
public sealed class StartupLoadingScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private List<CardDataProvider> cardProviders = new();
    [SerializeField] private TextMeshProUGUI continuePromptText;
    [SerializeField] private float fadeSeconds = 0.25f;
    [Tooltip("How often (in seconds) each card slot re-rolls to a new random card.")]
    [SerializeField] private float rotationSeconds = 3f;
    [Tooltip("Total duration of the flip animation played when a card slot changes card.")]
    [SerializeField] private float cardFlipSeconds = 0.4f;

    private Illustrations illustrations;
    private readonly Dictionary<CardDataProvider, Coroutine> activeFlips = new();
    private readonly Dictionary<CardDataProvider, Vector3> cardRestScales = new();

    private IEnumerator Start()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        SetVisible(true);
        SetContinuePromptVisible(false);
        if (progressBar != null) progressBar.value = 0.02f;
        SetStatus("> Preparing the Runeboard <");

        // Ensure this canvas is actually presented before synchronous deck parsing begins.
        yield return null;

        Coroutine cardRotation = cardProviders != null && cardProviders.Count > 0 ? StartCoroutine(RotateCardArt()) : null;

        float waitStartedAt = Time.realtimeSinceStartup;
        while (!StartupReady() && Time.realtimeSinceStartup - waitStartedAt < 30f)
        {
            float deckProgress = DeckManager.Instance != null && DeckManager.Instance.IsLoaded ? 1f : 0.08f;
            float artProgress = illustrations != null ? illustrations.LoadProgress : 0.02f;
            float combined = Mathf.Clamp01(deckProgress * 0.25f + artProgress * 0.75f);
            if (progressBar != null) progressBar.value = combined;
            SetStatus(deckProgress < 1f
                ? $"Reading the Decks ..."
                : $"Illuminating the Cards ...");
            yield return null;
        }

        if (progressBar != null) progressBar.value = 1f;
        SetStatus("Done!");
        SetContinuePromptVisible(true);

        // Keep cycling card art and wait for the player to press a key or click, rather than
        // auto-advancing the instant assets are ready.
        while (!Input.anyKeyDown)
        {
            yield return null;
        }

        if (cardRotation != null) StopCoroutine(cardRotation);
        SetContinuePromptVisible(false);

        yield return FadeOut();
        SetVisible(false);
        gameObject.SetActive(false);
        Debug.Log("StartupLoadingScreen: startup complete; menu input unblocked.");
    }

    // Re-rolls each of the loading screen's card slots to a random card from the full catalog.
    // Waits for DeckManager to be loaded (the catalog it reads from) before rolling; this can't
    // add work during DeckManager's synchronous catalog parse — that block freezes the whole main
    // thread regardless, and this coroutine simply doesn't get to run until it's over.
    private IEnumerator RotateCardArt()
    {
        while (true)
        {
            List<CardData> catalog = DeckManager.Instance != null && DeckManager.Instance.IsLoaded
                ? DeckManager.Instance.cards
                : null;

            // Encounter cards always render face-down (a "?" overlay) until actually played, so
            // they'd just sit there unrevealed if rolled here — exclude them from the pool.
            List<CardData> candidates = catalog?
                .Where(card => card != null && !string.IsNullOrWhiteSpace(card.name) && !card.IsEncounterCard())
                .ToList();

            if (candidates != null && candidates.Count > 0)
            {
                foreach (CardDataProvider provider in cardProviders)
                {
                    if (provider == null) continue;
                    CardData candidate = candidates[Random.Range(0, candidates.Count)];

                    if (activeFlips.TryGetValue(provider, out Coroutine running) && running != null)
                    {
                        StopCoroutine(running);
                    }
                    activeFlips[provider] = StartCoroutine(FlipToCard(provider, candidate.name));
                }
            }

            yield return new WaitForSeconds(rotationSeconds);
        }
    }

    // Card-flip transition: shrinks the slot flat (scale.x -> 0), swaps to the new card at the
    // midpoint so the reveal lands exactly on the "edge-on" frame, then unfolds back out.
    private IEnumerator FlipToCard(CardDataProvider provider, string cardName)
    {
        Transform cardTransform = provider.transform;
        if (!cardRestScales.TryGetValue(provider, out Vector3 restScale))
        {
            // First flip for this slot: snapshot whatever scale was authored on it (the "Cards"
            // row scales these down to fit 3 side by side), so the flip always ends back there
            // instead of snapping to Vector3.one.
            restScale = cardTransform.localScale;
            cardRestScales[provider] = restScale;
        }
        float halfDuration = Mathf.Max(0.01f, cardFlipSeconds * 0.5f);

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float scaleX = Mathf.Lerp(restScale.x, 0f, elapsed / halfDuration);
            cardTransform.localScale = new Vector3(scaleX, restScale.y, restScale.z);
            yield return null;
        }
        cardTransform.localScale = new Vector3(0f, restScale.y, restScale.z);

        provider.Initialize(cardName, provider.startAsToken);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float scaleX = Mathf.Lerp(0f, restScale.x, elapsed / halfDuration);
            cardTransform.localScale = new Vector3(scaleX, restScale.y, restScale.z);
            yield return null;
        }
        cardTransform.localScale = restScale;
        activeFlips[provider] = null;
    }

    private bool StartupReady()
    {
        DeckManager[] deckManagers = FindObjectsByType<DeckManager>(FindObjectsSortMode.None);
        Illustrations[] illustrationServices = FindObjectsByType<Illustrations>(FindObjectsSortMode.None);
        illustrations = illustrationServices.FirstOrDefault(service => service.IsLoaded)
            ?? illustrationServices.FirstOrDefault();
        return deckManagers.Any(manager => manager.IsLoaded)
            && illustrationServices.Any(service => service.IsLoaded);
    }

    private IEnumerator FadeOut()
    {
        if (canvasGroup == null || fadeSeconds <= 0f) yield break;
        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
            yield return null;
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void SetStatus(string value)
    {
        if (statusText != null) statusText.text = value;
    }

    private void SetContinuePromptVisible(bool visible)
    {
        if (continuePromptText != null) continuePromptText.gameObject.SetActive(visible);
    }
}
