using UnityEngine;

/// <summary>
/// Context handed to a DateEvent when its calendar day arrives.
/// </summary>
public class DateEventContext
{
    public Game game;
    public int turn;
    public MiddleEarthDate date;
    public CalendarEntry entry;
}

/// <summary>
/// Base class for scripted calendar beats. A CalendarEntry's "dateEvent" field names
/// a concrete subclass (by class name); DateEventManager instantiates it via reflection
/// and calls <see cref="Run"/> once, on the first turn that lands on the entry's date.
///
/// Subclasses live in Assets/Scripts/Actions/DateEvents/ and stay deliberately small:
/// each one scripts a single moment of the War of the Ring. Use the helpers below for
/// the common "announce + apply" shape, and reach into ctx.game / Board / etc. for logic.
/// </summary>
public abstract class DateEvent
{
    /// <summary>Run the event. Called exactly once when the date is reached.</summary>
    public abstract void Run(DateEventContext ctx);

    /// <summary>Convenience banner used by most beats; safe to call from any DateEvent.</summary>
    protected static void Announce(string message, Color? color = null)
    {
        MessageDisplay.ShowMessage(message, color ?? new Color(0.95f, 0.85f, 0.45f));
        Debug.Log($"[DateEvent] {message}");
    }
}
