using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class MinimapManager : MonoBehaviour
{
    
    private static MinimapManager instance;

    public Camera minimapCamera;
    private bool refreshing = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if(instance.refreshing) StartCoroutine(UpdateCoroutine());
    }

    private IEnumerator UpdateCoroutine()
    {
        yield return new WaitForEndOfFrame();
        minimapCamera.enabled = true;
        yield return new WaitForEndOfFrame();
        minimapCamera.enabled = false;
        instance.refreshing = false;
    }

     public static void RefreshMinimap()
    {
        instance.refreshing = true;
    }
}
