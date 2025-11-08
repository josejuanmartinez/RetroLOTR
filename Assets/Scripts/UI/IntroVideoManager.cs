using UnityEngine;
using UnityEngine.Video;

public class IntroVideoManager : MonoBehaviour
{
    public bool lightVersion = true;
    private BoardGenerator boardGenerator;
    private VideoPlayer vp;

    void Start()    
    {
        boardGenerator = GameObject.Find("Board").GetComponent<BoardGenerator>();
        vp = GetComponent<VideoPlayer>();
        vp.clip = GameObject.Find("Videos").GetComponent<Videos>().GetVideoByName(lightVersion? "intro_light" : "intro_dark");
        vp.loopPointReached += OnVideoFinished;     // fires at end (and on each loop)
        vp.errorReceived += OnVideoError;
        vp.started += OnVideoStarted;
    }

    void OnVideoStarted(VideoPlayer p) { boardGenerator.SetVideoPlaying(true); }
    void OnVideoFinished(VideoPlayer p) { boardGenerator.SetVideoPlaying(false); }
    void OnVideoError(VideoPlayer p, string msg) { boardGenerator.SetVideoPlaying(false); }
}
