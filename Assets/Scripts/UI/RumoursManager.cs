using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public struct Rumour
{
    public Leader leader;
    public string characterName;
    public string rumour;
    public Vector2Int v2;
}

public class RumoursManager : MonoBehaviour
{
    public static RumoursManager Instance { get; private set; }

    [Header("Config")]
    public int rumoursShown = 25;

    [Header("References")]
    public TextMeshProUGUI textWidget;
    public ScrollRect scrollRect;

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

    public static void AddRumour(Rumour rumour, bool isPublic)
    {
        if (!EnsureInstance(nameof(AddRumour)))
            return;

        if (isPublic)
        {
            Instance.rumours.Add(rumour);
            UpdateRumourText();
        }
        else
        {
            // Strip location for private rumours from enemies so location isn't leaked
            Rumour sanitized = rumour;
            sanitized.v2 = default;
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

        Instance.rumours.Add(rumour);
        UpdateRumourText();
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
        return totalRumours;
    }

    /// <summary>
    /// Updates the textWidget to show up to MAX_RUMOURS_SHOWN latest rumours.
    /// </summary>
    private static void UpdateRumourText()
    {
        if (!EnsureInstance(nameof(UpdateRumourText)))
            return;

        if (!Instance.game.IsPlayerCurrentlyPlaying()) return;
        
        if (Instance.textWidget == null)
            return;

        if (Instance.rumours.Count == 0)
        {
            Instance.textWidget.text = string.Empty;
            return;
        }

        // How many to show (can't be more than we have)
        int toShow = Mathf.Min(Instance.rumoursShown, Instance.rumours.Count);

        // Start index so we get the last `toShow` items
        int startIndex = Instance.rumours.Count - toShow;

        // Safe GetRange: startIndex >= 0, count == toShow, and startIndex + toShow <= Count
        List<Rumour> recentRumours = Instance.rumours.GetRange(startIndex, toShow);

        Instance.textWidget.text = string.Join("\n", recentRumours.ConvertAll(FormatRumourText));
    }

    private static string FormatRumourText(Rumour rumour)
    {
        string leaderName = rumour.leader?.characterName ?? "Unknown";
        string characterName = !string.IsNullOrWhiteSpace(rumour.characterName) ? rumour.characterName : leaderName;
        string locationPart = rumour.v2 != default ? $" at ({rumour.v2.x},{rumour.v2.y})" : string.Empty;
        string body = string.IsNullOrWhiteSpace(rumour.rumour) ? "No details" : rumour.rumour;
        return $"[{leaderName}]({characterName}){locationPart}: {body}";
    }

    private static bool EnsureInstance(string caller)
    {
        if (Instance != null)
            return true;

        Debug.LogWarning($"RumoursManager.{caller} called before the singleton instance was initialized.");
        return false;
    }
}
