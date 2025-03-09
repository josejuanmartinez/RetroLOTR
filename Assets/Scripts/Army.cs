using System;
using UnityEngine;

[Serializable]
public class Army
{
    [SerializeField] public Character commander;

    [SerializeField] public int ma = 0;
    [SerializeField] public int ar = 0;
    [SerializeField] public int li = 0;
    [SerializeField] public int hi = 0;
    [SerializeField] public int lc = 0;
    [SerializeField] public int hc = 0;
    [SerializeField] public int ca = 0;
    [SerializeField] public int tr = 0;
    [SerializeField] public int ws = 0;

    public Army(Character commander, int ma=0, int ar=0, int li=0, int hi=0, int lc=0, int hc=0, int ca=0, int tr=0, int ws=0)
    {
        this.commander = commander;
        this.ma = ma;
        this.ar = ar;
        this.li = li;
        this.hi = hi;
        this.lc = lc;
        this.hc = hc;
        this.ca = ca;
        this.tr = tr;
        this.ws = ws;
    }

    public Army(Character commander, TroopsTypeEnum troopsType, int amount, int ws = 0)
    {
        this.commander = commander;

        // Use reflection to set the field based on the enum value
        string fieldName = troopsType.ToString();

        // Get field info using reflection
        var fieldInfo = GetType().GetField(fieldName);

        if (fieldInfo != null)
        {
            // Set the field value to the amount
            fieldInfo.SetValue(this, amount);
        }
        else
        {
            throw new ArgumentException($"Could not find field for troop type: {troopsType}");
        }

        if (ws > 0) this.ws += ws;
    }

    public AlignmentEnum GetAlignment()
    {
        return commander.GetAlignment();
    }

    public void Recruit(Army otherArmy)
    {
        ma += otherArmy.ma;
        ar += otherArmy.ar;
        li += otherArmy.li;
        hi += otherArmy.hi;
        lc += otherArmy.lc;
        hc += otherArmy.hc;
        ca += otherArmy.ca;
        tr += otherArmy.tr;
        ws += otherArmy.ws;
    }

    public int GetSize()
    {
        return ma + ar + li + hi + lc + hc + ca + tr + ws;
    }

    public string GetHoverText()
    {
        string result = "";

        if (ma > 0) result += $"<b>MA</b>{ma} ";
        if (ar > 0) result += $"<b>AR</b>{ar} ";
        if (li > 0) result += $"<b>LI</b>{li} ";
        if (hi > 0) result += $"<b>HI</b>{hi} ";
        if (lc > 0) result += $"<b>LC</b>{lc} ";
        if (hc > 0) result += $"<b>HC</b>{hc} ";
        if (ca > 0) result += $"<b>CA</b>{ca} ";
        if (ws > 0) result += $"<b>WS</b>{ws} ";

        return result;
    }
}
