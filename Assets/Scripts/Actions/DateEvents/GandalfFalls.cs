using UnityEngine;

/// <summary>
/// 15 Afteryule 3019 - the Bridge of Khazad-dum. Gandalf the Grey falls with the Balrog.
/// Template: announces the fall. Hook real logic here (remove/weaken the Gandalf
/// character, apply a morale shock to the free peoples, etc.).
/// </summary>
public class GandalfFalls : DateEvent
{
    public override void Run(DateEventContext ctx)
    {
        Announce("\"Fly, you fools!\" Gandalf falls into shadow at Khazad-dum.", new Color(0.85f, 0.55f, 0.3f));
    }
}
