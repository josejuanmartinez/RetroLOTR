using System.Collections.Generic;
using UnityEngine;

public static class RegionColors
{
    // Alpha applied to every region color overlay on hexes.
    public const float OverlayAlpha = 0.38f;

    private static readonly Dictionary<string, Color> Colors = new()
    {
        { "anduin",            new Color(0.35f, 0.65f, 0.35f) },
        { "angmar",            new Color(0.35f, 0.10f, 0.45f) },
        { "arthedain",         new Color(0.20f, 0.35f, 0.80f) },
        { "cardolan",          new Color(0.45f, 0.25f, 0.70f) },
        { "dorwinion",         new Color(0.80f, 0.65f, 0.15f) },
        { "dunland",           new Color(0.55f, 0.35f, 0.15f) },
        { "eredluin",          new Color(0.30f, 0.45f, 0.65f) },
        { "farharad",          new Color(0.85f, 0.40f, 0.10f) },
        { "gapofrohan",        new Color(0.55f, 0.75f, 0.30f) },
        { "gorgoroth",         new Color(0.60f, 0.08f, 0.08f) },
        { "ironhills",         new Color(0.50f, 0.52f, 0.58f) },
        { "ithilien",          new Color(0.25f, 0.60f, 0.30f) },
        { "khand",             new Color(0.72f, 0.55f, 0.20f) },
        { "lindon",            new Color(0.15f, 0.55f, 0.75f) },
        { "lothlorien",        new Color(0.85f, 0.82f, 0.15f) },
        { "mistymountains",    new Color(0.52f, 0.54f, 0.65f) },
        { "northerngondor",    new Color(0.18f, 0.28f, 0.72f) },
        { "northernmirkwood",  new Color(0.20f, 0.45f, 0.20f) },
        { "nurn",              new Color(0.50f, 0.30f, 0.15f) },
        { "rhovanion",         new Color(0.65f, 0.72f, 0.25f) },
        { "rhun",              new Color(0.75f, 0.30f, 0.12f) },
        { "rivendell",         new Color(0.50f, 0.78f, 0.85f) },
        { "rohan",             new Color(0.88f, 0.72f, 0.12f) },
        { "seaofrhun",         new Color(0.18f, 0.50f, 0.82f) },
        { "southerngondor",    new Color(0.22f, 0.38f, 0.78f) },
        { "southernmirkwood",  new Color(0.30f, 0.58f, 0.30f) },
        { "theshire",          new Color(0.40f, 0.82f, 0.25f) },
        { "udun",              new Color(0.65f, 0.10f, 0.10f) },
        { "umbar",             new Color(0.70f, 0.15f, 0.20f) },
        { "ungol",             new Color(0.45f, 0.10f, 0.28f) },
    };

    private static string Normalize(string region)
    {
        if (string.IsNullOrWhiteSpace(region)) return string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (char c in region)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    public static Color GetColor(string region, float alpha = OverlayAlpha)
    {
        if (string.IsNullOrWhiteSpace(region)) return Color.clear;
        if (Colors.TryGetValue(Normalize(region), out Color c))
            return new Color(c.r, c.g, c.b, alpha);
        // Unknown region: assign a deterministic grey-hued fallback.
        int hash = Normalize(region).GetHashCode();
        return new Color(
            0.3f + 0.4f * ((hash & 0xFF) / 255f),
            0.3f + 0.4f * (((hash >> 8) & 0xFF) / 255f),
            0.3f + 0.4f * (((hash >> 16) & 0xFF) / 255f),
            alpha);
    }
}
