public enum AdvisorType
{
    None = 0,
    Militaristic = 1,
    Economic = 2,
    Diplomatic = 3,
    Intelligence = 4,
    Magic = 5,
    // Movement was split into Disruption (deny/debuff the enemy) and Logistics (reposition/heal
    // our own side) — two distinct strategic intents that a single "move toward a destination"
    // formula couldn't represent.
    Disruption = 6,
    Logistics = 7
}

public enum EconomyStatus
{
    Critical = 0,
    Weak = 1,
    Stable = 2,
    Surplus = 3
}
