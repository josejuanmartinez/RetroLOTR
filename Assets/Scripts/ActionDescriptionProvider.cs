using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class ActionDescriptionProvider
{
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        { "You Shall Not Pass!", "Halt an enemy character in this hex; Balrogs also take damage." },
        { "Attack", "Lead your army to attack enemy armies or population centers in this hex." },
        { "State Allegiance", "At an aligned NPC capital, swear allegiance to recruit them if their conditions are met." },
        { "Undouble Character", "Withdraw your double agent from a character you previously doubled here." },
        { "Buy 5 Leather", "Buy 5 <sprite name=\"leather\"/> from the caravans." },
        { "Assassinate Character", "Attempt to kill an enemy character in this hex; the agent returns to capital and may be wounded." },
        { "Steal Iron", "Steal up to your agent rating in <sprite name=\"iron\"/> from the current population center." },
        { "Buy 5 Mounts", "Buy 5 <sprite name=\"mounts\"/> from the caravans." },
        { "Train Light Cavalry", "Add 1 <sprite name=\"lc\"/> to your army here (or form a new army)." },
        { "Perceive Danger", "Reveal the nearest enemy army and the nearby area." },
        { "Sell 5 Mithril", "Sell 5 <sprite name=\"mithril\"/> from your stores for gold." },
        { "Train Warships", "Add 1 <sprite name=\"ws\"/> at this port." },
        { "Haste", "Refund movement so this non-army commander can move farther this turn." },
        { "Teleport", "Blink to a random explored hex and reveal tiles based on mage skill." },
        { "Scout Area", "Reveal the surrounding hexes." },
        { "Words of Dispair", "Inflict magical casualties on an enemy army sharing your hex." },
        { "Hear Stories", "Gather rumours based on your emissary skill." },
        { "Cast Light", "Reveal the area around your hex." },
        { "Reveal PC", "Expose a hidden population center in this hex." },
        { "Train Archers", "Add 1 <sprite name=\"ar\"/> to your army here (or form a new army)." },
        { "Train Light Infantry", "Add 1 <sprite name=\"li\"/> to your army here (or form a new army)." },
        { "Steal Timber", "Steal <sprite name=\"timber\"/> from the current population center." },
        { "Block", "Engage a selected enemy army in this hex (no additional effect yet)." },
        { "Steal Mithril", "Steal <sprite name=\"mithril\"/> from the current population center." },
        { "Buy 5 Iron", "Buy 5 <sprite name=\"iron\"/> from the caravans." },
        { "Sell 5 Timber", "Sell 5 <sprite name=\"timber\"/> from your stores for gold." },
        { "Pass", "End this character's turn without taking another action." },
        { "Wizard's Fire", "Burn an enemy army in your hex, causing casualties." },
        { "Steal Mounts", "Steal <sprite name=\"mounts\"/> from the current population center." },
        { "Ice Storm", "Freeze an enemy army in this hex, halting it for a turn." },
        { "Courage", "Encourage a friendly army here for several turns." },
        { "Wound Character", "Attempt to wound an enemy character here; the agent returns to capital." },
        { "Sell 5 Iron", "Sell 5 <sprite name=\"iron\"/> from your stores for gold." },
        { "Sell 5 Steel", "Sell 5 <sprite name=\"steel\"/> from your stores for gold." },
        { "Increase Loyalty", "Raise the loyalty of the population center in this hex." },
        { "Scry Artifact", "Reveal a hidden artifact's location and show its details." },
        { "Steal Leather", "Steal <sprite name=\"leather\"/> from the current population center." },
        { "Double Character", "Turn an enemy character here into your double agent." },
        { "Increase Fortifications", "Upgrade the fortification level of your population center here." },
        { "Fireworks", "Shift this PC's loyalty using fireworks (up if aligned, down if hostile)." },
        { "Wizards Laugh", "Clear doubles on allied characters here or reduce loyalty of a hostile/neutral PC." },
        { "Wizard's Laugh", "Clear doubles on allied characters here or reduce loyalty of a hostile/neutral PC." },
        { "Train Heavy Infantry", "Add 1 <sprite name=\"hi\"/> to your army here (or form a new army)." },
        { "Transfer Artifact", "Give a transferable artifact to a friendly character in this hex." },
        { "Return To Capital", "Teleport this character back to your capital (not available to army commanders)." },
        { "Halt", "Stop a non-army enemy character in this hex from acting or moving next turn." },
        { "Conjure Mounts", "Create 1-3 <sprite name=\"mounts\"/> at a friendly or aligned population center here." },
        { "Scry Area", "Reveal a random unseen hex and its surroundings." },
        { "Oh, Elbereth!", "Smite an enemy character here; Nazgul are also halted." },
        { "Train Army", "Drill your troops, improving army XP up to elite levels based on commander skill." },
        { "Decrease Loyalty", "Lower the loyalty of the population center in this hex." },
        { "Summon Men-at-arms", "Add 1 <sprite name=\"ma\"/> to an owned population center here." },
        { "Sell 5 Leather", "Sell 5 <sprite name=\"leather\"/> from your stores for gold." },
        { "Cast Darkness", "Obscure the area around your hex from enemy sight." },
        { "Curse", "Damage an enemy character in your hex with dark magic." },
        { "Sell 5 Mounts", "Sell 5 <sprite name=\"mounts\"/> from your stores for gold." },
        { "Train Heavy Cavalry", "Add 1 <sprite name=\"hc\"/> to your army here (or form a new army)." },
        { "Buy 5 Timber", "Buy 5 <sprite name=\"timber\"/> from the caravans." },
        { "Train Met-at-arms", "Add 1 <sprite name=\"ma\"/> to your army here (or form a new army)." },
        { "Steal Gold", "Steal gold from the current population center." },
        { "Train Catapults", "Add 1 <sprite name=\"ca\"/> to your army here (or form a new army)." },
        { "Heal", "Heal a wounded allied or aligned character in your hex." },
        { "Find Artifact", "Search this hex for hidden artifacts and claim one if found." },
        { "Buy 5 Mithril", "Buy 5 <sprite name=\"mithril\"/> from the caravans." },
        { "Buy 5 Steel", "Buy 5 <sprite name=\"steel\"/> from the caravans." }
    };

    public static string Get(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName)) return string.Empty;
        if (Descriptions.TryGetValue(actionName, out string description)) return description;

        string normalized = Normalize(actionName);
        return NormalizedDescriptions.TryGetValue(normalized, out string normalizedDescription)
            ? normalizedDescription
            : string.Empty;
    }

    private static readonly Dictionary<string, string> NormalizedDescriptions = BuildNormalizedDescriptions();

    private static Dictionary<string, string> BuildNormalizedDescriptions()
    {
        Dictionary<string, string> result = new();
        foreach (var kvp in Descriptions)
        {
            string key = Normalize(kvp.Key);
            if (!result.ContainsKey(key)) result[key] = kvp.Value;
        }
        return result;
    }

    // Mirrors SearcherByName.Normalize to keep matching behavior consistent
    private static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string normalized = name.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new();
        foreach (char c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace(" ", "").ToLower();
    }
}
