using UnityEngine;
using System.Collections.Generic;

public class Sounds : SearcherByName
{
    public List<AudioClip> sounds;
    public AudioSource soundAudioSource;

    public AudioClip GetSoundByName(string name)
    {
        AudioClip sound = sounds.Find(x => Normalize(x.name) == Normalize(name));
        if (!sound) Debug.LogWarning($"Sprite for {name} is not registered. Typo? Forgot to add it?");
        return sound;
    }

    public void StopAllSounds()
    {
        soundAudioSource.Stop();
    }
}
