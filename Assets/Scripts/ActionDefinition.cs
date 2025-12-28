using System;
using System.Collections.Generic;

[Serializable]
public class ActionDefinition
{
    public string className;
    public string actionName;
    public string iconName;
    public string description;
    public int actionId;
    public int difficulty;
    public int commanderSkillRequired;
    public int agentSkillRequired;
    public int emissarySkillRequired;
    public int mageSkillRequired;

    public int leatherCost;
    public int mountsCost;
    public int timberCost;
    public int ironCost;
    public int steelCost;
    public int mithrilCost;
    public int goldCost;
    public bool isBuyCaravans;
    public bool isSellCaravans;
    public bool isOffensive;

    public int commanderXP;
    public int agentXP;
    public int emmissaryXP;
    public int mageXP;

    public int reward;
    public AdvisorType advisorType;
}

[Serializable]
public class ActionDefinitionCollection
{
    public List<ActionDefinition> actions = new();
}
