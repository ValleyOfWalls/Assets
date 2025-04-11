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

    public override void Spawned()
    {
        base.Spawned();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // Initialize local managers FIRST
        _cardManager = new CardManager();
        // Pass 'this' (the NetworkBehaviour) to MonsterManager
        _monsterManager = new MonsterManager(this, Object.InputAuthority); // Assumes MonsterManager needs the owner NB

        if (HasStateAuthority)
        {
            // Initialize default stats
            MaxHealth = STARTING_HEALTH;
            Health = MaxHealth;
            MaxEnergy = STARTING_ENERGY;
            Energy = MaxEnergy;
            Score = 0;
            IsFightComplete = false;

            // Initialize player name from associated Player object
            InitializePlayerName(); // Sets PlayerName Networked property

            // Monster creation requires PlayerName. It's called AFTER name is set.
             _monsterManager.CreatePlayerMonsterLocally(); // Create local first
             UpdateMonsterNetworkedProperties();         // Sync to network
        }

        // CRITICAL: Update local monster representation immediately after spawn,
        // using the potentially just-set networked values.
         UpdateLocalMonsterRepresentationFromNetworked();


        GameManager.Instance?.LogManager?.LogMessage($"PlayerState spawned for {PlayerName} (ID: {Id}, Auth: {HasStateAuthority}, InputAuth: {Object.HasInputAuthority})");

        // Register with GameState. This should succeed quickly now.
        StartCoroutine(RegisterWithGameStateWhenAvailable());

        // If this is the local player's state object, draw initial hand
        if (Object.HasInputAuthority)
        {
            DrawInitialHandLocally();
        }
    }

     // Coroutine to wait for GameState and register
    private IEnumerator RegisterWithGameStateWhenAvailable()
    {
         // Increased timeout slightly, although it should be fast now.
         float timer = 0;
         float timeout = 15f;

         // Log entry into coroutine
         GameManager.Instance?.LogManager?.LogMessage($"PlayerState {Id} ({PlayerName}): Starting RegisterWithGameState coroutine.");


         while (GameState.Instance == null || !GameState.Instance.IsSpawned())
         {
             timer += Time.deltaTime;
             if(timer > timeout)
             {
                  // Log the error with more context
                  GameManager.Instance?.LogManager?.LogError($"PlayerState {Id} ({PlayerName}): Timed out ({timeout}s) waiting for GameState.Instance to register. GameState.Instance is {(GameState.Instance == null ? "null" : "not null")}, IsSpawned: {GameState.Instance?.IsSpawned()}.");
                  yield break; // Exit coroutine on timeout
             }
             // Log waiting status periodically
             // if(Mathf.FloorToInt(timer) % 2 == 0) // Log every 2 seconds
             //     GameManager.Instance?.LogManager?.LogMessage($"PlayerState {Id} ({PlayerName}): Still waiting for GameState...");

             yield return new WaitForSeconds(0.1f); // Check less frequently
         }

          // Log successful finding of GameState
          GameManager.Instance?.LogManager?.LogMessage($"PlayerState {Id} ({PlayerName}): GameState.Instance found (ID: {GameState.Instance.Id}). Registering player {Object.InputAuthority}.");

         // Register with the valid GameState instance
         GameState.Instance.RegisterPlayer(Object.InputAuthority, this);

         // Log successful registration
         GameManager.Instance?.LogManager?.LogMessage($"PlayerState {Id} ({PlayerName}): Successfully registered with GameState.");
    }


    // Called every frame for rendering/interpolation. Use this for change detection.
     public override void Render()
     {
         if (_changeDetector == null || !Object.IsValid) return; // Guard clause, check if object is still valid

         try { // Add try-catch block for safety during Render

             // --- Fusion v2 Change Detection Loop ---
             foreach (var propertyName in _changeDetector.DetectChanges(this))
             {
                 // Verbose logging: Uncomment if needed
                 // GameManager.Instance?.LogManager?.LogMessage($"Detected change in '{propertyName}' for {PlayerName} (ID: {Id})");

                 // --- Handle specific property changes ---
                 if (propertyName == nameof(Health) ||
                     propertyName == nameof(MaxHealth) ||
                     propertyName == nameof(Energy) ||
                     propertyName == nameof(MaxEnergy) ||
                     propertyName == nameof(Score))
                 {
                     OnStatsChanged?.Invoke(this);
                 }
                 else if (propertyName == nameof(MonsterName) ||
                          propertyName == nameof(MonsterHealth) ||
                          propertyName == nameof(MonsterMaxHealth) ||
                          propertyName == nameof(MonsterAttack) ||
                          propertyName == nameof(MonsterDefense) ||
                          propertyName == nameof(MonsterColor))
                 {
                      // Update local representation of OWN monster
                     UpdateLocalMonsterRepresentationFromNetworked();

                      // Check if this change belongs to the opponent and update opponent representation
                     PlayerState localPlayerState = GameState.Instance?.GetLocalPlayerState();
                     if (localPlayerState != null && this != localPlayerState && localPlayerState.GetOpponentPlayerRef() == this.Object.InputAuthority)
                     {
                          // GameManager.Instance?.LogManager?.LogMessage($"Updating opponent monster representation for {localPlayerState.PlayerName} due to network change in {propertyName} for {this.PlayerName}.");
                          localPlayerState.UpdateOpponentMonsterRepresentation();
                     }
                      // Trigger general stat update as monster stats might affect UI
                      OnStatsChanged?.Invoke(this);
                 }
                 else if (propertyName == nameof(IsFightComplete))
                 {
                      // GameManager.Instance?.LogManager?.LogMessage($"Network Change: Fight completion status for {PlayerName} (ID: {Id}) is now {IsFightComplete}.");

                      if (Object.HasInputAuthority) // Only react if it's the local player's state
                      {
                           if (IsFightComplete && !_isFightOverLocally)
                           {
                               GameManager.Instance?.LogManager?.LogMessage($"Network confirms fight COMPLETE for local player {PlayerName}. Ending local simulation.");
                               EndFightLocally(true); // Assume win if network forces complete? Or determine based on state?
                           }
                           else if (!IsFightComplete && _isFightOverLocally)
                           {
                                GameManager.Instance?.LogManager?.LogMessage($"Network confirms fight NOT complete for local player {PlayerName} (likely new round). Resetting local fight state.");
                                ResetLocalFightStateForNewRound(); // Call the reset helper
                           }
                      }
                       // Update Opponent UI (if this state belongs to an opponent)
                       // This logic is typically handled in OpponentsUIController reacting to GameState events or polling
                 }
                 else if (propertyName == nameof(PlayerName))
                 {
                      // Update UI if the player name changes
                     OnStatsChanged?.Invoke(this);
                      // Potentially update Monster name if it depends on PlayerName
                     if (HasStateAuthority && _monsterManager?.GetPlayerMonster() != null && _monsterManager.GetPlayerMonster().Name.EndsWith("'s Monster"))
                     {
                          UpdateMonsterNetworkedProperties(); // Resync monster name if player name changed
                     }
                 }
             }
         } catch (Exception ex) {
              // Log any exception during Render to avoid breaking the game loop
              GameManager.Instance?.LogManager?.LogError($"Exception during PlayerState.Render for {PlayerName} (ID: {Id}): {ex.Message}\n{ex.StackTrace}");
         }
     }


    // Initialize PlayerName from the associated Player component
    private void InitializePlayerName()
    {
        if (!HasStateAuthority) return;
        Player playerComponent = GetAssociatedPlayerComponent();
        if (playerComponent != null)
        {
            string name = playerComponent.GetPlayerName();
            // Use a default if the component's name is empty, otherwise use component's name
            PlayerName = string.IsNullOrEmpty(name) ? $"Player_{Object.InputAuthority.PlayerId}" : name;
             GameManager.Instance?.LogManager?.LogMessage($"PlayerState {Id}: Initialized name to '{PlayerName}' from Player component.");
        }
        else
        {
            PlayerName = $"Player_{Object.InputAuthority.PlayerId}"; // Fallback
            // **FIXED:** Changed LogWarning to LogMessage
            GameManager.Instance?.LogManager?.LogMessage($"PlayerState {Id}: Could not find Player component to initialize name. Using default: '{PlayerName}'.");
        }
    }

     // Gets the Player component associated with this PlayerState
    private Player GetAssociatedPlayerComponent()
    {
        if (GameManager.Instance?.PlayerManager == null) return null;
        // Use InputAuthority of this PlayerState's NetworkObject to find the corresponding Player object
         if (Object == null) return null; // Check if NetworkObject is valid
        NetworkObject playerObject = GameManager.Instance.PlayerManager.GetPlayerObject(Object.InputAuthority);
        return playerObject?.GetComponent<Player>();
    }

    // Updates networked monster properties from the local MonsterManager
    // Should only be called by State Authority
    public void UpdateMonsterNetworkedProperties()
    {
        if (!HasStateAuthority) return;
        Monster monster = _monsterManager?.GetPlayerMonster(); // Use safe navigation
        if (monster != null)
        {
            // Ensure unique name based on current PlayerName
            string uniqueName = monster.Name;
             // Ensure PlayerName is valid before using it
            string currentPName = "";
            try { currentPName = PlayerName.ToString(); } catch { /* ignore, might not be ready */ }

            if (string.IsNullOrEmpty(uniqueName) || uniqueName == "Your Monster")
            {
                 uniqueName = !string.IsNullOrEmpty(currentPName) ? $"{currentPName}'s Monster" : $"Monster_{Id}";
                 monster.Name = uniqueName; // Update local representation too
            }


            // --- Update Networked Properties ---
            // Check if values actually changed before setting to minimize network traffic
            if (MonsterName != uniqueName) MonsterName = uniqueName;
            if (MonsterHealth != monster.Health) MonsterHealth = monster.Health;
            if (MonsterMaxHealth != monster.MaxHealth) MonsterMaxHealth = monster.MaxHealth;
            if (MonsterAttack != monster.Attack) MonsterAttack = monster.Attack;
            if (MonsterDefense != monster.Defense) MonsterDefense = monster.Defense;
            if (MonsterColor != monster.TintColor) MonsterColor = monster.TintColor;
        }
        else {
             // **FIXED:** Changed LogWarning to LogMessage
             GameManager.Instance?.LogManager?.LogMessage($"Authority {Id} ({PlayerName}): Tried UpdateMonsterNetworkedProperties but local monster was null.");
        }
    }

    // Updates the local Monster representation from networked data
    private void UpdateLocalMonsterRepresentationFromNetworked()
    {
        if (_monsterManager == null) {
            // GameManager.Instance?.LogManager?.LogWarning($"UpdateLocalMonsterRep: MonsterManager is null for {PlayerName} (ID: {Id}).");
             return; // MonsterManager might not be initialized yet
        }

        try { // Add try-catch for safety when accessing Networked properties
            _monsterManager.UpdateLocalMonsterFromNetworked(
                MonsterName.ToString(), // Convert NetworkString safely
                MonsterHealth,
                MonsterMaxHealth,
                MonsterAttack,
                MonsterDefense,
                MonsterColor
            );
            // Trigger local event for UI update AFTER the monster manager has updated the local monster instance
             Monster currentMonster = _monsterManager.GetPlayerMonster();
             if (currentMonster != null) { // Check if monster exists after update
                 OnPlayerMonsterChanged?.Invoke(currentMonster);
             }
        } catch (InvalidOperationException ex) {
             GameManager.Instance?.LogManager?.LogMessage($"UpdateLocalMonsterRep: Caught InvalidOperationException for {PlayerName} (ID: {Id}): {ex.Message}. Network properties might not be ready.");
        } catch (Exception ex) {
             GameManager.Instance?.LogManager?.LogError($"UpdateLocalMonsterRep: Unexpected error for {PlayerName} (ID: {Id}): {ex.Message}\n{ex.StackTrace}");
        }
    }


     // Updates the local representation of the opponent's monster
    private void UpdateOpponentMonsterRepresentation()
    {
        if (_opponentPlayerState != null && _opponentPlayerState.Object != null && _opponentPlayerState.Object.IsValid) // Check validity
        {
            // Create or update the local representation
            if (_opponentMonsterRepresentation == null)
            {
                 _opponentMonsterRepresentation = new Monster();
            }
            try { // Safe access to opponent's networked state
                _opponentMonsterRepresentation.Name = _opponentPlayerState.MonsterName.ToString();
                _opponentMonsterRepresentation.MaxHealth = _opponentPlayerState.MonsterMaxHealth;
                // Use SetHealth which handles events and clamping
                _opponentMonsterRepresentation.SetHealth(_opponentPlayerState.MonsterHealth);
                _opponentMonsterRepresentation.Attack = _opponentPlayerState.MonsterAttack;
                _opponentMonsterRepresentation.Defense = _opponentPlayerState.MonsterDefense;
                _opponentMonsterRepresentation.TintColor = _opponentPlayerState.MonsterColor;
                _opponentMonsterRepresentation.ResetBlockLocally(); // Block is turn-local

                // Trigger local event for UI update
                OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation);
             } catch (InvalidOperationException ex) {
                 GameManager.Instance?.LogManager?.LogMessage($"UpdateOpponentMonsterRep: Caught InvalidOperationException accessing opponent {(_opponentPlayerState?.PlayerName)} state: {ex.Message}");
                 // Optionally clear representation if opponent state is invalid?
                 // _opponentMonsterRepresentation = null;
                 // OnOpponentMonsterChanged?.Invoke(null);
             } catch (Exception ex) {
                 GameManager.Instance?.LogManager?.LogError($"UpdateOpponentMonsterRep: Unexpected error for opponent {(_opponentPlayerState?.PlayerName)}: {ex.Message}\n{ex.StackTrace}");
             }
        }
        else
        {
            // Opponent state is gone or invalid, clear the representation
            if (_opponentMonsterRepresentation != null)
            {
                _opponentMonsterRepresentation = null;
                OnOpponentMonsterChanged?.Invoke(null); // Notify UI
                // GameManager.Instance?.LogManager?.LogMessage($"Cleared opponent monster representation for {PlayerName}. Opponent state was null or invalid.");
            }
        }
    }


    // --- Local Hand Management ---

    public void DrawInitialHandLocally()
    {
        if (!Object.HasInputAuthority) return;
        if (_cardManager == null) return;

        _cardManager.CreateStartingDeck();
        _cardManager.ShuffleDeck();
        _cardManager.DrawToHandSize();
        OnHandChanged?.Invoke(this, _cardManager.GetHand());
    }

    public void DrawNewHandLocally()
    {
        if (!Object.HasInputAuthority) return;
        if (_cardManager == null) return;

        _cardManager.PrepareForNewRound();
        OnHandChanged?.Invoke(this, _cardManager.GetHand());
    }

    // --- Local Turn and Fight Logic ---

     public void PlayCardLocally(int cardIndex, Monster target)
    {
        if (!Object.HasInputAuthority || _isFightOverLocally || !_isPlayerTurnActive) return;
        if (_cardManager == null || _monsterManager == null) return; // Ensure managers exist

        List<CardData> hand = _cardManager.GetHand();
        if (cardIndex < 0 || cardIndex >= hand.Count) return;
        CardData card = hand[cardIndex];

        // Local energy check (use the networked property as source of truth)
         try { // Safe access
            if (Energy < card.EnergyCost)
            {
                 GameManager.Instance?.LogManager?.LogMessage($"Cannot play {card.Name}: Not enough energy ({Energy}/{card.EnergyCost}).");
                 return; // Not enough energy
            }
         } catch (InvalidOperationException) {
             GameManager.Instance?.LogManager?.LogMessage($"PlayCardLocally: Cannot check energy for {PlayerName}, state likely not ready.");
             return;
         }


        Monster playerMonster = _monsterManager.GetPlayerMonster();
        // Determine target validity
        bool isTargetingOwnMonster = (target != null && playerMonster != null && target == playerMonster);
        bool isTargetingOpponentMonster = (target != null && _opponentMonsterRepresentation != null && target == _opponentMonsterRepresentation);

        // Validate target based on card rules using PlayerStateValidator
        if (!PlayerStateValidator.IsValidMonsterTarget(card, target, isTargetingOwnMonster))
         {
              // Allow non-monster targeting cards (e.g., draw, energy gain) to proceed by assuming 'self' context
              if (card.Target != CardTarget.Self && card.Target != CardTarget.Enemy && card.Target != CardTarget.AllEnemies && card.Target != CardTarget.All)
              {
                   GameManager.Instance?.LogManager?.LogMessage($"Card {card.Name} doesn't target monsters, applying effects to player {PlayerName}.");
                   isTargetingOwnMonster = true; // Treat as targeting self for effect application
                   isTargetingOpponentMonster = false;
              }
              else if (target != null) // It required a monster target, but the provided one was invalid
              {
                    GameManager.Instance?.LogManager?.LogMessage($"Invalid target ({target.Name}) for card {card.Name}. Required: {card.Target}.");
                    return; // Invalid target
              } else { // Target was null, but card required a monster target
                    GameManager.Instance?.LogManager?.LogMessage($"Card {card.Name} requires a monster target, but none provided.");
                    return;
              }
         }


        // GameManager.Instance?.LogManager?.LogMessage($"Local player {PlayerName} playing {card.Name} on {(target != null ? target.Name : "Self")}.");

        // Request Energy change via RPC
        RPC_RequestModifyEnergy(-card.EnergyCost);

        // Apply LOCAL effects first for responsiveness
        ApplyCardEffectsLocally(card, target, isTargetingOwnMonster, isTargetingOpponentMonster);

        // If targeting opponent, send RPC to damage their actual networked monster state
        if (isTargetingOpponentMonster && card.DamageAmount > 0)
        {
            if (_opponentPlayerRef != default)
            {
                RPC_RequestApplyDamageToOpponent(_opponentPlayerRef, card.DamageAmount);
                // GameManager.Instance?.LogManager?.LogMessage($"Sent RPC to apply {card.DamageAmount} damage to opponent {_opponentPlayerRef}.");
            }
             // **FIXED:** Changed LogWarning to LogMessage
             // else { GameManager.Instance?.LogManager?.LogMessage($"Warning: Tried to damage opponent, but opponent ref is not set for {PlayerName}."); }
        }

        // Remove card from local hand
        _cardManager.PlayCard(cardIndex);
        OnHandChanged?.Invoke(this, _cardManager.GetHand()); // Update local UI

        // Check if the fight ended locally due to this card play
        CheckLocalFightEndCondition();
    }

     // Applies card effects to local representations or requests networked changes
    private void ApplyCardEffectsLocally(CardData card, Monster target, bool isTargetingOwn, bool isTargetingOpponent)
    {
        Monster ownMonster = _monsterManager?.GetPlayerMonster();

        if (isTargetingOwn && ownMonster != null)
        {
            if (card.BlockAmount > 0) ownMonster.AddBlock(card.BlockAmount);
            if (card.HealAmount > 0)
            {
                int oldHealth = ownMonster.Health;
                ownMonster.Heal(card.HealAmount);
                // Request health update via RPC ONLY if health actually changed
                 if(oldHealth != ownMonster.Health) RPC_RequestModifyMonsterHealth(ownMonster.Health);
            }
            OnPlayerMonsterChanged?.Invoke(ownMonster); // Update local UI for own monster
        }
        else if (isTargetingOpponent && _opponentMonsterRepresentation != null)
        {
            // Apply visual feedback locally immediately for damage, block breaking etc.
            // The actual health change comes from the network update via Render() later.
            if (card.DamageAmount > 0) {
                // Trigger opponent damage animation/VFX locally here
                // GameManager.Instance?.LogManager?.LogMessage($"Visually applying {card.DamageAmount} damage effect towards opponent representation {_opponentMonsterRepresentation.Name}.");
            }
            // We don't directly modify the opponent representation's health here.
            OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation); // Update local UI (maybe show predicted state?)
        }

        // Apply effects targeting the player directly (Energy/Card Draw)
        if (card.EnergyGain > 0)
        {
            RPC_RequestModifyEnergy(card.EnergyGain); // Request energy change
        }
        if (card.DrawAmount > 0 && Object.HasInputAuthority) // Only local player draws cards
        {
             if (_cardManager == null) return;
             for (int i = 0; i < card.DrawAmount; i++)
            {
                if (!_cardManager.DrawCard()) break; // Stop if deck/discard empty
            }
            OnHandChanged?.Invoke(this, _cardManager.GetHand()); // Update local UI
        }
    }

    // Request to apply damage to a specific opponent player's monster
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestApplyDamageToOpponent(PlayerRef opponentPlayerRef, int damageAmount, RpcInfo info = default)
    {
        if (GameState.Instance == null) {
             GameManager.Instance?.LogManager?.LogError("RPC_RequestApplyDamageToOpponent: GameState.Instance is null on Authority!");
             return;
        }
        PlayerState opponentState = GameState.Instance.GetPlayerState(opponentPlayerRef);
        if (opponentState != null && opponentState.Object != null && opponentState.Object.IsValid) // Check validity
        {
            // Call the target RPC on the opponent's state object instance
             opponentState.RPC_TakeDamageOnMonster(damageAmount);
            // GameManager.Instance?.LogManager?.LogMessage($"State Authority forwarded damage request ({damageAmount}) to {opponentPlayerRef} ({opponentState.PlayerName}).");
        }
        else {
             // **FIXED:** Changed LogWarning to LogMessage
             GameManager.Instance?.LogManager?.LogMessage($"State Authority could not find valid PlayerState for {opponentPlayerRef} to apply damage.");
        }
    }

     // This RPC runs *on the PlayerState instance that owns the monster* being damaged, called by State Authority
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TakeDamageOnMonster(int damageAmount, RpcInfo info = default)
    {
        if (!HasStateAuthority) return; // Only the authority for this object modifies its state

        Monster monster = _monsterManager?.GetPlayerMonster();
        if (monster != null)
        {
            int healthBefore = monster.Health;
            monster.TakeDamage(damageAmount); // Apply damage locally on the authority first
            int actualDamage = healthBefore - monster.Health;

            // Update the networked health property ONLY if it changed.
            if (MonsterHealth != monster.Health)
            {
                MonsterHealth = monster.Health; // Networked prop gets updated
                // GameManager.Instance?.LogManager?.LogMessage($"Authority {Id} ({PlayerName}): Monster took {actualDamage} damage. Health set to {MonsterHealth}/{MonsterMaxHealth}.");

                // Check if the monster was defeated by this damage (on authority)
                if (monster.IsDefeated())
                {
                    GameManager.Instance?.LogManager?.LogMessage($"Authority {Id} ({PlayerName}): Monster was defeated!");
                    SetFightCompleteStatus(true); // Update the networked flag
                }
            }
             //else { GameManager.Instance?.LogManager?.LogMessage($"Authority {Id} ({PlayerName}): Monster took {actualDamage} damage (blocked/reduced). Health remains {MonsterHealth}."); }
        }
         // **FIXED:** Changed LogWarning to LogMessage
         //else { GameManager.Instance?.LogManager?.LogMessage($"Warning: Authority {Id} ({PlayerName}): RPC_TakeDamageOnMonster called, but local monster was null."); }
    }


    // RPCs to request authoritative changes for non-authority clients
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestModifyEnergy(int amount, RpcInfo info = default)
    {
        ModifyEnergy(amount); // Executed on State Authority
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestModifyMonsterHealth(int newHealth, RpcInfo info = default)
    {
        ModifyMonsterHealth(newHealth); // Executed on State Authority
    }

    // Called by GameState to prepare all clients for a new round
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PrepareForNewRound(RpcInfo info = default)
    {
         GameManager.Instance?.LogManager?.LogMessage($"RPC_PrepareForNewRound received for {PlayerName} (ID: {Id}).");
         ResetLocalFightStateForNewRound();

         // State Authority specific resets (Energy, IsFightComplete, MonsterHealth)
         // are typically handled within GameState's StartNextRound *before* this RPC is sent,
         // or directly on the PlayerState by the authority if needed.
         // Clients will receive the updated networked values via the ChangeDetector in Render().

         // DrawNewHandLocally is now triggered by a separate RPC from GameState
    }

     // Helper method to reset local state variables
    private void ResetLocalFightStateForNewRound()
    {
         _isFightOverLocally = false;
         _isPlayerTurnActive = true; // Player usually starts
         _monsterManager?.ResetPlayerMonsterLocally(); // Resets local monster health/block
         UpdateOpponentMonsterRepresentation(); // Update opponent view (they likely reset too)

         // Trigger local UI updates
         OnLocalTurnStateChanged?.Invoke(_isPlayerTurnActive);
         OnLocalFightOver?.Invoke(false);
         OnStatsChanged?.Invoke(this); // General UI update
         // Ensure hand UI is also updated if needed (though GameState handles draw trigger)
          if (Object.HasInputAuthority && _cardManager != null) {
             OnHandChanged?.Invoke(this, _cardManager.GetHand());
         }
    }


    // --- Authority Methods (Modify Networked State) ---

    public void ModifyHealth(int amount)
    {
        if (!HasStateAuthority) return;
        Health = Mathf.Clamp(Health + amount, 0, MaxHealth);
    }

    public void ModifyMonsterHealth(int newHealth)
    {
        if (!HasStateAuthority) return;
        newHealth = Mathf.Clamp(newHealth, 0, MonsterMaxHealth);
        if (MonsterHealth != newHealth)
        {
            MonsterHealth = newHealth;
            // If health change causes defeat, mark fight as complete
             if (MonsterHealth <= 0 && !IsFightComplete)
             {
                 GameManager.Instance?.LogManager?.LogMessage($"Authority {Id} ({PlayerName}): Monster health reached 0. Setting fight complete.");
                 SetFightCompleteStatus(true);
             }
        }
    }

    public void ModifyEnergy(int amount)
    {
        if (!HasStateAuthority) return;
        Energy = Mathf.Clamp(Energy + amount, 0, MaxEnergy);
    }

     // Authority method to directly set energy (e.g., reset for turn start)
     public void SetEnergy(int value)
     {
          if (!HasStateAuthority) return;
          Energy = Mathf.Clamp(value, 0, MaxEnergy);
     }


    public void IncreaseScore(int amount = 1)
    {
        if (!HasStateAuthority) return;
        if (amount > 0) Score += amount;
    }

    private void SetFightCompleteStatus(bool isComplete)
    {
        if (!HasStateAuthority) return;
        // Only set if changed to avoid unnecessary network sync
        if (IsFightComplete != isComplete) {
             IsFightComplete = isComplete;
             GameManager.Instance?.LogManager?.LogMessage($"Authority {Id} ({PlayerName}): Set IsFightComplete to {isComplete}.");
             // If fight is now complete, notify GameState (authority only)
             if(isComplete && GameState.Instance != null) {
                 // GameState should ideally observe this change detector or be notified via another mechanism
                 // For now, maybe call GameState's check completion method? Requires careful design.
                 // GameState.Instance.CheckAllFightsComplete(); // Potential immediate check
             }
        }
    }

    // --- Local Fight Simulation ---

    public void EndPlayerTurnLocally()
    {
        if (!Object.HasInputAuthority || !_isPlayerTurnActive || _isFightOverLocally) return;
        // GameManager.Instance?.LogManager?.LogMessage($"Local player {PlayerName} ending their turn.");
        _isPlayerTurnActive = false;
        _monsterManager?.GetPlayerMonster()?.ResetBlockLocally(); // Player block decays
        OnPlayerMonsterChanged?.Invoke(_monsterManager?.GetPlayerMonster()); // Update UI

        OnLocalTurnStateChanged?.Invoke(false); // Notify UI it's monster's turn

        // Reset Energy locally for next turn preview? Or wait for network? For now, wait.
        // Don't start monster turn simulation automatically if fight might be over
         if (!CheckLocalFightEndCondition()) {
             StartCoroutine(SimulateMonsterTurnLocally());
         } else {
             GameManager.Instance?.LogManager?.LogMessage($"Fight ended for {PlayerName} during EndPlayerTurnLocally check.");
         }
    }

     private IEnumerator SimulateMonsterTurnLocally()
    {
        if (!Object.HasInputAuthority) yield break; // Only simulate for local player's perspective
        // GameManager.Instance?.LogManager?.LogMessage($"Simulating opponent's monster turn for {PlayerName}.");
        yield return new WaitForSeconds(1.0f); // Simulate thinking time

        if (_isFightOverLocally)
        {
            // GameManager.Instance?.LogManager?.LogMessage($"Monster turn skipped for {PlayerName}: Fight already over.");
            yield break;
        }

        Monster ownMonster = _monsterManager?.GetPlayerMonster();
        Monster opponentRep = _opponentMonsterRepresentation;

         // Ensure both monsters exist and opponent representation isn't defeated locally
        if (ownMonster != null && opponentRep != null && !opponentRep.IsDefeated())
        {
            CardData monsterAttack = opponentRep.ChooseAction(); // Get opponent's intended action
            // GameManager.Instance?.LogManager?.LogMessage($"Opponent's monster ({opponentRep.Name}) intends to use {monsterAttack.Name} on {ownMonster.Name} for {monsterAttack.DamageAmount} damage.");

            // Apply damage to OWN monster LOCALLY
            int healthBefore = ownMonster.Health;
            ownMonster.TakeDamage(monsterAttack.DamageAmount);
            OnPlayerMonsterChanged?.Invoke(ownMonster); // Update UI

            // Request the authoritative health change via RPC
             if (healthBefore != ownMonster.Health) { // Only send RPC if health actually changed
                 RPC_RequestModifyMonsterHealth(ownMonster.Health);
             }

            // Check if OUR monster was defeated by this simulated attack
            if (ownMonster.IsDefeated()) {
                 GameManager.Instance?.LogManager?.LogMessage($"Local simulation: {PlayerName}'s monster was defeated by opponent's attack.");
                 CheckLocalFightEndCondition(); // This will end the fight locally
            }
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"Monster turn skipped: Own ({ownMonster?.Name}), OpponentRep ({opponentRep?.Name}), OpponentRep Defeated ({opponentRep?.IsDefeated()})"); }

        // End the monster's turn simulation only if the fight didn't end
        if (!_isFightOverLocally)
        {
            EndMonsterTurnLocally();
        }
    }

    private void EndMonsterTurnLocally()
    {
        if (!Object.HasInputAuthority || _isFightOverLocally) return;
        // GameManager.Instance?.LogManager?.LogMessage($"Ending opponent's monster turn simulation for {PlayerName}.");

        // Reset block on the *representation* of the opponent monster for local UI
        _opponentMonsterRepresentation?.ResetBlockLocally();
        OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation); // Update UI

        // Double-check end condition
        if (CheckLocalFightEndCondition()) return;

        // It's player's turn again
        _isPlayerTurnActive = true;

        // Request energy replenish via RPC (request setting to MaxEnergy)
        RPC_RequestSetEnergy(MaxEnergy); // Use specific RPC for setting energy

        // Draw cards - Handled by GameState RPC now

        OnLocalTurnStateChanged?.Invoke(true); // Notify UI it's player's turn again
    }

     // RPC to specifically request setting energy to a value (e.g., max for turn start)
     [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
     private void RPC_RequestSetEnergy(int value, RpcInfo info = default)
     {
         SetEnergy(value); // Executed on State Authority
     }


    private bool CheckLocalFightEndCondition()
    {
        if (_isFightOverLocally || !Object.HasInputAuthority) return _isFightOverLocally; // Already over or not local player

        Monster playerMonster = _monsterManager?.GetPlayerMonster();
        Monster opponentMonsterRep = _opponentMonsterRepresentation;

        bool playerLost = playerMonster != null && playerMonster.IsDefeated();
        // Check opponent defeat based on the *networked* state if possible
        bool opponentLost = (_opponentPlayerState != null && _opponentPlayerState.Object != null && _opponentPlayerState.Object.IsValid) ?
                              _opponentPlayerState.MonsterHealth <= 0 :
                              (opponentMonsterRep != null && opponentMonsterRep.IsDefeated()); // Fallback to local rep

        bool playerWon = opponentLost;

        if (playerWon || playerLost)
        {
            string opponentStatus = (_opponentPlayerState != null && _opponentPlayerState.Object != null && _opponentPlayerState.Object.IsValid) ?
                 $"Opponent Networked HP: {_opponentPlayerState.MonsterHealth}" :
                 $"Opponent Local Rep HP: {opponentMonsterRep?.Health ?? -1}";
            GameManager.Instance?.LogManager?.LogMessage($"Local fight end condition met for {PlayerName}. Player Lost: {playerLost} (HP: {playerMonster?.Health ?? -1}), Player Won: {playerWon} ({opponentStatus}).");
            EndFightLocally(playerWon);
            return true;
        }
        return false;
    }

    private void EndFightLocally(bool didWin)
    {
        if (_isFightOverLocally || !Object.HasInputAuthority) return; // Already over or not local player
        _isFightOverLocally = true;
        _isPlayerTurnActive = false; // Turn ends

        GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Id}): Local fight simulation ended. Result: {(didWin ? "Win" : "Loss")}. Notifying GameState.");
        OnLocalFightOver?.Invoke(true);
        OnLocalTurnStateChanged?.Invoke(false);

        // If won, request score increase from authority
        if (didWin) RPC_RequestScoreIncrease();

        // Notify GameState (Authority) that this player finished their fight simulation
        if (GameState.Instance != null)
        {
            GameState.Instance.RPC_NotifyFightComplete(Object.InputAuthority);
        }

         // Optionally request the authority to set the networked IsFightComplete flag
         // This might be redundant if defeat already set it, but covers cases like conceding.
         RPC_RequestSetFightComplete(true);

    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSetFightComplete(bool status, RpcInfo info = default)
    {
        SetFightCompleteStatus(status); // Executed on State Authority
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestScoreIncrease(int amount = 1, RpcInfo info = default)
    {
        // Additional check on authority: Only increase score if the fight was actually won
         // Check opponent's actual networked health
         if (_opponentPlayerState != null && _opponentPlayerState.Object != null && _opponentPlayerState.Object.IsValid && _opponentPlayerState.MonsterHealth <= 0) {
             IncreaseScore(amount); // Executed on State Authority
             GameManager.Instance?.LogManager?.LogMessage($"Authority {Id} ({PlayerName}): Granted score increase. Opponent HP: {_opponentPlayerState.MonsterHealth}");
         } else {
             // **FIXED:** Changed LogWarning to LogMessage
             GameManager.Instance?.LogManager?.LogMessage($"Authority {Id} ({PlayerName}): Denied score increase request. Opponent ({_opponentPlayerState?.PlayerName}) monster HP is {_opponentPlayerState?.MonsterHealth}. State Valid: {_opponentPlayerState?.Object?.IsValid}");
         }
    }


    // --- Getters for Local Representations & State ---

    public Monster GetMonster() => _monsterManager?.GetPlayerMonster();
    public Monster GetOpponentMonster() => _opponentMonsterRepresentation;
    public List<CardData> GetHand() => _cardManager?.GetHand() ?? new List<CardData>(); // Return empty list if manager is null
    public PlayerRef GetOpponentPlayerRef() => _opponentPlayerRef;
    public int GetScore() => Score; // Directly return networked property
    public bool GetIsLocalPlayerTurn() => Object.HasInputAuthority && _isPlayerTurnActive && !_isFightOverLocally; // Check input authority too
    public bool GetIsLocalFightOver() => _isFightOverLocally;

    // --- Setting Opponent ---
     // Called locally by GameState's RPC_SetMonsterMatchup
    public void SetOpponentMonsterLocally(PlayerRef opponentRef, PlayerState opponentState)
    {
         if (!Object.HasInputAuthority && this != GameState.Instance?.GetLocalPlayerState()?._opponentPlayerState) return; // Optimization: Only local player or their direct opponent needs this

        _opponentPlayerRef = opponentRef;
        _opponentPlayerState = opponentState; // Cache the opponent's state
        string oppName = "None";
         try { oppName = opponentState?.PlayerName.ToString() ?? "Null State"; } catch { oppName = "State Invalid"; }
        // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ({Id}): Setting opponent locally to {opponentRef} ({oppName}).");
        UpdateOpponentMonsterRepresentation(); // Update local view immediately
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        // GameManager.Instance?.LogManager?.LogMessage($"PlayerState Despawned for {PlayerName} (ID: {Id})");

        // Unregister from GameState only if GameState still exists
         if (GameState.Instance != null && Object != null) // Check if Object is valid before accessing InputAuthority
         {
             GameState.Instance.UnregisterPlayer(Object.InputAuthority);
         }
         // Cleanup local stuff if necessary
         _cardManager = null;
         _monsterManager = null;
         _opponentMonsterRepresentation = null;
         _opponentPlayerState = null;

         // Consider unsubscribing static events if this object was the only listener,
         // but typically static events are managed elsewhere or persist.
    }
}