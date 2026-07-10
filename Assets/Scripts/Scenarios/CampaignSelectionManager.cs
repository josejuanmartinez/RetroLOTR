using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetroLOTR.Scenarios
{
    /// <summary>
    /// Campaign/scenario selection — the first screen of a fresh game. Lives on a prefab
    /// (Assets/Resources/CampaignSelectionScreen.prefab) so every visual — background, panel,
    /// buttons, texts — is authored in Unity, never generated at runtime. Board instantiates the
    /// prefab and waits on <see cref="GameConfig.ScenarioChosen"/>.
    ///
    /// The scenario list is the only data-driven part: <see cref="scenarioButtonTemplate"/> is
    /// cloned once per authored scenario (ScenarioLoader.GetAvailableScenarios), so styling the
    /// template styles every entry. The clone's child TMP named "Title" receives the scenario
    /// name; everything else on it stays exactly as authored.
    /// </summary>
    public class CampaignSelectionManager : MonoBehaviour
    {
        [Header("Wiring (style everything else freely in the prefab)")]
        [Tooltip("Starts the default random campaign (ScenarioToLoad = null). Its texts/visuals are whatever the prefab says.")]
        [SerializeField] private Button defaultCampaignButton;
        [Tooltip("Parent the per-scenario buttons are instantiated under (usually the scroll view's Content).")]
        [SerializeField] private RectTransform scenarioButtonContainer;
        [Tooltip("Button inside the container cloned once per authored scenario. Kept inactive; its child TMP named 'Title' receives the scenario name.")]
        [SerializeField] private Button scenarioButtonTemplate;

        private void Start()
        {
            if (defaultCampaignButton != null)
                defaultCampaignButton.onClick.AddListener(() => Choose(null));
            else
                Debug.LogError("CampaignSelectionManager: defaultCampaignButton is not wired in the prefab.");

            PopulateScenarioButtons();
        }

        private void PopulateScenarioButtons()
        {
            if (scenarioButtonContainer == null || scenarioButtonTemplate == null)
            {
                Debug.LogError("CampaignSelectionManager: scenario list is not wired (container/template) in the prefab.");
                return;
            }

            scenarioButtonTemplate.gameObject.SetActive(false);

            List<string> scenarios = ScenarioLoader.GetAvailableScenarios();
            foreach (string scenario in scenarios)
            {
                string captured = scenario;
                Button button = Instantiate(scenarioButtonTemplate, scenarioButtonContainer);
                button.gameObject.name = $"Scenario_{captured}";
                button.gameObject.SetActive(true);
                button.onClick.AddListener(() => Choose(captured));

                TMP_Text title = FindTitleLabel(button.transform);
                if (title != null) title.text = captured;
            }
        }

        // The scenario name goes into the clone's TMP child named "Title"; if the author renamed
        // it, fall back to the first TMP found so the button is never nameless.
        private static TMP_Text FindTitleLabel(Transform root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (string.Equals(text.name, "Title", System.StringComparison.OrdinalIgnoreCase)) return text;
            return root.GetComponentInChildren<TMP_Text>(true);
        }

        private void Choose(string scenarioName)
        {
            GameConfig.ScenarioToLoad = scenarioName; // null = default random campaign
            GameConfig.ScenarioChosen = true;
            Destroy(gameObject);
        }
    }
}
