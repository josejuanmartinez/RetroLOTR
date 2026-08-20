using System.Linq;
using UnityEngine;
using UnityEngine.Video;

[System.Serializable]
public class SkinVideo
{
    public Skins skin;
    public VideoClip clip;
}

public class Videos : SearcherByName
{
    public SkinVideo[] skinVideos;

    public VideoClip GetClipForSkin(Skins skin)
    {
        return skinVideos.FirstOrDefault(entry => entry.skin == skin)?.clip;
    }
}
