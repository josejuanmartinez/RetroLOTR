using System;
using System.Collections.Generic;

namespace RetroLOTR.Scenarios
{
    /// <summary>
    /// Serializable description of a hand-authored map: terrain, region paint, and every
    /// starting placement (leaders, PCs, characters and their armies). Saved as JSON under
    /// Resources/Scenarios and loaded by <see cref="ScenarioLoader"/> at runtime.
    ///
    /// Coordinates use the same convention as <c>Hex.v2</c>: <c>row</c> is the vertical axis
    /// (Board height index) and <c>col</c> is the horizontal axis (Board width index). The
    /// flat <see cref="terrain"/> array is row-major: <c>index = row * width + col</c>.
    ///
    /// Shared content (PlayableLeaderBiomes.json / NonPlayableLeaderBiomes.json and the card
    /// decks) is NEVER embedded here — placements only reference it by name so a single edit to
    /// a leader or card propagates to every scenario.
    /// </summary>
    [Serializable]
    public class ScenarioData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string scenarioName = "New Scenario";

        public int width;
        public int height;

        /// <summary>Row-major terrain grid, each entry cast from <see cref="TerrainEnum"/>.</summary>
        public int[] terrain = Array.Empty<int>();

        /// <summary>Sparse per-hex land region overrides (only hexes the author painted a region on).</summary>
        public List<ScenarioRegionCell> regions = new();

        /// <summary>Sparse per-hex terrain-sprite overrides. When set, the loader applies this exact
        /// tile variation (by sprite name), which also drives that hex's landmark features. Hexes
        /// without an override fall back to the terrain's default/random variation.</summary>
        public List<ScenarioSpriteCell> terrainSprites = new();

        public List<ScenarioLeaderStart> leaderStarts = new();
        public List<ScenarioPC> pcs = new();
        public List<ScenarioCharacter> characters = new();

        public int Index(int row, int col) => row * width + col;

        public bool InBounds(int row, int col) => row >= 0 && row < height && col >= 0 && col < width;

        public TerrainEnum GetTerrain(int row, int col)
        {
            if (!InBounds(row, col) || terrain == null) return TerrainEnum.deepWater;
            int i = Index(row, col);
            return (i >= 0 && i < terrain.Length) ? (TerrainEnum)terrain[i] : TerrainEnum.deepWater;
        }
    }

    [Serializable]
    public class ScenarioRegionCell
    {
        public int row;
        public int col;
        public string region;
    }

    [Serializable]
    public class ScenarioSpriteCell
    {
        public int row;
        public int col;
        public string spriteName;
    }

    /// <summary>
    /// Marks a hex as the starting position of a shared leader (playable or non-playable),
    /// referenced by its biome <c>characterName</c>. Only spawns the leader unit itself; its
    /// capital and retinue are authored separately as <see cref="ScenarioPC"/>/<see cref="ScenarioCharacter"/>.
    /// </summary>
    [Serializable]
    public class ScenarioLeaderStart
    {
        public int row;
        public int col;
        public string leaderName;
        public bool isPlayable = true;
    }

    [Serializable]
    public class ScenarioPC
    {
        public int row;
        public int col;
        public string pcName;            // from a PC card
        public string ownerLeaderName;   // a leaderStart's leaderName, or empty for ownerless
        public int citySize = (int)PCSizeEnum.village;
        public int fortSize = (int)FortSizeEnum.NONE;
        public bool hasPort;
        public bool isHidden;
        public bool isCapital;
        public int loyalty = 100;
        public string region = "";
        public bool isIsland;
        public string pcFeature = "";
        public string fortFeature = "";
    }

    [Serializable]
    public class ScenarioCharacter
    {
        public int row;
        public int col;
        public string characterName;     // from a Character card
        public string ownerLeaderName;   // a leaderStart's leaderName
        public ScenarioArmy army;        // null when the character bears no army
    }

    /// <summary>An army described as a set of army-card stacks plus shared XP.</summary>
    [Serializable]
    public class ScenarioArmy
    {
        public int xp = 25;
        public List<ScenarioArmyStack> stacks = new();

        public bool IsEmpty()
        {
            if (stacks == null) return true;
            foreach (ScenarioArmyStack s in stacks)
                if (s != null && s.amount > 0 && !string.IsNullOrWhiteSpace(s.armyCardName)) return false;
            return true;
        }
    }

    [Serializable]
    public class ScenarioArmyStack
    {
        public string armyCardName;      // from an Army card (supplies troop type + abilities)
        public int amount;
    }
}
