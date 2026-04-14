using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GreenwayDiversionAction : EventAction
{
    private const int Radius = 1;

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null || target.killed) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment();
    }

    private static int GetPriority(Character target)
    {
        if (target == null) return 0;
        return target.GetCommander() + target.GetAgent() + target.GetEmmissary() + target.GetMage();
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

            Board board = FindFirstObjectByType<Board>();
            Leader owner = character.GetOwner();
            if (board == null || owner == null) return false;

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => IsEnemy(character, ch))
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            int revealed = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                Character target = enemies[i];
                if (target.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    target.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealed++;
                }
            }

            Character strongest = enemies
                .OrderByDescending(GetPriority)
                .ThenByDescending(ch => ch.health)
                .FirstOrDefault();

            int moved = 0;
            if (strongest != null && strongest.hex != null)
            {
                List<Hex> escapeHexes = character.hex.GetHexesInRadius(1)
                    .Where(h => h != null && h != character.hex && (h.characters == null || h.characters.Count == 0))
                    .ToList();

                if (escapeHexes.Count > 0)
                {
                    Hex destination = escapeHexes[UnityEngine.Random.Range(0, escapeHexes.Count)];
                    board.MoveCharacterOneHex(strongest, strongest.hex, destination, true, false);
                    moved = 1;
                }
            }

            if (owner.goldAmount > 0)
            {
                owner.RemoveGold(1, owner == FindFirstObjectByType<Game>()?.player);
            }
            else
            {
                List<ProducesEnum> availableResources = new();
                if (owner.leatherAmount > 0) availableResources.Add(ProducesEnum.leather);
                if (owner.timberAmount > 0) availableResources.Add(ProducesEnum.timber);
                if (owner.mountsAmount > 0) availableResources.Add(ProducesEnum.mounts);
                if (owner.ironAmount > 0) availableResources.Add(ProducesEnum.iron);
                if (owner.steelAmount > 0) availableResources.Add(ProducesEnum.steel);
                if (owner.mithrilAmount > 0) availableResources.Add(ProducesEnum.mithril);

                if (availableResources.Count > 0)
                {
                    ProducesEnum lost = availableResources[UnityEngine.Random.Range(0, availableResources.Count)];
                    owner.RemoveResource(lost, 1, owner == FindFirstObjectByType<Game>()?.player);
                }
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Greenway Diversion: {revealed} hidden enemy unit(s) are exposed, {moved} enemy unit is shoved aside, and the road tax bites the enemy owner.",
                new Color(0.68f, 0.61f, 0.34f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => IsEnemy(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
