using UnityEngine;

/// <summary>
/// 25 Rethe 3019 - with the Ring unmade, the Dark Tower crumbles and Sauron ends.
/// Template: announces the fall. Hook real logic here (collapse the Dark Servants,
/// trigger the Free Peoples' victory / Dark defeat, end the game, etc.).
/// </summary>
public class BaradDurFalls : DateEvent
{
    public override void Run(DateEventContext ctx)
    {
        Announce("Barad-dur falls. The Dark Tower is thrown down and Sauron is no more.", new Color(0.7f, 0.2f, 0.15f));
    }
}
