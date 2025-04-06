using UnityEngine;

public class PlayableLeaders : MonoBehaviour
{
    public LeaderBiomeConfigCollection playableLeaders;

    public void Initialize()
    {
        // Playable Leaders
        TextAsset jsonFile = Resources.Load<TextAsset>("PlayableLeaderBiomes");
        playableLeaders = JsonUtility.FromJson<LeaderBiomeConfigCollection>(jsonFile.text);
    }
}
