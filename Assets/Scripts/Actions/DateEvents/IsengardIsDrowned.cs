using UnityEngine;

/// <summary>
/// 3 Rethe 3019 - the Ents break the dam and flood Isengard.
/// Template: announces the fall of Isengard. Hook real logic here (cripple Saruman's
/// faction, destroy his armies/production, flip allegiances, etc.).
/// </summary>
public class IsengardIsDrowned : DateEvent
{
    public override void Run(DateEventContext ctx)
    {
        Announce("The Ents loose the Isen. Isengard is drowned and Saruman's war is undone.", new Color(0.55f, 0.75f, 0.85f));
    }
}
