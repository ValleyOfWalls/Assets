using System;
using System.Collections;
using System.Collections.Generic;
using Fusion; // Ensure Fusion namespace is included
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
    [Networked] public NetworkBool IsFightComplete { get; private set; } // True if fight is done for the round

    // --- Local References and State ---
    private PlayerRef _opponentPlayerRef;       // Network reference to the opponent player
    private PlayerState _opponentPlayerState;   // Cached PlayerState of the opponent (might be null)
    private Monster _opponentMonsterRepresentation; // Local representation of the opponent's monster

    private CardManager _cardManager;           // Manages local deck, hand, discard
    private MonsterManager _monsterManager;     // Manages local representation of owned monster

    // --- Local Turn State ---
    private bool _isPlayerTurnActive = true; // Player starts their turn first in a fight
    private bool _isFightOverLocally = false; // Tracks if the *local* fight simulation is done

    // --- Fusion v2 Change Detector ---
    private ChangeDetector _changeDetector;

    // --- Events ---
    // These events are LOCAL and trigger UI updates on this client
    public static event Action<PlayerState> OnStatsChanged; // For player H/E/S changes (triggered by Render)
    public static event Action<PlayerState, List<CardData>> OnHandChanged; // When local hand changes (triggered by local actions)
    public static event Action<Monster> OnPlayerMonsterChanged; // When local player's monster data changes (triggered by Render or local actions)
    public static event Action<Monster> OnOpponentMonsterChanged; // When local representation of opponent monster changes (triggered by Render or local actions)
    public static event Action<bool> OnLocalTurnStateChanged; // When local turn switches (player/monster)
    public static event Action<bool> OnLocalFightOver; // When the local fight simulation ends

    // --- Constants ---
    private const int STARTING_HEALTH = 50;
    private const int STARTING_ENERGY = 3;

    public override void Spawned()
    {
        base.Spawned();

        // --- Initialize Fusion v2 Change Detector ---
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // Initialize local managers
        _cardManager = new CardManager();
        _monsterManager = new MonsterManager(this, Object.InputAuthority);

        if (HasStateAuthority)
        {
            // Initialize default stats (only by state authority)
            MaxHealth = STARTING_HEALTH;
            Health = MaxHealth;
            MaxEnergy = STARTING_ENERGY;
            Energy = MaxEnergy;
            Score = 0;
            IsFightComplete = false; // Start fight as incomplete

            // Initialize player name from associated Player object
            InitializePlayerName(); // Sets PlayerName Networked property

            // Create the monster locally first, then update networked properties
             // Needs PlayerName to be set first for unique monster name
             _monsterManager.CreatePlayerMonsterLocally();
             UpdateMonsterNetworkedProperties(); // Sync initial monster state
        }

        // Always create/update a local representation of the owned monster from current network state
        UpdateLocalMonsterRepresentationFromNetworked();

        GameManager.Instance?.LogManager?.LogMessage($"PlayerState spawned for {PlayerName} (ID: {Id}, Auth: {HasStateAuthority})");

        // Register with GameState
        StartCoroutine(RegisterWithGameStateWhenAvailable());

        // If this is the local player's state object, draw initial hand
        if (Object.HasInputAuthority)
        {
            DrawInitialHandLocally();
        }
    }

    // Called every frame for rendering/interpolation. Use this for change detection.
    public override void Render()
    {
        if (_changeDetector == null) return; // Guard clause

        // --- Fusion v2 Change Detection Loop ---
        foreach (var propertyName in _changeDetector.DetectChanges(this))
        {
             // GameManager.Instance?.LogManager?.LogMessage($"Detected change in '{propertyName}' for {PlayerName}"); // Verbose logging

            // --- Handle specific property changes ---
            if (propertyName == nameof(Health) ||
                propertyName == nameof(MaxHealth) ||
                propertyName == nameof(Energy) ||
                propertyName == nameof(MaxEnergy) ||
                propertyName == nameof(Score))
            {
                // Trigger local UI update for player stats
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

                // Also check if this change belongs to the opponent and update opponent representation
                PlayerState localPlayerState = GameState.Instance?.GetLocalPlayerState();
                 // Check if the state that changed IS the opponent of the local player
                if (localPlayerState != null && localPlayerState._opponentPlayerRef == this.Object.InputAuthority)
                {
                    // GameManager.Instance?.LogManager?.LogMessage($"Updating opponent monster representation due to network change in {propertyName} for {this.PlayerName}.");
                    localPlayerState.UpdateOpponentMonsterRepresentation(); // Tell local player state to update its view of this opponent
                }
                 // Optionally trigger OnStatsChanged as well if monster stats affect general UI
                 OnStatsChanged?.Invoke(this); // Trigger general update
            }
            else if (propertyName == nameof(IsFightComplete))
            {
                 // Handle fight completion status change (e.g., update UI state)
                 // GameManager.Instance?.LogManager?.LogMessage($"Fight completion status changed for {PlayerName}: {IsFightComplete}.");
                 if(Object.HasInputAuthority) // Check if this change applies to the local player
                 {
                      // If the networked flag says the fight is complete, ensure local state matches
                     if(IsFightComplete && !_isFightOverLocally)
                     {
                          GameManager.Instance?.LogManager?.LogMessage($"Network indicates fight complete for local player {PlayerName}. Ending local fight simulation.");
                           // Determine win/loss based on score comparison if needed, otherwise just end
                           bool assumedWin = (_opponentPlayerState != null) ? (Score > _opponentPlayerState.Score) : false;
                          EndFightLocally(assumedWin); // Ensure local fight logic stops
                     }
                      // If network says fight is NOT complete, but locally it is (e.g. new round start), reset local state
                     else if (!IsFightComplete && _isFightOverLocally)
                     {
                           GameManager.Instance?.LogManager?.LogMessage($"Network indicates fight incomplete for local player {PlayerName} (likely new round). Resetting local fight state.");
                          _isFightOverLocally = false;
                          _isPlayerTurnActive = true; // Assume player starts turn
                          OnLocalFightOver?.Invoke(false);
                          OnLocalTurnStateChanged?.Invoke(true);
                     }
                 }
                  // Update UI for opponent completion status if this PlayerState is the opponent
                 PlayerState localPs = GameState.Instance?.GetLocalPlayerState();
                 if(localPs != null && localPs._opponentPlayerRef == this.Object.InputAuthority)
                 {
                      // Update opponent UI elements based on this.IsFightComplete
                 }
            }
            else if (propertyName == nameof(PlayerName))
            {
                 // Update UI if the player name changes
                 OnStatsChanged?.Invoke(this);
            }
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
            PlayerName = string.IsNullOrEmpty(name) ? $"Player_{Object.InputAuthority.PlayerId}" : name;
            // GameManager.Instance?.LogManager?.LogMessage($"PlayerState set name to: {PlayerName}");
        }
        else
        {
            PlayerName = $"Player_{Object.InputAuthority.PlayerId}"; // Fallback
            GameManager.Instance?.LogManager?.LogMessage($"PlayerState could not find Player component, using default name: {PlayerName}");
        }
    }

    // Gets the Player component associated with this PlayerState
    private Player GetAssociatedPlayerComponent()
    {
        // Access PlayerManager safely
        if (GameManager.Instance == null || GameManager.Instance.PlayerManager == null) return null;
        NetworkObject playerObject = GameManager.Instance.PlayerManager.GetPlayerObject(Object.InputAuthority);
        return playerObject?.GetComponent<Player>();
    }

    // Updates networked monster properties from the local MonsterManager
    // Should only be called by State Authority
    public void UpdateMonsterNetworkedProperties() // Made public for call from MonsterManager if needed
    {
        if (!HasStateAuthority) return;

        Monster monster = _monsterManager.GetPlayerMonster();
        if (monster != null)
        {
            // Ensure unique name based on current PlayerName
             string uniqueName = monster.Name;
            if (uniqueName == "Your Monster" || string.IsNullOrEmpty(uniqueName))
            {
                 uniqueName = $"{PlayerName}'s Monster";
                 monster.Name = uniqueName; // Update local name too
            }


            // --- Update Networked Properties ---
            var currentName = uniqueName;
            var currentHealth = monster.Health;
            var currentMaxHealth = monster.MaxHealth;
            var currentAttack = monster.Attack;
            var currentDefense = monster.Defense;
            var currentColor = monster.TintColor;

            // Check if values actually changed before setting networked props
            if (MonsterName != currentName) MonsterName = currentName;
            if (MonsterHealth != currentHealth) MonsterHealth = currentHealth;
            if (MonsterMaxHealth != currentMaxHealth) MonsterMaxHealth = currentMaxHealth;
            if (MonsterAttack != currentAttack) MonsterAttack = currentAttack;
            if (MonsterDefense != currentDefense) MonsterDefense = currentDefense;
            if (MonsterColor != currentColor) MonsterColor = currentColor;
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"Warning: Authority tried UpdateMonsterNetworkedProperties for {PlayerName} but local monster was null."); }
    }

    // Updates the local Monster representation from networked data
    private void UpdateLocalMonsterRepresentationFromNetworked()
    {
        if (_monsterManager == null) // Ensure manager exists
        {
             // This might happen if Render runs before Spawned fully completes init
             // GameManager.Instance?.LogManager?.LogMessage($"Warning: UpdateLocalMonsterRepresentationFromNetworked called before MonsterManager initialized for {PlayerName}.");
             return;
        }
        _monsterManager.UpdateLocalMonsterFromNetworked(
            MonsterName.ToString(), // Convert NetworkString
            MonsterHealth,
            MonsterMaxHealth,
            MonsterAttack,
            MonsterDefense,
            MonsterColor
        );
        // Trigger local event for UI update AFTER the monster manager has updated the local monster instance
        OnPlayerMonsterChanged?.Invoke(_monsterManager.GetPlayerMonster());
    }

    // Updates the local representation of the opponent's monster
    private void UpdateOpponentMonsterRepresentation()
    {
        if (_opponentPlayerState != null)
        {
            // Create or update the local representation
            if (_opponentMonsterRepresentation == null)
            {
                _opponentMonsterRepresentation = new Monster();
            }

            _opponentMonsterRepresentation.Name = _opponentPlayerState.MonsterName.ToString();
            // Read opponent's networked health directly for the representation
            _opponentMonsterRepresentation.MaxHealth = _opponentPlayerState.MonsterMaxHealth;
            _opponentMonsterRepresentation.SetHealth(_opponentPlayerState.MonsterHealth);
            _opponentMonsterRepresentation.Attack = _opponentPlayerState.MonsterAttack;
            _opponentMonsterRepresentation.Defense = _opponentPlayerState.MonsterDefense;
            _opponentMonsterRepresentation.TintColor = _opponentPlayerState.MonsterColor;

            // Reset block for the representation (block is usually turn-local)
            _opponentMonsterRepresentation.ResetBlockLocally();

            // Trigger local event for UI update
            OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation);
        }
        else
        {
            // Opponent state is gone, clear the representation
            if (_opponentMonsterRepresentation != null)
            {
                _opponentMonsterRepresentation = null;
                OnOpponentMonsterChanged?.Invoke(null); // Notify UI
                // GameManager.Instance?.LogManager?.LogMessage($"Cleared opponent monster representation for {PlayerName}.");
            }
        }
    }


    // Coroutine to wait for GameState and register
    private IEnumerator RegisterWithGameStateWhenAvailable()
    {
         float timer = 0;
         float timeout = 10f; // Wait up to 10 seconds
        while (GameState.Instance == null || !GameState.Instance.IsSpawned())
        {
             timer += Time.deltaTime;
             if(timer > timeout)
             {
                  GameManager.Instance?.LogManager?.LogError($"PlayerState {PlayerName}: Timed out waiting for GameState to register.");
                  yield break;
             }
            yield return null; // Wait for next frame
        }
        GameState.Instance.RegisterPlayer(Object.InputAuthority, this);
    }

    // --- Local Hand Management ---

    public void DrawInitialHandLocally()
    {
        if (!Object.HasInputAuthority) return; // Only local player draws their hand
        if (_cardManager == null) return;

        _cardManager.CreateStartingDeck(); // Ensure deck is ready
        _cardManager.ShuffleDeck();
        _cardManager.DrawToHandSize();
        OnHandChanged?.Invoke(this, _cardManager.GetHand()); // Notify local UI
        // GameManager.Instance?.LogManager?.LogMessage($"Local player {PlayerName} drew initial hand locally.");
    }

    public void DrawNewHandLocally()
    {
        if (!Object.HasInputAuthority) return;
         if (_cardManager == null) return;

        _cardManager.PrepareForNewRound(); // Discards old, draws new
        OnHandChanged?.Invoke(this, _cardManager.GetHand()); // Notify local UI
        // GameManager.Instance?.LogManager?.LogMessage($"Local player {PlayerName} drew new hand locally.");
    }

    // --- Local Turn and Fight Logic ---

    public void PlayCardLocally(int cardIndex, Monster target)
    {
        if (!Object.HasInputAuthority || _isFightOverLocally || !_isPlayerTurnActive) return;
        if (_cardManager == null) return;


        List<CardData> hand = _cardManager.GetHand();
        if (cardIndex < 0 || cardIndex >= hand.Count) return;

        CardData card = hand[cardIndex];

        // Local energy check (use the networked property as source of truth)
        if (Energy < card.EnergyCost)
        {
            // GameManager.Instance?.LogManager?.LogMessage($"Cannot play {card.Name}: Not enough energy ({Energy}/{card.EnergyCost}).");
            return; // Not enough energy
        }

        Monster playerMonster = _monsterManager?.GetPlayerMonster();
        bool isTargetingOwnMonster = (target != null && playerMonster != null && target == playerMonster);
        bool isTargetingOpponentMonster = (target != null && _opponentMonsterRepresentation != null && target == _opponentMonsterRepresentation);

        if (!isTargetingOwnMonster && !isTargetingOpponentMonster)
        {
             // GameManager.Instance?.LogManager?.LogMessage($"PlayCardLocally: Target ({target?.Name}) is neither own monster nor opponent representation.");
             // Allow targeting if card doesn't require a monster (e.g., draw card, gain energy)
              if(card.Target != CardTarget.Self && card.Target != CardTarget.Enemy && card.Target != CardTarget.AllEnemies) {
                   // Assume effects apply to self if no valid monster target needed/provided
                   isTargetingOwnMonster = true; // Treat as self-target for effects like EnergyGain/DrawAmount
                   GameManager.Instance?.LogManager?.LogMessage($"Card {card.Name} target is not monster-specific, applying effects to self.");
              } else {
                   GameManager.Instance?.LogManager?.LogMessage($"Invalid target ({target?.Name}) for card {card.Name}. Requires specific monster target.");
                   return; // Invalid target if card requires one
              }
        }


        // Validate target based on card rules
        if (isTargetingOwnMonster && !(card.Target == CardTarget.Self || card.Target == CardTarget.All))
        {
             GameManager.Instance?.LogManager?.LogMessage($"Invalid target: Card {card.Name} cannot target Self.");
             return;
        }
         if (isTargetingOpponentMonster && !(card.Target == CardTarget.Enemy || card.Target == CardTarget.AllEnemies || card.Target == CardTarget.All))
        {
             GameManager.Instance?.LogManager?.LogMessage($"Invalid target: Card {card.Name} cannot target Enemy.");
             return;
        }


        // GameManager.Instance?.LogManager?.LogMessage($"Local player {PlayerName} playing {card.Name} on {(target != null ? target.Name : "Self")}.");

        // Request Energy change via RPC (handles authority check inside)
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
             // else { GameManager.Instance?.LogManager?.LogMessage($"Warning: Tried to damage opponent, but opponent ref is not set."); }
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
        if (isTargetingOwn)
        {
            Monster ownMonster = _monsterManager?.GetPlayerMonster();
             if (ownMonster == null) return; // Safety check

            if (card.BlockAmount > 0) ownMonster.AddBlock(card.BlockAmount);
            if (card.HealAmount > 0)
            {
                int oldHealth = ownMonster.Health;
                ownMonster.Heal(card.HealAmount);
                // Request health update via RPC (handles authority check inside)
                 if(oldHealth != ownMonster.Health) RPC_RequestModifyMonsterHealth(ownMonster.Health);
            }
            OnPlayerMonsterChanged?.Invoke(ownMonster); // Update local UI for own monster
        }
        else if (isTargetingOpponent)
        {
            if (_opponentMonsterRepresentation != null && card.DamageAmount > 0)
            {
                 // Trigger visual feedback locally
                 // GameManager.Instance?.LogManager?.LogMessage($"Visually applying {card.DamageAmount} damage to opponent representation {_opponentMonsterRepresentation.Name}.");
                 // TODO: Trigger opponent damage animation/VFX locally here
            }
            OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation); // Update local UI for opponent monster
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
                _cardManager.DrawCard();
            }
            OnHandChanged?.Invoke(this, _cardManager.GetHand()); // Update local UI
        }
    }

    // Request to apply damage to a specific opponent player's monster
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestApplyDamageToOpponent(PlayerRef opponentPlayerRef, int damageAmount, RpcInfo info = default)
    {
        PlayerState opponentState = GameState.Instance?.GetPlayerState(opponentPlayerRef);
        if (opponentState != null)
        {
            opponentState.RPC_TakeDamageOnMonster(damageAmount); // Call the target RPC on the opponent's state object
            // GameManager.Instance?.LogManager?.LogMessage($"State Authority forwarded damage request to {opponentPlayerRef}.");
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"Warning: State Authority could not find PlayerState for {opponentPlayerRef} to apply damage."); }
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

            // Update the networked health property ONLY if it changed
            // The change detection in Render() on other clients will pick this up.
            if (MonsterHealth != monster.Health)
            {
                MonsterHealth = monster.Health;
                // GameManager.Instance?.LogManager?.LogMessage($"Authority: {PlayerName}'s monster took {actualDamage} damage. Health set to {MonsterHealth}/{MonsterMaxHealth}.");

                // Check if the monster was defeated by this damage (on authority)
                if (monster.IsDefeated())
                {
                    // GameManager.Instance?.LogManager?.LogMessage($"Authority: {PlayerName}'s monster was defeated!");
                    SetFightCompleteStatus(true); // Update the networked flag
                }
            }
            // else { GameManager.Instance?.LogManager?.LogMessage($"Authority: {PlayerName}'s monster took {actualDamage} damage (blocked/reduced). Health remains {MonsterHealth}."); }
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"Warning: Authority: RPC_TakeDamageOnMonster called for {PlayerName}, but local monster was null."); }
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
        // GameManager.Instance?.LogManager?.LogMessage($"RPC_PrepareForNewRound received for {PlayerName}.");
        // Reset local state for the new round/fight
        _isFightOverLocally = false;
        _isPlayerTurnActive = true; // Player usually starts
        _monsterManager?.ResetPlayerMonsterLocally(); // Resets local monster health/block

        // If this is the local player, draw a new hand (Triggered separately by GameState)
        // DrawNewHandLocally(); // Don't call here, GameState triggers this via separate RPC

        // Update opponent representation (their state might have reset too)
        UpdateOpponentMonsterRepresentation();

        // Trigger local UI updates
        OnLocalTurnStateChanged?.Invoke(_isPlayerTurnActive);
        OnLocalFightOver?.Invoke(false);
        OnStatsChanged?.Invoke(this); // Trigger general UI update

        // State Authority specific resets happen within GameState's StartNextRound
        // Networked properties (Energy, IsFightComplete, MonsterHealth) are reset there

        // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} prepared locally for new round.");
    }



    // --- Authority Methods (Modify Networked State) ---

    public void ModifyHealth(int amount)
    {
        if (!HasStateAuthority) return;
        int oldHealth = Health;
        Health = Mathf.Clamp(Health + amount, 0, MaxHealth);
    }

    public void ModifyMonsterHealth(int newHealth)
    {
        if (!HasStateAuthority) return;
        newHealth = Mathf.Clamp(newHealth, 0, MonsterMaxHealth);
        if (MonsterHealth != newHealth)
        {
            MonsterHealth = newHealth;
            if (MonsterHealth <= 0) // Check defeat on authority after change
            {
                SetFightCompleteStatus(true);
            }
        }
    }

    public void ModifyEnergy(int amount)
    {
        if (!HasStateAuthority) return;
        int oldEnergy = Energy;
        Energy = Mathf.Clamp(Energy + amount, 0, MaxEnergy);
    }

    public void IncreaseScore(int amount = 1)
    {
        if (!HasStateAuthority) return;
        Score += amount;
    }

    private void SetFightCompleteStatus(bool isComplete)
    {
        if (!HasStateAuthority) return;
        if (IsFightComplete != isComplete) IsFightComplete = isComplete;
    }

    // --- Local Fight Simulation ---

    public void EndPlayerTurnLocally()
    {
        if (!Object.HasInputAuthority || !_isPlayerTurnActive || _isFightOverLocally) return;

        // GameManager.Instance?.LogManager?.LogMessage($"Local player {PlayerName} ending their turn.");
        _isPlayerTurnActive = false;
        _monsterManager?.GetPlayerMonster()?.ResetBlockLocally(); // Player block decays at end of turn
        OnPlayerMonsterChanged?.Invoke(_monsterManager?.GetPlayerMonster()); // Update UI

        OnLocalTurnStateChanged?.Invoke(false); // Notify UI it's monster's turn

        StartCoroutine(SimulateMonsterTurnLocally());
    }

    private IEnumerator SimulateMonsterTurnLocally()
    {
        // GameManager.Instance?.LogManager?.LogMessage($"Simulating monster turn for {PlayerName}.");
        yield return new WaitForSeconds(1.0f); // Simulate thinking time

        if (_isFightOverLocally)
        {
            // GameManager.Instance?.LogManager?.LogMessage($"Monster turn skipped for {PlayerName}: Fight already over.");
            yield break;
        }

        Monster ownMonster = _monsterManager?.GetPlayerMonster();
        Monster opponentRep = _opponentMonsterRepresentation;

        if (ownMonster != null && opponentRep != null && !opponentRep.IsDefeated())
        {
            CardData monsterAttack = opponentRep.ChooseAction();
            // GameManager.Instance?.LogManager?.LogMessage($"Monster ({opponentRep.Name}) attacks {ownMonster.Name} for {monsterAttack.DamageAmount} damage.");

            int healthBefore = ownMonster.Health;
            ownMonster.TakeDamage(monsterAttack.DamageAmount);
            OnPlayerMonsterChanged?.Invoke(ownMonster); // Update UI

            // Request health update via RPC
            RPC_RequestModifyMonsterHealth(ownMonster.Health);

            if (ownMonster.IsDefeated()) CheckLocalFightEndCondition();
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"Monster turn skipped: Own monster ({ownMonster?.Name}), Opponent ({opponentRep?.Name}), Opponent Defeated ({opponentRep?.IsDefeated()})"); }

        if (!_isFightOverLocally) EndMonsterTurnLocally();
    }

    private void EndMonsterTurnLocally()
    {
        if (_isFightOverLocally) return;

        // GameManager.Instance?.LogManager?.LogMessage($"Ending monster turn for {PlayerName}.");
        _opponentMonsterRepresentation?.ResetBlockLocally();
        OnOpponentMonsterChanged?.Invoke(_opponentMonsterRepresentation); // Update UI

        if (CheckLocalFightEndCondition()) return;

        _isPlayerTurnActive = true;
        // Request energy replenish via RPC (request setting to MaxEnergy)
        RPC_RequestModifyEnergy(MaxEnergy);

        // Draw cards locally - triggered by GameState's RPC_TriggerLocalNewRoundDraw now
        // DrawNewHandLocally(); // Don't call here

        OnLocalTurnStateChanged?.Invoke(true); // Notify UI it's player's turn again
    }


    private bool CheckLocalFightEndCondition()
    {
        if (_isFightOverLocally) return true;

        Monster playerMonster = _monsterManager?.GetPlayerMonster();
        Monster opponentMonsterRep = _opponentMonsterRepresentation;

        bool playerLost = playerMonster != null && playerMonster.IsDefeated();
        bool opponentLostNetworked = _opponentPlayerState != null && _opponentPlayerState.MonsterHealth <= 0;
        bool opponentLostLocalRep = opponentMonsterRep != null && opponentMonsterRep.IsDefeated();
        bool playerWon = opponentLostNetworked || opponentLostLocalRep;

        if (playerWon || playerLost)
        {
             // string opponentStatus = _opponentPlayerState != null ? $"Opponent Networked HP: {_opponentPlayerState.MonsterHealth}" : $"Opponent Local Rep HP: {opponentMonsterRep?.Health ?? -1}";
             // GameManager.Instance?.LogManager?.LogMessage($"Local fight end check for {PlayerName}. Player Lost: {playerLost} (HP: {playerMonster?.Health ?? -1}), Player Won: {playerWon} ({opponentStatus}).");
            EndFightLocally(playerWon);
            return true;
        }
        return false;
    }

    private void EndFightLocally(bool didWin)
    {
        if (_isFightOverLocally) return;

        _isFightOverLocally = true;
        _isPlayerTurnActive = false;

        // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} ended fight locally. Result: {(didWin ? "Win" : "Loss")}");
        OnLocalFightOver?.Invoke(true);
        OnLocalTurnStateChanged?.Invoke(false);

        if (didWin) RPC_RequestScoreIncrease();

        if (GameState.Instance != null && Object.HasInputAuthority)
        {
            // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} sending RPC_NotifyFightComplete to GameState.");
            GameState.Instance.RPC_NotifyFightComplete(Object.InputAuthority);
        }

        if (!HasStateAuthority) RPC_RequestSetFightComplete(true);
        else if (!IsFightComplete) SetFightCompleteStatus(true); // Authority sets directly if needed
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSetFightComplete(bool status, RpcInfo info = default)
    {
        SetFightCompleteStatus(status); // Executed on State Authority
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestScoreIncrease(int amount = 1, RpcInfo info = default)
    {
        IncreaseScore(amount); // Executed on State Authority
    }


    // --- Getters for Local Representations & State ---

    public Monster GetMonster() => _monsterManager?.GetPlayerMonster();
    public Monster GetOpponentMonster() => _opponentMonsterRepresentation;
    public List<CardData> GetHand() => _cardManager?.GetHand() ?? new List<CardData>(); // Return empty list if manager is null
    public PlayerRef GetOpponentPlayerRef() => _opponentPlayerRef;
    public int GetScore() => Score;
    public bool GetIsLocalPlayerTurn() => _isPlayerTurnActive && !_isFightOverLocally;
    public bool GetIsLocalFightOver() => _isFightOverLocally;


    // --- Setting Opponent ---
    public void SetOpponentMonsterLocally(PlayerRef opponentRef, PlayerState opponentState)
    {
        _opponentPlayerRef = opponentRef;
        _opponentPlayerState = opponentState;
        // GameManager.Instance?.LogManager?.LogMessage($"{PlayerName} setting opponent locally to {opponentState?.PlayerName ?? "None"}.");
        UpdateOpponentMonsterRepresentation(); // Update local view immediately
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        // GameManager.Instance?.LogManager?.LogMessage($"PlayerState Despawned for {PlayerName}");
        if (GameState.Instance != null) GameState.Instance.UnregisterPlayer(Object.InputAuthority);
    }
}