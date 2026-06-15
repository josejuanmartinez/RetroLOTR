using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One scripted beat on the campaign calendar, deserialized from Resources/Calendar.json.
/// Authoring fields match the JSON exactly:
///   date        - "D Month YYYY" Shire date, e.g. "23 Halimath 3018"
///   description - flavour text shown in the calendar widget / tooltip
///   environment - name of an environmental CardData to make active that turn (optional)
///   dateEvent   - class name of a DateEvent (Scripts/Actions/DateEvents) to run that turn (optional)
///   faction     - which storyline this beat belongs to: "Gandalf", "Saruman" or "Sauron"
///                 (drives the marker colour; defaults to "Gandalf")
/// </summary>
[Serializable]
public class CalendarEntry
{
    public string date;
    public string description;
    public string environment;
    public string dateEvent;
    public string faction;
    public string spriteName; // optional explicit marker art; falls back to the environment card's art

    /// <summary>Normalised faction, defaulting to Gandalf/Free Peoples when unset.</summary>
    public string Faction => string.IsNullOrWhiteSpace(faction) ? "Gandalf" : faction.Trim();

    [NonSerialized] private bool parsed;
    [NonSerialized] private bool valid;
    [NonSerialized] private MiddleEarthDate parsedDate;

    private void EnsureParsed()
    {
        if (parsed) return;
        parsed = true;
        valid = MiddleEarthCalendar.TryParse(date, out parsedDate);
        if (!valid)
        {
            Debug.LogWarning($"CalendarEntry: could not parse date '{date}' (expected 'D Month YYYY').");
        }
    }

    public bool HasValidDate { get { EnsureParsed(); return valid; } }

    public MiddleEarthDate Date { get { EnsureParsed(); return parsedDate; } }

    /// <summary>First turn this entry fires on (0 if the date is invalid or before the campaign start).</summary>
    public int Turn => HasValidDate ? MiddleEarthCalendar.GetTurnFromDate(parsedDate) : 0;

    public bool HasEnvironment => !string.IsNullOrWhiteSpace(environment);
    public bool HasDateEvent => !string.IsNullOrWhiteSpace(dateEvent);
}

/// <summary>
/// JsonUtility wrapper. JsonUtility cannot deserialize a bare top-level array, so the
/// JSON file is shaped as { "events": [ ... ] }.
/// </summary>
[Serializable]
public class CalendarCollection
{
    public List<CalendarEntry> events = new();
}
