namespace RetroLOTR.Scenarios
{
    /// <summary>
    /// Process-wide selection carried from the menu/start flow into the InGame board.
    /// The menu sets <see cref="ScenarioToLoad"/> before the board boots; if it is non-empty
    /// the board loads that scenario instead of generating a procedural map.
    ///
    /// Static (not a MonoBehaviour) so it survives independently of any scene object; the
    /// board reads it once during startup and then clears nothing — set it back to null to
    /// return to procedural generation.
    /// </summary>
    public static class GameConfig
    {
        /// <summary>Resources-relative scenario name (no extension), e.g. "Scenarios/MyMap".
        /// When null/empty the board generates a procedural map as before.</summary>
        public static string ScenarioToLoad;

        /// <summary>Set when the scenario selector reloads the scene, so the intro video is
        /// skipped on those rebuilds (the player is already past the title screen).</summary>
        public static bool SkipIntro;

        public static bool HasScenario => !string.IsNullOrWhiteSpace(ScenarioToLoad);
    }
}
