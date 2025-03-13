using System.Collections.Generic;
using UnityEngine;

public class Initialize : MonoBehaviour
{
    public List<GameObject> deactivate;
    public List<GameObject> activate;
    public List<MonoBehaviour> disable;
    public List<MonoBehaviour> enable;

    void Awake()
    {
        deactivate.ForEach(x => x.SetActive(false));
        activate.ForEach(x => x.SetActive(true));
        disable.ForEach(x => x.enabled = false);
        enable.ForEach(x => x.enabled = true);
    }
}
