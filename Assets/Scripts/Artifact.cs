using System;
using System.Collections.Generic;

[Serializable]
public class ArtifactCollection
{
	public List<Artifact> artifacts = new ();
}


[Serializable]
public class Artifact
{
    public string artifactName;
    public string artifactDescription;
    public bool hidden = false; 
    public string providesSpell = "";
    public int commanderBonus = 0;
    public int agentBonus = 0;
    public int emmissaryBonus = 0;
    public int mageBonus = 0;
    public int bonusAttack = 0;
    public int bonusDefense = 0;
    public bool oneShot = false;
    public bool transferable = true;
    public string spriteString = "";

    
    public string GetSpriteString()
    {
        return spriteString != "" ? spriteString : "artifact";
    }

    public string GetHoverText()
    {
        List<string> sb = new() {$"<sprite name=\"{GetSpriteString()}\"><u>{artifactName}</u>"};
        if (artifactDescription.Trim().Length > 0) sb.Add($"<br>{artifactDescription}");  
        
        List<string> sbDetails = new();
        if(providesSpell.Trim().Length>0) sbDetails.Add($"Enables <i>{providesSpell}</i> action");
        if(commanderBonus>0) sbDetails.Add($"+{commanderBonus}<sprite name=\"commander\">");
        if(agentBonus>0) sbDetails.Add($"+{agentBonus}<sprite name=\"agent\">");
        if(emmissaryBonus>0) sbDetails.Add($"+{emmissaryBonus}<sprite name=\"emmissary\">");
        if(mageBonus>0) sbDetails.Add($"+{mageBonus}<sprite name=\"mage\">");
        if(bonusAttack>0) sbDetails.Add($"+{bonusAttack} to attack");
        if(bonusDefense>0) sbDetails.Add($"+{bonusDefense} to defense");
        if(oneShot) sbDetails.Add("consumable");
        if(!transferable) sbDetails.Add("non-transferable");
        string sbDetailStr = string.Join(", ", sbDetails);
        if(sbDetailStr.Length > 0) sb.Add($"<br>{sbDetailStr}");
        return string.Join("", sb);
    }
}
