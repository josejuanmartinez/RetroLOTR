using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Renders the current Shire date and owns the pop-up month calendar.
/// Date math lives in <see cref="MiddleEarthCalendar"/> so the text, the calendar grid,
/// and the scripted DateEvents all agree on what day it is. Hovering the date text opens
/// the <see cref="CalendarWidget"/>; moving the pointer away from both the date and the
/// calendar hides it again (with a short grace period so you can reach the panel).
/// (Pointer events only fire in Play mode and require an EventSystem + GraphicRaycaster.)
/// </summary>
public class DateManager : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI textWidget;

    [SerializeField]
    private CalendarWidget calendarWidget;

    private Game game;
    private MiddleEarthDate currentDate;

    private void OnEnable()
    {
        EnsureCalendarWidget();
        EnsureDateInteractions();
        DateEventManager.GetOrCreate(); // drives scripted environment cards + date events

        game = FindFirstObjectByType<Game>();
        if (game != null)
        {
            game.NewTurnStarted += Show;
            Show(game.turn);
        }
    }

    private void OnDisable()
    {
        if (game != null)
        {
            game.NewTurnStarted -= Show;
        }
    }

    public void Show(int turnNumber)
    {
        currentDate = MiddleEarthCalendar.GetDateFromTurn(turnNumber);
        if (textWidget != null) textWidget.text = currentDate.ToString();
        if (calendarWidget != null) calendarWidget.RefreshIfOpen(currentDate);
    }

    public void OpenCalendar()
    {
        EnsureCalendarWidget();
        calendarWidget?.OnDateEnter(currentDate);
    }

    public void CloseCalendar()
    {
        calendarWidget?.OnDateExit();
    }

    private void EnsureCalendarWidget()
    {
        if (calendarWidget == null)
        {
            calendarWidget = FindFirstObjectByType<CalendarWidget>();
        }
        if (calendarWidget == null)
        {
            var go = new GameObject("CalendarWidget");
            go.transform.SetParent(transform, false);
            calendarWidget = go.AddComponent<CalendarWidget>();
        }
        if (textWidget != null) calendarWidget.SetHostCanvas(textWidget.canvas);
    }

    private void EnsureDateInteractions()
    {
        if (textWidget == null)
        {
            Debug.LogWarning("DateManager: 'textWidget' is not assigned, so the date cannot be hovered/clicked to open the calendar. Assign the date TextMeshProUGUI in the inspector.");
            return;
        }

        textWidget.raycastTarget = true; // required for pointer events on a TMP text
        EventTrigger trigger = textWidget.gameObject.GetComponent<EventTrigger>()
                               ?? textWidget.gameObject.AddComponent<EventTrigger>();

        // Rebuild our listeners (avoid stacking duplicates across re-enables).
        trigger.triggers.RemoveAll(t =>
            t.eventID == EventTriggerType.PointerEnter || t.eventID == EventTriggerType.PointerExit);

        // Tooltip-style: hover the date to open the calendar, move away to hide it.
        AddTrigger(trigger, EventTriggerType.PointerEnter, OpenCalendar);
        AddTrigger(trigger, EventTriggerType.PointerExit, CloseCalendar);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new() { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }
}
