using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NonPlayableLeaderBiomeConfig: LeaderBiomeConfig
{
    [Header("Conditions to Join")]
    [Header("Artifacts to Join")]
    public List<Artifact> artifactsToJoin;

    [Header("Stores to Join")]
    public int leatherToJoin = 0;
    public int mountsToJoin = 0;
    public int timberToJoin = 0;
    public int ironToJoin = 0;
    public int mithrilToJoin = 0;
    public int goldToJoin = 0;

    [Header("Character Level to Join")]
    public int commanderLevelToJoin = 0;
    public int agentLevelToJoin = 0;
    public int emmissaryLevelToJoin = 0;
    public int mageLevelToJoin = 0;

    [Header("Armies Size to Join")]
    public int armiesToJoin = 0;
    public int maSizeToJoin = 0;
    public int arSizeToJoin = 0;
    public int liSizeToJoin = 0;
    public int hiSizeToJoin = 0;
    public int lcSizeToJoin = 0;
    public int hcSizeToJoin = 0;
    public int caSizeToJoin = 0;
    public int wsSizeToJoin = 0;

    [Header("Characters to Join")]
    public int commandersToJoin = 0;
    public int agentsToJoin = 0;
    public int emmissarysToJoin = 0;
    public int magesToJoin = 0;

    [Header("Actions to Join")]
    [Header("Actions At Capital to Join")]
    public List<CharacterAction> actionsAtCapital;

    [Header("Actions Anywhere to Join")]
    public List<CharacterAction> actionsAnywhere;
}
