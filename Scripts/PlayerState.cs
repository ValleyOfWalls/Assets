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

    // Store opponent reference
    private PlayerRef _opponentPlayerRef;

    // Hand cards
    [Networked, Capacity(10)]
    private NetworkArray<NetworkedCardData> _networkedHand { get; }

    // Managers
    private CardManager _cardManager;
    private MonsterManager _monsterManager;
    
    // Events
    public static event Action<PlayerState> OnStatsChanged;
    public static event Action<PlayerState, List<CardData>> OnHandChanged;

    // Constants
    private const int STARTING_HEALTH = 50;
    private const int STARTING_ENERGY = 3;
    private const int MAX_HAND_SIZE = 10;

    // Change detector
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        base.Spawned();
        
        // Initialize managers
        _cardManager = new CardManager();
        _monsterManager = new MonsterManager(this, Object.InputAuthority);
        
        if (HasStateAuthority)
        {
            // Initialize default stats
            MaxHealth = STARTING_HEALTH;
            Health = MaxHealth;
            MaxEnergy = STARTING_ENERGY;
            Energy = MaxEnergy;
            Score = 0;
            
            // Get name from the Player component
            InitializePlayerName();
            
            // Update monster networked stats
            UpdateMonsterNetworkedProperties();
        }
        
        // Initialize change detector for networked properties
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        
        GameManager.Instance.LogManager.LogMessage($"PlayerState spawned for {PlayerName}");
        
        // Register with GameState
        StartCoroutine(RegisterWithGameStateWhenAvailable());
        
        // Create local monster instance to represent networked data
        UpdateLocalMonsterFromNetworked();
    }

    private void InitializePlayerName()
    {
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
    }

    private void UpdateMonsterNetworkedProperties()
    {
        // Use a local string variable for convenience
        string name = _monsterManager.GetPlayerMonster()?.Name ?? "Default Monster";
        // Then set the networked string property
        MonsterName = name;
        
        // Set other monster properties directly
        Monster monster = _monsterManager.GetPlayerMonster();
        if (monster != null)
        {
            MonsterHealth = monster.Health;
            MonsterMaxHealth = monster.MaxHealth;
            MonsterAttack = monster.Attack;
            MonsterDefense = monster.Defense;
            MonsterColor = monster.TintColor;
        }
    }

    private void UpdateLocalMonsterFromNetworked()
    {
        _monsterManager.UpdateLocalMonsterFromNetworked(
            MonsterName,
            MonsterHealth,
            MonsterMaxHealth,
            MonsterAttack,
            MonsterDefense,
            MonsterColor
        );
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
                // Update local monster from networked data
                UpdateLocalMonsterFromNetworked();
                
                // Force an update to the UI
                OnStatsChanged?.Invoke(this);
            }
            else if (change == nameof(Health) || change == nameof(MaxHealth) ||
                    change == nameof(Energy) || change == nameof(MaxEnergy) ||
                    change == nameof(Score))
            {
                // Notify UI about stats change
                OnStatsChanged?.Invoke(this);
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
        GameState.Instance.RegisterPlayer(Object.InputAuthority, this);
        GameManager.Instance.LogManager.LogMessage($"PlayerState registered with GameState for player {Object.InputAuthority}");
    }

    public void DrawInitialHand()
    {
        if (HasStateAuthority)
        {
            // Draw starting hand
            _cardManager.DrawToHandSize();
            
            // Update networked hand
            UpdateNetworkedHand();
            
            // Log for debugging
            GameManager.Instance.LogManager.LogMessage($"Initial hand drawn for {PlayerName}");
            
            // Call RPC to ensure all clients update their local hands from networked data
            RPC_NotifyHandChanged();
        }
    }
    
    // Force draw hand without authority check (for RPC calls)
    public void ForceDrawInitialHand()
    {
        // Draw starting hand
        _cardManager.DrawToHandSize();
        
        // Update networked hand only if we have authority
        if (HasStateAuthority)
        {
            UpdateNetworkedHand();
        }
        
        GameManager.Instance.LogManager.LogMessage($"Force drew initial hand for {PlayerName}");
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyHandChanged()
    {
        // Force a local update of the hand from networked data
        UpdateLocalHandFromNetworked();
    }

    // Update networked hand from local hand
    private void UpdateNetworkedHand()
    {
        if (!HasStateAuthority) return;
        
        NetworkedCardData[] networkedHand = _cardManager.GetNetworkedHand(MAX_HAND_SIZE);
        
        // Update networked array
        for (int i = 0; i < networkedHand.Length; i++)
        {
            _networkedHand.Set(i, networkedHand[i]);
        }
    }

    // Update local hand from networked data
    private void UpdateLocalHandFromNetworked()
    {
        NetworkedCardData[] networkedHand = new NetworkedCardData[_networkedHand.Length];
        
        // Copy networked data to local array
        for (int i = 0; i < _networkedHand.Length; i++)
        {
            networkedHand[i] = _networkedHand[i];
        }
        
        // Update local hand
        _cardManager.UpdateFromNetworkedHand(networkedHand);
        
        // Notify UI
        OnHandChanged?.Invoke(this, _cardManager.GetHand());
        GameManager.Instance.LogManager.LogMessage($"Hand updated for {PlayerName}");
    }

    // Add RPC to broadcast stat changes to all clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyStatsChanged()
    {
        // Invoke the event for UI update
        OnStatsChanged?.Invoke(this);
    }

    public void PlayCard(int cardIndex, Monster target)
    {
        if (!HasStateAuthority || cardIndex < 0) return;
        
        List<CardData> hand = _cardManager.GetHand();
        if (cardIndex >= hand.Count) return;
        
        CardData card = hand[cardIndex];
        
        // Check if player can play this card
        if (!PlayerStateValidator.CanPlayCard(this, card))
        {
            GameManager.Instance.LogManager.LogMessage($"Cannot play card {card.Name}: insufficient energy or not player's turn");
            return;
        }
        
        // Log the card play
        GameManager.Instance.LogManager.LogMessage($"Player {PlayerName} playing {card.Name} on {(target != null ? target.Name : "null")}");
        
        // Handle card effects
        bool cardPlayed = false;
        
        // Check if target is player's own monster
        bool isOwnMonster = (target == _monsterManager.GetPlayerMonster());
        
        // Check if target is opponent's monster
        bool isOpponentMonster = (target == _monsterManager.GetOpponentMonster());
        
        // Validate target based on card type
        if (PlayerStateValidator.IsValidMonsterTarget(card, target, isOwnMonster))
        {
            if (isOpponentMonster)
            {
                // Send RPC to damage opponent monster
                if (_opponentPlayerRef != default)
                {
                    RPC_RequestDamageToMonster(_opponentPlayerRef, card.DamageAmount);
                    cardPlayed = true;
                }
            }
            else if (isOwnMonster)
            {
                // Apply effects to own monster
                _cardManager.ApplyCardEffects(card, target, this);
                // Update networked monster stats
                UpdateMonsterNetworkedProperties();
                cardPlayed = true;
            }
            else
            {
                GameManager.Instance.LogManager.LogError($"Invalid target for card {card.Name}");
            }
        }
        
        if (cardPlayed)
        {
            // Remove card from hand and update
            _cardManager.PlayCard(cardIndex);
            
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

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_RequestDamageToMonster(PlayerRef targetPlayer, int damageAmount)
    {
        var networkRunner = GameManager.Instance.NetworkManager.GetRunner();
        
        // Check if this is for our monster
        bool isOurMonster = false;
        if (networkRunner != null && networkRunner.LocalPlayer == targetPlayer)
        {
            isOurMonster = true;
        }
        
        // Only proceed if this is our monster
        if (isOurMonster)
        {
            GameManager.Instance.LogManager.LogMessage($"Processing damage to our monster");
            
            if (HasStateAuthority)
            {
                // Store current health value
                int currentHealth = MonsterHealth;
                
                // Apply damage and update networked value
                _monsterManager.ApplyDamageToPlayerMonster(damageAmount, ref currentHealth);
                
                // Now set the property
                MonsterHealth = currentHealth;
                
                // Notify all clients
                RPC_NotifyMonsterHealthChanged(MonsterHealth);
            }
            else
            {
                // Request update from state authority
                RPC_RequestMonsterHealthUpdate(MonsterHealth - damageAmount);
            }
        }
    }

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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyMonsterHealthChanged(int newHealth)
    {
        GameManager.Instance.LogManager.LogMessage($"Monster health notification: {newHealth}");
        
        // Update local monster if this is our monster
        if (HasInputAuthority)
        {
            Monster playerMonster = _monsterManager.GetPlayerMonster();
            if (playerMonster != null)
            {
                playerMonster.Health = newHealth;
            }
        }
        
        // Force the UI to update
        OnStatsChanged?.Invoke(this);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_GrantScoreToAttacker(PlayerRef attackerRef)
    {
        // Find the attacker state
        if (GameState.Instance != null)
        {
            var allPlayerStates = GameState.Instance.GetAllPlayerStates();
            if (allPlayerStates.TryGetValue(attackerRef, out PlayerState attackerState))
            {
                if (attackerState.HasStateAuthority)
                {
                    attackerState.IncreaseScore();
                    GameManager.Instance.LogManager.LogMessage($"Player {attackerState.PlayerName} scored a point for defeating monster");
                }
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
        // Store the opponent reference
        _opponentPlayerRef = opponentRef;
        
        // Call the monster manager to set up the opponent monster
        _monsterManager.SetOpponentMonster(opponentRef, opponentState);
    }

    public Monster GetMonster()
    {
        return _monsterManager.GetPlayerMonster();
    }

    public Monster GetOpponentMonster()
    {
        return _monsterManager.GetOpponentMonster();
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
        _monsterManager.ResetPlayerMonster();
        UpdateMonsterNetworkedProperties();
        
        // Reset and draw new cards
        _cardManager.PrepareForNewRound();
        
        // Update networked hand
        UpdateNetworkedHand();
        
        // Notify clients about stats changes
        RPC_NotifyStatsChanged();
        
        GameManager.Instance.LogManager.LogMessage($"{PlayerName} ready for new round");
    }

    public void EndTurn()
    {
        if (!HasStateAuthority) return;
        
        // Request next turn for this specific player
        if (GameState.Instance != null)
        {
            PlayerRef localPlayer = Object.InputAuthority;
            GameState.Instance.NextTurn(localPlayer);
            GameManager.Instance.LogManager.LogMessage($"{PlayerName} ended their turn, passing to monster");
        }
        else
        {
            GameManager.Instance.LogManager.LogError("GameState.Instance is null in EndTurn");
        }
    }

    public List<CardData> GetHand()
    {
        return _cardManager.GetHand();
    }
}