using UnityEngine;

/// <summary>
/// 25 Rethe 3019 - the Ring goes into the Fire and Barad-dur falls. The campaign's climax.
/// Template: announces the downfall. Hook real logic here (trigger victory/defeat,
/// collapse the Dark Servants, end the game, etc.).
/// </summary>
public class TheRingIsDestroyed : DateEvent
{
    public override void Run(DateEventContext ctx)
    {
        Announce("The One Ring is unmade. Barad-dur falls and the Shadow passes from the world.", new Color(1f, 0.9f, 0.4f));
    }
}
