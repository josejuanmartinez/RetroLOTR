using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
public class LeaderSelector : MonoBehaviour
{
    public Image leaderPicture;
    public TypewriterEffect typewriterEffect;
    public TextMeshProUGUI textUI;
    public Illustrations illustrations;
    public TextsEN textsEN;

    private List<TMP_Dropdown.OptionData> options;
    void Awake()
    {
        options = GetComponent<TMP_Dropdown>().options;
        illustrations = FindFirstObjectByType<Illustrations>();
        textsEN = FindFirstObjectByType<TextsEN>();
    }

    public void SelectLeader(int value)
    {
        if (options.Count > value)
        {
            string leaderName = options[value].text;

            // Get leader sprite using reflection
            System.Reflection.FieldInfo[] illustrationFields = illustrations.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in illustrationFields)
            {
                if (string.Equals(field.Name, leaderName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (field.FieldType == typeof(Sprite))
                    {
                        Sprite leaderSprite = (Sprite)field.GetValue(illustrations);
                        leaderPicture.sprite = leaderSprite;
                        break;
                    }
                }
            }

            // Get leader text using reflection in the same way
            System.Reflection.FieldInfo[] textFields = textsEN.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in textFields)
            {
                if (string.Equals(field.Name, leaderName + "Text", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field.Name, leaderName + "Description", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field.Name, leaderName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (field.FieldType == typeof(string))
                    {
                        string leaderText = (string)field.GetValue(textsEN);
                        if (typewriterEffect)
                        {
                            typewriterEffect.StartWriting(leaderText);
                        } else
                        {
                            textUI.text = leaderText;
                        }
                        break;
                    }
                }
            }

            PlayableLeader player = FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None).ToList().Find((x) => x.characterName.ToLower() == leaderName.ToLower());
            FindFirstObjectByType<Game>().SelectPlayer(player);
        }
    }
}