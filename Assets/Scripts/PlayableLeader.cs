
using System;
using System.Collections.Generic;
using System.Linq;

public class PlayableLeader : Leader
{
    public VictoryPoints victoryPoints;
    private readonly HashSet<string> playedLandCards = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> playedPcCards = new(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeCardName(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName)) return string.Empty;
        return new string(cardName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    new public void Initialize(Hex hex, LeaderBiomeConfig playableLeaderBiome, bool showSpawnMessage = true)
    {
        base.Initialize(hex, playableLeaderBiome, showSpawnMessage);
        victoryPoints = null;
        playedLandCards.Clear();
        playedPcCards.Clear();
    }

    public void RecordPlayedCard(CardData card)
    {
        if (card == null) return;

        switch (card.GetCardType())
        {
            case CardTypeEnum.Land:
            {
                string normalizedLand = NormalizeCardName(card.name);
                if (!string.IsNullOrEmpty(normalizedLand)) playedLandCards.Add(normalizedLand);
                break;
            }
            case CardTypeEnum.PC:
            {
                string normalizedPc = NormalizeCardName(card.name);
                if (!string.IsNullOrEmpty(normalizedPc)) playedPcCards.Add(normalizedPc);
                break;
            }
        }
    }

    public bool HasPlayedLandCardForRegion(string region)
    {
        string normalizedRegion = NormalizeCardName(region);
        return !string.IsNullOrEmpty(normalizedRegion) && playedLandCards.Contains(normalizedRegion);
    }

    public bool HasPlayedPcCard(string pcName)
    {
        string normalizedPc = NormalizeCardName(pcName);
        return !string.IsNullOrEmpty(normalizedPc) && playedPcCards.Contains(normalizedPc);
    }

    override public void Killed(Leader killedBy, bool onlyMask = false)
    {
        if (killed) return;

        FindFirstObjectByType<PlayableLeaderIcons>().AddDeadIcon(this);

        health = 0;
        killed = true;

        if (FindFirstObjectByType<Game>().player == this)
        {
            FindFirstObjectByType<Game>().EndGame(false);
            return;
        }

        FindFirstObjectByType<Game>().competitors.Remove(this);

        base.Killed(killedBy);
    }
    new public void NewTurn()
    {

        FindFirstObjectByType<PlayableLeaderIcons>().HighlightCurrentlyPlaying(this);

        base.NewTurn();
    }

}
