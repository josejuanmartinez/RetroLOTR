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

    // Add a unique identifier for each artifact
    public string artifactId;

    public string GetText()
    {
        return $"{artifactName}( {artifactDescription} )";
    }
}