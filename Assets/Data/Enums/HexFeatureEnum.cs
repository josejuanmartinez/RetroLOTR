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
    Lumbermill     = 1 << 15  // 25% of probability of getting wood resource if rested here
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
        { "forest_01", HexFeatureEnum.River },
        { "forest_02", HexFeatureEnum.Road },
        { "forest_03", HexFeatureEnum.StandingStones | HexFeatureEnum.Ruins },        
        { "forest_04", HexFeatureEnum.Road | HexFeatureEnum.Watchtower},
        { "forest_05", HexFeatureEnum.River | HexFeatureEnum.Road | HexFeatureEnum.Bridge },       
        { "forest_06", HexFeatureEnum.Watchtower | HexFeatureEnum.StandingStones},
        { "forest_07", HexFeatureEnum.River | HexFeatureEnum.Watchtower | HexFeatureEnum.Road },  
        { "forest_08", HexFeatureEnum.River | HexFeatureEnum.Watchtower },       
        { "forest_09", HexFeatureEnum.Fountain },     
        { "forest_10", HexFeatureEnum.Monument },
        { "forest_11", HexFeatureEnum.Watchtower | HexFeatureEnum.River | HexFeatureEnum.Bridge },
        { "forest_12", HexFeatureEnum.Monument },
        { "forest_13", HexFeatureEnum.Watchtower | HexFeatureEnum.River},
        { "forest_15", HexFeatureEnum.Road | HexFeatureEnum.Ruins},
        { "forest_16", HexFeatureEnum.River | HexFeatureEnum.Road | HexFeatureEnum.Bridge | HexFeatureEnum.Watchtower},
        { "forest_17", HexFeatureEnum.Monument | HexFeatureEnum.Road},
        { "forest_18", HexFeatureEnum.Watchtower | HexFeatureEnum.Road },
        { "forest_19", HexFeatureEnum.Road },
        { "forest_20", HexFeatureEnum.Monument | HexFeatureEnum.Road | HexFeatureEnum.River },
        { "forest_21", HexFeatureEnum.StandingStones },
        { "forest_22", HexFeatureEnum.StandingStones },
        { "forest_23", HexFeatureEnum.Lumbermill | HexFeatureEnum.Road },
        { "forest_24", HexFeatureEnum.None },
        { "forest_25", HexFeatureEnum.StandingStones },
        { "forest_26", HexFeatureEnum.Monument },
        { "forest_27", HexFeatureEnum.StandingStones },
        { "forest_28", HexFeatureEnum.Village | HexFeatureEnum.Road},
        { "forest_29", HexFeatureEnum.Lumbermill | HexFeatureEnum.Road },        
        { "forest_30", HexFeatureEnum.None },      
        { "forest_31", HexFeatureEnum.Watchtower | HexFeatureEnum.Pond | HexFeatureEnum.Road | HexFeatureEnum.Blighted },      
        { "forest_32", HexFeatureEnum.Blighted | HexFeatureEnum.Ruins | HexFeatureEnum.Ruins },      
        { "forest_33", HexFeatureEnum.Blighted },

        // ---------- PLAINS ----------
        { "plains_01", HexFeatureEnum.Watchtower | HexFeatureEnum.Road | HexFeatureEnum.Pond },
        { "plains_02", HexFeatureEnum.Watchtower | HexFeatureEnum.Road },
        { "plains_03", HexFeatureEnum.Watchtower | HexFeatureEnum.Road | HexFeatureEnum.River | HexFeatureEnum.Bridge},
        { "plains_04", HexFeatureEnum.Road | HexFeatureEnum.Ruins },
        { "plains_05", HexFeatureEnum.Village | HexFeatureEnum.Watchtower | HexFeatureEnum.Road | HexFeatureEnum.River | HexFeatureEnum.Bridge },
        { "plains_06", HexFeatureEnum.Road | HexFeatureEnum.Pond | HexFeatureEnum.Ruins},
        { "plains_07", HexFeatureEnum.Road | HexFeatureEnum.Ruins},
        { "plains_08", HexFeatureEnum.Village | HexFeatureEnum.Road | HexFeatureEnum.River | HexFeatureEnum.Bridge },
        { "plains_09", HexFeatureEnum.Village | HexFeatureEnum.Road },
        { "plains_10", HexFeatureEnum.StandingStones | HexFeatureEnum.Road},
        { "plains_11", HexFeatureEnum.Road | HexFeatureEnum.River | HexFeatureEnum.Bridge | HexFeatureEnum.Ruins},
        { "plains_12", HexFeatureEnum.Ruins },
        { "plains_13", HexFeatureEnum.StandingStones },
        { "plains_14", HexFeatureEnum.Road | HexFeatureEnum.StandingStones },
        { "plains_15", HexFeatureEnum.Village },
        { "plains_16", HexFeatureEnum.Watchtower | HexFeatureEnum.Road | HexFeatureEnum.Bridge | HexFeatureEnum.Bridge },
        { "plains_17", HexFeatureEnum.Ruins | HexFeatureEnum.Road },
        { "plains_18", HexFeatureEnum.Blighted },
        { "plains_19", HexFeatureEnum.None },
        { "plains_20", HexFeatureEnum.None },
        { "plains_21", HexFeatureEnum.None },

        // ---------- HILLS ----------
        { "hills_01", HexFeatureEnum.Ruins | HexFeatureEnum.Road },
        { "hills_02", HexFeatureEnum.Road | HexFeatureEnum.Ruins},
        { "hills_03", HexFeatureEnum.Mine | HexFeatureEnum.Road },
        { "hills_04", HexFeatureEnum.Road |  HexFeatureEnum.River | HexFeatureEnum.Watchtower},
        { "hills_05", HexFeatureEnum.Road | HexFeatureEnum.Watchtower},
        { "hills_06", HexFeatureEnum.Monument },
        { "hills_07", HexFeatureEnum.Road | HexFeatureEnum.Ruins },
        { "hills_08", HexFeatureEnum.Road | HexFeatureEnum.River | HexFeatureEnum.Watchtower},
        { "hills_09", HexFeatureEnum.None},
        { "hills_10", HexFeatureEnum.Chasm },
        { "hills_12", HexFeatureEnum.Monument | HexFeatureEnum.Village | HexFeatureEnum.Road },
        { "hills_13", HexFeatureEnum.Road },
        { "hills_14", HexFeatureEnum.Road },
        { "hills_15", HexFeatureEnum.Blighted },
        { "hills_16", HexFeatureEnum.Mine },
        { "hills_17", HexFeatureEnum.Fountain },
        { "hills_18", HexFeatureEnum.Bridge | HexFeatureEnum.Watchtower | HexFeatureEnum.River },
        { "hills_19", HexFeatureEnum.None },
        { "hills_20", HexFeatureEnum.Road | HexFeatureEnum.Watchtower },
        { "hills_21", HexFeatureEnum.Road | HexFeatureEnum.Village },

        // ---------- MOUNTAINS ----------
        { "mountains_01", HexFeatureEnum.River },
        { "mountains_02", HexFeatureEnum.None },
        { "mountains_03", HexFeatureEnum.Lava },
        { "mountains_04", HexFeatureEnum.Lava },
        { "mountains_05", HexFeatureEnum.None },
        { "mountains_06", HexFeatureEnum.Ruins },

        // ---------- GRASS (grasslands) ----------
        { "grass_01", HexFeatureEnum.StandingStones | HexFeatureEnum.StandingStones | HexFeatureEnum.Road },
        { "grass_02", HexFeatureEnum.River | HexFeatureEnum.Road | HexFeatureEnum.Bridge | HexFeatureEnum.Watchtower},
        { "grass_03", HexFeatureEnum.Road | HexFeatureEnum.Watchtower},
        { "grass_04", HexFeatureEnum.River | HexFeatureEnum.Road | HexFeatureEnum.Bridge | HexFeatureEnum.Watchtower},
        { "grass_06", HexFeatureEnum.River | HexFeatureEnum.Road | HexFeatureEnum.Bridge | HexFeatureEnum.Watchtower},
        { "grass_07", HexFeatureEnum.River | HexFeatureEnum.Road | HexFeatureEnum.Bridge | HexFeatureEnum.Watchtower},
        { "grass_08", HexFeatureEnum.Village | HexFeatureEnum.Road | HexFeatureEnum.Pond},
        { "grass_09", HexFeatureEnum.Road | HexFeatureEnum.Village},
        { "grass_10", HexFeatureEnum.River | HexFeatureEnum.Road | HexFeatureEnum.Bridge | HexFeatureEnum.Village},
        { "grass_11", HexFeatureEnum.StandingStones },
        { "grass_12", HexFeatureEnum.Pond | HexFeatureEnum.Ruins},
        { "grass_13", HexFeatureEnum.Watchtower | HexFeatureEnum.River | HexFeatureEnum.Bridge },
        { "grass_14", HexFeatureEnum.Road | HexFeatureEnum.River | HexFeatureEnum.Village | HexFeatureEnum.Village},
        { "grass_15", HexFeatureEnum.Road | HexFeatureEnum.River | HexFeatureEnum.Watchtower},
        { "grass_16", HexFeatureEnum.Village | HexFeatureEnum.Road },
        { "grass_17", HexFeatureEnum.None },

        // ---------- DESERT ----------
        { "desert_01", HexFeatureEnum.Monument | HexFeatureEnum.Road },
        { "desert_02", HexFeatureEnum.Watchtower},
        { "desert_03", HexFeatureEnum.Road | HexFeatureEnum.Watchtower},
        { "desert_04", HexFeatureEnum.Road | HexFeatureEnum.Watchtower},
        { "desert_05", HexFeatureEnum.Road | HexFeatureEnum.Ruins},
        { "desert_06", HexFeatureEnum.Road | HexFeatureEnum.Ruins},
        { "desert_07", HexFeatureEnum.Monument | HexFeatureEnum.Blighted | HexFeatureEnum.River },
        { "desert_08", HexFeatureEnum.Road | HexFeatureEnum.Watchtower},
        { "desert_09", HexFeatureEnum.Pond | HexFeatureEnum.Village | HexFeatureEnum.Road},
        { "desert_10", HexFeatureEnum.Pond | HexFeatureEnum.Village | HexFeatureEnum.Road},
        { "desert_11", HexFeatureEnum.Monument | HexFeatureEnum.Road},
        { "desert_12", HexFeatureEnum.Monument | HexFeatureEnum.Ruins},
        { "desert_13", HexFeatureEnum.Monument },
        { "desert_14", HexFeatureEnum.Ruins | HexFeatureEnum.Road },
        { "desert_15", HexFeatureEnum.Village },
        { "desert_16", HexFeatureEnum.Monument | HexFeatureEnum.Fountain },
        { "desert_17", HexFeatureEnum.Pond | HexFeatureEnum.Village | HexFeatureEnum.Road},
        { "desert_18", HexFeatureEnum.None },
        { "desert_19", HexFeatureEnum.None },
        { "desert_20", HexFeatureEnum.None },

        // ---------- WASTELANDS ----------
        { "wastelands_01", HexFeatureEnum.Lava | HexFeatureEnum.Watchtower},
        { "wastelands_02", HexFeatureEnum.Lava | HexFeatureEnum.Watchtower},
        { "wastelands_03", HexFeatureEnum.Lava },
        { "wastelands_04", HexFeatureEnum.Lava },
        { "wastelands_05", HexFeatureEnum.Lava | HexFeatureEnum.Watchtower},
        { "wastelands_06", HexFeatureEnum.Lava | HexFeatureEnum.Watchtower | HexFeatureEnum.Ruins },
        { "wastelands_07", HexFeatureEnum.Lava | HexFeatureEnum.Watchtower},
        { "wastelands_08", HexFeatureEnum.Ruins | HexFeatureEnum.Blighted},
        { "wastelands_09", HexFeatureEnum.Lava },
        { "wastelands_10", HexFeatureEnum.Lava },
        { "wastelands_11", HexFeatureEnum.Lava },
        { "wastelands_12", HexFeatureEnum.Watchtower | HexFeatureEnum.Pond | HexFeatureEnum.Road | HexFeatureEnum.Blighted},
        { "wastelands_13", HexFeatureEnum.None },        
        { "wastelands_15", HexFeatureEnum.Chasm | HexFeatureEnum.StandingStones},
        { "wastelands_17", HexFeatureEnum.None },

        // ---------- SWAMP ----------
        { "swamp_01", HexFeatureEnum.Watchtower | HexFeatureEnum.Pond },
        { "swamp_02", HexFeatureEnum.Ruins },
        { "swamp_03", HexFeatureEnum.Watchtower | HexFeatureEnum.River | HexFeatureEnum.Bridge},
        { "swamp_04", HexFeatureEnum.Ruins | HexFeatureEnum.Pond},
        { "swamp_05", HexFeatureEnum.Monument | HexFeatureEnum.River },
        { "swamp_06", HexFeatureEnum.None },
        { "swamp_07", HexFeatureEnum.Ruins | HexFeatureEnum.Watchtower},
        { "swamp_08", HexFeatureEnum.Village },
        { "swamp_09", HexFeatureEnum.None },

        // ---------- SHORE ----------
        { "shore_01", HexFeatureEnum.None },
        { "shore_02", HexFeatureEnum.Lighthouse },
        { "shore_03", HexFeatureEnum.Lighthouse },

        
        // ---------- SNOW ----------
        { "snow_01", HexFeatureEnum.River |HexFeatureEnum.Bridge },
        { "snow_02", HexFeatureEnum.StandingStones },
        { "snow_03", HexFeatureEnum.Monument },
        { "snow_04", HexFeatureEnum.Fountain },
        { "snow_05", HexFeatureEnum.Watchtower },
        { "snow_06", HexFeatureEnum.Monument | HexFeatureEnum.Road },
        { "snow_07", HexFeatureEnum.None },
        { "snow_08", HexFeatureEnum.Pond | HexFeatureEnum.Watchtower },
        { "snow_09", HexFeatureEnum.Chasm },
        { "snow_10", HexFeatureEnum.Chasm | HexFeatureEnum.Pond },
        { "snow_11", HexFeatureEnum.None },


        // ---------- DEEP WATER ----------
        { "deepWater_01", HexFeatureEnum.None },
        { "deepWater_02", HexFeatureEnum.Lighthouse },

        
        // ---------- DEEP WATER ----------
        { "shallowWater_01", HexFeatureEnum.None },
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
        (HexFeatureEnum.Lumbermill, "Lumbermill"),
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

    /// <summary>The (flag, label) pairs present, in a stable display order.</summary>
    public static IEnumerable<(HexFeatureEnum flag, string label)> GetPresentFeatures(HexFeatureEnum features)
    {
        foreach (var entry in displayOrder)
            if ((features & entry.flag) != 0) yield return entry;
    }

    /// <summary>Short description of what a single feature gives, shown in the hex hover tooltip.</summary>
    public static string GetFeatureDescription(HexFeatureEnum flag) => flag switch
    {
        HexFeatureEnum.Road => "Reduces this tile's movement cost by 2 (minimum 1).",
        HexFeatureEnum.River => "Movement stops upon entering.",
        HexFeatureEnum.Pond => "Minor health recovery when resting here.",
        HexFeatureEnum.Bridge => "Lets you cross a river without movement stopping.",
        HexFeatureEnum.Watchtower => "Scouts a radius of 1. An army resting here is fortified (1 turn).",
        HexFeatureEnum.Lighthouse => "Scouts all water hexes within a radius of 3.",
        HexFeatureEnum.Ruins => "Resting here: 5% chance to recover a hidden artifact.",
        HexFeatureEnum.StandingStones => "A mage resting here gains arcane insight (1 turn).",
        HexFeatureEnum.Monument => "An army ending its move here gains courage (1 turn).",
        HexFeatureEnum.Village => "Resting here: 25% chance of a random resource.",
        HexFeatureEnum.Fountain => "Major health recovery when resting here.",
        HexFeatureEnum.Lava => "Hazard: attrition damage to non dark-servant units that stay.",
        HexFeatureEnum.Chasm => "Resting here: 10% chance to be wounded and teleported to another chasm.",
        HexFeatureEnum.Mine => "Resting here: 25% chance of a mineral resource.",
        HexFeatureEnum.Lumbermill => "Resting here: 25% chance of a wood resource.",
        HexFeatureEnum.Blighted => "Resting here: 10% chance of poison, 5% chance of being cursed.",
        _ => string.Empty
    };
}
