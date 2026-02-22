using System;

public enum CardTypeEnum
{
    Unknown = 0,
    Action = 1,
    Event = 2,
    Land = 3,
    PC = 4,
    Character = 5,
    Army = 6,
    Rest = 7,
    Encounter = 8
}

public static class CardTypeParser
{
    public static CardTypeEnum Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return CardTypeEnum.Unknown;
        return Enum.TryParse(value.Trim(), true, out CardTypeEnum parsed)
            ? parsed
            : CardTypeEnum.Unknown;
    }
}
