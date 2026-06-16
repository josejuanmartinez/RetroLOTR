using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroLOTR.Scenarios
{
    /// <summary>
    /// Drop-in component for the leader-selection screen: a dropdown that chooses which map the
    /// game plays. Because the board (and its playable leaders) is built at scene load, picking a
    /// scenario reloads the scene with <see cref="GameConfig.ScenarioToLoad"/> set — the board
    /// then loads the authored map and the carousel repopulates with that scenario's leaders.
    /// "Procedural" clears the selection and reloads into a normal generated map.
    ///
    /// Wiring: add this to the LeaderSelector screen and assign a TMP_Dropdown. No changes to the
    /// existing Start Game / Start Tutorial buttons are needed — they operate on whatever board
    /// is currently loaded.
    /// </summary>
    public class ScenarioSelector : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown dropdown;
        [SerializeField] private string proceduralLabel = "Procedural (random)";

        private readonly List<string> scenarioNames = new();

        private void Awake()
        {
            if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();
            Populate();
        }

        private void Populate()
        {
            if (dropdown == null)
            {
                Debug.LogWarning("ScenarioSelector: no TMP_Dropdown assigned.");
                return;
            }

            scenarioNames.Clear();
            scenarioNames.AddRange(ScenarioLoader.GetAvailableScenarios());

            var options = new List<string> { proceduralLabel };
            options.AddRange(scenarioNames);
            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            // Reflect the currently-loaded scenario (survives the scene reload via GameConfig).
            int current = 0;
            if (GameConfig.HasScenario)
            {
                string loaded = StripFolder(GameConfig.ScenarioToLoad);
                int i = scenarioNames.FindIndex(n => string.Equals(n, loaded, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) current = i + 1;
            }
            dropdown.SetValueWithoutNotify(current);

            dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            dropdown.onValueChanged.AddListener(OnDropdownChanged);
        }

        private void OnDropdownChanged(int index)
        {
            string chosen = (index <= 0 || index - 1 >= scenarioNames.Count) ? null : scenarioNames[index - 1];
            string currentlyLoaded = GameConfig.HasScenario ? StripFolder(GameConfig.ScenarioToLoad) : null;

            // No-op if the choice already matches what is loaded.
            if (string.Equals(chosen, currentlyLoaded, StringComparison.OrdinalIgnoreCase)) return;

            GameConfig.ScenarioToLoad = chosen; // ScenarioLoader resolves the Resources/Scenarios prefix
            GameConfig.SkipIntro = true;

            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex);
        }

        private static string StripFolder(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            int slash = value.LastIndexOf('/');
            return slash >= 0 ? value.Substring(slash + 1) : value;
        }
    }
}
