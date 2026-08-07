public enum AIDifficulty
{
    Easy = 1,
    Normal = 2,
    Hard = 3,
    VeryHard = 4
}

public static class AIDifficultySettings
{
    public static AIDifficulty CurrentDifficulty { get; set; } = AIDifficulty.Normal;
}
