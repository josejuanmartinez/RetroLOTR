using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class Initialize : MonoBehaviour
{
    public List<GameObject> deactivate;
    public List<GameObject> activate;
    public List<MonoBehaviour> disable;
    public List<MonoBehaviour> enable;

    void Awake()
    {
        deactivate.FindAll(x => x != null).ForEach(x => x.SetActive(false));
        activate.FindAll(x => x != null).ForEach(x => x.SetActive(true));
        disable.FindAll(x => x != null).ForEach(x => x.enabled = false);
        enable.FindAll(x => x != null).ForEach(x => x.enabled = true);
    }
}
