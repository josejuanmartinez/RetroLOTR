
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayableLeader : Leader
{
    public VictoryPoints victoryPoints;
    private readonly HashSet<string> playedLandCards = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> playedPcCards = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> discoveredRegions = new(StringComparer.OrdinalIgnoreCase);
    private string selectedSubdeckId;
    private string selectedDeckIdentity;
    private string selectedLeaderDescription;
    private string selectedVariantName;
    private string selectedVariantCharacterName;

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
        discoveredRegions.Clear();
        selectedSubdeckId = playableLeaderBiome?.subdeckId;
        selectedDeckIdentity = playableLeaderBiome?.deckIdentity;
        selectedLeaderDescription = playableLeaderBiome?.description;
        selectedVariantName = null;
        RefreshStatsFromCard();
    }

    public void SetDeckSelection(string subdeckId, string deckIdentity = null, string leaderDescription = null, string variantName = null, string variantCharacterName = null)
    {
        selectedSubdeckId = subdeckId;
        selectedDeckIdentity = deckIdentity;
        selectedLeaderDescription = leaderDescription;
        selectedVariantName = variantName;
        selectedVariantCharacterName = variantCharacterName;
    }

    public void RefreshStatsFromCard(string name = null)
    {
        string lookup = string.IsNullOrWhiteSpace(name) ? characterName : name;
        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        CardData card = deckManager?.cards?.Find(c =>
            string.Equals(c.name, lookup, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.type, "Character", StringComparison.OrdinalIgnoreCase));
        ApplyStatsFromCard(card);
    }

    public void ApplyVariantTransformation()
    {
        if (string.IsNullOrWhiteSpace(selectedVariantCharacterName)) return;
        if (string.Equals(characterName, selectedVariantCharacterName, StringComparison.OrdinalIgnoreCase)) return;

        string fromName = characterName;
        characterName = selectedVariantCharacterName;
        RefreshStatsFromCard();
        MessageDisplay.ShowMessage($"{fromName} steps forward as {selectedVariantCharacterName}.", new Color(1f, 0.84f, 0f));
        Sounds.Instance?.PlayArtifactFound();
        CharacterIcons.RefreshForHumanPlayerOf(this);
    }

    public string GetSelectedSubdeckId()
    {
        return string.IsNullOrWhiteSpace(selectedSubdeckId) ? GetBiome()?.subdeckId : selectedSubdeckId;
    }

    public string GetSelectedDeckIdentity()
    {
        return string.IsNullOrWhiteSpace(selectedDeckIdentity) ? GetBiome()?.deckIdentity : selectedDeckIdentity;
    }

    public string GetSelectedLeaderDescription()
    {
        return string.IsNullOrWhiteSpace(selectedLeaderDescription) ? GetBiome()?.description : selectedLeaderDescription;
    }

    public string GetSelectedVariantName()
    {
        return selectedVariantName;
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

    public bool TryDiscoverRegion(string region)
    {
        if (string.IsNullOrWhiteSpace(region)) return false;
        return discoveredRegions.Add(NormalizeCardName(region));
    }

    public bool HasDiscoveredRegion(string region)
    {
        if (string.IsNullOrWhiteSpace(region)) return false;
        return discoveredRegions.Contains(NormalizeCardName(region));
    }

    public bool HasPlayedLandCardThisTurn()
    {
        return HasPlayedLandThisTurn();
    }

    override public void Killed(Leader killedBy, bool onlyMask = false)
    {
        if (killed) return;

        PlayableLeaderIcons leaderIcons = FindFirstObjectByType<PlayableLeaderIcons>();
        leaderIcons?.AddDeadIcon(this);

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
        PlayableLeaderIcons leaderIcons = FindFirstObjectByType<PlayableLeaderIcons>();
        leaderIcons?.HighlightCurrentlyPlaying(this);

        base.NewTurn();
    }

}
