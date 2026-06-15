using UnityEngine;

/// <summary>
/// 25 Halimath 3018 - Sauron looses the Nine into Eriador to hunt the Ring.
/// Template: announces the hunt. Hook real logic here (spawn/empower the Nazgul,
/// raise the Dark Servants' aggression, reveal the player to the enemy, etc.).
/// </summary>
public class TheHuntForTheRing : DateEvent
{
    public override void Run(DateEventContext ctx)
    {
        Announce("The Nine ride out. Sauron looses the Ringwraiths to hunt the Ring across Eriador.", new Color(0.8f, 0.25f, 0.2f));
    }
}
