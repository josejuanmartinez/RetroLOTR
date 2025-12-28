using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public struct Rumour
{
    public Leader leader;
    public Character character;
    public string characterName;
    public string rumour;
    public Vector2Int v2;
    public bool seen;
}

public class RumoursManager : MonoBehaviour
{
    public static RumoursManager Instance { get; private set; }

    [Header("Config")]
    public int rumoursShown = 25;

    [Header("References")]
    public GameObject rumourIconPrefab;
    public GridLayoutGroup rumoursGridLayout;
    public CanvasGroup rumoursCanvasGroup;

    private List<Rumour> rumours = new();
    private List<Rumour> privateRumours = new();
    
    private Game game;
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        game = FindFirstObjectByType<Game>();
    }

    public void Show()
    {
        if (rumours.Count < 1)
        {
            _ = ConfirmationDialog.AskOk("No rumours yet. Hear Stories with an Emmissary to get information about the world");
            return;
        }

        if (rumoursCanvasGroup != null)
        {
            rumoursCanvasGroup.alpha = 1f;
            rumoursCanvasGroup.interactable = true;
            rumoursCanvasGroup.blocksRaycasts = true;
        }

        MarkAllPublicRumoursSeen();
    }

    public void Close()
    {
        if (rumoursCanvasGroup != null)
        {
            rumoursCanvasGroup.alpha = 0f;
            rumoursCanvasGroup.interactable = false;
            rumoursCanvasGroup.blocksRaycasts = false;
            return;
        }
    }

    public static void AddRumour(Rumour rumour, bool isPublic)
    {
        if (!EnsureInstance(nameof(AddRumour)))
            return;

        if (isPublic)
        {
            rumour.seen = false;
            Instance.rumours.Add(rumour);
            UpdateRumourText();
            RefreshNewRumoursUI();
        }
        else
        {
            // Strip location for private rumours from enemies so location isn't leaked
            Rumour sanitized = rumour;
            sanitized.v2 = default;
            sanitized.seen = false;
            Instance.privateRumours.Add(sanitized);
        }
    }

    /// <summary>
    /// Promote a rumour from the private pool into the public list, avoiding duplicates.
    /// Used for "doubled" characters that should always leak their actions.
    /// </summary>
    public static void PromoteRumourToPublic(Rumour rumour)
    {
        if (!EnsureInstance(nameof(PromoteRumourToPublic)))
            return;

        // Remove one matching private copy so we don't double-count later reveals
        int privateIndex = Instance.privateRumours.FindIndex(r =>
            r.leader == rumour.leader &&
            r.rumour == rumour.rumour);
        if (privateIndex >= 0)
        {
            Instance.privateRumours.RemoveAt(privateIndex);
        }

        // Skip if already public
        bool alreadyPublic = Instance.rumours.Exists(r =>
            r.leader == rumour.leader &&
            r.rumour == rumour.rumour &&
            r.v2 == rumour.v2);
        if (alreadyPublic) return;

        rumour.seen = false;
        Instance.rumours.Add(rumour);
        UpdateRumourText();
        RefreshNewRumoursUI();
    }


    /// <summary>
    /// Moves the last `qty` private rumours into the public list,
    /// then updates the UI.
    /// </summary>
    public static int GetRumours(AlignmentEnum alignment, int enemyRumoursQty, int friendlyRumoursQty)
    {
        if (!EnsureInstance(nameof(GetRumours)))
            return 0;

        if (enemyRumoursQty + friendlyRumoursQty <= 0 || Instance.privateRumours.Count == 0)
            return 0;

        int enemyAvailable = 0;
        int friendlyAvailable = 0;
        foreach (Rumour rumour in Instance.privateRumours)
        {
            if (rumour.leader == Instance.game.player) continue;
            bool isFriendly = rumour.leader.alignment == alignment && rumour.leader.alignment != AlignmentEnum.neutral;
            if (isFriendly)
            {
                friendlyAvailable++;
            }
            else
            {
                enemyAvailable++;
            }
        }

        // Clamp qty so we don't ask for more than exist
        enemyRumoursQty = Mathf.Clamp(enemyRumoursQty, 0, enemyAvailable);
        friendlyRumoursQty = Mathf.Clamp(friendlyRumoursQty, 0, friendlyAvailable);

        int totalRumours = enemyRumoursQty + friendlyRumoursQty;

        List<int> toRemove = new();
        for(int i=Instance.privateRumours.Count-1; i>=0;i--)
        {
            if(enemyRumoursQty + friendlyRumoursQty <= 0) break;
            Rumour rumour = Instance.privateRumours[i];
            if(enemyRumoursQty > 0 && (rumour.leader.alignment != alignment || rumour.leader.alignment == AlignmentEnum.neutral))
            {
                AddRumour(rumour, true);
                toRemove.Add(i);
                enemyRumoursQty--;
            }
            if(friendlyRumoursQty > 0 && rumour.leader.alignment == alignment && rumour.leader.alignment != AlignmentEnum.neutral)
            {
                AddRumour(rumour, true);
                toRemove.Add(i);
                friendlyRumoursQty--;
            }
        }
        
        toRemove.ForEach(x => Instance.privateRumours.RemoveAt(x));

        UpdateRumourText();
        RefreshNewRumoursUI();
        return totalRumours;
    }

    public static int GetUnseenRumoursCount(Leader leader)
    {
        if (leader == null || !EnsureInstance(nameof(GetUnseenRumoursCount)))
            return 0;

        int count = 0;
        for (int i = 0; i < Instance.rumours.Count; i++)
        {
            Rumour rumour = Instance.rumours[i];
            if (rumour.leader == leader && !rumour.seen)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Updates the textWidget to show up to MAX_RUMOURS_SHOWN latest rumours.
    /// </summary>
    private static void UpdateRumourText()
    {
        if (!EnsureInstance(nameof(UpdateRumourText)))
            return;

        if (!Instance.game.IsPlayerCurrentlyPlaying()) return;

        if (Instance.rumourIconPrefab == null || Instance.rumoursGridLayout == null)
            return;

        for (int i = Instance.rumoursGridLayout.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(Instance.rumoursGridLayout.transform.GetChild(i).gameObject);
        }

        if (Instance.rumours.Count == 0)
            return;

        int toShow = Mathf.Min(Instance.rumoursShown, Instance.rumours.Count);
        int startIndex = Instance.rumours.Count - toShow;
        List<Rumour> recentRumours = Instance.rumours.GetRange(startIndex, toShow);

        for (int i = 0; i < recentRumours.Count; i++)
        {
            Rumour rumour = recentRumours[i];
            string formatted = FormatRumourText(rumour);
            if (string.IsNullOrWhiteSpace(formatted)) continue;

            Character iconCharacter = rumour.character != null ? rumour.character : rumour.leader;
            CharacterIconWithText icon = Instantiate(Instance.rumourIconPrefab, Instance.rumoursGridLayout.transform).GetComponent<CharacterIconWithText>();
            icon.Initialize(iconCharacter, formatted);
        }
    }

    private static string FormatRumourText(Rumour rumour)
    {
        string leaderName = rumour.leader?.characterName ?? "Unknown";
        string body = string.IsNullOrWhiteSpace(rumour.rumour) ? string.Empty : rumour.rumour.Trim();
        if (string.IsNullOrEmpty(body)) return string.Empty;
        return $"[{leaderName}] {body}";
    }

    private void MarkAllPublicRumoursSeen()
    {
        if (rumours.Count == 0) return;

        for (int i = 0; i < rumours.Count; i++)
        {
            Rumour rumour = rumours[i];
            if (rumour.seen) continue;
            rumour.seen = true;
            rumours[i] = rumour;
        }

        RefreshNewRumoursUI();
    }

    private static void RefreshNewRumoursUI()
    {
        if (!EnsureInstance(nameof(RefreshNewRumoursUI)))
            return;

        PlayableLeaderIcons icons = FindFirstObjectByType<PlayableLeaderIcons>();
        if (icons != null) icons.RefreshNewRumoursCounts();
    }

    private static bool EnsureInstance(string caller)
    {
        if (Instance != null)
            return true;

        Instance = FindFirstObjectByType<RumoursManager>();
        if (Instance != null)
        {
            if (Instance.game == null)
            {
                Instance.game = FindFirstObjectByType<Game>();
            }
            return true;
        }

        Debug.LogWarning($"RumoursManager.{caller} called before the singleton instance was initialized.");
        return false;
    }
}
