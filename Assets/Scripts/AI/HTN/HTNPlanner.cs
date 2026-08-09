using System;
using System.Collections.Generic;
using System.Linq;

// ---------------------------------------------------------------------------
// Runtime decomposition/execution for HTN strategies. See the design plan
// (cosmic-wiggling-otter.md §3) for the full rationale. Summary:
//   - Decompose: fresh top-down walk, first Method per CompoundTask whose
//     precondition holds, descending until a PrimitiveTask leaf is reached.
//   - AdvanceStack: the per-leader-turn entry point. Three things, in order:
//       1) priority-interrupt scan (higher-priority siblings can always preempt)
//       2) failure check (the active primitive's own precondition breaking)
//       3) completion-driven advancement (the active primitive's completion
//          condition firing moves execution to the next subtask in its
//          Method's sequence, cascading upward through the stack as needed)
// Planning is lazy/interleaved: each CompoundTask is only decomposed against
// the real current AIContext/Blackboard state once execution reaches it.
// ---------------------------------------------------------------------------

public static class HTNPlanner
{
    // Tries each candidate Method in priority order; a candidate is only actually chosen if its
    // precondition holds AND (recursively) its subtree bottoms out at a leaf this character can
    // act on. A leaf with a real advisor bias is skipped if the character has no role-eligible
    // card for that advisor anywhere in its deck (AIContext.HasEligibleCard) — otherwise that
    // bias would be wasted for the whole turn AND would monopolize the branch slot, starving a
    // lower-priority branch the character could actually use. This is why Decompose recurses
    // and backtracks (try the next sibling if a chosen candidate's whole subtree turns out
    // empty) instead of committing greedily to the first precondition match, all the way up to
    // the root — so an ineligible character still reaches root.fallback (or any other viable
    // top-level branch) instead of getting nothing for the turn.
    public static List<HTNStackFrame> Decompose(HTNCompoundTask compoundTask, AIContext ctx, AIBlackboard bb)
    {
        if (compoundTask == null) return new List<HTNStackFrame>();

        foreach (HTNMethod candidate in compoundTask.Methods)
        {
            if (candidate == null) continue;
            if (candidate.Precondition == null || !candidate.Precondition(ctx, bb)) continue;
            if (candidate.Subtasks.Count == 0) continue;

            HTNStackFrame ownFrame = new() { MethodTaskId = candidate.TaskId, SubtaskIndex = 0 };
            IHTNNode firstSubtask = candidate.Subtasks[0];

            if (firstSubtask is HTNPrimitiveTask primitive)
            {
                if (!string.IsNullOrEmpty(primitive.AdvisorName)
                    && Enum.TryParse(primitive.AdvisorName, true, out AdvisorType advisor)
                    && !ctx.HasEligibleCard(advisor))
                {
                    continue; // no eligible card for this leaf's advisor — try the next sibling
                }

                return new List<HTNStackFrame> { ownFrame };
            }

            if (firstSubtask is HTNCompoundTask nested)
            {
                List<HTNStackFrame> tail = Decompose(nested, ctx, bb);
                if (tail.Count == 0) continue; // nothing eligible anywhere in this subtree — try next sibling

                List<HTNStackFrame> frames = new() { ownFrame };
                frames.AddRange(tail);
                return frames;
            }

            // Neither a primitive nor a compound (malformed) — same tolerance the old walk had:
            // treat this method's own frame as the terminal result rather than failing outright.
            return new List<HTNStackFrame> { ownFrame };
        }

        return new List<HTNStackFrame>(); // total failure — no candidate (at any depth) succeeded
    }

    public static HTNPrimitiveTask ResolveActivePrimitive(List<HTNStackFrame> stack, HTNCompoundTask root)
    {
        if (stack == null || stack.Count == 0 || root == null) return null;
        return ResolvePrimitive(stack, BuildIndex(root));
    }

    public static List<HTNStackFrame> AdvanceStack(List<HTNStackFrame> stack, HTNCompoundTask root, AIContext ctx, AIBlackboard bb)
    {
        if (root == null) return new List<HTNStackFrame>();
        if (stack == null || stack.Count == 0) return Decompose(root, ctx, bb);

        Index index = BuildIndex(root);

        // Step 1: priority-interrupt scan, outer frame to inner frame.
        for (int i = 0; i < stack.Count; i++)
        {
            if (!index.MethodsById.TryGetValue(stack[i].MethodTaskId, out HTNMethod method)
                || !index.OwnerByMethodId.TryGetValue(stack[i].MethodTaskId, out HTNCompoundTask owner))
            {
                return Decompose(root, ctx, bb); // stale id (e.g. after a widget Save/Reload) — safety net
            }

            foreach (HTNMethod sibling in owner.Methods)
            {
                if (sibling == method) break; // reached the currently-chosen method; nothing after it outranks it
                if (sibling.Precondition != null && sibling.Precondition(ctx, bb))
                {
                    return ReplaceFrom(stack, i, owner, ctx, bb);
                }
            }
        }

        // Step 2: failure check on the innermost active primitive's own precondition.
        HTNPrimitiveTask primitive = ResolvePrimitive(stack, index);
        if (primitive == null) return Decompose(root, ctx, bb);
        if (primitive.Precondition != null && !primitive.Precondition(ctx, bb))
        {
            int i = stack.Count - 1;
            index.OwnerByMethodId.TryGetValue(stack[i].MethodTaskId, out HTNCompoundTask owner);
            return ReplaceFrom(stack, i, owner, ctx, bb);
        }

        // Step 3: completion-driven advancement (loops/cascades internally). Step 4 ("no
        // change") is simply Advance returning the stack unmodified when nothing completed.
        return Advance(stack, index, root, ctx, bb);
    }

    private static List<HTNStackFrame> ReplaceFrom(List<HTNStackFrame> stack, int i, HTNCompoundTask owner, AIContext ctx, AIBlackboard bb)
    {
        List<HTNStackFrame> head = stack.Take(i).ToList();
        if (owner == null) return head;
        List<HTNStackFrame> tail = Decompose(owner, ctx, bb);
        head.AddRange(tail);
        return head;
    }

    private static List<HTNStackFrame> Advance(List<HTNStackFrame> stack, Index index, HTNCompoundTask root, AIContext ctx, AIBlackboard bb)
    {
        List<HTNStackFrame> current = new(stack);

        while (true)
        {
            HTNPrimitiveTask primitive = ResolvePrimitive(current, index);
            if (primitive == null) return Decompose(root, ctx, bb);
            if (primitive.CompletionCondition == null || !primitive.CompletionCondition(ctx, bb))
            {
                return current; // not complete — nothing more to do this turn
            }

            // Pop from the innermost frame outward until we find a method with a next subtask.
            int i = current.Count - 1;
            bool advanced = false;
            while (i >= 0)
            {
                if (!index.MethodsById.TryGetValue(current[i].MethodTaskId, out HTNMethod method))
                {
                    return Decompose(root, ctx, bb); // stale id — safety net
                }

                int nextIndex = current[i].SubtaskIndex + 1;
                if (nextIndex < method.Subtasks.Count)
                {
                    current = current.Take(i).ToList();
                    current.Add(new HTNStackFrame { MethodTaskId = method.TaskId, SubtaskIndex = nextIndex });
                    advanced = true;
                    break;
                }

                i--; // this method's sequence is exhausted — try the parent frame
            }

            if (!advanced)
            {
                return Decompose(root, ctx, bb); // whole strategy's sequence exhausted — start over
            }

            HTNStackFrame lastFrame = current[^1];
            index.MethodsById.TryGetValue(lastFrame.MethodTaskId, out HTNMethod lastMethod);
            IHTNNode lastNode = lastMethod.Subtasks[lastFrame.SubtaskIndex];
            if (lastNode is HTNCompoundTask compound)
            {
                List<HTNStackFrame> pushed = Decompose(compound, ctx, bb);
                if (pushed.Count == 0) return Decompose(root, ctx, bb); // nothing matched — safety net
                current.AddRange(pushed);
            }

            // Loop again: the new innermost primitive might already be complete too (cascading).
        }
    }

    private static HTNPrimitiveTask ResolvePrimitive(List<HTNStackFrame> stack, Index index)
    {
        if (stack == null || stack.Count == 0) return null;
        HTNStackFrame last = stack[^1];
        if (!index.MethodsById.TryGetValue(last.MethodTaskId, out HTNMethod method)) return null;
        if (last.SubtaskIndex < 0 || last.SubtaskIndex >= method.Subtasks.Count) return null;
        return method.Subtasks[last.SubtaskIndex] as HTNPrimitiveTask;
    }

    private class Index
    {
        public readonly Dictionary<string, HTNMethod> MethodsById = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, HTNCompoundTask> OwnerByMethodId = new(StringComparer.OrdinalIgnoreCase);
    }

    private static Index BuildIndex(HTNCompoundTask root)
    {
        Index index = new();
        void Visit(HTNCompoundTask compound)
        {
            if (compound == null) return;
            foreach (HTNMethod method in compound.Methods)
            {
                if (method == null || string.IsNullOrWhiteSpace(method.TaskId)) continue;
                index.MethodsById[method.TaskId] = method;
                index.OwnerByMethodId[method.TaskId] = compound;
                foreach (IHTNNode subtask in method.Subtasks)
                {
                    if (subtask is HTNCompoundTask nested) Visit(nested);
                }
            }
        }
        Visit(root);
        return index;
    }
}
