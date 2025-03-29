using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BiomeConfig
{
    [Header("Alignment")]
    public AlignmentEnum alignment;

    [Header("Character stats")]
    public int commander = 0;
    public int agent = 0;
    public int emmissary = 0;
    public int mage = 0;

    [Header("Artifacts")]
    public List<Artifact> artifacts;
}
