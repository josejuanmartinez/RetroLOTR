using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public enum ArmySpecialAbilityEnum
{
    Longrange = 0,
    ShortRange = 1,
    Poison = 2,
    Fire = 3,
    Cursed = 4,
    Raid = 5,
    Pikemen = 6,
    Shielded = 7,
    Encouraging = 8,
    Discouraging = 9,
    Berserker = 10,
    Charging = 11
}

[Serializable]
public class ArmyTroopAbilityGroup
{
    public TroopsTypeEnum troopType = TroopsTypeEnum.ma;
    public int amount = 0;
    public string troopName = string.Empty;
    public List<ArmySpecialAbilityEnum> abilities = new();

    public bool Matches(TroopsTypeEnum type, string queryTroopName, IEnumerable<ArmySpecialAbilityEnum> queryAbilities)
    {
        if (troopType != type) return false;
        if (!string.Equals(troopName ?? string.Empty, queryTroopName ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return false;
        return BuildSignature(abilities) == BuildSignature(queryAbilities);
    }

    public static string BuildSignature(IEnumerable<ArmySpecialAbilityEnum> abilities)
    {
        if (abilities == null) return string.Empty;
        return string.Join("|", abilities
            .Distinct()
            .OrderBy(value => (int)value)
            .Select(value => value.ToString()));
    }
}
