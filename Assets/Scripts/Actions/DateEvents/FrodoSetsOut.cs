using UnityEngine;

/// <summary>
/// 23 Halimath 3018 - the campaign's opening beat. Frodo leaves Bag End.
/// Template: announces the departure. Hook real logic here (spawn the Ring-bearer,
/// start a clock, reveal a quest objective, etc.).
/// </summary>
public class FrodoSetsOut : DateEvent
{
    public override void Run(DateEventContext ctx)
    {
        Announce("The Ring-bearer sets out from Bag End. The road goes ever on.", new Color(0.6f, 0.8f, 0.5f));
    }
}
