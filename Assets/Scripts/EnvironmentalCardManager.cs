using System;
using UnityEngine;

public class EnvironmentalCardManager : MonoBehaviour
{
    public static EnvironmentalCardManager Instance { get; private set; }

    public CardData ActiveCard { get; private set; }

    // Per-turn modifier fields — reset each turn before ApplyOngoingEffect runs.
    // Environmental card scripts set these in ApplyOngoingEffect; core systems read them.
    public int FrozenMovementExtraPenalty = 0;
    public float FrozenCombatAttackFactor = 1f;
    public float FrozenCombatDefenseExtraFactor = 1f;
    public float GlobalArmyAttackFactor = 1f;
    public float FreePeopleArmyAttackFactor = 1f;
    public float DarkServantsArmyAttackFactor = 1f;

    private Game subscribedGame;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SubscribeToGame();
    }

    private void SubscribeToGame()
    {
        Game game = FindFirstObjectByType<Game>();
        if (game == null || game == subscribedGame) return;
        if (subscribedGame != null) subscribedGame.NewTurnStarted -= OnNewTurn;
        subscribedGame = game;
        game.NewTurnStarted += OnNewTurn;
    }

    public void SetActiveCard(CardData card)
    {
        if (subscribedGame == null) SubscribeToGame();
        ActiveCard = card;
        Layout layout = FindFirstObjectByType<Layout>();
        layout?.SetEnvironmentalCard(card);
    }

    private void OnNewTurn(int turn)
    {
        ResetModifiers();
        if (ActiveCard == null) return;
        ApplyActiveCardEffect();
    }

    private void ResetModifiers()
    {
        FrozenMovementExtraPenalty = 0;
        FrozenCombatAttackFactor = 1f;
        FrozenCombatDefenseExtraFactor = 1f;
        GlobalArmyAttackFactor = 1f;
        FreePeopleArmyAttackFactor = 1f;
        DarkServantsArmyAttackFactor = 1f;
    }

    private void ApplyActiveCardEffect()
    {
        if (ActiveCard == null) return;
        string className = ActiveCard.GetActionRef();
        if (string.IsNullOrEmpty(className)) return;

        Type type = Type.GetType(className);
        if (type == null || !typeof(CharacterAction).IsAssignableFrom(type)) return;

        try
        {
            var instance = (CharacterAction)Activator.CreateInstance(type);
            instance.ApplyOngoingEffect();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"EnvironmentalCardManager: failed to apply ongoing effect for {className}: {e.Message}");
        }
    }

    public void ClearActiveCard()
    {
        ActiveCard = null;
        ResetModifiers();
        Layout layout = FindFirstObjectByType<Layout>();
        layout?.SetEnvironmentalCard(null);
    }

    public static EnvironmentalCardManager GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("EnvironmentalCardManager");
        return go.AddComponent<EnvironmentalCardManager>();
    }
}
