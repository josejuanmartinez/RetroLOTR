using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroLOTR.Scenarios
{
    /// <summary>
    /// Runtime entry point for reading authored scenarios. Mirrors how DeckManager and the
    /// leader-biome configs are loaded: plain JSON under Resources, parsed with JsonUtility.
    /// </summary>
    public static class ScenarioLoader
    {
        public const string ResourceFolder = "Scenarios";
        public const string IndexResource = "Scenarios/ScenariosIndex";

        [Serializable]
        private class ScenarioIndex
        {
            public List<string> scenarioNames = new();
        }

        /// <summary>
        /// Loads a scenario by its Resources path. Accepts either the bare name ("MyMap") or a
        /// fully-qualified resource path ("Scenarios/MyMap").
        /// </summary>
        public static ScenarioData Load(string scenarioName)
        {
            if (string.IsNullOrWhiteSpace(scenarioName)) return null;

            string resourcePath = scenarioName.Replace("\\", "/").Trim();
            if (!resourcePath.StartsWith(ResourceFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                resourcePath = $"{ResourceFolder}/{resourcePath}";
            }

            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError($"ScenarioLoader: could not load scenario at Resources/{resourcePath}.json");
                return null;
            }

            ScenarioData data;
            try
            {
                data = JsonUtility.FromJson<ScenarioData>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"ScenarioLoader: failed to parse scenario '{resourcePath}': {e.Message}");
                return null;
            }

            if (data == null || data.width <= 0 || data.height <= 0)
            {
                Debug.LogError($"ScenarioLoader: scenario '{resourcePath}' is empty or has an invalid size.");
                return null;
            }

            int expected = data.width * data.height;
            if (data.terrain == null || data.terrain.Length != expected)
            {
                Debug.LogError($"ScenarioLoader: scenario '{resourcePath}' terrain length {data.terrain?.Length ?? 0} " +
                               $"does not match {data.width}x{data.height} ({expected}).");
                return null;
            }

            return data;
        }

        /// <summary>Names available to a menu, read from the editor-maintained index file.
        /// Entries whose scenario file no longer exists (renamed/deleted without the index
        /// being pruned) are skipped so menus never offer an unloadable scenario.</summary>
        public static List<string> GetAvailableScenarios()
        {
            TextAsset asset = Resources.Load<TextAsset>(IndexResource);
            if (asset == null) return new List<string>();
            ScenarioIndex index = JsonUtility.FromJson<ScenarioIndex>(asset.text);
            if (index?.scenarioNames == null) return new List<string>();

            List<string> available = new();
            foreach (string name in index.scenarioNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (Resources.Load<TextAsset>($"{ResourceFolder}/{name}") == null)
                {
                    Debug.LogWarning($"ScenarioLoader: index lists '{name}' but Resources/{ResourceFolder}/{name}.json does not exist — skipping (stale index entry).");
                    continue;
                }
                available.Add(name);
            }
            return available;
        }

        [Serializable]
        private class ScenarioSize
        {
            public int width;
            public int height;
        }

        /// <summary>
        /// Largest width/height across all indexed scenarios — used to size the hex-pool
        /// prewarm before the player has picked what to play. (0,0) when none exist.
        /// </summary>
        public static Vector2Int GetLargestScenarioSize()
        {
            int width = 0, height = 0;
            foreach (string name in GetAvailableScenarios())
            {
                TextAsset asset = Resources.Load<TextAsset>($"{ResourceFolder}/{name}");
                if (asset == null) continue;
                ScenarioSize size = null;
                try { size = JsonUtility.FromJson<ScenarioSize>(asset.text); }
                catch { /* malformed file — skip */ }
                if (size == null) continue;
                width = Mathf.Max(width, size.width);
                height = Mathf.Max(height, size.height);
            }
            return new Vector2Int(width, height);
        }

        /// <summary>
        /// Builds the <c>TerrainEnum[height, width]</c> grid the Board/BoardGenerator expect from
        /// a scenario's flat row-major array.
        /// </summary>
        public static TerrainEnum[,] BuildTerrainGrid(ScenarioData data)
        {
            if (data == null) return null;
            var grid = new TerrainEnum[data.height, data.width];
            for (int row = 0; row < data.height; row++)
            {
                for (int col = 0; col < data.width; col++)
                {
                    int i = row * data.width + col;
                    int value = (i >= 0 && i < data.terrain.Length) ? data.terrain[i] : (int)TerrainEnum.deepWater;
                    if (value < 0 || value >= (int)TerrainEnum.MAX) value = (int)TerrainEnum.deepWater;
                    grid[row, col] = (TerrainEnum)value;
                }
            }
            return grid;
        }
    }
}
