using UnityEngine;
using UnityEngine.Video;

public class IntroVideoManager : MonoBehaviour
{
    private BoardGenerator boardGenerator;
    private VideoPlayer vp;

    void Start()    
    {
        boardGenerator = GameObject.Find("Board").GetComponent<BoardGenerator>();
        vp = GetComponent<VideoPlayer>();
        vp.clip = GameObject.Find("Videos").GetComponent<Videos>().intro;
        vp.loopPointReached += OnVideoFinished;     // fires at end (and on each loop)
        vp.errorReceived += OnVideoError;
        vp.started += OnVideoStarted;
    }

    void OnVideoStarted(VideoPlayer p) { boardGenerator.SetVideoPlaying(true); }
    void OnVideoFinished(VideoPlayer p) { boardGenerator.SetVideoPlaying(false); }
    void OnVideoError(VideoPlayer p, string msg) { boardGenerator.SetVideoPlaying(false); }
}
