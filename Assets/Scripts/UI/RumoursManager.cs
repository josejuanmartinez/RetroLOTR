using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RumoursManager : MonoBehaviour
{
    public static RumoursManager Instance { get; private set; }

    [Header("Config")]
    public int rumoursShown = 25;

    [Header("References")]
    public TextMeshProUGUI textWidget;
    public ScrollRect scrollRect;

    private List<string> rumours = new();
    private List<string> privateRumours = new();

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // optional: persists across scenes
    }

    public static void AddRumour(string rumour, bool isPublic)
    {
        if (isPublic)
        {
            Instance.rumours.Add(rumour);
            UpdateRumourText();
        }
        else
        {
            Instance.privateRumours.Add(rumour);
        }
    }

    /// <summary>
    /// Moves the last `qty` private rumours into the public list,
    /// then updates the UI.
    /// </summary>
    public static void GetRumours(int qty)
    {
        if (qty <= 0 || Instance.privateRumours.Count == 0)
            return;

        // Clamp qty so we don't ask for more than exist
        qty = Mathf.Clamp(qty, 0, Instance.privateRumours.Count);

        int startIndex = Instance.privateRumours.Count - qty;

        // Safe GetRange: startIndex is >= 0 and qty <= Count
        Instance.rumours.AddRange(Instance.privateRumours.GetRange(startIndex, qty));

        UpdateRumourText();
    }

    /// <summary>
    /// Updates the textWidget to show up to MAX_RUMOURS_SHOWN latest rumours.
    /// </summary>
    private static void UpdateRumourText()
    {
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
        List<string> recentRumours = Instance.rumours.GetRange(startIndex, toShow);

        Instance.textWidget.text = string.Join("\n", recentRumours);
    }
}
