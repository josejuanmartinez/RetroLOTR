using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WhiteHorsesFoam : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.hex == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            int radius = Mathf.Clamp(c.GetMage(), 1, 5);
            List<Character> nazguls = c.hex.GetHexesInRadius(radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul)
                .Distinct()
                .ToList();

            List<Character> burningUnits = c.hex.GetHexesInRadius(radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.HasStatusEffect(StatusEffectEnum.Burning))
                .Distinct()
                .ToList();

            if (nazguls.Count == 0 && burningUnits.Count == 0) return false;

            int movedCount = 0;
            for (int i = 0; i < nazguls.Count; i++)
            {
                Character nazgul = nazguls[i];
                Leader owner = nazgul.GetOwner();
                if (owner == null) continue;

                Hex capitalHex = board.GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == owner && x.GetPC().isCapital);
                if (capitalHex == null || capitalHex == nazgul.hex) continue;

                board.MoveCharacterOneHex(nazgul, nazgul.hex, capitalHex, true);
                movedCount++;
            }

            int burningCleared = 0;
            for (int i = 0; i < burningUnits.Count; i++)
            {
                burningUnits[i].ClearStatusEffect(StatusEffectEnum.Burning);
                burningCleared++;
            }

            if (movedCount == 0 && burningCleared == 0) return false;

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"White Horses' Foam drives {movedCount} Nazgul back to their capitals and removes Burning from {burningCleared} unit(s).", Color.cyan);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            int radius = Mathf.Clamp(c.GetMage(), 1, 5);
            List<Character> nazguls = c.hex.GetHexesInRadius(radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul)
                .Distinct()
                .ToList();

            bool canMoveNazgul = nazguls.Any(n =>
            {
                Leader owner = n.GetOwner();
                if (owner == null) return false;
                Hex capitalHex = board.GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == owner && x.GetPC().isCapital);
                return capitalHex != null && capitalHex != n.hex;
            });

            if (canMoveNazgul) return true;

            return c.hex.GetHexesInRadius(radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && ch.HasStatusEffect(StatusEffectEnum.Burning));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
