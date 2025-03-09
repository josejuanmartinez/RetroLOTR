using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NonPlayableLeaders : MonoBehaviour
{
    public List<NonPlayableLeader> nonPlayableLeaders;

    public void Initialize()
    {
        nonPlayableLeaders = transform.GetComponentsInChildren<NonPlayableLeader>().ToList();
    }
}
