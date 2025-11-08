using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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
    public Color freePeople;
    public Color neutral;
    public Color darkServants;
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
