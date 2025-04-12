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
    public override void Spawned()
    {
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

    private IEnumerator RegisterWithGameStateWhenAvailable()
    {
         float timer = 0, timeout = 15f;
         while (GameState.Instance == null || !GameState.Instance.IsSpawned()) { timer += Time.deltaTime; if(timer > timeout) { yield break; } yield return new WaitForSeconds(0.1f); }
         GameState.Instance.RegisterPlayer(Object.InputAuthority, this);
    }

    // --- Change Detection (Fusion 2 Pattern) ---
     public override void Render()
     {
         if (_changeDetector == null || !Object.IsValid) return;
         try {
             foreach (var propertyName in _changeDetector.DetectChanges(this)) {
                 if (propertyName == nameof(Health) || propertyName == nameof(MaxHealth) || propertyName == nameof(Energy) || propertyName == nameof(MaxEnergy) || propertyName == nameof(Score) || propertyName == nameof(PlayerName)) {
                     OnStatsChanged?.Invoke(this); if (propertyName == nameof(PlayerName) && HasStateAuthority) UpdateMonsterNetworkedProperties();
                 } else if (propertyName == nameof(MonsterName) || propertyName == nameof(MonsterHealth) || propertyName == nameof(MonsterMaxHealth) || propertyName == nameof(MonsterAttack) || propertyName == nameof(MonsterDefense) || propertyName == nameof(MonsterColor)) {
                     UpdateRepresentationsAfterNetworkChange();
                 } else if (propertyName == nameof(IsFightComplete)) {
                     HandleFightCompleteChangeLocally(IsFightComplete);
                 }
             }
         } catch (Exception ex) { GameManager.Instance?.LogManager?.LogError($"Exception during PlayerState.Render for {PlayerName} (ID: {Id}): {ex.Message}\n{ex.StackTrace}"); }
     }

    private void UpdateRepresentationsAfterNetworkChange() { if (Object.HasInputAuthority) UpdateLocalMonsterRepresentationFromNetworked(); PlayerState localPlayerState = GameState.Instance?.GetLocalPlayerState(); if (localPlayerState != null && this == localPlayerState._opponentPlayerState) localPlayerState.UpdateLocalOpponentRepresentation(); }
    private void HandleFightCompleteChangeLocally(bool isCompleteNetworkValue) { if (Object.HasInputAuthority) { if (isCompleteNetworkValue && !_isFightOverLocally) { _isFightOverLocally = true; _isPlayerTurnActive = false; OnLocalFightOver?.Invoke(true); OnLocalTurnStateChanged?.Invoke(false); } else if (!isCompleteNetworkValue && _isFightOverLocally) ResetLocalFightStateForNewRound(); } }

    // --- Player and Monster Initialization & Syncing ---
    private void InitializePlayerName() { if (!HasStateAuthority) return; Player playerComponent = GetAssociatedPlayerComponent(); PlayerName = playerComponent != null && !string.IsNullOrEmpty(playerComponent.GetPlayerName()) ? playerComponent.GetPlayerName() : $"Player_{Object.InputAuthority.PlayerId}"; }
    private Player GetAssociatedPlayerComponent() { if (GameManager.Instance?.PlayerManager == null || Object == null) return null; NetworkObject playerObject = GameManager.Instance.PlayerManager.GetPlayerObject(Object.InputAuthority); return playerObject?.GetComponent<Player>(); }
    public void UpdateMonsterNetworkedProperties() { if (!HasStateAuthority || _monsterManager == null) return; Monster monster = _monsterManager.GetPlayerMonster(); if (monster != null) { string uniqueName = monster.Name; string currentPName = ""; try { currentPName = PlayerName.ToString(); } catch { } if (string.IsNullOrEmpty(uniqueName) || uniqueName == "Your Monster" || uniqueName.StartsWith("Monster_")) { uniqueName = !string.IsNullOrEmpty(currentPName) ? $"{currentPName}'s Monster" : $"Monster_{Id}"; monster.Name = uniqueName; } if (MonsterName != uniqueName) MonsterName = uniqueName; if (MonsterHealth != monster.Health) MonsterHealth = monster.Health; if (MonsterMaxHealth != monster.MaxHealth) MonsterMaxHealth = monster.MaxHealth; if (MonsterAttack != monster.Attack) MonsterAttack = monster.Attack; if (MonsterDefense != monster.Defense) MonsterDefense = monster.Defense; if (MonsterColor != monster.TintColor) MonsterColor = monster.TintColor; } }
    private void UpdateLocalMonsterRepresentationFromNetworked() { if (_monsterManager == null) return; try { _monsterManager.UpdateLocalMonsterFromNetworked( MonsterName.ToString(), MonsterHealth, MonsterMaxHealth, MonsterAttack, MonsterDefense, MonsterColor); Monster currentMonster = _monsterManager.GetPlayerMonster(); if (currentMonster != null) OnPlayerMonsterChanged?.Invoke(currentMonster); } catch (Exception ex) { GameManager.Instance?.LogManager?.LogError($"UpdateLocalMonsterRep Error for {PlayerName} (ID: {Id}): {ex.Message}"); } }
    private void UpdateLocalOpponentRepresentation() { if (_opponentPlayerState != null && _opponentPlayerState.Object != null && _opponentPlayerState.Object.IsValid) { if (_opponentMonsterRepresentation == null) _opponentMonsterRepresentation = new Monster(); try { _opponentMonsterRepresentation.Name = _opponentPlayerState.MonsterName.ToString(); _opponentMonsterRepresentation.MaxHealth = _opponentPlayerState.MonsterMaxHealth; _opponentMonsterRepresentation.SetHealth(_opponentPlayerState.MonsterHealth); _opponentMonsterRepresentation.Attack = _opponentPlayerState.MonsterAttack; _opponentMonsterRepresentation.Defense = _opponentPlayerState.MonsterDefense; _opponentMonsterRepresentation.TintColor = _opponentPlayerState.MonsterColor; _opponentMonsterRepresentation.ResetBlockLocally(); _opponentMonsterDataReady = true; OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation); } catch (Exception ex) { _opponentMonsterDataReady = false; OnOpponentMonsterChanged?.Invoke(null); GameManager.Instance?.LogManager?.LogError($"UpdateOpponentMonsterRep Error for {PlayerName}'s opponent ({_opponentPlayerState?.PlayerName}): {ex.Message}"); } } else { if (_opponentMonsterRepresentation != null) { _opponentMonsterRepresentation = null; OnOpponentMonsterChanged?.Invoke(null); } _opponentMonsterDataReady = false; } }

    // --- Local Hand Management ---
    public void DrawInitialHandLocally() { if (!Object.HasInputAuthority || _cardManager == null) return; _cardManager.CreateStartingDeck(); _cardManager.ShuffleDeck(); _cardManager.DrawToHandSize(); OnHandChanged?.Invoke(this, _cardManager.GetHand()); }
    public void DrawNewHandLocally() { if (!Object.HasInputAuthority || _cardManager == null) return; _cardManager.PrepareForNewRound(); OnHandChanged?.Invoke(this, _cardManager.GetHand()); }

    // --- Local Turn and Fight Logic ---
    public void PlayCardLocally(int cardIndex, Monster target) { // target here is the logical monster (own or opponent rep)
        if (!Object.HasInputAuthority || _isFightOverLocally || !_isPlayerTurnActive || _cardManager == null || _monsterManager == null) return;
        List<CardData> hand = _cardManager.GetHand(); if (cardIndex < 0 || cardIndex >= hand.Count) return; CardData card = hand[cardIndex];
        try { if (Energy < card.EnergyCost) return; } catch { return; }
        Monster playerMonster = _monsterManager.GetPlayerMonster(); bool isTargetingOwnMonster = (target == playerMonster); bool isTargetingOpponentMonster = (target == _opponentMonsterRepresentation);
        GameManager.Instance?.LogManager?.LogMessage($"PlayCardLocally: Attacker={Object.InputAuthority}, Target Monster='{target?.Name ?? "NULL"}', OpponentRef='{_opponentPlayerRef}', isTargetingOpponent={isTargetingOpponentMonster}");
        if (!PlayerStateValidator.IsValidMonsterTarget(card, target, isTargetingOwnMonster)) { if (card.Target != CardTarget.Self && card.Target != CardTarget.Enemy && card.Target != CardTarget.AllEnemies && card.Target != CardTarget.All) { target = null; isTargetingOwnMonster = false; isTargetingOpponentMonster = false; } else return; }

        RPC_RequestModifyEnergy(-card.EnergyCost);
        ApplyCardEffectsLocally(card, target, isTargetingOwnMonster, isTargetingOpponentMonster);

        if (isTargetingOpponentMonster && card.DamageAmount > 0 && _opponentPlayerRef != default) {
            GameManager.Instance?.LogManager?.LogMessage($"PlayCardLocally: Announcing damage to Target PlayerRef: {_opponentPlayerRef}, Damage: {card.DamageAmount}");
            RPC_AnnounceDamage(_opponentPlayerRef, card.DamageAmount);
        }

        _cardManager.PlayCard(cardIndex); OnHandChanged?.Invoke(this, _cardManager.GetHand()); CheckLocalFightEndCondition();
    }

    private void ApplyCardEffectsLocally(CardData card, Monster target, bool isTargetingOwn, bool isTargetingOpponent) {
        Monster ownMonster = _monsterManager?.GetPlayerMonster();
        if (isTargetingOwn && ownMonster != null) { if (card.BlockAmount > 0) ownMonster.AddBlock(card.BlockAmount); if (card.HealAmount > 0) { int oldH = ownMonster.Health; ownMonster.Heal(card.HealAmount); if (oldH != ownMonster.Health) RPC_RequestModifyOwnMonsterHealth(ownMonster.Health); } OnPlayerMonsterChanged?.Invoke(ownMonster); }
        if (card.EnergyGain > 0) RPC_RequestModifyEnergy(card.EnergyGain);
        if (card.DrawAmount > 0 && Object.HasInputAuthority && _cardManager != null) { for (int i = 0; i < card.DrawAmount; i++) { if (!_cardManager.DrawCard()) break; } OnHandChanged?.Invoke(this, _cardManager.GetHand()); }
    }

    // --- RPCs ---

    // RPC: Sent By Attacker --> All Clients
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_AnnounceDamage(PlayerRef targetPlayerRef, int damageAmount, RpcInfo info = default)
    {
        int localPlayerId = Runner.LocalPlayer.PlayerId;
        int targetPlayerId = targetPlayerRef.PlayerId;
        bool isTarget = localPlayerId == targetPlayerId;

        GameManager.Instance?.LogManager?.LogMessage($"Client {Runner.LocalPlayer}: Received RPC_AnnounceDamage. TargetRef={targetPlayerRef} (ID:{targetPlayerId}), Dmg={damageAmount}. Comparing with Local ID:{localPlayerId}. IsTarget = {isTarget}");

        if (isTarget)
        {
            GameManager.Instance?.LogManager?.LogMessage($"Client {Runner.LocalPlayer}: Target matches self. Calling RPC_ExecuteLocalDamage (StateAuthority Targeted)...");
            // Call the RPC that targets StateAuthority - this client *should* be the State Authority for itself.
            this.RPC_ExecuteLocalDamage(damageAmount);
        }
    }

    // ** REVISED RPC: Targets State Authority **
    // Called locally by the target of RPC_AnnounceDamage.
    // This RPC will execute *only* on the client that holds State Authority for this specific PlayerState object.
    // In default Shared Mode, this should be the same client that holds Input Authority.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)] // Changed Target to StateAuthority
    private void RPC_ExecuteLocalDamage(int damageAmount, RpcInfo info = default)
    {
        // This code now executes ONLY on the State Authority for this PlayerState object.
        GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}): Executing RPC_ExecuteLocalDamage (State Authority Check Implicit via RpcTarget). Applying {damageAmount} damage.");

        // No need for explicit 'if (HasStateAuthority)' check here, as the RPC target ensures it.
        // However, defensive checks are still good practice.
        if (!HasStateAuthority) {
             GameManager.Instance?.LogManager?.LogError($"{PlayerName} ({Runner.LocalPlayer}): executing RPC_ExecuteLocalDamage BUT HasStateAuthority is FALSE! This indicates a critical authority mismatch.");
             return;
        }

        if (_monsterManager == null) { GameManager.Instance?.LogManager?.LogError($"{PlayerName}: MonsterManager is null in RPC_ExecuteLocalDamage."); return; }
        Monster monster = _monsterManager.GetPlayerMonster(); // Get own monster

        GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Runner.LocalPlayer}): Applying {damageAmount} damage via RPC_ExecuteLocalDamage to monster named '{monster?.Name ?? "NULL"}'");

        if (monster != null)
        {
            int hBeforeNetworked = MonsterHealth;
            monster.TakeDamage(damageAmount); // Apply damage LOCALLY first
            int hAfterLocal = monster.Health;

            if (hBeforeNetworked != hAfterLocal)
            {
                // Update the NETWORKED property (we have State Authority here)
                MonsterHealth = hAfterLocal;
                if (MonsterHealth <= 0 && !IsFightComplete)
                {
                    // Update the NETWORKED property (we have State Authority here)
                    SetFightCompleteStatusAuthority(true);
                }
            }
        } else { GameManager.Instance?.LogManager?.LogError($"{PlayerName}: Monster is null in RPC_ExecuteLocalDamage."); }
    }


    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestModifyEnergy(int amount, RpcInfo info = default) { if (HasStateAuthority) Energy = Mathf.Clamp(Energy + amount, 0, MaxEnergy); else { GameManager.Instance?.LogManager?.LogError($"{PlayerName}: RPC_RequestModifyEnergy received but lacks State Authority!"); } }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestModifyOwnMonsterHealth(int newHealth, RpcInfo info = default) { if (HasStateAuthority) { newHealth = Mathf.Clamp(newHealth, 0, MonsterMaxHealth); if (MonsterHealth != newHealth) MonsterHealth = newHealth; } else { GameManager.Instance?.LogManager?.LogError($"{PlayerName}: RPC_RequestModifyOwnMonsterHealth received but lacks State Authority!"); } }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)] public void RPC_PrepareForNewRound(RpcInfo info = default) { ResetLocalFightStateForNewRound(); }

    private void ResetLocalFightStateForNewRound() { _isFightOverLocally = false; _isPlayerTurnActive = true; _monsterManager?.ResetPlayerMonsterLocally(); _opponentMonsterRepresentation = null; _opponentMonsterDataReady = false; OnOpponentMonsterChanged?.Invoke(null); OnLocalTurnStateChanged?.Invoke(_isPlayerTurnActive); OnLocalFightOver?.Invoke(false); if (Object.HasInputAuthority && _cardManager != null) DrawNewHandLocally(); }

    // --- Authority Methods ---
    public void SetEnergyAuthority(int value) { if (!HasStateAuthority) return; Energy = Mathf.Clamp(value, 0, MaxEnergy); }
    public void IncreaseScoreAuthority(int amount = 1) { if (!HasStateAuthority || amount <= 0) return; Score += amount; }
    private void SetFightCompleteStatusAuthority(bool isComplete) { if (!HasStateAuthority || IsFightComplete == isComplete) return; IsFightComplete = isComplete; }

    // --- Local Fight Simulation ---
    public void EndPlayerTurnLocally() { if (!Object.HasInputAuthority || !_isPlayerTurnActive || _isFightOverLocally) return; _isPlayerTurnActive = false; _monsterManager?.GetPlayerMonster()?.ResetBlockLocally(); OnPlayerMonsterChanged?.Invoke(_monsterManager?.GetPlayerMonster()); OnLocalTurnStateChanged?.Invoke(false); if (!CheckLocalFightEndCondition()) StartCoroutine(SimulateMonsterTurnLocally()); }
    private IEnumerator SimulateMonsterTurnLocally() { if (!Object.HasInputAuthority) yield break; yield return new WaitForSeconds(1.0f); if (_isFightOverLocally) yield break; Monster ownMonster = _monsterManager?.GetPlayerMonster(); Monster opponentRep = _opponentMonsterRepresentation; if (ownMonster != null && opponentRep != null && !opponentRep.IsDefeated()) { CardData monsterAttack = opponentRep.ChooseAction(); int healthBefore = ownMonster.Health; ownMonster.TakeDamage(monsterAttack.DamageAmount); OnPlayerMonsterChanged?.Invoke(ownMonster); if (healthBefore != ownMonster.Health) RPC_RequestModifyOwnMonsterHealth(ownMonster.Health); if (ownMonster.IsDefeated()) CheckLocalFightEndCondition(); } if (!_isFightOverLocally) EndMonsterTurnLocally(); yield break; }
    private void EndMonsterTurnLocally() { if (!Object.HasInputAuthority || _isFightOverLocally) return; _opponentMonsterRepresentation?.ResetBlockLocally(); OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation); if (CheckLocalFightEndCondition()) return; _isPlayerTurnActive = true; RPC_RequestSetEnergy(MaxEnergy); OnLocalTurnStateChanged?.Invoke(true); }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestSetEnergy(int value, RpcInfo info = default) { SetEnergyAuthority(value); }
    private bool CheckLocalFightEndCondition() { if (_isFightOverLocally || !Object.HasInputAuthority) return _isFightOverLocally; Monster playerMonster = _monsterManager?.GetPlayerMonster(); bool opponentLostNetwork = (_opponentPlayerState?.Object?.IsValid ?? false) && _opponentPlayerState.MonsterHealth <= 0; bool playerLost = playerMonster?.IsDefeated() ?? false; bool playerWon = opponentLostNetwork; if (playerWon || playerLost) { EndFightLocally(playerWon); return true; } return false; }
    private void EndFightLocally(bool didWin) { if (_isFightOverLocally || !Object.HasInputAuthority) return; _isFightOverLocally = true; _isPlayerTurnActive = false; OnLocalFightOver?.Invoke(true); OnLocalTurnStateChanged?.Invoke(false); if (didWin) RPC_RequestScoreIncrease(); if (GameState.Instance != null) GameState.Instance.RPC_NotifyFightComplete(Object.InputAuthority); RPC_RequestSetFightComplete(true); }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestSetFightComplete(bool status, RpcInfo info = default) { SetFightCompleteStatusAuthority(status); }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] private void RPC_RequestScoreIncrease(int amount = 1, RpcInfo info = default) { if (HasStateAuthority) { bool opponentDefeated = (_opponentPlayerState?.Object?.IsValid ?? false) && _opponentPlayerState.MonsterHealth <= 0; if (opponentDefeated) IncreaseScoreAuthority(amount); else { /* Log Warning */ } } else { GameManager.Instance?.LogManager?.LogError($"{PlayerName}: RPC_RequestScoreIncrease received but lacks State Authority!"); } }

    // --- Getters ---
    public Monster GetMonster() => _monsterManager?.GetPlayerMonster(); public Monster GetOpponentMonster() => _opponentMonsterRepresentation; public bool IsOpponentMonsterReady() => _opponentMonsterDataReady && _opponentMonsterRepresentation != null; public List<CardData> GetHand() => _cardManager?.GetHand() ?? new List<CardData>(); public PlayerRef GetOpponentPlayerRef() => _opponentPlayerRef; public int GetScore() => Score; public bool GetIsLocalPlayerTurn() => Object.HasInputAuthority && _isPlayerTurnActive && !_isFightOverLocally; public bool GetIsLocalFightOver() => _isFightOverLocally;

    // --- Setting Opponent ---
    public void SetOpponentMonsterLocally(PlayerRef opponentRef, PlayerState opponentState) { if (!Object.HasInputAuthority) return; if (opponentState == this || opponentRef == Object.InputAuthority) { /* Log Error */ return; } _opponentPlayerRef = opponentRef; _opponentPlayerState = opponentState; UpdateLocalOpponentRepresentation(); }

    // --- Despawn ---
    public override void Despawned(NetworkRunner runner, bool hasState) { base.Despawned(runner, hasState); if (GameState.Instance != null && Object != null && Object.IsValid) GameState.Instance.UnregisterPlayer(Object.InputAuthority); _cardManager = null; _monsterManager = null; _opponentMonsterRepresentation = null; _opponentPlayerState = null; }

} // End of PlayerState class