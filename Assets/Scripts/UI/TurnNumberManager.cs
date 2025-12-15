using TMPro;
using UnityEngine;

public class TurnNumberManager : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI textWidget;

    private Game game;

    private void OnEnable()
    {
        game = FindFirstObjectByType<Game>();
        if (game != null)
        {
            game.NewTurnStarted += Show;
            Show(game.turn);
        }
    }

    private void OnDisable()
    {
        if (game != null)
        {
            game.NewTurnStarted -= Show;
        }
    }

    public void Show(int turnNumber)
    {
        textWidget.text = $"Turn {turnNumber}";
    }
}
