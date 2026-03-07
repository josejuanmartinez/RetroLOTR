using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class NonPlayableLeader : Leader
{
    public bool joined = false;
    private bool iconsInitialized = false;

    public List<PlayableLeader> revealedTo = new();
    private bool playerRevealPopupShown = false;

    private NonPlayableLeaderBiomeConfig nonPlayableLeaderBiome;

    public void Initialize(Hex hex, NonPlayableLeaderBiomeConfig nonPlayableLeaderBiome, bool showSpawnMessage = true)
    {
        this.nonPlayableLeaderBiome = nonPlayableLeaderBiome;
        base.Initialize(hex, nonPlayableLeaderBiome, showSpawnMessage);
        Game game = FindFirstObjectByType<Game>();
        if (game != null && game.started)
        {
            InitializeIcons();
        }
    }

    public void InitializeIcons()
    {
        if (iconsInitialized) return;
        iconsInitialized = true;
        if (nonPlayableLeaderBiome == null) return;

        PlayableLeaderIcon alignmentPlayableLeader = FindObjectsByType<PlayableLeaderIcon>(FindObjectsSortMode.None)
            .FirstOrDefault(x => x.alignment == nonPlayableLeaderBiome.alignment);
        if (!alignmentPlayableLeader)
        {
            Debug.LogWarning($"Could not find PlayableLeaderIcons for alignment {nonPlayableLeaderBiome.alignment}");
            return;
        }
        alignmentPlayableLeader.AddNonPlayableLeader(this);
    }

    public bool CanJoinWithStateAllegiance(PlayableLeader leader)
    {
        if (leader == null || killed || joined) return false;
        if (leader == this) return false;
        if (leader.GetAlignment() != alignment) return false;

        string capitalName = GetCapitalName();
        if (string.IsNullOrWhiteSpace(capitalName)) return false;
        return leader.HasPlayedPcCard(capitalName);
    }

    public string GetJoiningConditionsText(Leader leader)
    {
        StringBuilder sb = new();
        string capitalName = GetCapitalName();
        bool sameAlignment = leader != null && leader.GetAlignment() == alignment;
        bool hasCapitalCard = leader is PlayableLeader playableLeader
            && !string.IsNullOrWhiteSpace(capitalName)
            && playableLeader.HasPlayedPcCard(capitalName);

        sb.Append($"<b>{characterName}</b> only accepts State Allegiance from a leader of the same alignment.<br>");
        sb.Append(FormatRequirement("Alignment matches", sameAlignment));

        if (!string.IsNullOrWhiteSpace(capitalName))
        {
            sb.Append(FormatRequirement($"Play the PC card '{capitalName}' first", hasCapitalCard));
            sb.Append($"Then send an emissary to {capitalName} and issue State Allegiance.");
        }
        else
        {
            sb.Append("Their capital is unknown, so allegiance cannot currently be sworn.");
        }

        return sb.ToString();
    }

    private string GetCapitalName()
    {
        if (!string.IsNullOrWhiteSpace(nonPlayableLeaderBiome?.startingCityName))
        {
            return nonPlayableLeaderBiome.startingCityName;
        }

        return controlledPcs?.FirstOrDefault(pc => pc != null && pc.isCapital)?.pcName;
    }

    private static string FormatRequirement(string description, bool met)
    {
        string status = met ? "<color=#00ff00>completed</color>" : "<color=#ff0000>pending</color>";
        return $"- {description} [{status}]<br>";
    }

    override public void Killed(Leader killedBy, bool onlyMask = false)
    {
        if (killed) return;

        if (joined)
        {
            base.Killed(killedBy, onlyMask);
            return;
        }

        NonPlayableLeaderIcon npli = FindObjectsByType<NonPlayableLeaderIcon>(FindObjectsSortMode.None)
            .FirstOrDefault(x => x.nonPlayableLeader == this);
        if (npli != null) npli.SetDead();

        base.Killed(this, onlyMask);
    }

    public bool Joined(Leader joinedTo)
    {
        short max_it = 10;
        while (true)
        {
            if (max_it-- < 0) break;
            if (joinedTo == null) break;
            if (joinedTo is PlayableLeader) break;
            if (joinedTo is not PlayableLeader) joinedTo = joinedTo.GetOwner();
        }
        if (joinedTo == null || joinedTo is not PlayableLeader) return false;

        PlayableLeader playableLeaderJoinedTo = joinedTo as PlayableLeader;

        Leader owner = GetOwner();

        List<Character> originalCharacters = new(owner.controlledCharacters);
        List<PC> originalPcs = new(owner.controlledPcs);
        List<Hex> originalVisibleHexes = new(visibleHexes);
        List<Character> originalJoinedToCharacters = new(joinedTo.controlledCharacters);
        List<PC> originalJoinedToPcs = new(joinedTo.controlledPcs);
        List<Hex> originalJoinedToVisibleHexes = new(joinedTo.visibleHexes);

        var characterSnapshots = originalCharacters.Select(character => new
        {
            character,
            character.owner,
            character.alignment,
            character.startingCharacter,
            character.health
        }).ToList();

        var pcSnapshots = originalPcs.Select(pc => new
        {
            pc,
            pc.owner,
            pc.citySize,
            pc.fortSize,
            pc.loyalty
        }).ToList();

        int originalHealth = health;
        bool originalJoined = joined;
        int originalLeatherAmount = leatherAmount;
        int originalMountsAmount = mountsAmount;
        int originalTimberAmount = timberAmount;
        int originalIronAmount = ironAmount;
        int originalSteelAmount = steelAmount;
        int originalMithrilAmount = mithrilAmount;
        int originalGoldAmount = goldAmount;

        int targetLeatherAmount = joinedTo.leatherAmount;
        int targetMountsAmount = joinedTo.mountsAmount;
        int targetTimberAmount = joinedTo.timberAmount;
        int targetIronAmount = joinedTo.ironAmount;
        int targetSteelAmount = joinedTo.steelAmount;
        int targetMithrilAmount = joinedTo.mithrilAmount;
        int targetGoldAmount = joinedTo.goldAmount;

        NonPlayableLeaderIcons npls = FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None)
            .FirstOrDefault(x => x.playableLeader == playableLeaderJoinedTo);
        if (!npls) return false;
        NonPlayableLeaderIcon npli = FindObjectsByType<NonPlayableLeaderIcon>(FindObjectsSortMode.None)
            .FirstOrDefault(x => x.nonPlayableLeader == this);
        if (!npli) return false;
        npli.transform.parent = npls.transform;

        Color? npliBorderColor = npli != null ? npli.border.color : null;

        try
        {
            List<Character> charactersToTransfer = new(owner.controlledCharacters);
            List<PC> pcsToTransfer = new(owner.controlledPcs);

            foreach (Character character in charactersToTransfer)
            {
                character.owner = joinedTo;
                character.alignment = joinedTo.alignment;
                character.startingCharacter = false;
                joinedTo.controlledCharacters.Add(character);
            }

            foreach (PC pc in pcsToTransfer)
            {
                pc.owner = joinedTo;
                pc.acquisitionType = PCAcquisitionType.Joined;
                joinedTo.controlledPcs.Add(pc);
                joinedTo.visibleHexes.Add(pc.hex);
                if (joinedTo == FindAnyObjectByType<Game>().player) pc.hex.RevealArea(1);
            }

            owner.controlledCharacters.Clear();
            owner.controlledPcs.Clear();
            visibleHexes.Clear();

            health = Mathf.Max(health, 50);
            joined = true;

            if (npli != null) npli.SetHired();
            StartCoroutine(RemoveFromNPCsNextFrame());

            MessageDisplayNoUI.ShowMessage(hex, this, $"{name} has joined {joinedTo.characterName}", Color.green);
            ShowJoinPopup(joinedTo);
            CharacterIcons.RefreshForHumanPlayerOf(joinedTo);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            joined = originalJoined;
            health = originalHealth;

            foreach (var snapshot in characterSnapshots)
            {
                snapshot.character.owner = snapshot.owner;
                snapshot.character.alignment = snapshot.alignment;
                snapshot.character.startingCharacter = snapshot.startingCharacter;
                snapshot.character.health = snapshot.health;
            }

            foreach (var snapshot in pcSnapshots)
            {
                snapshot.pc.owner = snapshot.owner;
                snapshot.pc.citySize = snapshot.citySize;
                snapshot.pc.fortSize = snapshot.fortSize;
                snapshot.pc.loyalty = snapshot.loyalty;
                snapshot.pc.hex.RedrawPC();
            }

            owner.controlledCharacters.Clear();
            owner.controlledCharacters.AddRange(originalCharacters);
            owner.controlledPcs.Clear();
            owner.controlledPcs.AddRange(originalPcs);
            visibleHexes.Clear();
            visibleHexes.AddRange(originalVisibleHexes);

            joinedTo.controlledCharacters.Clear();
            joinedTo.controlledCharacters.AddRange(originalJoinedToCharacters);
            joinedTo.controlledPcs.Clear();
            joinedTo.controlledPcs.AddRange(originalJoinedToPcs);
            joinedTo.visibleHexes.Clear();
            joinedTo.visibleHexes.AddRange(originalJoinedToVisibleHexes);

            leatherAmount = originalLeatherAmount;
            mountsAmount = originalMountsAmount;
            timberAmount = originalTimberAmount;
            ironAmount = originalIronAmount;
            steelAmount = originalSteelAmount;
            mithrilAmount = originalMithrilAmount;
            goldAmount = originalGoldAmount;

            joinedTo.leatherAmount = targetLeatherAmount;
            joinedTo.mountsAmount = targetMountsAmount;
            joinedTo.timberAmount = targetTimberAmount;
            joinedTo.ironAmount = targetIronAmount;
            joinedTo.steelAmount = targetSteelAmount;
            joinedTo.mithrilAmount = targetMithrilAmount;
            joinedTo.goldAmount = targetGoldAmount;

            if (npli != null && npliBorderColor.HasValue) npli.border.color = npliBorderColor.Value;
        }

        return joined;
    }

    private void ShowJoinPopup(Leader joinedTo)
    {
        Game game = FindFirstObjectByType<Game>();
        if (game == null || game.player == null) return;

        Illustrations illustrations = FindFirstObjectByType<Illustrations>();
        string title = $"{characterName} joins {joinedTo.characterName}";
        string text = $"{characterName} has pledged allegiance to {joinedTo.characterName}.";
        PopupManager.Show(
            title,
            illustrations != null ? illustrations.GetIllustrationByName(characterName) : null,
            illustrations != null ? illustrations.GetIllustrationByName(joinedTo.characterName) : null,
            text,
            false);
    }

    private IEnumerator RemoveFromNPCsNextFrame()
    {
        yield return null;
        if (!joined) yield break;
        Game game = FindFirstObjectByType<Game>();
        if (game != null && game.npcs.Contains(this)) game.npcs.Remove(this);
    }

    public void RevealToLeader(PlayableLeader leader, bool showPopup = true)
    {
        if (leader == null) return;

        if (!revealedTo.Contains(leader)) revealedTo.Add(leader);

        FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None)
            .ToList()
            .ForEach(x =>
            {
                if (x.playableLeader == leader) x.RevealToPlayerIfNot(this);
            });

        Game game = FindFirstObjectByType<Game>();
        if (showPopup && game != null && game.IsPlayerCurrentlyPlaying() && leader == game.player && !playerRevealPopupShown)
        {
            RevealToPlayerIcons(game);
            playerRevealPopupShown = true;
        }
    }

    public void RevealToPlayer()
    {
        Game game = FindFirstObjectByType<Game>();
        if (game == null || game.player == null) return;
        if (!revealedTo.Contains(game.player)) revealedTo.Add(game.player);
        RevealToPlayerIcons(game);
        playerRevealPopupShown = true;
    }

    public bool IsRevealedToLeader(PlayableLeader leader)
    {
        return revealedTo.Contains(leader);
    }

    public bool IsRevealedToPlayer()
    {
        return revealedTo.Contains(FindFirstObjectByType<Game>().currentlyPlaying);
    }

    public bool ShouldShowPlayerRevealPopup()
    {
        Game game = FindFirstObjectByType<Game>();
        return game != null && game.IsPlayerCurrentlyPlaying() && !playerRevealPopupShown;
    }

    private void RevealToPlayerIcons(Game game)
    {
        FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None)
            .ToList()
            .ForEach(x =>
            {
                x.RevealToPlayerIfNot(this);
            });
    }

    new public void NewTurn()
    {
        base.NewTurn();
    }
}
