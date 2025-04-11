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
    private PlayerState _opponentPlayerState;

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
    
    // Track whether we're currently drawing cards to prevent double-draws
    private bool _isCurrentlyDrawingCards = false;

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
        Monster monster = _monsterManager.GetPlayerMonster();
        if (monster != null)
        {
            // Update monster name to include player name for uniqueness
            if (monster.Name == "Your Monster")
            {
                string playerName = PlayerName.ToString();
                monster.Name = $"{playerName}'s Monster";
                GameManager.Instance.LogManager.LogMessage($"Set unique monster name: {monster.Name}");
            }
            
            // Use a local string variable for convenience
            string name = monster.Name;
            // Then set the networked string property
            MonsterName = name;
            
            // Set other monster properties directly
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
            else if (change.StartsWith("Monster") && HasInputAuthority)
            {
                // Only update your own monster from network changes
                UpdateLocalMonsterFromNetworked();
                
                // Notify UI about stats change
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
            if (_isCurrentlyDrawingCards)
            {
                GameManager.Instance.LogManager.LogMessage($"Skipping duplicate DrawInitialHand call for {PlayerName}");
                return;
            }
            
            _isCurrentlyDrawingCards = true;
            
            // Draw starting hand
            _cardManager.DrawToHandSize();
            
            // Update networked hand
            UpdateNetworkedHand();
            
            // Log for debugging
            GameManager.Instance.LogManager.LogMessage($"Initial hand drawn for {PlayerName}");
            
            // Call RPC to ensure all clients update their local hands from networked data
            RPC_NotifyHandChanged();
            
            _isCurrentlyDrawingCards = false;
        }
    }
    
    // Force draw hand without authority check (for RPC calls)
    public void ForceDrawInitialHand()
    {
        if (_isCurrentlyDrawingCards)
        {
            GameManager.Instance.LogManager.LogMessage($"Skipping duplicate ForceDrawInitialHand call for {PlayerName}");
            return;
        }
        
        _isCurrentlyDrawingCards = true;
        
        // Draw starting hand
        _cardManager.DrawToHandSize();
        
        // Update networked hand only if we have authority
        if (HasStateAuthority)
        {
            UpdateNetworkedHand();
            RPC_NotifyHandChanged();
        }
        
        GameManager.Instance.LogManager.LogMessage($"Force drew initial hand for {PlayerName}");
        
        _isCurrentlyDrawingCards = false;
    }
    
    // FIXED: Enhanced method for forced draws with better debugging
    public void ForceDrawNewHandDirectly()
{
    // Generate a unique debug ID for tracking this operation
    string debugId = UnityEngine.Random.Range(1000, 9999).ToString();
    
    // Prevent multiple draws happening simultaneously
    if (_isCurrentlyDrawingCards)
    {
        GameManager.Instance.LogManager.LogMessage($"[{debugId}] SKIPPED - Already drawing cards for {PlayerName}");
        return;
    }
    
    _isCurrentlyDrawingCards = true;
    
    try
    {
        GameManager.Instance.LogManager.LogMessage($"[{debugId}] START DRAW - Force draw for {PlayerName} (HasStateAuthority: {HasStateAuthority}, InputAuthority: {Object.InputAuthority})");
        
        // Always draw locally first regardless of authority
        _cardManager.PrepareForNewRound();
        List<CardData> hand = _cardManager.GetHand();
        
        // Log the card count
        GameManager.Instance.LogManager.LogMessage($"[{debugId}] LOCAL CARDS - {hand.Count} cards drawn for {PlayerName}");
        
        // If we have authority, update networked state
        if (HasStateAuthority)
        {
            // Update networked hand
            UpdateNetworkedHand(debugId);
            
            // Use RPC to ensure all clients see the change
            RPC_NotifyHandChanged(debugId);
            
            GameManager.Instance.LogManager.LogMessage($"[{debugId}] NETWORK UPDATED - Hand data sent to network for {PlayerName}");
        }
        
        // Always notify UI about the change - even without authority
        OnHandChanged?.Invoke(this, hand);
        
        GameManager.Instance.LogManager.LogMessage($"[{debugId}] COMPLETE - Force draw completed for {PlayerName} with {hand.Count} cards");
    }
    catch (Exception ex)
    {
        GameManager.Instance.LogManager.LogError($"[{debugId}] ERROR - Force draw failed for {PlayerName}: {ex.Message}");
    }
    finally
    {
        _isCurrentlyDrawingCards = false;
    }
}

    
    // MODIFIED: Changed to use authority check more carefully
    public void DrawNewHandForTurn()
    {
        if (_isCurrentlyDrawingCards)
        {
            GameManager.Instance.LogManager.LogMessage($"Skipping duplicate DrawNewHandForTurn call for {PlayerName}");
            return;
        }
        
        // Send RPC to owner of this state to draw new cards
        if (HasStateAuthority)
        {
            GameManager.Instance.LogManager.LogMessage($"DrawNewHandForTurn with authority for {PlayerName}");
            DrawNewHandImpl();
        }
        else
        {
            // Request the state authority to draw for us
            GameManager.Instance.LogManager.LogMessage($"Requesting hand draw for {PlayerName} via RPC");
            RPC_RequestDrawNewHand(Object.InputAuthority);
        }
    }
    
    // Implementation of drawing a new hand
    private void DrawNewHandImpl()
    {
        if (_isCurrentlyDrawingCards)
        {
            GameManager.Instance.LogManager.LogMessage($"Skipping duplicate DrawNewHandImpl call for {PlayerName}");
            return;
        }
        
        _isCurrentlyDrawingCards = true;
        
        // Reset and draw a new hand of cards
        _cardManager.PrepareForNewRound();
        
        // Update networked hand
        UpdateNetworkedHand();
        
        // Call RPC to ensure all clients update their local hands from networked data
        RPC_NotifyHandChanged();
        
        GameManager.Instance.LogManager.LogMessage($"Drew new hand of cards for {PlayerName}'s turn");
        
        _isCurrentlyDrawingCards = false;
    }
    
    // FIXED: Improved RPC to handle player validation
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDrawNewHand(PlayerRef requestingPlayer)
    {
        if (!HasStateAuthority) 
        {
            GameManager.Instance.LogManager.LogError($"RPC_RequestDrawNewHand received but we don't have state authority!");
            return;
        }
        
        // Verify this request is coming from our owner
        if (Object.InputAuthority != requestingPlayer)
        {
            GameManager.Instance.LogManager.LogMessage($"Ignoring draw request from non-owner: {requestingPlayer}");
            return;
        }
        
        GameManager.Instance.LogManager.LogMessage($"Received verified request to draw new hand for {PlayerName}");
        
        // Draw the new hand
        DrawNewHandImpl();
    }
    
   // Added parameter for debug tracking
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_NotifyHandChanged(string debugId = "")
{
    if (string.IsNullOrEmpty(debugId))
        debugId = UnityEngine.Random.Range(1000, 9999).ToString();
    
    GameManager.Instance.LogManager.LogMessage($"[{debugId}] RPC_NotifyHandChanged received for {PlayerName}");
    
    // Force a local update of the hand from networked data
    UpdateLocalHandFromNetworked(debugId);
    
    // Get current hand data for logging and UI notification
    List<CardData> handData = _cardManager.GetHand();
    GameManager.Instance.LogManager.LogMessage($"[{debugId}] Hand updated from network for {PlayerName} with {handData.Count} cards");
    
    // Always notify UI about the change
    OnHandChanged?.Invoke(this, handData);
}

    // Update networked hand from local hand
    private void UpdateNetworkedHand(string debugId = "")
{
    if (string.IsNullOrEmpty(debugId))
        debugId = UnityEngine.Random.Range(1000, 9999).ToString();
    
    if (!HasStateAuthority)
    {
        GameManager.Instance.LogManager.LogError($"[{debugId}] ERROR - UpdateNetworkedHand called without state authority for {PlayerName}");
        return;
    }
    
    NetworkedCardData[] networkedHand = _cardManager.GetNetworkedHand(MAX_HAND_SIZE);
    
    // Log information about the hand being sent to the network
    List<string> cardNames = new List<string>();
    for (int i = 0; i < networkedHand.Length && !string.IsNullOrEmpty(networkedHand[i].Name.ToString()); i++)
    {
        cardNames.Add(networkedHand[i].Name.ToString());
    }
    
    // Update networked array
    for (int i = 0; i < networkedHand.Length; i++)
    {
        _networkedHand.Set(i, networkedHand[i]);
    }
    
    GameManager.Instance.LogManager.LogMessage($"[{debugId}] NETWORK UPDATE - Updated networked hand for {PlayerName} with {cardNames.Count} cards");
}


    // Update local hand from networked data with extra debugging
    private void UpdateLocalHandFromNetworked(string debugId = "")
{
    if (string.IsNullOrEmpty(debugId))
        debugId = UnityEngine.Random.Range(1000, 9999).ToString();
    
    GameManager.Instance.LogManager.LogMessage($"[{debugId}] START - UpdateLocalHandFromNetworked for {PlayerName}");
    
    // Copy networked data to local array
    NetworkedCardData[] networkedHand = new NetworkedCardData[_networkedHand.Length];
    int validCardCount = 0;
    
    for (int i = 0; i < _networkedHand.Length; i++)
    {
        networkedHand[i] = _networkedHand[i];
        
        // Count only non-empty cards
        if (!string.IsNullOrEmpty(networkedHand[i].Name.ToString()))
        {
            validCardCount++;
        }
    }
    
    // Log the current state before update
    int startingCardCount = _cardManager.GetHand().Count;
    GameManager.Instance.LogManager.LogMessage($"[{debugId}] BEFORE UPDATE - {startingCardCount} cards in hand. Network has {validCardCount} valid cards");
    
    // Update local hand
    _cardManager.UpdateFromNetworkedHand(networkedHand);
    
    // Get the current hand for logging
    List<CardData> handData = _cardManager.GetHand();
    int newCardCount = handData.Count;
    
    // Log details about the update
    GameManager.Instance.LogManager.LogMessage($"[{debugId}] AFTER UPDATE - Hand updated for {PlayerName}: {startingCardCount} → {newCardCount} cards");
    
    if (newCardCount > 0)
    {
        // Log the first few cards for verification
        List<string> cardNames = new List<string>();
        for (int i = 0; i < Math.Min(3, handData.Count); i++)
        {
            cardNames.Add(handData[i].Name);
        }
        GameManager.Instance.LogManager.LogMessage($"[{debugId}] SAMPLE CARDS - First cards: {string.Join(", ", cardNames)}");
    }
    
    GameManager.Instance.LogManager.LogMessage($"[{debugId}] COMPLETE - UpdateLocalHandFromNetworked completed for {PlayerName}");
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
                // THIS IS THE KEY FIX - We need to find the opponent's PlayerState to modify their monster
                if (_opponentPlayerState != null && _opponentPlayerState.HasStateAuthority)
                {
                    // Opponent's PlayerState has authority, so it should apply the damage
                    RPC_OpponentApplyDamageToMonster(_opponentPlayerRef, card.DamageAmount);
                    cardPlayed = true;
                }
                else
                {
                    // Fallback in case opponent's player state is not available
                    RPC_ApplyDamageToMonster(_opponentPlayerRef, card.DamageAmount);
                    cardPlayed = true;
                }
            }
            else if (isOwnMonster)
            {
                // Apply effects to own monster
                if (card.BlockAmount > 0)
                {
                    target.AddBlock(card.BlockAmount);
                }
                
                if (card.HealAmount > 0)
                {
                    target.Heal(card.HealAmount);
                }
                
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

    // NEW: This RPC is called on the opponent's PlayerState to apply damage to *their* monster
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_OpponentApplyDamageToMonster(PlayerRef targetPlayer, int damageAmount)
    {
        // This should only be executed by the PlayerState with state authority over the monster being damaged
        if (!HasStateAuthority) return;
        
        if (Object.InputAuthority == targetPlayer)
        {
            // This is our monster that's being targeted
            Monster monster = _monsterManager.GetPlayerMonster();
            if (monster != null)
            {
                // Get current health before damage
                int currentHealth = monster.Health;
                
                // Apply damage
                monster.TakeDamage(damageAmount);
                
                // Update networked property
                MonsterHealth = monster.Health;
                
                // Send a notification to all clients to update UI
                RPC_NotifyMonsterDamaged(Object.InputAuthority, monster.Health);
                
                GameManager.Instance.LogManager.LogMessage($"Applied {damageAmount} damage to our monster. Health: {currentHealth} -> {monster.Health}");
            }
        }
    }

    // Legacy RPC for backward compatibility
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_ApplyDamageToMonster(PlayerRef targetPlayer, int damageAmount)
    {
        // Find the target player state to apply damage to their monster
        if (GameState.Instance != null)
        {
            var playerStates = GameState.Instance.GetAllPlayerStates();
            if (playerStates.TryGetValue(targetPlayer, out PlayerState targetState) && targetState.HasStateAuthority)
            {
                // Forward to the correct player state
                targetState.RPC_OpponentApplyDamageToMonster(targetPlayer, damageAmount);
            }
        }
    }

    // MODIFIED: Notify all clients about monster damage with improved UI updating
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyMonsterDamaged(PlayerRef monsterOwner, int newHealth)
    {
        var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
        if (networkRunner == null) return;
        
        // We need to identify if this is about the local player's monster or an opponent's monster
        if (networkRunner.LocalPlayer == monsterOwner)
        {
            // This is about our own monster
            Monster playerMonster = _monsterManager.GetPlayerMonster();
            if (playerMonster != null)
            {
                // Use the SetHealth method instead of directly modifying health
                playerMonster.SetHealth(newHealth);
                GameManager.Instance.LogManager.LogMessage($"Updated our monster's health to {newHealth}");
            }
        }
        else
        {
            // This is about an opponent's monster
            // First try to update directly via the BattleUIController
            var gameUI = UnityEngine.Object.FindAnyObjectByType<GameUI>(); // Fixed: Using FindAnyObjectByType
            if (gameUI != null && gameUI.GetBattleUIController() != null)
            {
                gameUI.GetBattleUIController().UpdateOpponentMonsterHealth(newHealth);
                GameManager.Instance.LogManager.LogMessage($"Directly updated opponent monster's health to {newHealth} via UI controller");
            }
            else
            {
                // Fallback to the old method
                Monster opponentMonster = _monsterManager.GetOpponentMonster();
                if (opponentMonster != null)
                {
                    opponentMonster.SetHealth(newHealth);
                    GameManager.Instance.LogManager.LogMessage($"Updated opponent monster's health to {newHealth} via monster manager");
                }
            }
        }
        
        // Notify UI to update
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
        // Store the opponent references
        _opponentPlayerRef = opponentRef;
        _opponentPlayerState = opponentState;
        
        // Call the monster manager to set up the opponent monster
        _monsterManager.SetOpponentMonster(opponentRef, opponentState);
    }

    // Add method to get opponent player reference
    public PlayerRef GetOpponentPlayerRef()
    {
        return _opponentPlayerRef;
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

    // Reset player for a new round
    public void PrepareForNewRound()
    {
        if (!HasStateAuthority) return;
        
        // Reset energy
        Energy = MaxEnergy;
        
        // Reset monster health
        _monsterManager.ResetPlayerMonster();
        UpdateMonsterNetworkedProperties();
        
        // Notify clients about stats changes
        RPC_NotifyStatsChanged();
        
        GameManager.Instance.LogManager.LogMessage($"{PlayerName} ready for new round");
    }

    // FIXED: Improved EndTurn method with better error handling
    public void EndTurn()
{
    string debugId = UnityEngine.Random.Range(1000, 9999).ToString();
    
    if (GameState.Instance == null)
    {
        GameManager.Instance.LogManager.LogError($"[{debugId}] ERROR - EndTurn failed: GameState.Instance is null");
        return;
    }
    
    var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
    if (networkRunner == null)
    {
        GameManager.Instance.LogManager.LogError($"[{debugId}] ERROR - EndTurn failed: NetworkRunner is null");
        return;
    }
    
    PlayerRef localPlayer = Object.InputAuthority;
    
    if (localPlayer == default)
    {
        GameManager.Instance.LogManager.LogError($"[{debugId}] ERROR - EndTurn failed: Invalid local player reference");
        return;
    }
    
    // Add more info to the logs to help with debugging
    GameManager.Instance.LogManager.LogMessage($"[{debugId}] END TURN - Player {PlayerName} (ID: {localPlayer.PlayerId}) ending turn. HasStateAuthority: {HasStateAuthority}, InputAuthority: {Object.InputAuthority.PlayerId}");
    
    // Send the turn end request directly to GameState via RPC
    GameState.Instance.RPC_RequestEndTurn(localPlayer);
}

    
    public List<CardData> GetHand()
    {
        return _cardManager.GetHand();
    }
}