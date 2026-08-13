using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

public class Colors : SearcherByName
{
    public Color mountains;
    public Color hills;
    public Color forest;
    public Color grasslands;
    public Color plains;
    public Color shore;
    public Color deepWater;
    public Color shallowWater;
    public Color swamp;
    public Color desert;
    public Color wastelands;
    public Color snow;
    public Color freePeople;
    public Color neutral;
    public Color darkServants;
    [FormerlySerializedAs("pcCard")] public Color pc;
    [FormerlySerializedAs("landCard")] public Color land;
    [FormerlySerializedAs("characterCard")] public Color character;
    [FormerlySerializedAs("armyCard")] public Color army;
    [FormerlySerializedAs("eventCard")] public Color @event;
    [FormerlySerializedAs("actionCard")] public Color action;
    [FormerlySerializedAs("spellCard")] public Color spell;
    public Color encounter;
    public Color environmental;
    public Color logNotification = new Color(0.55f, 0.85f, 1f);
    public Color logRumour = new Color(0.85f, 0.55f, 1f);
    public Color logEvent = new Color(1f, 0.75f, 0.3f);
    public Color movementStart = new Color(0.72f, 0.16f, 0.2f);
    public Color movementEnd = new Color(0.08f, 0.58f, 0.52f);
    public Color MAX;

    private Dictionary<string, FieldInfo> normalizedLookup;

    private void Awake()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        normalizedLookup = new Dictionary<string, FieldInfo>();

        var fields = typeof(Colors).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (field.FieldType == typeof(Color))
            {
                string normalized = Normalize(field.Name);
                normalizedLookup[normalized] = field;
            }
        }
    }

    public Color GetColorByName(string colorName)
    {
        if (normalizedLookup == null)
        {
            BuildLookup();
        }

        string normalized = Normalize(colorName);

        if (normalizedLookup.TryGetValue(normalized, out var field))
        {
            return (Color)field.GetValue(this);
        }

        throw new System.ArgumentException($"No color found for name '{colorName}' (normalized: '{normalized}').");
    }

    public string GetHexColorByName(string colorName)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(GetColorByName(colorName));
    }
}
