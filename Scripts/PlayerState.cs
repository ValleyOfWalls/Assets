// valleyofwalls-assets/Scripts/PlayerState.cs
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    // --- Networked Stats ---
    [Networked] public int Health { get; private set; }
    [Networked] public int MaxHealth { get; private set; }
    [Networked] public int Energy { get; private set; }
    [Networked] public int MaxEnergy { get; private set; }
    [Networked] public int Score { get; private set; }
    [Networked] public NetworkString<_32> PlayerName { get; private set; } // Max 32 chars

    // --- Networked Monster Stats ---
    [Networked] public NetworkString<_32> MonsterName { get; private set; }
    [Networked] public int MonsterHealth { get; private set; }
    [Networked] public int MonsterMaxHealth { get; private set; }
    [Networked] public int MonsterAttack { get; private set; }
    [Networked] public int MonsterDefense { get; private set; }
    [Networked] public Color MonsterColor { get; private set; }

    // --- Networked Fight Status ---
    [Networked] public NetworkBool IsFightComplete { get; private set; }

    // --- Local References and State ---
    private PlayerRef _opponentPlayerRef;
    private PlayerState _opponentPlayerState;
    private Monster _opponentMonsterRepresentation;
    private CardManager _cardManager;
    private MonsterManager _monsterManager;
    private bool _isPlayerTurnActive = true;
    private bool _isFightOverLocally = false;
    private bool _opponentMonsterDataReady = false;
    // Removed coroutine reference as we removed the delayed notification attempt
    // private Coroutine _updateOpponentCoroutine = null;

    // --- Fusion v2 Change Detector ---
    private ChangeDetector _changeDetector;
    // --- Events ---
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
    public override void Spawned()
    {
        base.Spawned();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _cardManager = new CardManager();
        _monsterManager = new MonsterManager(this, Object.InputAuthority);
        _opponentMonsterDataReady = false;

        if (HasStateAuthority)
        {
            MaxHealth = STARTING_HEALTH; Health = MaxHealth;
            MaxEnergy = STARTING_ENERGY; Energy = MaxEnergy;
            Score = 0; IsFightComplete = false;
            InitializePlayerName(); // Sets PlayerName Networked property
             _monsterManager.CreatePlayerMonsterLocally(); // Create local monster
             UpdateMonsterNetworkedProperties(); // Sync initial monster stats
        }
        UpdateLocalMonsterRepresentationFromNetworked(); // Sync local visuals for own monster
        StartCoroutine(RegisterWithGameStateWhenAvailable());
        if (Object.HasInputAuthority) DrawInitialHandLocally(); // Draw if local player

        // GameManager.Instance?.LogManager?.LogMessage($"PlayerState spawned for {PlayerName} (ID: {Id}, InputAuth: {Object.HasInputAuthority})");
    }

    private IEnumerator RegisterWithGameStateWhenAvailable()
    {
         float timer = 0, timeout = 15f;
         while (GameState.Instance == null || !GameState.Instance.IsSpawned()) {
             timer += Time.deltaTime;
             if(timer > timeout) {
                  GameManager.Instance?.LogManager?.LogError($"PlayerState {Id} ({PlayerName}): Timed out waiting for GameState to register.");
                  yield break;
             }
             yield return new WaitForSeconds(0.1f);
         }
         GameState.Instance.RegisterPlayer(Object.InputAuthority, this);
    }

    // --- Change Detection and Updates ---
     public override void Render()
     {
         if (_changeDetector == null || !Object.IsValid) return;
         try {
             foreach (var propertyName in _changeDetector.DetectChanges(this)) {
                 // Update own monster visuals if own networked monster properties changed
                 if (Object.HasInputAuthority && (propertyName == nameof(MonsterName) || propertyName == nameof(MonsterHealth) ||
                     propertyName == nameof(MonsterMaxHealth) || propertyName == nameof(MonsterAttack) ||
                     propertyName == nameof(MonsterDefense) || propertyName == nameof(MonsterColor)))
                 {
                     UpdateLocalMonsterRepresentationFromNetworked();
                 }
                 // Update opponent representation if opponent's networked properties changed
                 else if (!Object.HasInputAuthority) // If this is a remote state...
                 {
                     PlayerState localPlayerState = GameState.Instance?.GetLocalPlayerState();
                      // Check if this remote state *is* the opponent of the local player
                      if (localPlayerState != null && this == localPlayerState._opponentPlayerState) {
                         // GameManager.Instance?.LogManager?.LogMessage($"{localPlayerState.PlayerName}: Opponent state ({this.PlayerName}) changed ({propertyName}). Updating local representation.");
                         localPlayerState.UpdateOpponentMonsterRepresentation(); // Tell local player state to update its view of opponent
                     }
                 }
                 // Handle other property changes (Health, Energy, Score, PlayerName for self or opponent)
                 else if (propertyName == nameof(Health) || propertyName == nameof(MaxHealth) ||
                          propertyName == nameof(Energy) || propertyName == nameof(MaxEnergy) ||
                          propertyName == nameof(Score) || propertyName == nameof(PlayerName))
                 {
                      OnStatsChanged?.Invoke(this); // Trigger general stats update (for Player/Opponent UI)
                      if(propertyName == nameof(PlayerName) && HasStateAuthority) UpdateMonsterNetworkedProperties(); // Resync monster name if player name changed
                 }
                 // Handle fight completion status change
                 else if (propertyName == nameof(IsFightComplete))
                 {
                      HandleFightCompleteChange();
                 }
             }
         } catch (Exception ex) {
              // ** FIXED CS0168 ** Use ex
              GameManager.Instance?.LogManager?.LogError($"Exception during PlayerState.Render for {PlayerName} (ID: {Id}): {ex.Message}\n{ex.StackTrace}");
         }
     }

     private void HandleFightCompleteChange() {
          // GameManager.Instance?.LogManager?.LogMessage($"Network Change: Fight completion status for {PlayerName} (ID: {Id}) is now {IsFightComplete}.");
          if (Object.HasInputAuthority) { // Only react if it's the local player's state
               if (IsFightComplete && !_isFightOverLocally) {
                    // GameManager.Instance?.LogManager?.LogMessage($"Network confirms fight COMPLETE for local player {PlayerName}. Ending local simulation.");
                    EndFightLocally(true); // Assume win? Or determine outcome?
               } else if (!IsFightComplete && _isFightOverLocally) {
                    // GameManager.Instance?.LogManager?.LogMessage($"Network confirms fight NOT complete for local player {PlayerName} (likely new round). Resetting local fight state.");
                    ResetLocalFightStateForNewRound();
               }
          }
     }

    // --- Player and Monster Initialization ---
    private void InitializePlayerName() {
        if (!HasStateAuthority) return;
        Player playerComponent = GetAssociatedPlayerComponent();
        if (playerComponent != null) {
            string name = playerComponent.GetPlayerName();
            PlayerName = string.IsNullOrEmpty(name) ? $"Player_{Object.InputAuthority.PlayerId}" : name;
        } else { PlayerName = $"Player_{Object.InputAuthority.PlayerId}"; }
         // GameManager.Instance?.LogManager?.LogMessage($"PlayerState {Id}: Initialized name to '{PlayerName}'.");
    }

    private Player GetAssociatedPlayerComponent() {
        if (GameManager.Instance?.PlayerManager == null || Object == null) return null;
        NetworkObject playerObject = GameManager.Instance.PlayerManager.GetPlayerObject(Object.InputAuthority);
        return playerObject?.GetComponent<Player>();
    }

    public void UpdateMonsterNetworkedProperties() {
        if (!HasStateAuthority) return;
        Monster monster = _monsterManager?.GetPlayerMonster();
        if (monster != null) {
            string uniqueName = monster.Name; string currentPName = "";
            try { currentPName = PlayerName.ToString(); } catch { }
            if (string.IsNullOrEmpty(uniqueName) || uniqueName == "Your Monster") { uniqueName = !string.IsNullOrEmpty(currentPName) ? $"{currentPName}'s Monster" : $"Monster_{Id}"; monster.Name = uniqueName; }
            if (MonsterName != uniqueName) MonsterName = uniqueName; if (MonsterHealth != monster.Health) MonsterHealth = monster.Health; if (MonsterMaxHealth != monster.MaxHealth) MonsterMaxHealth = monster.MaxHealth;
            if (MonsterAttack != monster.Attack) MonsterAttack = monster.Attack; if (MonsterDefense != monster.Defense) MonsterDefense = monster.Defense; if (MonsterColor != monster.TintColor) MonsterColor = monster.TintColor;
        }
    }

    // Updates the local *OWN* Monster representation from networked data
    private void UpdateLocalMonsterRepresentationFromNetworked() {
        if (_monsterManager == null) return;
        try {
            _monsterManager.UpdateLocalMonsterFromNetworked(
                MonsterName.ToString(), MonsterHealth, MonsterMaxHealth,
                MonsterAttack, MonsterDefense, MonsterColor
            );
             Monster currentMonster = _monsterManager.GetPlayerMonster();
             if (currentMonster != null) { OnPlayerMonsterChanged?.Invoke(currentMonster); }
        } catch (Exception ex) {
             // ** FIXED CS0168 ** Use ex
             GameManager.Instance?.LogManager?.LogError($"UpdateLocalMonsterRep Error for {PlayerName} (ID: {Id}): {ex.Message}");
         }
    }

     // Updates the internal representation of the opponent's monster
     // This is called by Render when the opponent's state changes, OR by SetOpponentMonsterLocally
    private void UpdateOpponentMonsterRepresentation()
    {
        if (_opponentPlayerState != null && _opponentPlayerState.Object != null && _opponentPlayerState.Object.IsValid) {
            if (_opponentMonsterRepresentation == null) _opponentMonsterRepresentation = new Monster();
            try {
                 string oppStateMonsterName = _opponentPlayerState.MonsterName.ToString();
                 int oppStateHealth = _opponentPlayerState.MonsterHealth;
                 int oppStateMaxHealth = _opponentPlayerState.MonsterMaxHealth;
                 // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName}: Reading opponent state ({_opponentPlayerState.PlayerName}): Monster='{oppStateMonsterName}', Health={oppStateHealth}/{oppStateMaxHealth}");

                _opponentMonsterRepresentation.Name = oppStateMonsterName;
                _opponentMonsterRepresentation.MaxHealth = oppStateMaxHealth;
                _opponentMonsterRepresentation.SetHealth(oppStateHealth);
                _opponentMonsterRepresentation.Attack = _opponentPlayerState.MonsterAttack;
                _opponentMonsterRepresentation.Defense = _opponentPlayerState.MonsterDefense;
                _opponentMonsterRepresentation.TintColor = _opponentPlayerState.MonsterColor;
                _opponentMonsterRepresentation.ResetBlockLocally(); // Block is local state
                _opponentMonsterDataReady = true;
            } catch (Exception ex) {
                 GameManager.Instance?.LogManager?.LogError($"UpdateOpponentMonsterRep Error for {PlayerName}'s opponent ({_opponentPlayerState?.PlayerName}): {ex.Message}");
                 _opponentMonsterDataReady = false;
            }
        } else {
             if (_opponentMonsterRepresentation != null) { _opponentMonsterRepresentation = null; }
            _opponentMonsterDataReady = false;
        }
    }

    // --- Local Hand Management ---
    public void DrawInitialHandLocally() {
        if (!Object.HasInputAuthority || _cardManager == null) return;
        _cardManager.CreateStartingDeck(); _cardManager.ShuffleDeck(); _cardManager.DrawToHandSize();
        OnHandChanged?.Invoke(this, _cardManager.GetHand());
    }
    public void DrawNewHandLocally() {
        if (!Object.HasInputAuthority || _cardManager == null) return;
        _cardManager.PrepareForNewRound();
        OnHandChanged?.Invoke(this, _cardManager.GetHand());
    }

    // --- Local Turn and Fight Logic ---
    public void PlayCardLocally(int cardIndex, Monster target) {
        if (!Object.HasInputAuthority || _isFightOverLocally || !_isPlayerTurnActive || _cardManager == null || _monsterManager == null) return;
        List<CardData> hand = _cardManager.GetHand();
        if (cardIndex < 0 || cardIndex >= hand.Count) return;
        CardData card = hand[cardIndex];
        try { if (Energy < card.EnergyCost) return; } catch { return; }

        Monster playerMonster = _monsterManager.GetPlayerMonster();
        bool isTargetingOwnMonster = (target == playerMonster);
        bool isTargetingOpponentMonster = (target == _opponentMonsterRepresentation);

        if (!PlayerStateValidator.IsValidMonsterTarget(card, target, isTargetingOwnMonster)) {
             if (card.Target != CardTarget.Self && card.Target != CardTarget.Enemy && card.Target != CardTarget.AllEnemies && card.Target != CardTarget.All) {
                  isTargetingOwnMonster = true; isTargetingOpponentMonster = false;
             } else { return; }
        }

        RPC_RequestModifyEnergy(-card.EnergyCost);
        ApplyCardEffectsLocally(card, target, isTargetingOwnMonster, isTargetingOpponentMonster);
        if (isTargetingOpponentMonster && card.DamageAmount > 0 && _opponentPlayerRef != default) {
            RPC_RequestApplyDamageToOpponent(_opponentPlayerRef, card.DamageAmount);
        }
        _cardManager.PlayCard(cardIndex);
        OnHandChanged?.Invoke(this, _cardManager.GetHand());
        CheckLocalFightEndCondition();
    }

    private void ApplyCardEffectsLocally(CardData card, Monster target, bool isTargetingOwn, bool isTargetingOpponent) {
        Monster ownMonster = _monsterManager?.GetPlayerMonster();
        if (isTargetingOwn && ownMonster != null) {
            if (card.BlockAmount > 0) ownMonster.AddBlock(card.BlockAmount);
            if (card.HealAmount > 0) { int oldH = ownMonster.Health; ownMonster.Heal(card.HealAmount); if(oldH != ownMonster.Health) RPC_RequestModifyMonsterHealth(ownMonster.Health); }
            OnPlayerMonsterChanged?.Invoke(ownMonster);
        } else if (isTargetingOpponent && _opponentMonsterRepresentation != null) {
            OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation); // Update UI prediction/state
        }
        if (card.EnergyGain > 0) RPC_RequestModifyEnergy(card.EnergyGain);
        if (card.DrawAmount > 0 && Object.HasInputAuthority && _cardManager != null) {
             for (int i = 0; i < card.DrawAmount; i++) { if (!_cardManager.DrawCard()) break; }
            OnHandChanged?.Invoke(this, _cardManager.GetHand());
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestApplyDamageToOpponent(PlayerRef opponentPlayerRef, int damageAmount, RpcInfo info = default) {
        if (GameState.Instance == null) return; PlayerState opponentState = GameState.Instance.GetPlayerState(opponentPlayerRef);
        if (opponentState?.Object?.IsValid ?? false) { opponentState.RPC_TakeDamageOnMonster(damageAmount); }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TakeDamageOnMonster(int damageAmount, RpcInfo info = default) {
        if (!HasStateAuthority || _monsterManager == null) return; Monster monster = _monsterManager.GetPlayerMonster();
        if (monster != null) { int hBefore = monster.Health; monster.TakeDamage(damageAmount); if (MonsterHealth != monster.Health) { MonsterHealth = monster.Health; if (monster.IsDefeated()) SetFightCompleteStatus(true); } }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestModifyEnergy(int amount, RpcInfo info = default) => ModifyEnergy(amount);
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestModifyMonsterHealth(int newHealth, RpcInfo info = default) => ModifyMonsterHealth(newHealth);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PrepareForNewRound(RpcInfo info = default) { ResetLocalFightStateForNewRound(); }

    private void ResetLocalFightStateForNewRound() {
         _isFightOverLocally = false; _isPlayerTurnActive = true; _monsterManager?.ResetPlayerMonsterLocally();
         _opponentMonsterRepresentation = null; _opponentMonsterDataReady = false;
         OnOpponentMonsterChanged?.Invoke(null); // Trigger null to clear UI
         OnLocalTurnStateChanged?.Invoke(_isPlayerTurnActive); OnLocalFightOver?.Invoke(false); OnStatsChanged?.Invoke(this);
         if (Object.HasInputAuthority && _cardManager != null) { OnHandChanged?.Invoke(this, _cardManager.GetHand()); }
    }

    // --- Authority Methods ---
    public void ModifyHealth(int amount) { if (HasStateAuthority) Health = Mathf.Clamp(Health + amount, 0, MaxHealth); }
    public void ModifyMonsterHealth(int newHealth) { if (!HasStateAuthority) return; newHealth = Mathf.Clamp(newHealth, 0, MonsterMaxHealth); if (MonsterHealth != newHealth) { MonsterHealth = newHealth; if (MonsterHealth <= 0 && !IsFightComplete) SetFightCompleteStatus(true); } }
    public void ModifyEnergy(int amount) { if (HasStateAuthority) Energy = Mathf.Clamp(Energy + amount, 0, MaxEnergy); }
    public void SetEnergy(int value) { if (HasStateAuthority) Energy = Mathf.Clamp(value, 0, MaxEnergy); }
    public void IncreaseScore(int amount = 1) { if (HasStateAuthority && amount > 0) Score += amount; }
    private void SetFightCompleteStatus(bool isComplete) { if (!HasStateAuthority || IsFightComplete == isComplete) return; IsFightComplete = isComplete; }

    // --- Local Fight Simulation ---
    public void EndPlayerTurnLocally() {
         if (!Object.HasInputAuthority || !_isPlayerTurnActive || _isFightOverLocally) return;
        _isPlayerTurnActive = false; _monsterManager?.GetPlayerMonster()?.ResetBlockLocally();
        OnPlayerMonsterChanged?.Invoke(_monsterManager?.GetPlayerMonster()); OnLocalTurnStateChanged?.Invoke(false);
        if (!CheckLocalFightEndCondition()) StartCoroutine(SimulateMonsterTurnLocally());
    }

    // ** FIXED CS0161 HERE **
    private IEnumerator SimulateMonsterTurnLocally() {
         if (!Object.HasInputAuthority) yield break; // Added break
        yield return new WaitForSeconds(1.0f);
        if (_isFightOverLocally) yield break; // Added break

        Monster ownMonster = _monsterManager?.GetPlayerMonster(); Monster opponentRep = _opponentMonsterRepresentation;
        if (ownMonster != null && opponentRep != null && !opponentRep.IsDefeated()) {
            CardData monsterAttack = opponentRep.ChooseAction(); int healthBefore = ownMonster.Health; ownMonster.TakeDamage(monsterAttack.DamageAmount);
            OnPlayerMonsterChanged?.Invoke(ownMonster); if (healthBefore != ownMonster.Health) RPC_RequestModifyMonsterHealth(ownMonster.Health);
            if (ownMonster.IsDefeated()) CheckLocalFightEndCondition(); // This might set _isFightOverLocally
        }
        // Check again if fight ended during the above block
        if (!_isFightOverLocally) {
             EndMonsterTurnLocally();
        }
        yield break; // Ensure all paths yield or break
    }

    private void EndMonsterTurnLocally() {
         if (!Object.HasInputAuthority || _isFightOverLocally) return;
        _opponentMonsterRepresentation?.ResetBlockLocally(); OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation);
        if (CheckLocalFightEndCondition()) return;
        _isPlayerTurnActive = true; RPC_RequestSetEnergy(MaxEnergy); OnLocalTurnStateChanged?.Invoke(true);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestSetEnergy(int value, RpcInfo info = default) => SetEnergy(value);

    private bool CheckLocalFightEndCondition() {
         if (_isFightOverLocally || !Object.HasInputAuthority) return _isFightOverLocally;
        Monster playerMonster = _monsterManager?.GetPlayerMonster(); Monster opponentMonsterRep = _opponentMonsterRepresentation;
        bool playerLost = playerMonster?.IsDefeated() ?? false;
        bool opponentLost = (_opponentPlayerState?.Object?.IsValid ?? false) ? _opponentPlayerState.MonsterHealth <= 0 : (opponentMonsterRep?.IsDefeated() ?? false);
        bool playerWon = opponentLost;
        if (playerWon || playerLost) { EndFightLocally(playerWon); return true; }
        return false;
    }
    private void EndFightLocally(bool didWin) {
         if (_isFightOverLocally || !Object.HasInputAuthority) return;
        _isFightOverLocally = true; _isPlayerTurnActive = false;
        OnLocalFightOver?.Invoke(true); OnLocalTurnStateChanged?.Invoke(false);
        if (didWin) RPC_RequestScoreIncrease();
        if (GameState.Instance != null) GameState.Instance.RPC_NotifyFightComplete(Object.InputAuthority);
        RPC_RequestSetFightComplete(true);
    }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestSetFightComplete(bool status, RpcInfo info = default) => SetFightCompleteStatus(status);
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestScoreIncrease(int amount = 1, RpcInfo info = default) {
         if (_opponentPlayerState?.Object?.IsValid ?? false && _opponentPlayerState.MonsterHealth <= 0) { IncreaseScore(amount); }
    }

    // --- Getters ---
    public Monster GetMonster() => _monsterManager?.GetPlayerMonster();
    public Monster GetOpponentMonster() => _opponentMonsterRepresentation;
    public bool IsOpponentMonsterReady() => _opponentMonsterDataReady && _opponentMonsterRepresentation != null;
    public List<CardData> GetHand() => _cardManager?.GetHand() ?? new List<CardData>();
    public PlayerRef GetOpponentPlayerRef() => _opponentPlayerRef;
    public int GetScore() => Score;
    public bool GetIsLocalPlayerTurn() => Object.HasInputAuthority && _isPlayerTurnActive && !_isFightOverLocally;
    public bool GetIsLocalFightOver() => _isFightOverLocally;

    // --- Setting Opponent ---
    // Called locally by GameState's RPC_SetMonsterMatchup
    public void SetOpponentMonsterLocally(PlayerRef opponentRef, PlayerState opponentState)
    {
        if (!Object.HasInputAuthority) return; // Only process for the local player

        // ** Critical Check: Prevent setting self as opponent **
        if (opponentState == this || opponentRef == Object.InputAuthority) {
            GameManager.Instance?.LogManager?.LogError($"{PlayerName}: CRITICAL ERROR - Attempted to set self (Ref: {opponentRef}) as opponent!");
            return;
        }

        bool opponentChanged = (_opponentPlayerRef != opponentRef);
        // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName}: SetOpponentMonsterLocally called. New OpponentRef: {opponentRef}, Opponent State ID: {opponentState?.Id.ToString() ?? "NULL"}, Changed: {opponentChanged}");

        _opponentPlayerRef = opponentRef;
        _opponentPlayerState = opponentState;

        // Update internal representation immediately
        UpdateOpponentMonsterRepresentation();

        // Trigger event if data is now ready OR if opponent changed (to clear old display)
        string monsterNameToLog = _opponentMonsterRepresentation?.Name ?? "null";
        // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName}: Post UpdateOpponentMonsterRepresentation. Ready: {_opponentMonsterDataReady}, Monster: {monsterNameToLog}");

        if (_opponentMonsterDataReady) {
            // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName}: Triggering OnOpponentMonsterChanged with monster: {monsterNameToLog}");
             OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation);
        } else if (opponentChanged) {
            // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName}: Opponent changed but data not ready. Triggering OnOpponentMonsterChanged with NULL.");
             OnOpponentMonsterChanged?.Invoke(null); // Clear old display
        }
    }

    // --- Despawn ---
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
         if (GameState.Instance != null && Object != null) GameState.Instance.UnregisterPlayer(Object.InputAuthority);
         _cardManager = null; _monsterManager = null; _opponentMonsterRepresentation = null; _opponentPlayerState = null;
    }
}