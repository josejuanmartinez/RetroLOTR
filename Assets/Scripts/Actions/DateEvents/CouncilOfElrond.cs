using UnityEngine;

/// <summary>
/// 25 Winterfilth 3018 - the Council of Elrond. The Fellowship is formed.
/// Template: announces the council. Hook real logic here (grant the player the
/// Fellowship party, unlock cards, set alliances, etc.).
/// </summary>
public class CouncilOfElrond : DateEvent
{
    public override void Run(DateEventContext ctx)
    {
        Announce("The Council of Elrond is met. The Fellowship of the Ring is chosen.", new Color(0.7f, 0.85f, 0.95f));
    }
}
