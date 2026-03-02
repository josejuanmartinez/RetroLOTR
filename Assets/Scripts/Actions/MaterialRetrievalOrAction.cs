using System;
using UnityEngine;
public class MaterialRetrievalOrAction : MaterialRetrieval
{
    protected bool GrantResources(Character c, ProducesEnum first, int firstAmount, ProducesEnum second, int secondAmount, string sourceName)
    {
        if (c == null) return false;
        Leader owner = c.GetOwner();
        if (owner == null) return false;
        AddResource(owner, first, firstAmount);
        AddResource(owner, second, secondAmount);
        string firstText = $"+{firstAmount} {first}";
        string secondText = $"+{secondAmount} {second}";
        string label = string.IsNullOrWhiteSpace(sourceName) ? "PC" : sourceName;
        MessageDisplayNoUI.ShowMessage(c.hex, c, $"{label}: {firstText}, {secondText}", Color.yellow);
        return true;
    }
    private static void AddResource(Leader owner, ProducesEnum resource, int amount)
    {
        if (owner == null || amount <= 0) return;
        switch (resource)
        {
            case ProducesEnum.leather:
                owner.AddLeather(amount);
                break;
            case ProducesEnum.mounts:
                owner.AddMounts(amount);
                break;
            case ProducesEnum.timber:
                owner.AddTimber(amount);
                break;
            case ProducesEnum.iron:
                owner.AddIron(amount);
                break;
            case ProducesEnum.steel:
                owner.AddSteel(amount);
                break;
            case ProducesEnum.mithril:
                owner.AddMithril(amount);
                break;
        }
    }
}
