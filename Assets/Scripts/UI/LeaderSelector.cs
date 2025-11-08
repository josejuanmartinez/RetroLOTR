using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Video;
using System.Linq;

public class LeaderSelector : MonoBehaviour
{
    public VideoPlayer introVideo;
    public VideoPlayer leaderVideo;
    public TypewriterEffect typewriterEffect;
    public TextMeshProUGUI textUI;
    public GameObject progress;
    public GameObject progressText;

    Videos videos;
    List<TMP_Dropdown.OptionData> options;
    TMP_Dropdown dropdown;

    List<string> loadedLeaders = new();
    bool loadedFirst = false;
    void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        options = dropdown.options;
        videos = FindFirstObjectByType<Videos>();
    }

    void Update()
    {
        List<PlayableLeader> playableLeaders = FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None).ToList();
        if (loadedLeaders.Count < playableLeaders.Count)
        {
            for(int i=0; i<playableLeaders.Count; i++)
            {
                string leaderName = playableLeaders[i].characterName;
                if (loadedLeaders.Contains(leaderName)) continue;
                string alignment = playableLeaders[i].alignment.ToString();
                options.Add(new TMP_Dropdown.OptionData(leaderName, FindFirstObjectByType<Illustrations>().GetIllustrationByName(alignment), Color.white));
                loadedLeaders.Add(leaderName);

                if (!loadedFirst)
                {
                    loadedFirst = true;
                    dropdown.value = 0;
                    dropdown.RefreshShownValue();
                    SelectLeader(0);
                    introVideo.gameObject.SetActive(false);
                    progress.SetActive(false);
                    progressText.SetActive(false);
                }
            }
        }
    }

    public void SelectLeader(int value)
    {
        if (options.Count > value)
        {
            string leaderName = options[value].text;
            string leaderDescription = FindAnyObjectByType<PlayableLeaders>().playableLeaders.biomes.Find(x => x.characterName.ToLower() == leaderName.ToLower()).description;

            leaderVideo.clip = videos.GetVideoByName(leaderName);
    
            if (typewriterEffect) typewriterEffect.StartWriting(leaderDescription); else textUI.text = leaderDescription;

            PlayableLeader player = FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None).ToList().Find((x) => x.characterName.ToLower() == leaderName.ToLower());
            FindFirstObjectByType<Game>().SelectPlayer(player);
        }
    }
}