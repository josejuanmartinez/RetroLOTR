using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum BehaviourTreeStatus
{
    Success,
    Failure,
    Running
}

public interface IBehaviourNode
{
    Task<BehaviourTreeStatus> Tick(AIContext context);
}

public class SelectorNode : IBehaviourNode
{
    private readonly List<IBehaviourNode> children;

    public SelectorNode(params IBehaviourNode[] nodes)
    {
        children = new List<IBehaviourNode>(nodes);
    }

    public async Task<BehaviourTreeStatus> Tick(AIContext context)
    {
        foreach (IBehaviourNode child in children)
        {
            BehaviourTreeStatus status = await child.Tick(context);
            if (status != BehaviourTreeStatus.Failure) return status;
        }

        return BehaviourTreeStatus.Failure;
    }
}

public class SequenceNode : IBehaviourNode
{
    private readonly List<IBehaviourNode> children;

    public SequenceNode(params IBehaviourNode[] nodes)
    {
        children = new List<IBehaviourNode>(nodes);
    }

    public async Task<BehaviourTreeStatus> Tick(AIContext context)
    {
        foreach (IBehaviourNode child in children)
        {
            BehaviourTreeStatus status = await child.Tick(context);
            if (status != BehaviourTreeStatus.Success) return status;
        }

        return BehaviourTreeStatus.Success;
    }
}

public class ConditionNode : IBehaviourNode
{
    private readonly Func<AIContext, bool> predicate;

    public ConditionNode(Func<AIContext, bool> predicate)
    {
        this.predicate = predicate;
    }

    public Task<BehaviourTreeStatus> Tick(AIContext context)
    {
        bool result = predicate?.Invoke(context) ?? false;
        return Task.FromResult(result ? BehaviourTreeStatus.Success : BehaviourTreeStatus.Failure);
    }
}

public class ActionNode : IBehaviourNode
{
    private readonly Func<AIContext, Task<bool>> action;

    public ActionNode(Func<AIContext, Task<bool>> action)
    {
        this.action = action;
    }

    public async Task<BehaviourTreeStatus> Tick(AIContext context)
    {
        bool result = await action.Invoke(context);
        return result ? BehaviourTreeStatus.Success : BehaviourTreeStatus.Failure;
    }
}

public static class AIBehaviourTreeBuilder
{
    public static IBehaviourNode BuildDefault()
    {
        // Priority: keep the realm solvent -> attack nearest enemies -> pick any reasonable action -> pass
        return new SelectorNode(
            new SequenceNode(
                new ConditionNode(ctx => ctx.NeedsEconomicHelp),
                new ActionNode(ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Economic))
            ),
            new SequenceNode(
                new ConditionNode(ctx => ctx.HasEnemyTarget),
                new SelectorNode(
                    new ActionNode(ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Militaristic)),
                    new ActionNode(ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Intelligence)),
                    new ActionNode(ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Magic)),
                    new ActionNode(ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Diplomatic))
                )
            ),
            new SequenceNode(
                new ConditionNode(ctx => ctx.ShouldPrioritizeMovement),
                new ActionNode(ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Movement))
            ),
            new SelectorNode(
                new ActionNode(ctx => ctx.TryExecuteBestAvailableActionAsync()),
                new ActionNode(ctx => ctx.PassAsync())
            )
        );
    }
}
