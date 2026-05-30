using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(CanvasGroup))]
public class SelectedCharacterIcon : MonoBehaviour
{
    [Header("Game Objects")]
    public GameObject levelsGameObject;
    public GameObject moved;
    public GameObject actioned;
    public GameObject unactionedIcon;
    public GameObject actionedIcon;
    public GameObject border;
    public GameObject otherCharacters;

    [Header("Banner")]
    public Image bannerImage;

    [Header("Leader")]
    public Image icon;
    public RawImage rawImage;
    public VideoPlayer video;
    public TextMeshProUGUI textWidget;

    [Header("Health")]
    public Image health;

    [Header("Levels")]
    public TextMeshProUGUI commander;
    public TextMeshProUGUI agent;
    public TextMeshProUGUI emmissary;
    public TextMeshProUGUI mage;
    public TextMeshProUGUI movementLeft;

    [Header("Artifact-Status Items")]
    [FormerlySerializedAs("artifactPrefab")]
    public GameObject artifactStatusPrefab;
    [FormerlySerializedAs("artifactsGridLayoutTransform")]
    public Transform artifactStatusGridLayoutTransform;

    [Header("Played cards")]
    [SerializeField] private CanvasGroup cardCanvasGroup;
    [SerializeField] public GameObject playedCard;
    
    // private Videos videos;
    private Illustrations illustrations;
    private CanvasGroup canvasGroup;

    private Character pendingRefreshCharacter;
    public Character CurrentCharacter { get; private set; }
    private bool refreshScheduled;
    private readonly List<ArtifactRenderer> artifactStatusRenderers = new();
    private readonly List<GameObject> playedCardInstances = new();

    private void Awake()
    {
        BindPlayedCardTemplate();
        ClearPlayedCardInstances();
        SetPlayedCardVisible(false);
        if (icon != null && icon.GetComponent<CharacterShineEffect>() == null)
            icon.gameObject.AddComponent<CharacterShineEffect>();
    }

    private void OnDisable()
    {
        refreshScheduled = false;
        pendingRefreshCharacter = null;
        StopAllCoroutines();
    }

    public void Refresh(Character c)
    {
        pendingRefreshCharacter = c;
        if (refreshScheduled) return;
        refreshScheduled = true;
        StartCoroutine(RefreshNextFrame());
    }

    private System.Collections.IEnumerator RefreshNextFrame()
    {
        yield return null;
        refreshScheduled = false;
        Character c = pendingRefreshCharacter;
        pendingRefreshCharacter = null;
        if (c == null)
        {
            Hide();
            yield break;
        }
        ApplyRefresh(c);
    }

    private void ApplyRefresh(Character c)
    {
        CurrentCharacter = c;
SetVisible(true);
        border.SetActive(true);
        SetBannerImage(c);
        SetCharacterVisuals(GetIllustrationByName(!string.IsNullOrWhiteSpace(c.illustrationName) ? c.illustrationName : c.characterName));
        string baseHoverText = BuildSelectedCharacterTitle(c);
        string kidnappingText = BuildKidnappingStatusText(c);
        textWidget.text = string.IsNullOrWhiteSpace(kidnappingText)
            ? baseHoverText
            : $"{baseHoverText}\n{kidnappingText}";
        levelsGameObject.SetActive(true);
        actioned.SetActive(true);
        moved.SetActive(true);
        commander.text = c.GetCommander().ToString();
        agent.text = c.GetAgent().ToString();
        emmissary.text = c.GetEmmissary().ToString();
        mage.text = c.GetMage().ToString();
        actionedIcon.SetActive(c.hasActionedThisTurn);
        unactionedIcon.SetActive(!actionedIcon.activeSelf);
        health.gameObject.SetActive(true);
        health.fillAmount = c.health / 100f;

        RefreshArtifactStatusItems(c.artifacts, c);
        
        RefreshMovementLeft(c);
        RefreshPlayedCards(c);

        if (c != null)
        {
}
    }

    private string BuildKidnappingStatusText(Character c)
    {
        if (c == null) return string.Empty;

        List<string> parts = new();
        int captiveCount = c.kidnappedCharacters != null ? c.kidnappedCharacters.Count(x => x != null && x.character != null && !x.character.killed) : 0;
        if (captiveCount > 0)
        {
            parts.Add($"Captives: {captiveCount}");
        }

        if (c.IsKidnapped())
        {
            string kidnapperName = c.kidnappedBy != null ? c.kidnappedBy.characterName : "Unknown";
            parts.Add($"Captured by: {kidnapperName}");
        }

        return string.Join(" | ", parts);
    }

    public void RefreshHoverPreview(Character c, string hoverText, bool showHealth, bool showArtifacts)
    {
        if (c == null)
        {
            Hide();
            return;
        }

SetVisible(true);
        border.SetActive(true);
        SetBannerImage(c);
        SetCharacterVisuals(GetIllustrationByName(!string.IsNullOrWhiteSpace(c.illustrationName) ? c.illustrationName : c.characterName));
        textWidget.text = string.IsNullOrWhiteSpace(hoverText) ? BuildSelectedCharacterTitle(c) : hoverText;

        actioned.SetActive(false);
        moved.SetActive(false);
        actionedIcon.SetActive(false);
        unactionedIcon.SetActive(false);

        commander.text = c.GetCommander().ToString();
        agent.text = c.GetAgent().ToString();
        emmissary.text = c.GetEmmissary().ToString();
        mage.text = c.GetMage().ToString();
        movementLeft.text = "-";

        health.gameObject.SetActive(showHealth);
        if (showHealth) health.fillAmount = c.health / 100f;

        RefreshArtifactStatusItems(showArtifacts ? c.artifacts : null, showArtifacts ? c : null);

        ClearPlayedCardInstances();
        SetPlayedCardVisible(false);
    }


    public void RefreshForArmy(Army army)
    {
        if (army == null || army.commander == null) { Hide(); return; }

SetVisible(true);
        border.SetActive(true);
        SetCharacterVisuals(ResolveArmySprite(army));
        textWidget.text = army.GetHoverTextNoXp();

        actioned.SetActive(false);
        moved.SetActive(false);
        actionedIcon.SetActive(false);
        unactionedIcon.SetActive(false);

        commander.text = army.GetSize().ToString();
        agent.text = "-";
        emmissary.text = "-";
        mage.text = "-";
        movementLeft.text = "-";

        health.gameObject.SetActive(false);
        RefreshArtifactStatusItems(null, null);
        ClearPlayedCardInstances();
        SetPlayedCardVisible(false);
    }

    private Sprite ResolveArmySprite(Army army)
    {
        string troopName = army.GetTroopGroups().FirstOrDefault()?.troopName;
        if (!string.IsNullOrWhiteSpace(troopName))
        {
            DeckManager deckManager = FindFirstObjectByType<DeckManager>();
            if (deckManager != null)
            {
                CardData card = deckManager.cards.FirstOrDefault(c =>
                    string.Equals(c.name, troopName, System.StringComparison.OrdinalIgnoreCase));
                if (card != null)
                {
                    foreach (string candidate in new[] { card.spriteName, card.portraitName, card.name })
                    {
                        if (string.IsNullOrWhiteSpace(candidate)) continue;
                        Sprite s = GetIllustrationByName(candidate, false);
                        if (s != null) return s;
                    }
                }
            }
        }
        return null;
    }

    // Update is called once per frame
    public void Hide()
    {
SetVisible(false);
        border.SetActive(false);
        // Video path disabled for now; static illustrations only.
        // if (video != null)
        // {
        //     video.Stop();
        //     video.enabled = false;
        // }
        if (video != null) video.enabled = false;
        if (rawImage != null) rawImage.enabled = false;
        Image targetImage = GetImageTarget();
        if (targetImage != null) targetImage.enabled = false;
        textWidget.text = "";
        levelsGameObject.SetActive(false);
        actioned.SetActive(false);
        moved.SetActive(false);
        health.gameObject.SetActive(false);
        ClearPlayedCardInstances();
        SetPlayedCardVisible(false);
        pendingRefreshCharacter = null;
        CurrentCharacter = null;
        refreshScheduled = false;
    }

    public void RefreshMovementLeft(Character c)
    {
        movementLeft.text = c.GetMovementLeft().ToString();
    }

    private void SetBannerImage(Character c)
    {
        if (bannerImage == null) return;
        Leader owner = c?.GetOwner();
        Sprite sprite = ResolveBannerSprite(owner);
        bannerImage.sprite = sprite;
        bannerImage.enabled = sprite != null;
    }

    private Sprite ResolveBannerSprite(Leader owner)
    {
        if (owner == null) return null;
        LeaderBiomeConfig biome = owner.GetBiome();
        if (biome == null) return null;

        string bannerName = null;
        if (owner is PlayableLeader playableLeader)
        {
            string subdeckId = playableLeader.GetSelectedSubdeckId();
            if (!string.IsNullOrWhiteSpace(subdeckId) && biome.variants != null)
            {
                LeaderVariantConfig variant = biome.variants.Find(v =>
                    v != null
                    && ((!string.IsNullOrWhiteSpace(v.variantId) && string.Equals(v.variantId, subdeckId, System.StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(v.subdeckId) && string.Equals(v.subdeckId, subdeckId, System.StringComparison.OrdinalIgnoreCase))));
                if (!string.IsNullOrWhiteSpace(variant?.banner))
                    bannerName = variant.banner;
            }
        }

        if (string.IsNullOrWhiteSpace(bannerName))
            bannerName = biome.banner;

        if (string.IsNullOrWhiteSpace(bannerName)) return null;
        return GetIllustrationByName(bannerName, false);
    }

    private Sprite GetIllustrationByName(string name)
    {
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        return illustrations != null ? illustrations.GetIllustrationByName(name) : null;
    }

    private Sprite GetIllustrationByName(string name, bool logMissing)
    {
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        return illustrations != null ? illustrations.GetIllustrationByName(name, logMissing) : null;
    }

    // private VideoClip GetVideoByName(string name)
    // {
    //     if (videos == null) videos = FindFirstObjectByType<Videos>();
    //     return videos != null ? videos.GetVideoByName(name) : null;
    // }

    private Image GetImageTarget()
    {
        return icon;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        Image rootImage = GetComponent<Image>();
        if (rootImage != null) rootImage.enabled = visible;
    }

    private void SetCharacterVisuals(Sprite fallbackSprite)
    {
        // Video path disabled for now; static illustrations only.
        // bool hasClip = clip != null && video != null;
        //
        // if (video != null)
        // {
        //     if (hasClip)
        //     {
        //         video.enabled = true;
        //         video.clip = clip;
        //         video.Play();
        //     }
        //     else
        //     {
        //         video.Stop();
        //         video.enabled = false;
        //     }
        // }
        //
        // if (rawImage != null) rawImage.enabled = hasClip;
        if (video != null) video.enabled = false;
        if (rawImage != null) rawImage.enabled = false;

        Image targetImage = GetImageTarget();
        if (targetImage != null)
        {
            targetImage.enabled = true;
            targetImage.sprite = fallbackSprite;
        }
    }

    private void RefreshPlayedCards(Character c)
    {
        ClearPlayedCardInstances();

        if (c == null || c.playedCardSpritesThisTurn == null || c.playedCardSpritesThisTurn.Count == 0)
        {
            SetPlayedCardVisible(false);
            return;
        }

        BindPlayedCardTemplate();
        if (playedCard == null)
        {
            SetPlayedCardVisible(false);
            return;
        }

        Transform parent = cardCanvasGroup != null ? cardCanvasGroup.transform : playedCard.transform.parent;
        if (parent == null)
        {
            SetPlayedCardVisible(false);
            return;
        }

        playedCard.SetActive(false);
        for (int i = 0; i < c.playedCardSpritesThisTurn.Count; i++)
        {
            Sprite playedSprite = c.playedCardSpritesThisTurn[i];
            if (playedSprite == null) continue;

            GameObject instance = Instantiate(playedCard, parent);
            instance.name = $"PlayedCard_{i + 1}";
            instance.SetActive(true);

            PlayedCard playedCardComponent = instance.GetComponent<PlayedCard>();
            playedCardComponent?.Initialize(playedSprite);

            playedCardInstances.Add(instance);
        }

        SetPlayedCardVisible(playedCardInstances.Count > 0);
    }

    private void SetPlayedCardVisible(bool visible)
    {
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = visible ? 1f : 0f;
            cardCanvasGroup.interactable = visible;
            cardCanvasGroup.blocksRaycasts = visible;
        }
    }

    private void BindPlayedCardTemplate()
    {
        if (playedCard != null)
        {
            playedCard.SetActive(false);
            if (cardCanvasGroup == null)
            {
                Transform parent = playedCard.transform.parent;
                if (parent != null)
                {
                    cardCanvasGroup = parent.GetComponent<CanvasGroup>();
                    if (cardCanvasGroup == null)
                    {
                        cardCanvasGroup = parent.gameObject.AddComponent<CanvasGroup>();
                    }
                }
            }
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && string.Equals(child.name, "playedCard", System.StringComparison.OrdinalIgnoreCase))
            {
                playedCard = child.gameObject;
                playedCard.SetActive(false);
                if (cardCanvasGroup == null)
                {
                    Transform parent = child.parent;
                    if (parent != null)
                    {
                        cardCanvasGroup = parent.GetComponent<CanvasGroup>();
                        if (cardCanvasGroup == null)
                        {
                            cardCanvasGroup = parent.gameObject.AddComponent<CanvasGroup>();
                        }
                    }
                }
                break;
            }
        }
    }

    private void ClearPlayedCardInstances()
    {
        for (int i = 0; i < playedCardInstances.Count; i++)
        {
            GameObject instance = playedCardInstances[i];
            if (instance != null)
            {
                Destroy(instance);
            }
        }
        playedCardInstances.Clear();
    }

    private void RefreshArtifactStatusItems(List<Artifact> artifacts, Character c)
    {
        var items = new List<(string spriteName, string label)>();

        if (artifacts != null)
            foreach (Artifact a in artifacts)
                if (a != null) items.Add((a.GetSpriteString(), a.GetHoverText()));

        if (c?.statusEffects != null)
            foreach (StatusEffectEnum effect in c.statusEffects)
            {
                string spriteName = effect.ToString().ToLowerInvariant();
                if (GetIllustrationByName(spriteName, false) != null)
                    items.Add((spriteName, effect.ToString()));
            }

        for (int i = artifactStatusRenderers.Count; i < items.Count; i++)
        {
            GameObject go = Instantiate(artifactStatusPrefab, artifactStatusGridLayoutTransform);
            artifactStatusRenderers.Add(go.GetComponent<ArtifactRenderer>());
        }

        for (int i = 0; i < artifactStatusRenderers.Count; i++)
        {
            ArtifactRenderer renderer = artifactStatusRenderers[i];
            if (renderer == null) continue;
            bool active = i < items.Count;
            renderer.gameObject.SetActive(active);
            if (!active) continue;
            renderer.gameObject.name = items[i].label;
            renderer.Initialize(items[i].spriteName, items[i].label);
        }
    }

    private string BuildSelectedCharacterTitle(Character c)
    {
        if (c == null) return string.Empty;

        Army army = c.GetArmy();
        if (army == null || army.GetSize() < 1)
        {
            return $"<u>{c.characterName}</u> (wandering)";
        }

        return $"<u>{c.characterName}</u>\n{BuildArmyTroopSummary(c, army)}";
    }

    private string BuildArmyTroopSummary(Character c, Army army)
    {
        List<string> troops = army != null
            ? army.GetTroopGroups()
                .Where(group => group != null && group.amount > 0)
                .Select(group => $" - {group.amount}<sprite name=\"{group.troopType.ToString().ToLower()}\">{group.troopName}")
                .ToList()
            : new List<string>();

        return string.Join("\n", troops);
    }

}
