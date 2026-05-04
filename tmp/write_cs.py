import os

files = {}

files[r'Assets\Scripts\Actions\Events\The5RideAgain.cs'] = '''using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class The5RideAgain : EventAction
{
    private const int HasteTurns = 2;
    private const int InsightTurns = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            List<Character> maiaAllies = board.hexes.Values
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Maia && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (maiaAllies.Count == 0) return false;

            foreach (Character ally in maiaAllies)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Haste, HasteTurns);
                ally.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, InsightTurns);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The 5 ride again: {maiaAllies.Count} allied Maia gain Haste and Arcane Insight for {HasteTurns} turns.", new Color(0.8f, 0.7f, 0.4f));
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            return board.hexes.Values
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Maia && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
'''

files[r'Assets\Scripts\Actions\Events\BeastsBiddenToServe.cs'] = '''using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BeastsBiddenToServe : EventAction
{
    private const int Radius = 3;
    private const int StrengthenTurns = 2;
    private const int HasteTurns = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> beasts = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Beast && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (beasts.Count == 0) return false;

            foreach (Character beast in beasts)
            {
                beast.ApplyStatusEffect(StatusEffectEnum.Strengthened, StrengthenTurns);
                beast.ApplyStatusEffect(StatusEffectEnum.Haste, HasteTurns);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Beasts Bidden to Serve strengthen {beasts.Count} allied beast(s) for {StrengthenTurns} turns.", Color.green);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Beast && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
'''

files[r'Assets\Scripts\Actions\Events\FarShoreFadedStar.cs'] = '''using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FarShoreFadedStar : EventAction
{
    private const int BorderDistance = 5;
    private const int Duration = 3;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsNearBorder(Hex hex, Board board)
    {
        if (hex == null || board == null || board.hexes == null) return false;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var h in board.hexes.Values)
        {
            if (h == null) continue;
            minX = Mathf.Min(minX, h.v2.x);
            maxX = Mathf.Max(maxX, h.v2.x);
            minY = Mathf.Min(minY, h.v2.y);
            maxY = Mathf.Max(maxY, h.v2.y);
        }

        return hex.v2.x <= minX + BorderDistance
            || hex.v2.x >= maxX - BorderDistance
            || hex.v2.y <= minY + BorderDistance
            || hex.v2.y >= maxY - BorderDistance;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            List<Character> targets = board.hexes.Values
                .Where(h => h != null && h.characters != null && IsNearBorder(h, board))
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            foreach (Character target in targets)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Hope, Duration);
                target.ApplyStatusEffect(StatusEffectEnum.Encouraged, Duration);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Far Shore, Faded Star: {targets.Count} allied unit(s) near the border gain Hope and Courage for {Duration} turns.", new Color(0.6f, 0.7f, 0.9f));
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            return board.hexes.Values
                .Where(h => h != null && h.characters != null && IsNearBorder(h, board))
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
'''

files[r'Assets\Scripts\Actions\Events\HostOfOrthancAction.cs'] = '''using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HostOfOrthancAction : EventAction
{
    private const int Radius = 1;
    private const int HiddenTurns = 1;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || pc.pcName != "Orthanc") return false;

            List<Character> allies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            foreach (Character ally in allies)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Hidden, HiddenTurns);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Host of Orthanc: {allies.Count} allied unit(s) in radius {Radius} become Hidden for {HiddenTurns} turn.", Color.gray);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || pc.pcName != "Orthanc") return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
'''

files[r'Assets\Scripts\Actions\Events\TheSecondSending.cs'] = '''using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class TheSecondSending : EventAction
{
    private const int InsightTurns = 3;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            return board.hexes.Values.Any(h => h != null && !h.IsWaterTerrain());
        };

        async Task<bool> sendingAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            List<Hex> validHexes = board.hexes.Values
                .Where(h => h != null && !h.IsWaterTerrain())
                .ToList();

            if (validHexes.Count == 0) return false;

            Hex previousHex = character.hex;
            Hex targetHex = validHexes[UnityEngine.Random.Range(0, validHexes.Count)];

            if (previousHex.characters.Contains(character)) previousHex.characters.Remove(character);
            if (character.IsArmyCommander() && previousHex.armies != null && character.GetArmy() != null && previousHex.armies.Contains(character.GetArmy()))
                previousHex.armies.Remove(character.GetArmy());
            previousHex.RedrawCharacters();
            previousHex.RedrawArmies();

            if (!targetHex.characters.Contains(character)) targetHex.characters.Add(character);
            if (character.IsArmyCommander() && targetHex.armies != null && character.GetArmy() != null && !targetHex.armies.Contains(character.GetArmy()))
                targetHex.armies.Add(character.GetArmy());

            character.hex = targetHex;
            character.RefreshKidnappedCharactersPosition();
            Character.RefreshArtifactPcVisibilityForHex(previousHex);
            Character.RefreshArtifactPcVisibilityForHex(targetHex);

            targetHex.RedrawCharacters();
            targetHex.RedrawArmies();

            if (character.GetOwner() == UnityEngine.Object.FindFirstObjectByType<Game>()?.player)
            {
                targetHex.LookAt();
                targetHex.RevealArea(1, true);
            }

            character.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, InsightTurns);

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Second Sending transports {character.characterName} to a distant hex and grants Arcane Insight for {InsightTurns} turns.", new Color(0.5f, 0.4f, 0.7f));
            return true;
        }

        base.Initialize(c, condition, effect, sendingAsync);
    }
}
'''

files[r'Assets\Scripts\Actions\Events\TheTowersKindness.cs'] = '''using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheTowersKindness : EventAction
{
    private const int InsightTurns = 3;
    private const int HaltedTurns = 1;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || pc.pcName != "Orthanc") return false;

            List<Character> mages = character.hex.GetHexesInRadius(0)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetMage() > 0 && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (mages.Count == 0) return false;

            foreach (Character mage in mages)
            {
                mage.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, InsightTurns);
                mage.ApplyStatusEffect(StatusEffectEnum.Halted, HaltedTurns);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Tower's Kindness grants {mages.Count} mage(s) at Orthanc Arcane Insight for {InsightTurns} turns, but Halted for {HaltedTurns} turn.", Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || pc.pcName != "Orthanc") return false;

            return character.hex.GetHexesInRadius(0)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetMage() > 0 && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
'''

for path, content in files.items():
    with open(path, 'w') as f:
        f.write(content)
    print(f'Wrote {path}')

print('Done')
