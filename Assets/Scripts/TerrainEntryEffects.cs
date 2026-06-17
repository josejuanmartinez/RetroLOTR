using UnityEngine;

/// <summary>
/// Climate hazards a unit can suffer the instant it steps onto a hostile-weather tile.
/// Driven from <see cref="Board.MoveCharacterOneHex"/> every time a character enters a new hex.
///
///  - Snow can frostbite (Frozen, 1 turn) anyone not naturally cold-hardy (Trolls, Dwarves).
///  - Desert can sunburn (Sunburnt, 1 turn) anyone not desert-born (Southrons, Easterlings).
///
/// The moving character is the army's commander when it leads troops, so its own race stands in
/// for the army's make-up — matching how the Snow / Sand Storm event cards judge immunity.
/// </summary>
public static class TerrainEntryEffects
{
    private const float FrostbiteChance = 0.05f;
    private const float SunburnChance = 0.05f;

    public static void ProcessEntry(Character character, Hex hex)
    {
        if (character == null || character.killed || hex == null) return;

        if (hex.terrainType == TerrainEnum.snow) TryFrostbite(character, hex);
        else if (hex.terrainType == TerrainEnum.desert) TrySunburn(character, hex);
    }

    private static void TryFrostbite(Character character, Hex hex)
    {
        if (character.race == RacesEnum.Troll || character.race == RacesEnum.Dwarf) return;
        if (Random.value >= FrostbiteChance) return;

        character.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
        MessageDisplayNoUI.ShowMessage(hex, character, $"{character.characterName} is frostbitten by the snow! (Frozen)", Color.cyan);
    }

    private static void TrySunburn(Character character, Hex hex)
    {
        if (character.race == RacesEnum.Southron || character.race == RacesEnum.Easterling) return;
        float chance = SunburnChance + (EnvironmentalCardManager.Instance?.SunburntEntryChanceBonus ?? 0f);
        if (Random.value >= chance) return;

        character.ApplyStatusEffect(StatusEffectEnum.Sunburnt, 1);
        MessageDisplayNoUI.ShowMessage(hex, character, $"{character.characterName} is sunburnt by the desert heat! (Sunburnt)", new Color(1f, 0.55f, 0.1f));
    }
}
