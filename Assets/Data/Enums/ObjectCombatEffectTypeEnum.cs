// The closed set of combat-relevant effects an Object card can grant. Kept deliberately
// separate from the non-combat Object fields (commanderBonus, healPerTurn, scryAreaBonus,
// etc.) which don't feed Duel.cs or Army.cs and so aren't a balance risk the same way.
public enum ObjectCombatEffectTypeEnum
{
    AttackBonus = 0,
    DefenseBonus = 1,
    AttackBonusVsRace = 2,
    DefenseBonusVsRace = 3,
    AttackBonusVsTroopType = 4,
    DefenseBonusVsTroopType = 5,
    ArmyAttackBonus = 6,
    ArmyDefenseBonus = 7,
    EnemyArmyDefensePenaltySameHex = 8,
}
