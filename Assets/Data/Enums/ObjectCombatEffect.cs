using System;

// Closed magnitude tiers for combat effects, so nobody can type an unbalanced raw number.
// Current shipped cards only use Minor/Major (see balance review, 2026-08-06); Legendary is
// headroom for a deliberately rare best-in-class item, not a default to reach for.
public enum ObjectCombatEffectMagnitudeEnum
{
    Minor = 1,
    Major = 2,
    Legendary = 3,
}

// One entry in an Object card's combatEffects list (CardData.combatEffects). targetRace is
// only meaningful when type is *VsRace; targetTroopType only when type is *VsTroopType — both
// are ignored otherwise. All picked via dropdowns in Deck Explorer, never free-typed.
[Serializable]
public class ObjectCombatEffect
{
    public ObjectCombatEffectTypeEnum type;
    public RacesEnum targetRace;
    public TroopsTypeEnum targetTroopType;
    public ObjectCombatEffectMagnitudeEnum magnitude = ObjectCombatEffectMagnitudeEnum.Minor;

    public int Value => (int)magnitude;
}
