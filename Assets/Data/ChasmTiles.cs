using System;
using System.Collections.Generic;

/// <summary>
/// The terrain tile sprites (Assets/Art/Hexes/Tiles) whose art depicts a chasm. A hex assigned
/// one of these tiles is an entrance to the Underground (see Hex.IsUnderground and the Endless
/// Stairs opportunity card). Read from the art by asset name, never authored per hex.
/// </summary>
public static class ChasmTiles
{
    public const string Description =
        "An entrance to the Underground: characters without an army may take the Endless Stairs to another underground location.";

    private static readonly HashSet<string> tiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "forest_34",
        "plains_22",
        "hills_10",
        // NOTE: the mountains tile assets are NOT zero-padded (mountains_1.png … mountains_7.png),
        // unlike every other terrain — names here must match the sprite names exactly.
        "mountains_7",
        "grass_19",
        "wastelands_12",
        "wastelands_15",
        "snow_09",
        "snow_10",
    };

    /// <summary>True when the given tile sprite name depicts a chasm.</summary>
    public static bool Contains(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return false;
        if (tiles.Contains(spriteName)) return true;

        // Tolerate trailing suffixes (e.g. "forest_34 (Clone)") by matching the leading token.
        int space = spriteName.IndexOf(' ');
        string token = space > 0 ? spriteName.Substring(0, space) : spriteName;
        if (space > 0 && tiles.Contains(token)) return true;

        // Tolerate zero-padding mismatches between asset names and keys
        // (e.g. asset "mountains_7" vs key "mountains_07" or vice versa).
        int underscore = token.LastIndexOf('_');
        if (underscore > 0 && int.TryParse(token.Substring(underscore + 1), out int n))
        {
            string prefix = token.Substring(0, underscore + 1);
            if (tiles.Contains($"{prefix}{n:00}")) return true;
            if (tiles.Contains($"{prefix}{n}")) return true;
        }

        return false;
    }
}
