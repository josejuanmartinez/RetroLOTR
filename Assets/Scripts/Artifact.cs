using System;

[Serializable]
public class Artifact
{
    public string artifactName;
    public string artifactDescription;
    public bool hidden = false; 
    public string providesSpell;
    public int commanderBonus = 0;
    public int agentBonus = 0;
    public int emmissaryBonus = 0;
    public int mageBonus = 0;
    public int bonusAttack = 0;
    public int bonusDefense = 0;
    public bool oneShot = false;
    public bool transferable = true;

    public string GetText()
    {
        return $"{artifactName}( {artifactDescription} )";
    }
}