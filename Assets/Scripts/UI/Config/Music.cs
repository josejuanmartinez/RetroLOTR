using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class Music : MonoBehaviour
{
    public List<AudioClip> musicTracks;
    public AudioSource musicAudioSource;

    private int currentTrackIndex = 0;

    private void Start()
    {
        if (musicTracks != null && musicTracks.Count > 0)
        {
            Play();
        }
        else
        {
            Debug.LogWarning("No music tracks assigned!");
        }
    }

    public void Play()
    {
        // Stop any currently running playback coroutine first
        StopAllCoroutines();
        StartCoroutine(PlayMusicLoop());
    }

    private IEnumerator PlayMusicLoop()
    {
        while (true)
        {
            if (musicTracks.Count == 0) yield break;

            // Set clip and play
            musicAudioSource.clip = musicTracks[currentTrackIndex];
            musicAudioSource.Play();

            // Wait until the clip finishes playing
            yield return new WaitForSeconds(musicAudioSource.clip.length);

            // Advance to next track (loop back to start)
            currentTrackIndex = (currentTrackIndex + 1) % musicTracks.Count;
        }
    }
}
