using UnityEngine;

public class NonPlayableLeaders : MonoBehaviour
{
    public NonPlayableLeaderBiomeConfigCollection nonPlayableLeaders;

    public void Initialize()
    {
        // Non PLayable Leaders
        TextAsset jsonFile = Resources.Load<TextAsset>("NonPlayableLeaderBiomes");
        nonPlayableLeaders = JsonUtility.FromJson<NonPlayableLeaderBiomeConfigCollection>(jsonFile.text);
    }
}
