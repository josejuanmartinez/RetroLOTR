using TMPro;
using UnityEngine;

public class DateManager : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI textWidget;

    private Game game;

    private static readonly string[] Months =
    {
        "Afteryule", "Solmath", "Rethe", "Astron", "Thrimidge", "Forelithe",
        "Afterlithe", "Wedmath", "Halimath", "Winterfilth", "Blotmath", "Foreyule"
    };

    private const int DaysPerMonth = 30;
    private const int StartYear = 3018; // Third Age starting year for the campaign.
    private const string EraSuffix = "T.A.";

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
        var date = GetDateFromTurn(turnNumber);
        textWidget.text = $"{date.Day} {date.Month} {date.Year} {EraSuffix}";
    }

    private static MiddleEarthDate GetDateFromTurn(int turnNumber)
    {
        var dayIndex = Mathf.Max(turnNumber - 1, 0);
        var daysInYear = Months.Length * DaysPerMonth;

        var yearOffset = dayIndex / daysInYear;
        var dayOfYear = dayIndex % daysInYear;
        var monthIndex = dayOfYear / DaysPerMonth;
        var day = (dayOfYear % DaysPerMonth) + 1;

        return new MiddleEarthDate(day, Months[monthIndex], StartYear + yearOffset);
    }

    private readonly struct MiddleEarthDate
    {
        public readonly int Day;
        public readonly string Month;
        public readonly int Year;

        public MiddleEarthDate(int day, string month, int year)
        {
            Day = day;
            Month = month;
            Year = year;
        }
    }
}
