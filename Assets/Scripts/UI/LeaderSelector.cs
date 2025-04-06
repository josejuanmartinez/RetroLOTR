using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Video;
using System.Linq;

public class LeaderSelector : MonoBehaviour
{
    public VideoPlayer introVideoPlayer;
    public VideoPlayer leaderVideo;
    public TypewriterEffect typewriterEffect;
    public TextMeshProUGUI textUI;
    
    
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
                options.Add(new TMP_Dropdown.OptionData(leaderName, FindFirstObjectByType<IllustrationsSmall>().GetIllustrationByName(alignment), Color.white));
                loadedLeaders.Add(leaderName);

                if (!loadedFirst)
                {
                    loadedFirst = true;
                    dropdown.value = 0;
                    dropdown.RefreshShownValue();
                    SelectLeader(0);
                    introVideoPlayer.GetComponent<Canvas>().sortingOrder = 0;
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

            System.Reflection.FieldInfo[] videosFields = videos.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in videosFields)
            {
                if (string.Equals(field.Name, leaderName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (field.FieldType == typeof(VideoClip))
                    {
                        leaderVideo.clip = (VideoClip) field.GetValue(videos);
                        break;
                    }
                }
            }

            if (typewriterEffect) typewriterEffect.StartWriting(leaderDescription); else textUI.text = leaderDescription;

            PlayableLeader player = FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None).ToList().Find((x) => x.characterName.ToLower() == leaderName.ToLower());
            FindFirstObjectByType<Game>().SelectPlayer(player);
        }
    }
}