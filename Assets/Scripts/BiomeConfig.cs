using System;
using System.Collections.Generic;

[Serializable]
public class BiomeConfig
{
    public string characterName;

    public AlignmentEnum alignment;
    public RacesEnum race;
    public SexEnum sex = SexEnum.Male;

    public int commander = 0;
    public int agent = 0;
    public int emmissary = 0;
    public int mage = 0;

    public List<Artifact> artifacts = new ();
    public int startingArmySize = 0;
    public int startingWarships = 0;
    public TroopsTypeEnum preferedTroopType = TroopsTypeEnum.ma;
}
