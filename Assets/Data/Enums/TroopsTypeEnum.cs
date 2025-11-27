using System.Collections.Generic;

public enum TroopsTypeEnum 
{
    ma = 0,
    ar = 1,
    li = 2,
    hi = 3,
    lc = 4,
    hc = 5,
    ca = 6,
    ws = 7
}

public static class ArmyData
{
    public static Dictionary<TroopsTypeEnum, int> troopsStrength = new ()
    {
        {TroopsTypeEnum.ma, 1},
        {TroopsTypeEnum.ar, 2},
        {TroopsTypeEnum.li, 2},
        {TroopsTypeEnum.hi, 3},
        {TroopsTypeEnum.lc, 3},
        {TroopsTypeEnum.hc, 4},
        {TroopsTypeEnum.ca, 2}, // 5 if attacking a PC
        {TroopsTypeEnum.ws, 0}, 
    };

    public static Dictionary<TroopsTypeEnum, int> troopsDefence = new()
    {
        {TroopsTypeEnum.ma, 1},
        {TroopsTypeEnum.ar, 1},
        {TroopsTypeEnum.li, 2},
        {TroopsTypeEnum.hi, 3},
        {TroopsTypeEnum.lc, 2},
        {TroopsTypeEnum.hc, 3},
        {TroopsTypeEnum.ca, 1}, // 5 if attacking a PC
        {TroopsTypeEnum.ws, 0},
    };

    public static int transportedStrength = 1;
    public static int warshipStrength = 5;
    public static int catapultStrengthMultiplierInPC = 3; // 3 * troopsAttack[ca] if attacking a PC * ca
    public static int biomeTerrainMultiplier = 2;
}

