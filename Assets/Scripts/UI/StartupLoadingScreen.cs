using System.Collections;
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
    [SerializeField] private Image cardImage;
    [SerializeField] private float fadeSeconds = 0.25f;
    [SerializeField] private float cardRotationSeconds = 1.2f;

    private Illustrations illustrations;

    private IEnumerator Start()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        SetVisible(true);
        if (progressBar != null) progressBar.value = 0.02f;
        SetStatus("> Preparing the Runeboard <");

        // Ensure this canvas is actually presented before synchronous deck parsing begins.
        yield return null;

        Coroutine cardRotation = cardImage != null ? StartCoroutine(RotateCardArt()) : null;

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

        if (cardRotation != null) StopCoroutine(cardRotation);

        if (progressBar != null) progressBar.value = 1f;
        SetStatus("Done!");
        yield return FadeOut();
        SetVisible(false);
        gameObject.SetActive(false);
        Debug.Log("StartupLoadingScreen: startup complete; menu input unblocked.");
    }

    // Cycles the loading screen's card art through whatever art has finished loading so far.
    // Only reads sprites Illustrations has already loaded (GetRandomCardArt never triggers a new
    // load), so this can't add work during DeckManager's synchronous catalog parse — that block
    // freezes the whole main thread regardless, and this coroutine simply doesn't get to run
    // until it's over. Once Illustrations starts streaming sprites in (which does yield every
    // frame), swapping a UI Image's sprite reference is effectively free.
    private IEnumerator RotateCardArt()
    {
        Sprite lastSprite = null;
        while (true)
        {
            Sprite candidate = illustrations != null ? illustrations.GetRandomCardArt() : null;
            if (candidate != null && candidate != lastSprite)
            {
                cardImage.sprite = candidate;
                cardImage.enabled = true;
                lastSprite = candidate;
            }
            yield return new WaitForSeconds(cardRotationSeconds);
        }
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
}
