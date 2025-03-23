using UnityEngine;


public class Artifact : MonoBehaviour
{
    public string artifactName;
    public string artifactDescription;
    public bool hidden = false;
    public Spell providesSpell = null;

    public int commanderBonus = 0;
    public int agentBonus = 0;
    public int emmissaryBonus = 0;
    public int mageBonus = 0;

    public string GetText()
    {
        return $"{artifactName}( {artifactDescription} )";
    }
}
