// valleyofwalls-assets/Scripts/PlayerState.cs
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Implementing IPlayerLeft is good practice if you need specific cleanup when a player disconnects
public class PlayerState : NetworkBehaviour, IPlayerLeft
{
    // --- Networked Stats ---
    [Networked] public int Health { get; private set; }
    [Networked] public int MaxHealth { get; private set; }
    [Networked] public int Energy { get; private set; }
    [Networked] public int MaxEnergy { get; private set; }
    [Networked] public int Score { get; private set; }
    [Networked] public NetworkString<_32> PlayerName { get; private set; }

    // --- Networked Monster Stats ---
    [Networked] public NetworkString<_32> MonsterName { get; private set; }
    [Networked] public int MonsterHealth { get; private set; }
    [Networked] public int MonsterMaxHealth { get; private set; }
    [Networked] public int MonsterAttack { get; private set; }
    [Networked] public int MonsterDefense { get; private set; }
    [Networked] public Color MonsterColor { get; private set; }

    // --- Networked Fight Status ---
    [Networked] public NetworkBool IsFightComplete { get; private set; }

    // --- Local References and State (Not Networked) ---
    private PlayerRef _opponentPlayerRef;
    private PlayerState _opponentPlayerState;
    private Monster _opponentMonsterRepresentation;
    private CardManager _cardManager;
    private MonsterManager _monsterManager;
    private bool _isPlayerTurnActive = true;
    private bool _isFightOverLocally = false;
    private bool _opponentMonsterDataReady = false;

    // --- Input Queueing (Local Only, Polled by NetworkManager.OnInput) ---
    private bool _queuePlayCard = false;
    private int _queuedCardIndex = -1;
    private PlayerRef _queuedTargetPlayer = PlayerRef.None;
    private int _queuedDamageAmount = 0;

    // --- Fusion v2 Change Detector ---
    private ChangeDetector _changeDetector;

    // --- Events (Local C# Events for UI/Logic Updates) ---
    public static event Action<PlayerState> OnStatsChanged;
    public static event Action<PlayerState, List<CardData>> OnHandChanged;
    public static event Action<Monster> OnPlayerMonsterChanged;
    public static event Action<Monster> OnOpponentMonsterChanged;
    public static event Action<bool> OnLocalTurnStateChanged;
    public static event Action<bool> OnLocalFightOver;

    // --- Constants ---
    private const int STARTING_HEALTH = 50;
    private const int STARTING_ENERGY = 3;

    // --- Initialization ---
    public override void Spawned() { /* ... (Same as previous version) ... */
        base.Spawned();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _cardManager = new CardManager();
        _monsterManager = new MonsterManager(this, Object.InputAuthority);
        _opponentMonsterDataReady = false;

        if (HasStateAuthority)
        {
            MaxHealth = STARTING_HEALTH; Health = MaxHealth; MaxEnergy = STARTING_ENERGY;
            Energy = MaxEnergy; Score = 0; IsFightComplete = false;
            InitializePlayerName();
             if (_monsterManager != null) { _monsterManager.CreatePlayerMonsterLocally(); UpdateMonsterNetworkedProperties(); }
        }
        UpdateLocalMonsterRepresentationFromNetworked(); UpdateLocalOpponentRepresentation();
        StartCoroutine(RegisterWithGameStateWhenAvailable());
        if (Object.HasInputAuthority) DrawInitialHandLocally();
        OnStatsChanged?.Invoke(this);
     }
    private IEnumerator RegisterWithGameStateWhenAvailable() { /* ... (Same as previous version) ... */
         float timer = 0, timeout = 15f;
         while (GameState.Instance == null || !GameState.Instance.IsSpawned()) { timer += Time.deltaTime; if(timer > timeout) { yield break; } yield return new WaitForSeconds(0.1f); }
         GameState.Instance.RegisterPlayer(Object.InputAuthority, this);
     }

    // --- Input Polling (Called by NetworkManager.OnInput) ---
    public NetworkInputData PollQueuedInput() { /* ... (Same as previous version) ... */
        var data = new NetworkInputData();
        if (_queuePlayCard) {
            data.PlayedCard = true; data.CardIndex = _queuedCardIndex;
            data.TargetPlayer = _queuedTargetPlayer; data.DamageAmount = _queuedDamageAmount;
            _queuePlayCard = false; _queuedCardIndex = -1; _queuedTargetPlayer = PlayerRef.None; _queuedDamageAmount = 0;
        } else { data.PlayedCard = false; data.CardIndex = -1; data.TargetPlayer = PlayerRef.None; data.DamageAmount = 0; }
        return data;
     }


    // --- Simulation Update ---
    public override void FixedUpdateNetwork()
    {
        // *** ADDED LOG: Very first line ***
        GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: --- ENTERING FixedUpdateNetwork ---");

        // Process input submitted by the owner of THIS PlayerState
        if (GetInput(out NetworkInputData data))
        {
             // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: Got OWN input. PlayedCard={data.PlayedCard}, Target={data.TargetPlayer}, Dmg={data.DamageAmount}");
            if (HasStateAuthority)
            {
                if (data.PlayedCard && data.TargetPlayer == PlayerRef.None)
                {
                     // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: Processing own input for self-targeted card (Index:{data.CardIndex}).");
                    ProcessLocalCardPlayEffects(data.CardIndex);
                }
            }
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: No input available for self via GetInput()."); }

        // Process inputs from OTHER players targeting THIS PlayerState

        // *** LOG MOVED: Check authority only before applying damage ***
        // bool hasAuthorityForOthers = HasStateAuthority; // Removed check here

        // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: Checking inputs from other players...");

        foreach (var playerRef in Runner.ActivePlayers)
        {
            // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: Checking loop for PlayerRef {playerRef}. IsSelf? {playerRef == Object.InputAuthority}");
            if (playerRef == Object.InputAuthority) continue; // Skip self

            // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: Trying to get input for OTHER player {playerRef}...");
            if (Runner.TryGetInputForPlayer(playerRef, out NetworkInputData otherData))
            {
                 // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: Found input from OTHER Player {playerRef}. PlayedCard={otherData.PlayedCard}, Target={otherData.TargetPlayer} (OurRef={Object.InputAuthority}), Dmg={otherData.DamageAmount}");

                bool isTargetingUs = otherData.TargetPlayer == Object.InputAuthority;
                 // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: Input from {playerRef} targeting us? {isTargetingUs}");

                if (otherData.PlayedCard && isTargetingUs)
                {
                    // *** Moved Authority Check HERE ***
                    // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: ---> Attack DETECTED from Player {playerRef} via input! Damage={otherData.DamageAmount}. Checking HasStateAuthority before applying...");
                    if (HasStateAuthority)
                    {
                         // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: ---> HasStateAuthority is TRUE. Applying damage...");
                        ApplyIncomingDamage(otherData.DamageAmount);
                    } else {
                         GameManager.Instance?.LogManager?.LogError($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: ---> Attack detected from {playerRef}, but LACKS State Authority! Cannot apply damage.");
                    }
                }
            }
             // else { GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: No input found for OTHER Player {playerRef}."); }
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}) [Tick:{Runner.Tick}]: Does NOT have authority, skipping processing of other players' inputs.");}
    }


    // Helper method called by FixedUpdateNetwork when THIS player's input indicates a self-targeted card play
    private void ProcessLocalCardPlayEffects(int cardIndex) { /* ... (Same as previous version, including TODO) ... */
        if (!HasStateAuthority || _cardManager == null || _monsterManager == null) return;
        GameManager.Instance?.LogManager?.LogMessage($"{PlayerName}: Skipping ProcessLocalCardPlayEffects for index {cardIndex} - Authoritative card lookup not implemented.");
        /* // Original logic commented out: ... */
     }


    // Helper method called by FixedUpdateNetwork when another player's input targets us
    private void ApplyIncomingDamage(int damageAmount) { /* ... (Same logging as previous version) ... */
        // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}): Entered ApplyIncomingDamage({damageAmount}). Auth check passed in FUN.");
        if (_monsterManager == null) { GameManager.Instance?.LogManager?.LogError($"{PlayerName} ({Runner.LocalPlayer}): MonsterManager is null in ApplyIncomingDamage."); return; }
        Monster monster = _monsterManager.GetPlayerMonster();
        if (monster != null) {
            int hBeforeNetworked = MonsterHealth;
            // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}): ApplyIncomingDamage - Monster '{monster.Name}' Health={hBeforeNetworked}. Applying {damageAmount}...");
            monster.TakeDamage(damageAmount);
            int hAfterLocal = monster.Health;
            // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}): ApplyIncomingDamage - Monster '{monster.Name}' Health after local TakeDamage={hAfterLocal}.");
            if (hBeforeNetworked != hAfterLocal) {
                // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}): ApplyIncomingDamage - Updating Networked MonsterHealth from {hBeforeNetworked} to {hAfterLocal}.");
                 MonsterHealth = hAfterLocal;
                 if (MonsterHealth <= 0 && !IsFightComplete) {
                    // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}): ApplyIncomingDamage - Monster defeated. Setting IsFightComplete=true.");
                     SetFightCompleteStatusAuthority(true);
                 }
            } /* else { Log no change } */
        } else { GameManager.Instance?.LogManager?.LogError($"{PlayerName} ({Runner.LocalPlayer}): Monster is null when trying to ApplyIncomingDamage."); }
    }


    // --- Change Detection & Representation Updates ---
    public override void Render() { /* ... (Same as previous version) ... */
         if (_changeDetector == null || !Object.IsValid) return; try { foreach (var propertyName in _changeDetector.DetectChanges(this)) { if (propertyName == nameof(Health) || propertyName == nameof(MaxHealth) || propertyName == nameof(Energy) || propertyName == nameof(MaxEnergy) || propertyName == nameof(Score) || propertyName == nameof(PlayerName)) { OnStatsChanged?.Invoke(this); if (propertyName == nameof(PlayerName) && HasStateAuthority) UpdateMonsterNetworkedProperties(); } else if (propertyName == nameof(MonsterName) || propertyName == nameof(MonsterHealth) || propertyName == nameof(MonsterMaxHealth) || propertyName == nameof(MonsterAttack) || propertyName == nameof(MonsterDefense) || propertyName == nameof(MonsterColor)) { UpdateRepresentationsAfterNetworkChange(); } else if (propertyName == nameof(IsFightComplete)) { HandleFightCompleteChangeLocally(IsFightComplete); } } } catch (Exception ex) { GameManager.Instance?.LogManager?.LogError($"Exception during PlayerState.Render for {PlayerName} (ID: {Id}): {ex.Message}\n{ex.StackTrace}"); }
     }
    private void UpdateRepresentationsAfterNetworkChange() { /* ... (Same as previous version) ... */ if (Object.HasInputAuthority) UpdateLocalMonsterRepresentationFromNetworked(); PlayerState localPlayerState = GameState.Instance?.GetLocalPlayerState(); if (localPlayerState != null && this == localPlayerState._opponentPlayerState) localPlayerState.UpdateLocalOpponentRepresentation(); }
    private void HandleFightCompleteChangeLocally(bool isCompleteNetworkValue) { /* ... (Same as previous version) ... */ if (Object.HasInputAuthority) { if (isCompleteNetworkValue && !_isFightOverLocally) { _isFightOverLocally = true; _isPlayerTurnActive = false; OnLocalFightOver?.Invoke(true); OnLocalTurnStateChanged?.Invoke(false); } else if (!isCompleteNetworkValue && _isFightOverLocally) ResetLocalFightStateForNewRound(); } }
    private void InitializePlayerName() { /* ... (Same as previous version) ... */ if (!HasStateAuthority) return; Player playerComponent = GetAssociatedPlayerComponent(); PlayerName = playerComponent != null && !string.IsNullOrEmpty(playerComponent.GetPlayerName()) ? playerComponent.GetPlayerName() : $"Player_{Object.InputAuthority.PlayerId}"; }
    private Player GetAssociatedPlayerComponent() { /* ... (Same as previous version) ... */ if (GameManager.Instance?.PlayerManager == null || Object == null) return null; NetworkObject playerObject = GameManager.Instance.PlayerManager.GetPlayerObject(Object.InputAuthority); return playerObject?.GetComponent<Player>(); }
    public void UpdateMonsterNetworkedProperties() { /* ... (Same as previous version) ... */ if (!HasStateAuthority || _monsterManager == null) return; Monster monster = _monsterManager.GetPlayerMonster(); if (monster != null) { string uniqueName = monster.Name; string currentPName = ""; try { currentPName = PlayerName.ToString(); } catch { } if (string.IsNullOrEmpty(uniqueName) || uniqueName == "Your Monster" || uniqueName.StartsWith("Monster_")) { uniqueName = !string.IsNullOrEmpty(currentPName) ? $"{currentPName}'s Monster" : $"Monster_{Id}"; monster.Name = uniqueName; } if (MonsterName != uniqueName) MonsterName = uniqueName; if (MonsterHealth != monster.Health) MonsterHealth = monster.Health; if (MonsterMaxHealth != monster.MaxHealth) MonsterMaxHealth = monster.MaxHealth; if (MonsterAttack != monster.Attack) MonsterAttack = monster.Attack; if (MonsterDefense != monster.Defense) MonsterDefense = monster.Defense; if (MonsterColor != monster.TintColor) MonsterColor = monster.TintColor; } }
    private void UpdateLocalMonsterRepresentationFromNetworked() { /* ... (Same as previous version) ... */ if (_monsterManager == null) return; try { _monsterManager.UpdateLocalMonsterFromNetworked( MonsterName.ToString(), MonsterHealth, MonsterMaxHealth, MonsterAttack, MonsterDefense, MonsterColor); Monster currentMonster = _monsterManager.GetPlayerMonster(); if (currentMonster != null) OnPlayerMonsterChanged?.Invoke(currentMonster); } catch (Exception ex) { GameManager.Instance?.LogManager?.LogError($"UpdateLocalMonsterRep Error for {PlayerName} (ID: {Id}): {ex.Message}"); } }
    private void UpdateLocalOpponentRepresentation() { /* ... (Same as previous version) ... */ if (_opponentPlayerState != null && _opponentPlayerState.Object != null && _opponentPlayerState.Object.IsValid) { if (_opponentMonsterRepresentation == null) _opponentMonsterRepresentation = new Monster(); try { _opponentMonsterRepresentation.Name = _opponentPlayerState.MonsterName.ToString(); _opponentMonsterRepresentation.MaxHealth = _opponentPlayerState.MonsterMaxHealth; _opponentMonsterRepresentation.SetHealth(_opponentPlayerState.MonsterHealth); _opponentMonsterRepresentation.Attack = _opponentPlayerState.MonsterAttack; _opponentMonsterRepresentation.Defense = _opponentPlayerState.MonsterDefense; _opponentMonsterRepresentation.TintColor = _opponentPlayerState.MonsterColor; _opponentMonsterRepresentation.ResetBlockLocally(); _opponentMonsterDataReady = true; OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation); } catch (Exception ex) { _opponentMonsterDataReady = false; OnOpponentMonsterChanged?.Invoke(null); GameManager.Instance?.LogManager?.LogError($"UpdateOpponentMonsterRep Error for {PlayerName}'s opponent ({_opponentPlayerState?.PlayerName}): {ex.Message}"); } } else { if (_opponentMonsterRepresentation != null) { _opponentMonsterRepresentation = null; OnOpponentMonsterChanged?.Invoke(null); } _opponentMonsterDataReady = false; } }

    // --- Local Hand Management ---
    public void DrawInitialHandLocally() { /* ... (Same as previous version) ... */ if (!Object.HasInputAuthority || _cardManager == null) return; _cardManager.CreateStartingDeck(); _cardManager.ShuffleDeck(); _cardManager.DrawToHandSize(); OnHandChanged?.Invoke(this, _cardManager.GetHand()); }
    public void DrawNewHandLocally() { /* ... (Same as previous version) ... */ if (!Object.HasInputAuthority || _cardManager == null) return; _cardManager.PrepareForNewRound(); OnHandChanged?.Invoke(this, _cardManager.GetHand()); }

    // --- Local Gameplay Actions ---
    public void PlayCardLocally(int cardIndex, Monster target) { /* ... (Same as previous version) ... */ if (!Object.HasInputAuthority || _isFightOverLocally || !_isPlayerTurnActive || _cardManager == null || _monsterManager == null) return; List<CardData> hand = _cardManager.GetHand(); if (cardIndex < 0 || cardIndex >= hand.Count) { GameManager.Instance?.LogManager?.LogMessage($"{PlayerName}: PlayCardLocally - Invalid card index {cardIndex}"); return; } CardData card = hand[cardIndex]; try { if (Energy < card.EnergyCost) { GameManager.Instance?.LogManager?.LogMessage($"{PlayerName}: Cannot play {card.Name}, not enough energy ({Energy}/{card.EnergyCost})."); return; } } catch { return; } Monster playerMonster = _monsterManager.GetPlayerMonster(); bool isTargetingOwnMonster = (target == playerMonster); bool isTargetingOpponentMonster = (target == _opponentMonsterRepresentation); if (!PlayerStateValidator.IsValidMonsterTarget(card, target, isTargetingOwnMonster)) { if (card.Target != CardTarget.Self && card.Target != CardTarget.Enemy && card.Target != CardTarget.AllEnemies && card.Target != CardTarget.All) { target = null; isTargetingOwnMonster = false; isTargetingOpponentMonster = false; } else { GameManager.Instance?.LogManager?.LogMessage($"-- Invalid monster target for card '{card.Name}'. Aborting play."); return; } } _queuePlayCard = true; _queuedCardIndex = cardIndex; _queuedTargetPlayer = isTargetingOpponentMonster ? _opponentPlayerRef : PlayerRef.None; _queuedDamageAmount = card.DamageAmount; RPC_RequestModifyEnergy(-card.EnergyCost); if (isTargetingOwnMonster && playerMonster != null) { if (card.BlockAmount > 0) { playerMonster.AddBlock(card.BlockAmount); OnPlayerMonsterChanged?.Invoke(playerMonster); } if (card.HealAmount > 0) { int oldH = playerMonster.Health; playerMonster.Heal(card.HealAmount); if (oldH != playerMonster.Health) { RPC_RequestModifyOwnMonsterHealth(playerMonster.Health); OnPlayerMonsterChanged?.Invoke(playerMonster); } } } if (card.EnergyGain > 0) RPC_RequestModifyEnergy(card.EnergyGain); if (card.DrawAmount > 0) { for (int i = 0; i < card.DrawAmount; i++) { if (!_cardManager.DrawCard()) break; } } _cardManager.PlayCard(cardIndex); OnHandChanged?.Invoke(this, _cardManager.GetHand()); }

    // --- RPCs ---
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestModifyEnergy(int amount, RpcInfo info = default) { if (HasStateAuthority) Energy = Mathf.Clamp(Energy + amount, 0, MaxEnergy); else { GameManager.Instance?.LogManager?.LogError($"{PlayerName}: RPC_RequestModifyEnergy received but lacks State Authority!"); } }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestModifyOwnMonsterHealth(int newHealth, RpcInfo info = default) { if (HasStateAuthority) { newHealth = Mathf.Clamp(newHealth, 0, MonsterMaxHealth); if (MonsterHealth != newHealth) MonsterHealth = newHealth; } else { GameManager.Instance?.LogManager?.LogError($"{PlayerName}: RPC_RequestModifyOwnMonsterHealth received but lacks State Authority!"); } }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)] public void RPC_PrepareForNewRound(RpcInfo info = default) { ResetLocalFightStateForNewRound(); }

    private void ResetLocalFightStateForNewRound() { _isFightOverLocally = false; _isPlayerTurnActive = true; _monsterManager?.ResetPlayerMonsterLocally(); _opponentMonsterRepresentation = null; _opponentMonsterDataReady = false; OnOpponentMonsterChanged?.Invoke(null); OnLocalTurnStateChanged?.Invoke(_isPlayerTurnActive); OnLocalFightOver?.Invoke(false); if (Object.HasInputAuthority && _cardManager != null) DrawNewHandLocally(); }

    // --- Authority Methods ---
    public void SetEnergyAuthority(int value) { if (!HasStateAuthority) return; Energy = Mathf.Clamp(value, 0, MaxEnergy); }
    public void IncreaseScoreAuthority(int amount = 1) { if (!HasStateAuthority || amount <= 0) return; Score += amount; }
    private void SetFightCompleteStatusAuthority(bool isComplete) { if (!HasStateAuthority || IsFightComplete == isComplete) return; IsFightComplete = isComplete; }

    // --- Local Fight Simulation Control ---
    public void EndPlayerTurnLocally() { if (!Object.HasInputAuthority || !_isPlayerTurnActive || _isFightOverLocally) return; _isPlayerTurnActive = false; _monsterManager?.GetPlayerMonster()?.ResetBlockLocally(); OnPlayerMonsterChanged?.Invoke(_monsterManager?.GetPlayerMonster()); OnLocalTurnStateChanged?.Invoke(false); }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestSetEnergy(int value, RpcInfo info = default) { SetEnergyAuthority(value); }
    private bool CheckLocalFightEndCondition() { if (_isFightOverLocally || !Object.HasInputAuthority) return _isFightOverLocally; Monster playerMonster = _monsterManager?.GetPlayerMonster(); bool opponentLostNetwork = (_opponentPlayerState?.Object?.IsValid ?? false) && _opponentPlayerState.MonsterHealth <= 0; bool playerLost = playerMonster?.IsDefeated() ?? false; bool playerWon = opponentLostNetwork; if (playerWon || playerLost) { if (!_isFightOverLocally) EndFightLocally(playerWon); return true; } return false; }
    private void EndFightLocally(bool didWin) { if (_isFightOverLocally || !Object.HasInputAuthority) return; _isFightOverLocally = true; _isPlayerTurnActive = false; OnLocalFightOver?.Invoke(true); OnLocalTurnStateChanged?.Invoke(false); if (didWin) RPC_RequestScoreIncrease(); if (GameState.Instance != null) GameState.Instance.RPC_NotifyFightComplete(Object.InputAuthority); RPC_RequestSetFightComplete(true); }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestSetFightComplete(bool status, RpcInfo info = default) { SetFightCompleteStatusAuthority(status); }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestScoreIncrease(int amount = 1, RpcInfo info = default) { if (HasStateAuthority) { bool opponentDefeated = (_opponentPlayerState?.Object?.IsValid ?? false) && _opponentPlayerState.MonsterHealth <= 0; if (opponentDefeated) IncreaseScoreAuthority(amount); else { /* Log Warning */ } } else { GameManager.Instance?.LogManager?.LogError($"{PlayerName}: RPC_RequestScoreIncrease received but lacks State Authority!"); } }

    // --- Getters ---
    public Monster GetMonster() => _monsterManager?.GetPlayerMonster(); public Monster GetOpponentMonster() => _opponentMonsterRepresentation; public bool IsOpponentMonsterReady() => _opponentMonsterDataReady && _opponentMonsterRepresentation != null; public List<CardData> GetHand() => _cardManager?.GetHand() ?? new List<CardData>(); public PlayerRef GetOpponentPlayerRef() => _opponentPlayerRef; public int GetScore() => Score; public bool GetIsLocalPlayerTurn() => Object.HasInputAuthority && _isPlayerTurnActive && !_isFightOverLocally; public bool GetIsLocalFightOver() => _isFightOverLocally;

    // --- Setting Opponent ---
    public void SetOpponentMonsterLocally(PlayerRef opponentRef, PlayerState opponentState) { if (!Object.HasInputAuthority) return; if (opponentState == this || opponentRef == Object.InputAuthority) { /* Log Error */ return; } _opponentPlayerRef = opponentRef; _opponentPlayerState = opponentState; UpdateLocalOpponentRepresentation(); }

    // --- IPlayerLeft Implementation ---
    public void PlayerLeft(PlayerRef player) { if (player == _opponentPlayerRef) { _opponentPlayerRef = PlayerRef.None; _opponentPlayerState = null; _opponentMonsterRepresentation = null; _opponentMonsterDataReady = false; OnOpponentMonsterChanged?.Invoke(null); GameManager.Instance?.LogManager?.LogMessage($"{PlayerName}: Opponent Player {player} left, clearing opponent state."); } }

     // --- Despawn ---
     public override void Despawned(NetworkRunner runner, bool hasState) { base.Despawned(runner, hasState); if (GameState.Instance != null && Object != null && Object.IsValid) GameState.Instance.UnregisterPlayer(Object.InputAuthority); _cardManager = null; _monsterManager = null; _opponentMonsterRepresentation = null; _opponentPlayerState = null; }

} // End of PlayerState class