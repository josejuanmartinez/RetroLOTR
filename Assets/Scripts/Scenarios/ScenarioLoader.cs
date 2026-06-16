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

        /// <summary>Names available to a menu, read from the editor-maintained index file.</summary>
        public static List<string> GetAvailableScenarios()
        {
            TextAsset asset = Resources.Load<TextAsset>(IndexResource);
            if (asset == null) return new List<string>();
            ScenarioIndex index = JsonUtility.FromJson<ScenarioIndex>(asset.text);
            return index?.scenarioNames ?? new List<string>();
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
