using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Video;

public class Videos : SearcherByName
{
    public List<VideoClip> videos;

    public VideoClip GetVideoByName(string name)
    {
        VideoClip video = videos.Find(x => Normalize(x.name) == Normalize(name));
        if (!video) Debug.LogWarning($"Video for {name} is not registered. Typo? Forgot to add it?");
        return video;
    }
}
