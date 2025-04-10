using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    // Stats (networked)
    [Networked] public int Health { get; private set; }
    [Networked] public int MaxHealth { get; private set; }
    [Networked] public int Energy { get; private set; }
    [Networked] public int MaxEnergy { get; private set; }
    [Networked] public int Score { get; private set; }
    [Networked] public NetworkString<_32> PlayerName { get; private set; }

    // Monster stats (networked)
    [Networked] public NetworkString<_32> MonsterName { get; private set; }
    [Networked] public int MonsterHealth { get; private set; }
    [Networked] public int MonsterMaxHealth { get; private set; }
    [Networked] public int MonsterAttack { get; private set; }
    [Networked] public int MonsterDefense { get; private set; }
    [Networked] public Color MonsterColor { get; private set; }

    // Opponent monster references - stored locally
    private PlayerRef _opponentPlayerRef;
    private PlayerState _opponentPlayerState;

    // Hand cards
    [Networked, Capacity(10)]
    private NetworkArray<NetworkedCardData> _networkedHand { get; }

    // Local card collections
    private List<CardData> _deck = new List<CardData>();
    private List<CardData> _hand = new List<CardData>();
    private List<CardData> _discardPile = new List<CardData>();
    
    // Local monster instance
    private Monster _playerMonster;
    private Monster _opponentMonster;
    
    // Events
    public static event Action<PlayerState> OnStatsChanged;
    public static event Action<PlayerState, List<CardData>> OnHandChanged;

    // Constants
    private const int STARTING_HEALTH = 50;
    private const int STARTING_ENERGY = 3;
    private const int HAND_SIZE = 5;
    private const int MAX_HAND_SIZE = 10;

    // Change detector
    private ChangeDetector _changeDetector;
    
    // Track previous values for local change detection
    private int _previousHealth;
    private int _previousEnergy;
    private int _previousScore;
    private int _previousMonsterHealth;

    public override void Spawned()
    {
        base.Spawned();
        
        if (HasStateAuthority)
        {
            // Initialize default stats
            MaxHealth = STARTING_HEALTH;
            Health = MaxHealth;
            MaxEnergy = STARTING_ENERGY;
            Energy = MaxEnergy;
            Score = 0;
            
            // Get name from the Player component
            var networkRunner = GameManager.Instance.NetworkManager.GetRunner();
            if (networkRunner != null)
            {
                Player playerComponent = null;
                NetworkObject playerObject = GameManager.Instance.PlayerManager.GetPlayerObject(Object.InputAuthority);
                
                if (playerObject != null)
                {
                    playerComponent = playerObject.GetComponent<Player>();
                }
                
                if (playerComponent != null)
                {
                    PlayerName = playerComponent.GetPlayerName();
                    GameManager.Instance.LogManager.LogMessage($"PlayerState set name to: {PlayerName}");
                }
                else
                {
                    // Fallback to default name
                    PlayerName = $"Player_{Object.InputAuthority.PlayerId}";
                    GameManager.Instance.LogManager.LogMessage($"PlayerState using default name: {PlayerName}");
                }
            }
            
            // Initialize the player's monster
            InitializeMonster();
            
            // Create starting deck
            CreateStartingDeck();
        }
        
        // Initialize change detector for networked properties
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        
        // Store initial values for local change detection
        _previousHealth = Health;
        _previousEnergy = Energy;
        _previousScore = Score;
        _previousMonsterHealth = MonsterHealth;
        
        GameManager.Instance.LogManager.LogMessage($"PlayerState spawned for {PlayerName}");
        
        // Attempt to register with GameState if available, otherwise schedule for later
        AttemptRegisterWithGameState();
        
        // Start a coroutine to keep trying to register with GameState if it's not available yet
        StartCoroutine(RegisterWithGameStateWhenAvailable());
        
        // Create local monster instance to represent networked data
        RecreateMonsterFromNetworkedData();
    }

    public override void Render()
    {
        base.Render();
        
        // Check for changes in networked properties
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            GameManager.Instance.LogManager.LogMessage($"Detected network change in {change} for {PlayerName}");
            
            if (change == nameof(_networkedHand))
            {
                UpdateLocalHandFromNetworked();
            }
            else if (change.StartsWith("Monster"))
            {
                // Only update local monster from networked data if we own it
                if (HasInputAuthority)
                {
                    GameManager.Instance.LogManager.LogMessage($"Updating local monster from networked data: {change}");
                    RecreateMonsterFromNetworkedData();
                    
                    // Force an update to the UI
                    OnStatsChanged?.Invoke(this);
                }
            }
            else if (change == nameof(Health) || change == nameof(MaxHealth) ||
                    change == nameof(Energy) || change == nameof(MaxEnergy) ||
                    change == nameof(Score))
            {
                // Notify UI about stats change
                OnStatsChanged?.Invoke(this);
            }
        }
        
        // Also check for changes in properties even if we're not the state authority
        if (!HasStateAuthority)
        {
            // Only check for our own monster's health changes
            if (HasInputAuthority && _previousMonsterHealth != MonsterHealth && _playerMonster != null)
            {
                GameManager.Instance.LogManager.LogMessage($"Direct update to monster health: {_previousMonsterHealth} -> {MonsterHealth}");
                _previousMonsterHealth = MonsterHealth;
                _playerMonster.Health = MonsterHealth;
                
                // Ensure UI updates
                OnStatsChanged?.Invoke(this);
            }
        }
    }

    public void DrawInitialHand()
    {
        if (HasStateAuthority)
        {
            _hand.Clear();
            
            // Draw starting hand
            for (int i = 0; i < HAND_SIZE; i++)
            {
                DrawCard();
            }
            
            // Update networked hand directly
            UpdateNetworkedHand();
            
            // Log for debugging
            GameManager.Instance.LogManager.LogMessage($"Initial hand drawn for {PlayerName} with {_hand.Count} cards");
            
            // Call RPC to ensure all clients update their local hands from networked data
            RPC_NotifyHandChanged();
        }
    }
    
    // Force draw hand without authority check (for RPC calls)
    public void ForceDrawInitialHand()
    {
        _hand.Clear();
        
        // Draw starting hand
        for (int i = 0; i < HAND_SIZE; i++)
        {
            DrawCard();
        }
        
        // Update networked hand only if we have authority
        if (HasStateAuthority)
        {
            UpdateNetworkedHand();
        }
        
        GameManager.Instance.LogManager.LogMessage($"Force drew initial hand for {PlayerName} with {_hand.Count} cards");
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyHandChanged()
    {
        // Force a local update of the hand from networked data
        UpdateLocalHandFromNetworked();
    }

    // Create local monster from networked data
    private void RecreateMonsterFromNetworkedData()
    {
        // Create or update the monster from networked properties
        if (_playerMonster == null)
        {
            _playerMonster = new Monster();
        }
        
        _playerMonster.Name = MonsterName.ToString();
        _playerMonster.Health = MonsterHealth;
        _playerMonster.MaxHealth = MonsterMaxHealth;
        _playerMonster.Attack = MonsterAttack;
        _playerMonster.Defense = MonsterDefense;
        _playerMonster.TintColor = MonsterColor;
        
        GameManager.Instance.LogManager.LogMessage($"Recreated monster for {PlayerName} from networked data: HP={MonsterHealth}/{MonsterMaxHealth}");
        
        // Update the previously tracked monster health
        _previousMonsterHealth = MonsterHealth;
    }

    // Update opponent monster if opponent state changed
    private void UpdateOpponentMonsterFromState()
    {
        if (_opponentPlayerState != null)
        {
            if (_opponentMonster == null)
            {
                _opponentMonster = new Monster();
            }
            
            // Only update if values have changed
            bool hasChanged = false;
            
            if (_opponentMonster.Name != _opponentPlayerState.MonsterName.ToString())
            {
                _opponentMonster.Name = _opponentPlayerState.MonsterName.ToString();
                hasChanged = true;
            }
            
            if (_opponentMonster.Health != _opponentPlayerState.MonsterHealth)
            {
                _opponentMonster.Health = _opponentPlayerState.MonsterHealth;
                hasChanged = true;
                GameManager.Instance.LogManager.LogMessage($"Opponent monster health updated to: {_opponentMonster.Health}/{_opponentMonster.MaxHealth}");
            }
            
            if (_opponentMonster.MaxHealth != _opponentPlayerState.MonsterMaxHealth)
            {
                _opponentMonster.MaxHealth = _opponentPlayerState.MonsterMaxHealth;
                hasChanged = true;
            }
            
            if (_opponentMonster.Attack != _opponentPlayerState.MonsterAttack)
            {
                _opponentMonster.Attack = _opponentPlayerState.MonsterAttack;
                hasChanged = true;
            }
            
            if (_opponentMonster.Defense != _opponentPlayerState.MonsterDefense)
            {
                _opponentMonster.Defense = _opponentPlayerState.MonsterDefense;
                hasChanged = true;
            }
            
            if (!_opponentMonster.TintColor.Equals(_opponentPlayerState.MonsterColor))
            {
                _opponentMonster.TintColor = _opponentPlayerState.MonsterColor;
                hasChanged = true;
            }
            
            // Log only if something changed
            if (hasChanged)
            {
                GameManager.Instance.LogManager.LogMessage($"Updated opponent monster data for {_opponentMonster.Name}");
            }
        }
    }

    // Update local hand from networked data
    private void UpdateLocalHandFromNetworked()
    {
        _hand.Clear();
        
        // Convert networked data to local card data
        for (int i = 0; i < _networkedHand.Length; i++)
        {
            // Skip default/empty cards
            if (string.IsNullOrEmpty(_networkedHand[i].Name.ToString()))
                continue;
                
            _hand.Add(_networkedHand[i].ToCardData());
        }
        
        // Notify UI
        OnHandChanged?.Invoke(this, _hand);
        GameManager.Instance.LogManager.LogMessage($"Hand updated for {PlayerName} with {_hand.Count} cards");
    }

    // Try to register with GameState
    private void AttemptRegisterWithGameState()
    {
        if (Runner != null && Object != null)
        {
            if (GameState.Instance != null)
            {
                GameState.Instance.RegisterPlayer(Object.InputAuthority, this);
                GameManager.Instance.LogManager.LogMessage($"PlayerState registered with GameState for player {Object.InputAuthority}");
            }
            else
            {
                GameManager.Instance.LogManager.LogMessage("GameState.Instance is currently null, will try to register later");
            }
        }
    }

    // Keep trying to register until successful
    private IEnumerator RegisterWithGameStateWhenAvailable()
    {
        float timeoutSeconds = 30.0f; // Set a reasonable timeout
        float startTime = Time.time;
        
        // Keep trying until we succeed or timeout
        while (GameState.Instance == null)
        {
            yield return new WaitForSeconds(0.5f); // Check every half second
            
            // Check for timeout
            if (Time.time - startTime > timeoutSeconds)
            {
                GameManager.Instance.LogManager.LogError("Timed out waiting for GameState.Instance to become available");
                yield break;
            }
        }
        
        // GameState is now available, register with it
        AttemptRegisterWithGameState();
    }

    private void InitializeMonster()
    {
        // Create random color for monster
        Color monsterColor = new Color(
            UnityEngine.Random.Range(0.5f, 1f),
            UnityEngine.Random.Range(0.5f, 1f),
            UnityEngine.Random.Range(0.5f, 1f)
        );
        
        // Set networked monster properties
        MonsterName = $"{PlayerName}'s Monster";
        MonsterHealth = 40;
        MonsterMaxHealth = 40;
        MonsterAttack = 5;
        MonsterDefense = 3;
        MonsterColor = monsterColor;
        
        // Create local monster instance
        _playerMonster = new Monster
        {
            Name = MonsterName.ToString(),
            Health = MonsterHealth,
            MaxHealth = MonsterMaxHealth,
            Attack = MonsterAttack,
            Defense = MonsterDefense,
            TintColor = MonsterColor
        };
        
        GameManager.Instance.LogManager.LogMessage($"Monster initialized for {PlayerName}: HP={MonsterHealth}/{MonsterMaxHealth}");
    }

    private void CreateStartingDeck()
    {
        // Create some basic starter cards
        _deck.Clear();
        
        // Add basic attack cards
        for (int i = 0; i < 5; i++)
        {
            _deck.Add(new CardData
            {
                Name = "Strike",
                Description = "Deal 6 damage",
                EnergyCost = 1,
                Type = CardType.Attack,
                Target = CardTarget.Enemy,
                DamageAmount = 6
            });
        }
        
        // Add basic defense cards
        for (int i = 0; i < 5; i++)
        {
            _deck.Add(new CardData
            {
                Name = "Defend",
                Description = "Gain 5 block",
                EnergyCost = 1,
                Type = CardType.Skill,
                Target = CardTarget.Self,
                BlockAmount = 5
            });
        }
        
        // Add a couple special cards
        _deck.Add(new CardData
        {
            Name = "Cleave",
            Description = "Deal 8 damage to all enemies",
            EnergyCost = 2,
            Type = CardType.Attack,
            Target = CardTarget.AllEnemies,
            DamageAmount = 8
        });
        
        _deck.Add(new CardData
        {
            Name = "Second Wind",
            Description = "Gain 2 energy",
            EnergyCost = 0,
            Type = CardType.Skill,
            Target = CardTarget.Self,
            EnergyGain = 2
        });
        
        // Shuffle the deck
        ShuffleDeck();
        
        GameManager.Instance.LogManager.LogMessage($"Starting deck created for {PlayerName} with {_deck.Count} cards");
    }

    private void ShuffleDeck()
    {
        int n = _deck.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            CardData temp = _deck[k];
            _deck[k] = _deck[n];
            _deck[n] = temp;
        }
    }

    // Update networked hand from local hand
    private void UpdateNetworkedHand()
    {
        if (!HasStateAuthority) return;
        
        // Clear networked hand by filling with default values
        for (int i = 0; i < _networkedHand.Length; i++)
        {
            _networkedHand.Set(i, default);
        }
        
        // Update networked hand with current local hand
        for (int i = 0; i < _hand.Count && i < MAX_HAND_SIZE; i++)
        {
            _networkedHand.Set(i, NetworkedCardData.FromCardData(_hand[i]));
        }
    }

    private void DrawCard()
    {
        if (_deck.Count == 0)
        {
            // Shuffle discard into deck if empty
            _deck.AddRange(_discardPile);
            _discardPile.Clear();
            ShuffleDeck();
            
            if (_deck.Count == 0)
            {
                // If still empty, no cards to draw
                return;
            }
        }
        
        // Draw from the top
        CardData drawnCard = _deck[0];
        _deck.RemoveAt(0);
        _hand.Add(drawnCard);
    }

    // Add RPC to broadcast stat changes to all clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyStatsChanged()
    {
        // Update stored values for change detection
        _previousHealth = Health;
        _previousEnergy = Energy;
        _previousScore = Score;
        
        // Invoke the event for UI update
        OnStatsChanged?.Invoke(this);
    }

    // FIXED: Update this method to properly handle monster damage
    public void PlayCard(int cardIndex, Monster target)
    {
        if (!HasStateAuthority || cardIndex < 0 || cardIndex >= _hand.Count) return;
        
        CardData card = _hand[cardIndex];
        
        // Check if enough energy
        if (Energy < card.EnergyCost)
        {
            GameManager.Instance.LogManager.LogMessage($"Not enough energy to play {card.Name}");
            return;
        }
        
        // Log the card play
        GameManager.Instance.LogManager.LogMessage($"Player {PlayerName} playing {card.Name} on {(target != null ? target.Name : "null")}");
        
        // Handle card effects based on type and target
        bool cardPlayed = false;
        
        switch (card.Target)
        {
            case CardTarget.Enemy:
                if (target != null)
                {
                    // Apply damage
                    if (card.DamageAmount > 0)
                    {
                        // If we're targeting an opponent's monster, send an RPC to the owner
                        if (target == _opponentMonster && _opponentPlayerState != null && _opponentPlayerRef != default)
                        {
                            GameManager.Instance.LogManager.LogMessage($"Sending RPC to damage opponent's monster: {card.DamageAmount} damage to {target.Name} owned by {_opponentPlayerRef}");
                            
                            // Send RPC to request damage application
                            RPC_RequestDamageToMonster(_opponentPlayerRef, card.DamageAmount);
                            cardPlayed = true;
                        }
                        else if (target == _playerMonster)
                        {
                            // This is our own monster (weird but valid)
                            int healthBefore = target.Health;
                            target.TakeDamage(card.DamageAmount);
                            
                            // Update networked monster health
                            MonsterHealth = target.Health;
                            
                            // Log the damage
                            int actualDamage = healthBefore - target.Health;
                            GameManager.Instance.LogManager.LogMessage($"Applied {actualDamage} damage to our own monster {target.Name} (Health: {healthBefore} -> {target.Health})");
                            
                            cardPlayed = true;
                        }
                        else
                        {
                            GameManager.Instance.LogManager.LogError($"Invalid target for card {card.Name}: Not opponent's or player's monster");
                        }
                    }
                }
                break;
                
            case CardTarget.Self:
                // Apply self effects
                if (card.BlockAmount > 0)
                {
                    // Add block to player's monster
                    if (_playerMonster != null)
                    {
                        _playerMonster.AddBlock(card.BlockAmount);
                        
                        // Update networked monster data
                        if (HasStateAuthority)
                        {
                            RPC_UpdateMonsterBlock(_playerMonster.GetBlock());
                        }
                    }
                }
                
                if (card.HealAmount > 0)
                {
                    ModifyHealth(card.HealAmount);
                }
                
                if (card.EnergyGain > 0)
                {
                    ModifyEnergy(card.EnergyGain);
                }
                
                cardPlayed = true;
                break;
                
            case CardTarget.AllEnemies:
                // Apply to all enemies (currently just one)
                if (target != null && card.DamageAmount > 0)
                {
                    // IMPORTANT CHANGE: If we're targeting an opponent's monster,
                    // don't apply damage locally, just notify the target's owner to apply damage
                    if (target == _opponentMonster && _opponentPlayerState != null && _opponentPlayerRef != default)
                    {
                        GameManager.Instance.LogManager.LogMessage($"Sending RPC to damage opponent's monster: {card.DamageAmount} damage to {target.Name} owned by {_opponentPlayerRef}");
                        
                        // Send RPC to request damage application
                        RPC_RequestDamageToMonster(_opponentPlayerRef, card.DamageAmount);
                        cardPlayed = true;
                    }
                    else if (target == _playerMonster)
                    {
                        // This is our own monster (weird but valid)
                        int healthBefore = target.Health;
                        target.TakeDamage(card.DamageAmount);
                        
                        // Update networked monster health
                        MonsterHealth = target.Health;
                        
                        // Log the damage
                        int actualDamage = healthBefore - target.Health;
                        GameManager.Instance.LogManager.LogMessage($"Applied {actualDamage} damage to our own monster {target.Name} (Health: {healthBefore} -> {target.Health})");
                        
                        cardPlayed = true;
                    }
                    else
                    {
                        GameManager.Instance.LogManager.LogError($"Invalid target for card {card.Name}: Not opponent's or player's monster");
                    }
                }
                break;
                
            case CardTarget.All:
                // Apply effects to all characters and monsters
                cardPlayed = true;
                
                // First apply to opponent's monster if provided
                if (target != null && !target.Equals(_playerMonster) && card.DamageAmount > 0)
                {
                    // If we're targeting an opponent's monster,
                    // don't apply damage locally, just notify the target's owner to apply damage
                    if (target == _opponentMonster && _opponentPlayerState != null && _opponentPlayerRef != default)
                    {
                        GameManager.Instance.LogManager.LogMessage($"Sending RPC to damage opponent's monster: {card.DamageAmount} damage to {target.Name} owned by {_opponentPlayerRef}");
                        
                        // Send RPC to request damage application
                        RPC_RequestDamageToMonster(_opponentPlayerRef, card.DamageAmount);
                    }
                    else if (target == _playerMonster)
                    {
                        // This is our own monster (weird but valid)
                        int healthBefore = target.Health;
                        target.TakeDamage(card.DamageAmount);
                        
                        // Update networked monster health
                        MonsterHealth = target.Health;
                        
                        // Log the damage
                        int actualDamage = healthBefore - target.Health;
                        GameManager.Instance.LogManager.LogMessage($"Applied {actualDamage} damage to our own monster {target.Name} (Health: {healthBefore} -> {target.Health})");
                    }
                    else
                    {
                        GameManager.Instance.LogManager.LogError($"Invalid target for card {card.Name}: Not opponent's or player's monster");
                    }
                }
                
                // Then apply to player's monster if available
                if (_playerMonster != null && card.BlockAmount > 0)
                {
                    _playerMonster.AddBlock(card.BlockAmount);
                    
                    // Update networked monster block
                    if (HasStateAuthority)
                    {
                        RPC_UpdateMonsterBlock(_playerMonster.GetBlock());
                    }
                }
                
                // Apply effects to the player
                if (card.HealAmount > 0)
                {
                    ModifyHealth(card.HealAmount);
                }
                
                if (card.EnergyGain > 0)
                {
                    ModifyEnergy(card.EnergyGain);
                }
                break;
        }
        
        if (cardPlayed)
        {
            // Remove the card from hand and add to discard
            _hand.RemoveAt(cardIndex);
            _discardPile.Add(card);
            
            // Use energy
            ModifyEnergy(-card.EnergyCost);
            
            // Update networked hand
            UpdateNetworkedHand();
            
            // Notify all clients
            RPC_NotifyHandChanged();
            
            GameManager.Instance.LogManager.LogMessage($"{PlayerName} played {card.Name} successfully");
        }
        else
        {
            GameManager.Instance.LogManager.LogError($"Failed to play card {card.Name}");
        }
    }

    // NEW: RPC to request damage to another player's monster instead of directly applying it
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_RequestDamageToMonster(PlayerRef targetPlayer, int damageAmount)
    {
        GameManager.Instance.LogManager.LogMessage($"RPC_RequestDamageToMonster received: target={targetPlayer}, damage={damageAmount}, InputAuth={Object.InputAuthority}, HasStateAuth={HasStateAuthority}");
        
        // Log all current state for debugging
        GameManager.Instance.LogManager.LogMessage($"Local monster state: {(_playerMonster != null ? _playerMonster.Health : -1)}/{(_playerMonster != null ? _playerMonster.MaxHealth : -1)}");
        GameManager.Instance.LogManager.LogMessage($"Networked monster state: {MonsterHealth}/{MonsterMaxHealth}");
        
        // MODIFIED: Simplified check - if we receive damage request for our monster, process it
        if (Object.InputAuthority == targetPlayer) // This is our monster
        {
            GameManager.Instance.LogManager.LogMessage($"Processing damage to our monster as owner (InputAuth match)");
            
            if (_playerMonster != null)
            {
                // Log the damage request
                int healthBefore = _playerMonster.Health;
                
                // Apply the damage to our monster
                _playerMonster.TakeDamage(damageAmount);
                
                // Calculate actual damage dealt (after defense, etc.)
                int actualDamage = healthBefore - _playerMonster.Health;
                int newHealth = _playerMonster.Health;
                
                GameManager.Instance.LogManager.LogMessage($"Applied {actualDamage} damage to {_playerMonster.Name}: {healthBefore} -> {newHealth}");
                
                // Update networked health - critical to do this even without StateAuthority
                if (HasStateAuthority)
                {
                    GameManager.Instance.LogManager.LogMessage($"Updating networked monster health to {newHealth}");
                    MonsterHealth = newHealth;
                    
                    // Force notify all clients of the change
                    RPC_NotifyMonsterHealthChanged(MonsterHealth);
                }
                else
                {
                    // If we don't have state authority, request the update from whoever does
                    GameManager.Instance.LogManager.LogMessage($"Requesting networked health update to {newHealth} (don't have StateAuthority)");
                    RPC_RequestMonsterHealthUpdate(newHealth);
                }
                
                // Check for defeat
                if (_playerMonster.Health <= 0)
                {
                    // Grant score to the attacking player
                    RPC_GrantScoreToAttacker();
                }
            }
            else
            {
                GameManager.Instance.LogManager.LogError("Cannot apply damage: _playerMonster is null");
            }
        }
        else 
        {
            GameManager.Instance.LogManager.LogMessage($"Ignoring damage request - not for our monster (our auth: {Object.InputAuthority}, target: {targetPlayer})");
        }
    }

    // NEW: Added to sync monster health with all clients
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestMonsterHealthUpdate(int newHealth)
    {
        // Only the state authority should handle this
        if (HasStateAuthority)
        {
            GameManager.Instance.LogManager.LogMessage($"Received monster health update request: {newHealth}");
            MonsterHealth = newHealth;
            
            // Notify all clients
            RPC_NotifyMonsterHealthChanged(newHealth);
        }
    }

    // NEW: Notify all clients about monster health changes
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyMonsterHealthChanged(int newHealth)
    {
        GameManager.Instance.LogManager.LogMessage($"Monster health notification: {newHealth}");
        
        // Update local monster if this is our monster
        if (HasInputAuthority && _playerMonster != null)
        {
            _playerMonster.Health = newHealth;
            _previousMonsterHealth = newHealth;
        }
        
        // Try to force the UI to update
        OnStatsChanged?.Invoke(this);
    }

    // NEW: Added to sync monster block values
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateMonsterBlock(int blockAmount)
    {
        // Update networked data as needed
        // Currently we don't sync block in networked data, but we could add it if needed
        
        GameManager.Instance.LogManager.LogMessage($"Monster {MonsterName} block updated to {blockAmount}");
        
        // Ensure local monster has the block
        if (_playerMonster != null && HasInputAuthority)
        {
            _playerMonster.AddBlock(blockAmount);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_GrantScoreToAttacker()
    {
        // Find the player who was attacking this monster
        foreach (var entry in GameState.Instance.GetAllPlayerStates())
        {
            PlayerState attackerState = entry.Value;
            
            // If this player was attacking our monster
            if (attackerState._opponentPlayerRef == Object.InputAuthority)
            {
                // Grant them a point
                attackerState.IncreaseScore();
                GameManager.Instance.LogManager.LogMessage($"Player {attackerState.PlayerName} scored a point for defeating monster");
                break;
            }
        }
    }

    public void ModifyHealth(int amount)
    {
        if (!HasStateAuthority) return;
        
        int oldHealth = Health;
        Health = Mathf.Clamp(Health + amount, 0, MaxHealth);
        
        // Log health change
        if (oldHealth != Health)
        {
            GameManager.Instance.LogManager.LogMessage($"Player {PlayerName} health changed: {oldHealth} -> {Health}");
        }
        
        // Broadcast to all clients
        RPC_NotifyStatsChanged();
        
        if (Health <= 0)
        {
            GameManager.Instance.LogManager.LogMessage($"{PlayerName} has been defeated!");
            // Handle player defeat
        }
    }

    public void ModifyEnergy(int amount)
    {
        if (!HasStateAuthority) return;
        
        int oldEnergy = Energy;
        Energy = Mathf.Clamp(Energy + amount, 0, MaxEnergy);
        
        // Log energy change
        if (oldEnergy != Energy)
        {
            GameManager.Instance.LogManager.LogMessage($"Player {PlayerName} energy changed: {oldEnergy} -> {Energy}");
        }
        
        // Broadcast to all clients
        RPC_NotifyStatsChanged();
    }

    public void IncreaseScore()
    {
        if (!HasStateAuthority) return;
        
        Score++;
        
        // Broadcast to all clients
        RPC_NotifyStatsChanged();
        
        GameManager.Instance.LogManager.LogMessage($"{PlayerName} score increased to {Score}");
    }

    public void SetOpponentMonster(PlayerRef opponentRef, PlayerState opponentState)
    {
        _opponentPlayerRef = opponentRef;
        _opponentPlayerState = opponentState;
        
        // Create a local monster instance based on opponent's monster data
        if (_opponentPlayerState != null)
        {
            _opponentMonster = new Monster
            {
                Name = opponentState.MonsterName.ToString(),
                Health = opponentState.MonsterHealth,
                MaxHealth = opponentState.MonsterMaxHealth,
                Attack = opponentState.MonsterAttack,
                Defense = opponentState.MonsterDefense,
                TintColor = opponentState.MonsterColor
            };
            
            GameManager.Instance.LogManager.LogMessage($"{PlayerName} now fighting against {_opponentMonster.Name} from player {opponentRef}");
        }
        else
        {
            GameManager.Instance.LogManager.LogMessage($"{PlayerName} has no opponent monster assigned yet");
        }
    }

    public Monster GetMonster()
    {
        return _playerMonster;
    }

    public Monster GetOpponentMonster()
    {
        return _opponentMonster;
    }

    public int GetScore()
    {
        return Score;
    }

    public void PrepareForNewRound()
    {
        if (!HasStateAuthority) return;
        
        // Reset energy
        Energy = MaxEnergy;
        
        // Reset monster health
        MonsterHealth = MonsterMaxHealth;
        if (_playerMonster != null)
        {
            _playerMonster.Health = _playerMonster.MaxHealth;
        }
        
        // Discard hand and draw new cards
        _discardPile.AddRange(_hand);
        _hand.Clear();
        
        // Draw new hand
        DrawInitialHand();
        
        // Notify clients about stats changes
        RPC_NotifyStatsChanged();
        
        GameManager.Instance.LogManager.LogMessage($"{PlayerName} ready for new round");
    }

    public void EndTurn()
    {
        if (!HasStateAuthority) return;
        
        // Process end of turn effects
        
        // Request next turn for this specific player
        if (GameState.Instance != null)
        {
            // Get player ref
            var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
            if (networkRunner != null)
            {
                PlayerRef localPlayer = Object.InputAuthority;
                GameState.Instance.NextTurn(localPlayer);
                GameManager.Instance.LogManager.LogMessage($"{PlayerName} ended their turn, passing to monster");
            }
            else
            {
                GameManager.Instance.LogManager.LogError("Could not get NetworkRunner in EndTurn");
            }
        }
        else
        {
            GameManager.Instance.LogManager.LogError("GameState.Instance is null in EndTurn");
        }
    }

    public List<CardData> GetHand()
    {
        return new List<CardData>(_hand);
    }
}