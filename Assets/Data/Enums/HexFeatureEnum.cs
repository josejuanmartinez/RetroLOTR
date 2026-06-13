using System;
using System.Collections.Generic;

/// <summary>
/// Landmark features depicted in the individual hex tile art (see Assets/Art/Hexes/Tiles).
/// A single tile can show several of these at once, so the enum is a [Flags] bitmask.
///
/// Features are READ FROM THE ART: a hex looks up the variant sprite it was assigned
/// (e.g. "forest_05") in <see cref="HexFeatureData.featuresByTile"/> and gains whatever
/// features that picture shows. Nothing here is authored per-hex at board-gen time.
///
/// The comment after each value is the intended gameplay hook (not yet wired up).
/// </summary>
[Flags]
public enum HexFeatureEnum
{
    None           = 0,
    Road           = 1 << 0,  // -2 movement cost of the tile to a minimum of 1
    River          = 1 << 1,  // movement stops here
    Pond           = 1 << 2,  // minor health recover to the character if it rests here
    Bridge         = 1 << 3,  // if with bridge, movement does not stop there. If without (?), -1 movement cost of the tile to a minimum of 1
    Watchtower     = 1 << 4,  // scouts an area of 1 radius around the hex. If an army rests here, it gets fortified (1 turn)
    Lighthouse     = 1 << 5,  // scouts all the water hexes in a radius of 3
    Ruins          = 1 << 6,  // if you rest here, 5% of chance of getting a yet-to-discover-hidden-in-an-hex artifact and assigningi t to the character (removing it from its original hex)
    StandingStones = 1 << 7,  // if a mage rests here, it gets arcane insight for 1 turn
    Monument       = 1 << 8,  // if an army ends in this hex, it gets courage (1 turn)
    Village        = 1 << 9,  // if a character ends here, 25% of chance of getting a random resource if resting here
    Fountain       = 1 << 10, // major health recover if the character rests here
    Lava           = 1 << 11, // hazard — attrition damage to an army and/or character that stays there if it's not dark servants
    Chasm          = 1 << 12, // 10% of probability of army/character being wounded and automatically transported to another chasm if rested here
    Mine           = 1 << 13, // 25% of probability of getting a mineral resource if rested here
    Blighted       = 1 << 14, // 10% of probability of getting poison, 5% of getting cursed, if rested here
}

/// <summary>
/// Maps each terrain tile sprite (by its asset name, e.g. "forest_05") to the features it depicts.
///
/// Scope: NATURAL TERRAIN tiles only. The alignment city tiles (darkServants_*, freePeople_*,
/// neutral_*) are PC/settlement art chosen by HexTextureMapping.GetSprite and are intentionally
/// NOT listed here — those hexes already run on the PC system.
///
/// This map is a best-effort first pass read off the art. It is plain data: correct any tag by
/// editing the dictionary. Tiles not present in the map have <see cref="HexFeatureEnum.None"/>.
/// </summary>
public static class HexFeatureData
{
    public static readonly Dictionary<string, HexFeatureEnum> featuresByTile = new()
    {
        // ---------- FOREST ----------
        { "forest_02", HexFeatureEnum.Ruins },
        { "forest_03", HexFeatureEnum.Road },
        { "forest_05", HexFeatureEnum.River },
        { "forest_13", HexFeatureEnum.Watchtower },
        { "forest_14", HexFeatureEnum.Watchtower },
        { "forest_15", HexFeatureEnum.Road },
        { "forest_16", HexFeatureEnum.River | HexFeatureEnum.Road },
        { "forest_18", HexFeatureEnum.Watchtower },
        { "forest_19", HexFeatureEnum.Road },
        { "forest_20", HexFeatureEnum.StandingStones },
        { "forest_21", HexFeatureEnum.Ruins },
        { "forest_22", HexFeatureEnum.Ruins },
        { "forest_23", HexFeatureEnum.River },
        { "forest_24", HexFeatureEnum.River },
        { "forest_25", HexFeatureEnum.River },
        { "forest_26", HexFeatureEnum.Ruins },
        { "forest_27", HexFeatureEnum.Ruins },
        { "forest_28", HexFeatureEnum.Village },
        { "forest_29", HexFeatureEnum.Village },

        // ---------- PLAINS ----------
        { "plains_01", HexFeatureEnum.Watchtower | HexFeatureEnum.Road | HexFeatureEnum.Pond },
        { "plains_02", HexFeatureEnum.Ruins | HexFeatureEnum.Road },
        { "plains_03", HexFeatureEnum.Ruins | HexFeatureEnum.Road },
        { "plains_04", HexFeatureEnum.Road },
        { "plains_05", HexFeatureEnum.Ruins | HexFeatureEnum.Road | HexFeatureEnum.River },
        { "plains_06", HexFeatureEnum.Road },
        { "plains_08", HexFeatureEnum.Pond | HexFeatureEnum.Road },
        { "plains_09", HexFeatureEnum.StandingStones | HexFeatureEnum.Road },
        { "plains_10", HexFeatureEnum.StandingStones | HexFeatureEnum.Ruins },
        { "plains_11", HexFeatureEnum.Road },
        { "plains_12", HexFeatureEnum.Ruins },
        { "plains_13", HexFeatureEnum.StandingStones },
        { "plains_14", HexFeatureEnum.Road },
        { "plains_15", HexFeatureEnum.Village },
        { "plains_16", HexFeatureEnum.Village | HexFeatureEnum.Road },
        { "plains_18", HexFeatureEnum.Blighted },

        // ---------- HILLS ----------
        { "hills_01", HexFeatureEnum.Ruins | HexFeatureEnum.Road },
        { "hills_02", HexFeatureEnum.Road },
        { "hills_03", HexFeatureEnum.Ruins },
        { "hills_04", HexFeatureEnum.Monument | HexFeatureEnum.River },
        { "hills_05", HexFeatureEnum.Road },
        { "hills_06", HexFeatureEnum.Ruins },
        { "hills_08", HexFeatureEnum.Road | HexFeatureEnum.River },
        { "hills_09", HexFeatureEnum.Ruins },
        { "hills_10", HexFeatureEnum.Mine },
        { "hills_11", HexFeatureEnum.Mine },
        { "hills_12", HexFeatureEnum.Ruins },
        { "hills_13", HexFeatureEnum.Road },
        { "hills_15", HexFeatureEnum.StandingStones },
        { "hills_16", HexFeatureEnum.StandingStones },
        { "hills_17", HexFeatureEnum.Fountain },

        // ---------- MOUNTAINS ----------
        { "mountains_01", HexFeatureEnum.Road | HexFeatureEnum.Ruins },
        { "mountains_03", HexFeatureEnum.Road },
        { "mountains_04", HexFeatureEnum.Road },
        { "mountains_05", HexFeatureEnum.Road },
        { "mountains_06", HexFeatureEnum.Ruins },
        { "mountains_07", HexFeatureEnum.Road },
        { "mountains_08", HexFeatureEnum.Road },
        { "mountains_09", HexFeatureEnum.Ruins },
        { "mountains_10", HexFeatureEnum.Road },
        { "mountains_11", HexFeatureEnum.Road },
        { "mountains_13", HexFeatureEnum.Road },
        { "mountains_14", HexFeatureEnum.Road | HexFeatureEnum.River },
        { "mountains_16", HexFeatureEnum.Lava },
        { "mountains_17", HexFeatureEnum.Lava },

        // ---------- GRASS (grasslands) ----------
        { "grass_01", HexFeatureEnum.Watchtower | HexFeatureEnum.StandingStones | HexFeatureEnum.Road },
        { "grass_02", HexFeatureEnum.StandingStones | HexFeatureEnum.Road },
        { "grass_03", HexFeatureEnum.Road },
        { "grass_04", HexFeatureEnum.Ruins | HexFeatureEnum.Road },
        { "grass_05", HexFeatureEnum.Road },
        { "grass_06", HexFeatureEnum.Road },
        { "grass_07", HexFeatureEnum.River | HexFeatureEnum.Road },
        { "grass_08", HexFeatureEnum.Village | HexFeatureEnum.Road },
        { "grass_09", HexFeatureEnum.Road },
        { "grass_10", HexFeatureEnum.River | HexFeatureEnum.Road },
        { "grass_11", HexFeatureEnum.Ruins | HexFeatureEnum.Road },
        { "grass_12", HexFeatureEnum.Pond },
        { "grass_13", HexFeatureEnum.Watchtower | HexFeatureEnum.River | HexFeatureEnum.StandingStones },
        { "grass_14", HexFeatureEnum.Village },
        { "grass_15", HexFeatureEnum.Road },
        { "grass_16", HexFeatureEnum.Village | HexFeatureEnum.Road },

        // ---------- DESERT ----------
        { "desert_01", HexFeatureEnum.Ruins | HexFeatureEnum.Road },
        { "desert_02", HexFeatureEnum.Road },
        { "desert_03", HexFeatureEnum.Ruins },
        { "desert_04", HexFeatureEnum.Pond },
        { "desert_05", HexFeatureEnum.Ruins | HexFeatureEnum.Pond },
        { "desert_06", HexFeatureEnum.Road },
        { "desert_07", HexFeatureEnum.Monument },
        { "desert_08", HexFeatureEnum.Road },
        { "desert_09", HexFeatureEnum.Road },
        { "desert_10", HexFeatureEnum.Pond },
        { "desert_11", HexFeatureEnum.Monument },
        { "desert_12", HexFeatureEnum.Monument },
        { "desert_13", HexFeatureEnum.Monument | HexFeatureEnum.Ruins },
        { "desert_14", HexFeatureEnum.Ruins },
        { "desert_15", HexFeatureEnum.Village },
        { "desert_16", HexFeatureEnum.Ruins },
        { "desert_17", HexFeatureEnum.Pond | HexFeatureEnum.Village },

        // ---------- WASTELANDS ----------
        { "wastelands_01", HexFeatureEnum.Lava },
        { "wastelands_02", HexFeatureEnum.Lava },
        { "wastelands_03", HexFeatureEnum.Lava },
        { "wastelands_04", HexFeatureEnum.Lava },
        { "wastelands_05", HexFeatureEnum.Lava },
        { "wastelands_06", HexFeatureEnum.Lava },
        { "wastelands_07", HexFeatureEnum.Lava },
        { "wastelands_08", HexFeatureEnum.Ruins },
        { "wastelands_09", HexFeatureEnum.Lava },
        { "wastelands_10", HexFeatureEnum.Lava },
        { "wastelands_11", HexFeatureEnum.Lava },
        { "wastelands_12", HexFeatureEnum.Lava },
        { "wastelands_13", HexFeatureEnum.Blighted },
        { "wastelands_14", HexFeatureEnum.Blighted },
        { "wastelands_15", HexFeatureEnum.Chasm },
        { "wastelands_16", HexFeatureEnum.Blighted },

        // ---------- SWAMP ----------
        { "swamp_01", HexFeatureEnum.Ruins | HexFeatureEnum.River },
        { "swamp_02", HexFeatureEnum.Ruins },
        { "swamp_03", HexFeatureEnum.Ruins },
        { "swamp_04", HexFeatureEnum.Ruins },
        { "swamp_05", HexFeatureEnum.Ruins | HexFeatureEnum.Village },
        { "swamp_06", HexFeatureEnum.Ruins },
        { "swamp_07", HexFeatureEnum.Ruins },

        // ---------- SHORE ----------
        { "shore_01", HexFeatureEnum.Lighthouse },
        { "shore_02", HexFeatureEnum.Watchtower | HexFeatureEnum.Bridge | HexFeatureEnum.Pond },
        { "shore_03", HexFeatureEnum.Lighthouse },

        // ---------- DEEP WATER ----------
        { "deepWater_02", HexFeatureEnum.Lighthouse }
    };

    /// <summary>Returns the features depicted by the given tile sprite name, or None if untagged.</summary>
    public static HexFeatureEnum GetFeatures(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return HexFeatureEnum.None;
        if (featuresByTile.TryGetValue(spriteName, out HexFeatureEnum f)) return f;

        // Fallback: tolerate trailing suffixes (e.g. "forest_05 (Clone)") by matching on the leading token.
        int space = spriteName.IndexOf(' ');
        if (space > 0 && featuresByTile.TryGetValue(spriteName.Substring(0, space), out f)) return f;

        return HexFeatureEnum.None;
    }

    public static bool Has(HexFeatureEnum features, HexFeatureEnum flag) => (features & flag) != 0;

    // Ordered for stable display in the hex hover header.
    private static readonly (HexFeatureEnum flag, string label)[] displayOrder =
    {
        (HexFeatureEnum.River, "River"),
        (HexFeatureEnum.Bridge, "Bridge"),
        (HexFeatureEnum.Road, "Road"),
        (HexFeatureEnum.Pond, "Pond"),
        (HexFeatureEnum.Watchtower, "Watchtower"),
        (HexFeatureEnum.Lighthouse, "Lighthouse"),
        (HexFeatureEnum.Ruins, "Ruins"),
        (HexFeatureEnum.StandingStones, "Standing Stones"),
        (HexFeatureEnum.Monument, "Monument"),
        (HexFeatureEnum.Village, "Village"),
        (HexFeatureEnum.Fountain, "Fountain"),
        (HexFeatureEnum.Mine, "Mine"),
        (HexFeatureEnum.Lava, "Lava"),
        (HexFeatureEnum.Chasm, "Chasm"),
        (HexFeatureEnum.Blighted, "Blighted"),
    };

    /// <summary>Human-readable labels for the features present, in a stable display order.</summary>
    public static IEnumerable<string> GetFeatureLabels(HexFeatureEnum features)
    {
        foreach (var (flag, label) in displayOrder)
            if ((features & flag) != 0) yield return label;
    }
}
