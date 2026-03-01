using UnityEngine;

public static class StealthEffectHelper
{
    public static bool Apply(Character character, int turns, string message, Color messageColor)
    {
        if (character == null) return false;

        int clampedTurns = Mathf.Max(1, turns);
        character.RefuseDuels(clampedTurns);
        character.Hide(clampedTurns);
        MessageDisplayNoUI.ShowMessage(character.hex, character, message, messageColor);
        return true;
    }
}
