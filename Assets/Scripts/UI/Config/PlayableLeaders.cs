using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayableLeaders : MonoBehaviour
{
    public List<PlayableLeader> playableLeaders;

    public void Initialize()
    {
        playableLeaders = transform.GetComponentsInChildren<PlayableLeader>().ToList();
    }
}
