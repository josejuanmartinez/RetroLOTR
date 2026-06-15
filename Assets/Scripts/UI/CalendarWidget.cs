using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A self-building month calendar overlay. Shows the 30 days of the current Shire month
/// in a 6x5 grid, highlights today, and marks scripted days (from DateEventManager) with
/// a TMP &lt;sprite&gt; under the day number, drawn from the <see cref="eventSpriteSheet"/>
/// TMP sprite asset. Hovering a marked day shows its description.
///
/// Built entirely in code so it needs no prefab wiring: DateManager creates one and calls
/// ShowMonth(...). It parents itself to the first Canvas it finds. To use custom event
/// icons, add this component to a GameObject in the scene and assign the sprite sheet.
/// </summary>
public class CalendarWidget : MonoBehaviour
{
    private const int Columns = 6;
    private const int Rows = 5; // 6 * 5 = 30 days per month

    private static readonly Color PanelColor = new(0.10f, 0.09f, 0.07f, 0.96f);
    private static readonly Color CellColor = new(0.18f, 0.16f, 0.12f, 1f);
    private static readonly Color TodayColor = new(0.45f, 0.36f, 0.15f, 1f);
    private static readonly Color EventCellColor = new(0.28f, 0.20f, 0.10f, 1f);
    private static readonly Color TextColor = new(0.92f, 0.87f, 0.72f, 1f);

    // Faction colours, used to tint the day number by storyline.
    private static readonly Color GandalfColor = new(0.75f, 0.85f, 0.98f, 1f);
    private static readonly Color SarumanColor = new(0.86f, 0.80f, 0.93f, 1f);
    private static readonly Color SauronColor = new(0.90f, 0.30f, 0.22f, 1f);
    private static readonly Color MixedColor = new(0.95f, 0.80f, 0.35f, 1f);

    private static Color FactionColor(string faction)
    {
        switch ((faction ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "saruman": return SarumanColor;
            case "sauron": return SauronColor;
            default: return GandalfColor;
        }
    }

    [SerializeField]
    [Tooltip("TMP sprite asset (spritesheet) used to render event icons via <sprite name=...> on each day. Assign in the inspector.")]
    private TMP_SpriteAsset eventSpriteSheet;

    [SerializeField]
    [Tooltip("Render scale (percent of font size) for event sprites in the calendar. 200 = double size.")]
    private int eventSpriteScalePercent = 200;

    private RectTransform panel;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI footerText;
    private RectTransform grid;
    private readonly List<DayCell> dayCells = new();

    private DateEventManager calendar;
    private TMP_FontAsset font;
    private Canvas hostCanvas;

    private MiddleEarthDate currentMonth;
    private bool built;

    // Tooltip-style auto-hide: stay open while the pointer is over the date OR the panel,
    // hide shortly after it leaves both (the grace period lets the pointer cross the gap).
    private bool pointerOverDate;
    private bool pointerOverPanel;
    private const float HideGraceSeconds = 0.18f;

    /// <summary>Render the panel on this canvas (so it sits on the same UI layer as the date).</summary>
    public void SetHostCanvas(Canvas canvas)
    {
        hostCanvas = canvas;
    }

    private class DayCell
    {
        public Image background;
        public TextMeshProUGUI dayLabel;
        public TextMeshProUGUI iconLabel;
        public string description;
    }

    public bool IsVisible => panel != null && panel.gameObject.activeSelf;

    public void Toggle(MiddleEarthDate date)
    {
        if (IsVisible) Hide();
        else ShowMonth(date);
    }

    public void Hide()
    {
        CancelInvoke(nameof(HideIfIdle));
        if (panel != null) panel.gameObject.SetActive(false);
    }

    /// <summary>Pointer entered the date text: open (or keep open) the calendar for that date.</summary>
    public void OnDateEnter(MiddleEarthDate date)
    {
        pointerOverDate = true;
        CancelInvoke(nameof(HideIfIdle));
        ShowMonth(date);
    }

    /// <summary>Pointer left the date text: hide unless it has moved onto the panel.</summary>
    public void OnDateExit()
    {
        pointerOverDate = false;
        ScheduleHide();
    }

    private void ScheduleHide()
    {
        if (!isActiveAndEnabled) { HideIfIdle(); return; }
        CancelInvoke(nameof(HideIfIdle));
        Invoke(nameof(HideIfIdle), HideGraceSeconds);
    }

    private void HideIfIdle()
    {
        if (!pointerOverDate && !pointerOverPanel) Hide();
    }

    public void ShowMonth(MiddleEarthDate date)
    {
        EnsureBuilt();
        if (panel == null) return;
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling(); // draw on top of sibling UI
        currentMonth = date;
        Refresh(date);
        Debug.Log($"[CalendarWidget] showing {date.MonthName} {date.Year}");
    }

    /// <summary>Re-paints the open calendar for the given "today" (call on new turn).</summary>
    public void RefreshIfOpen(MiddleEarthDate today)
    {
        if (!IsVisible) return;
        currentMonth = today;
        Refresh(today);
    }

    private void Refresh(MiddleEarthDate today)
    {
        if (calendar == null) calendar = DateEventManager.Instance ?? FindFirstObjectByType<DateEventManager>();

        headerText.text = $"{today.MonthName}  {today.Year} {MiddleEarthCalendar.EraSuffix}";
        footerText.text = "Tap the date to close. Hover a marked day for its tale.";

        Dictionary<int, List<CalendarEntry>> byDay = new();
        if (calendar != null)
        {
            foreach (CalendarEntry e in calendar.GetEntriesForMonth(today.MonthIndex, today.Year))
            {
                if (!byDay.TryGetValue(e.Date.Day, out List<CalendarEntry> list))
                {
                    list = new List<CalendarEntry>();
                    byDay[e.Date.Day] = list;
                }
                list.Add(e);
            }
        }

        for (int i = 0; i < dayCells.Count; i++)
        {
            int day = i + 1;
            DayCell cell = dayCells[i];

            bool isToday = day == today.Day;
            bool hasEvent = byDay.TryGetValue(day, out List<CalendarEntry> entries) && entries.Count > 0;

            cell.background.color = isToday ? TodayColor : (hasEvent ? EventCellColor : CellColor);
            cell.description = hasEvent ? BuildDescription(entries) : null;
            cell.dayLabel.color = hasEvent ? DayMarkerColor(entries) : TextColor;

            // Day number stays top-left; the event icon(s) render centered in their own label.
            cell.dayLabel.text = day.ToString();
            cell.iconLabel.text = hasEvent ? BuildSpriteMarkup(entries) : string.Empty;
        }
    }

    private static string BuildDescription(List<CalendarEntry> entries)
    {
        if (entries.Count == 1) return $"<b>[{entries[0].Faction}]</b>  {entries[0].description}";
        return string.Join("\n", entries.Select(e => $"<b>[{e.Faction}]</b>  {e.description}"));
    }

    private static Color DayMarkerColor(List<CalendarEntry> entries)
    {
        string first = entries[0].Faction;
        bool mixed = entries.Any(e => !string.Equals(e.Faction, first, System.StringComparison.OrdinalIgnoreCase));
        return mixed ? MixedColor : FactionColor(first);
    }

    /// <summary>
    /// Builds the TMP "&lt;sprite name=...&gt;" markup for a day's events. Uses each entry's
    /// explicit spriteName when set, otherwise the normalized environmental card name
    /// (matching how environmental cards render their sprite). Names resolve against
    /// <see cref="eventSpriteSheet"/>.
    /// </summary>
    private string BuildSpriteMarkup(List<CalendarEntry> entries)
    {
        List<string> names = new();
        foreach (CalendarEntry e in entries)
        {
            string name = !string.IsNullOrWhiteSpace(e.spriteName)
                ? e.spriteName.Trim()
                : (!string.IsNullOrWhiteSpace(e.environment) ? CardNameUtility.Normalize(e.environment) : null);
            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name)) names.Add(name);
        }
        if (names.Count == 0) return string.Empty;

        // <sprite> has no scale attribute, so wrap the icons in a <size> tag (percent of font size).
        // The icon label is centered and clipped, so the scaled sprite stays inside the cell.
        int scale = Mathf.Max(100, eventSpriteScalePercent);
        string sprites = string.Join(" ", names.Select(n => $"<sprite name=\"{n}\">"));
        return scale == 100 ? sprites : $"<size={scale}%>{sprites}</size>";
    }

    // ---------------- UI construction ----------------

    private void EnsureBuilt()
    {
        if (built) return;

        Canvas canvas = hostCanvas != null ? hostCanvas : FindFirstObjectByType<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas == null)
        {
            Debug.LogWarning("CalendarWidget: no Canvas found in scene; cannot build calendar.");
            return;
        }

        font = TMP_Settings.defaultFontAsset;

        panel = CreateRect("CalendarPanel", canvas.transform);
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-20f, -90f);
        panel.sizeDelta = new Vector2(420f, 388f);
        AddImage(panel.gameObject, PanelColor);

        // Own sorting canvas so the calendar draws on top of every other UI layer,
        // with its own raycaster so its cells still receive pointer events.
        Canvas overlay = panel.gameObject.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = short.MaxValue; // 32767 — above all normal UI
        panel.gameObject.AddComponent<GraphicRaycaster>();

        // Track the pointer over the panel itself so auto-hide knows to stay open.
        EventTrigger panelTrigger = panel.gameObject.AddComponent<EventTrigger>();
        AddTrigger(panelTrigger, EventTriggerType.PointerEnter, _ =>
        {
            pointerOverPanel = true;
            CancelInvoke(nameof(HideIfIdle));
        });
        AddTrigger(panelTrigger, EventTriggerType.PointerExit, _ =>
        {
            pointerOverPanel = false;
            ScheduleHide();
        });

        VerticalLayoutGroup vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 8f;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;

        headerText = CreateText("Header", panel, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
        SetPreferredHeight(headerText.gameObject, 34f);

        grid = CreateRect("Grid", panel);
        GridLayoutGroup glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = Columns;
        glg.spacing = new Vector2(4f, 4f);
        glg.cellSize = new Vector2(60f, 50f);
        LayoutElement gridLe = grid.gameObject.AddComponent<LayoutElement>();
        gridLe.preferredHeight = Rows * 50f + (Rows - 1) * 4f;

        dayCells.Clear();
        for (int i = 0; i < Columns * Rows; i++)
        {
            dayCells.Add(BuildDayCell(grid, i + 1));
        }

        footerText = CreateText("Footer", panel, 15f, FontStyles.Italic, TextAlignmentOptions.Center);
        footerText.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0.7f);
        footerText.enableWordWrapping = true;
        SetPreferredHeight(footerText.gameObject, 64f);

        built = true;
    }

    private DayCell BuildDayCell(RectTransform parent, int day)
    {
        RectTransform cellRt = CreateRect($"Day{day}", parent);
        Image bg = AddImage(cellRt.gameObject, CellColor);
        cellRt.gameObject.AddComponent<RectMask2D>(); // hard-clip cell contents (icons can't spill out)

        TextMeshProUGUI label = CreateText("Num", cellRt, 14f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        RectTransform labelRt = label.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(4f, 2f);
        labelRt.offsetMax = new Vector2(-4f, -2f);

        // Event icon(s) live in their own label that fills the cell, centered, and clips so a
        // scaled-up <sprite> can never spill into neighbouring cells or the footer.
        TextMeshProUGUI icon = CreateText("Icon", cellRt, 14f, FontStyles.Normal, TextAlignmentOptions.Center);
        if (eventSpriteSheet != null) icon.spriteAsset = eventSpriteSheet; // resolves <sprite name=...>
        icon.enableWordWrapping = false;
        icon.overflowMode = TextOverflowModes.Truncate;
        RectTransform iconRt = icon.rectTransform;
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(2f, 2f);
        iconRt.offsetMax = new Vector2(-2f, -2f);

        DayCell cell = new() { background = bg, dayLabel = label, iconLabel = icon };

        EventTrigger trigger = cellRt.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ =>
        {
            if (!string.IsNullOrEmpty(cell.description) && footerText != null)
                footerText.text = cell.description;
        });
        AddTrigger(trigger, EventTriggerType.PointerExit, _ =>
        {
            if (footerText != null)
                footerText.text = "Tap the date to close. Hover a marked day for its tale.";
        });

        return cell;
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new() { eventID = type };
        entry.callback.AddListener(data => callback(data));
        trigger.triggers.Add(entry);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
    {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = align;
        text.color = TextColor;
        text.raycastTarget = false;
        return text;
    }

    private static void SetPreferredHeight(GameObject go, float height)
    {
        LayoutElement le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }
}
