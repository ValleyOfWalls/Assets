using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// This class handles the networked game state
public class GameState : NetworkBehaviour
{
    // Singleton instance - only valid when spawned
    private static GameState _instance;
    public static GameState Instance => _instance;

    // Game progression
    [Networked] public int CurrentRound { get; set; }
    [Networked] public NetworkBool DraftPhaseActive { get; set; }
    [Networked] public NetworkBool GameActive { get; set; }
    [Networked] public NetworkBool RoundComplete { get; set; }
    
    [Networked] public int RoundsToWin { get; set; }

    // Dictionary to track active turns per player
    // We can't directly network a dictionary, so we'll use RPCs to sync this
    private Dictionary<PlayerRef, bool> _isPlayerTurn = new Dictionary<PlayerRef, bool>();
    private Dictionary<PlayerRef, bool> _isMonsterTurn = new Dictionary<PlayerRef, bool>();
    
    // Dictionary to track player turn counts
    private Dictionary<PlayerRef, int> _playerTurnCount = new Dictionary<PlayerRef, int>();
    
    // For global round tracking - will only advance when all players trigger a round completion
    private Dictionary<PlayerRef, bool> _hasCompletedRound = new Dictionary<PlayerRef, bool>();
    private int _playersCompletedThisRound = 0;

    // Events
    public static event Action<int> OnRoundChanged;
    public static event Action<bool> OnDraftPhaseChanged;
    public static event Action<PlayerRef, bool> OnPlayerTurnChanged;
    public static event Action<bool> OnGameActiveChanged;
    public static event Action OnRoundComplete;
    public static event Action OnGameComplete;
    
    // Events for player state management
    public static event Action<PlayerRef, PlayerState> OnPlayerStateAdded;
    public static event Action<PlayerRef, PlayerState> OnPlayerStateRemoved;

    // Player references
    private Dictionary<PlayerRef, PlayerState> _playerStates = new Dictionary<PlayerRef, PlayerState>();
    private List<PlayerRef> _allPlayers = new List<PlayerRef>();

    // Card collections
    private List<CardData> _draftPool = new List<CardData>();
    
    // Flag to track spawned status
    private bool _isSpawned = false;

    // Called when the component is first initialized
    private void Awake()
    {
        // Don't set the instance in Awake - wait until we're spawned
        // This prevents non-networked instances from becoming the singleton
        if (GameManager.Instance != null)
            GameManager.Instance.LogManager.LogMessage("GameState Awake called");
    }

    // Override the Spawned method which is called when the object is spawned on the network
    public override void Spawned()
    {
        base.Spawned();
        
        // Only register this as the Instance when it's properly spawned on the network
        _instance = this;
        _isSpawned = true;
        
        if (GameManager.Instance != null)
            GameManager.Instance.LogManager.LogMessage("GameState spawned and singleton set");
        
        if (HasStateAuthority)
        {
            // Initialize game defaults
            CurrentRound = 1;
            RoundsToWin = 3;
            GameActive = false;
            DraftPhaseActive = false;
            RoundComplete = false;
            
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage("GameState initialized with default values");
        }
    }

    public void RegisterPlayer(PlayerRef player, PlayerState state)
    {
        if (!_playerStates.ContainsKey(player))
        {
            _playerStates.Add(player, state);
            
            if (_isSpawned && HasStateAuthority && !_allPlayers.Contains(player))
            {
                _allPlayers.Add(player);
                
                // Initialize turn state for this player - start with player's turn
                _isPlayerTurn[player] = true;
                _isMonsterTurn[player] = false;
                
                // Initialize turn count to 1
                _playerTurnCount[player] = 1;
                
                // Initialize round completion tracking
                _hasCompletedRound[player] = false;
                
                // Notify the player that their turn has started
                RPC_NotifyPlayerTurnState(player, true);
            }
            
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"Player {player} registered with GameState");
            
            // Notify listeners that a player state was added
            OnPlayerStateAdded?.Invoke(player, state);
        }
    }
    
    // Method to handle player removal more explicitly
    public void UnregisterPlayer(PlayerRef player)
    {
        if (_playerStates.TryGetValue(player, out PlayerState state))
        {
            // Notify listeners before removing
            OnPlayerStateRemoved?.Invoke(player, state);
            
            // Remove from collections
            _playerStates.Remove(player);
            _allPlayers.Remove(player);
            _isPlayerTurn.Remove(player);
            _isMonsterTurn.Remove(player);
            _playerTurnCount.Remove(player);
            _hasCompletedRound.Remove(player);
            
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"Player {player} unregistered from GameState");
        }
    }

    public void StartGame()
    {
        if (_isSpawned && HasStateAuthority)
        {
            GameActive = true;
            CurrentRound = 1;
            _playersCompletedThisRound = 0;
            
            // Reset the round completion tracker
            foreach (var player in _allPlayers)
            {
                _hasCompletedRound[player] = false;
                _playerTurnCount[player] = 1;
            }
            
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage("Game starting on network!");
            
            // Use coroutine with delay to ensure all players are properly initialized
            StartCoroutine(DelayedMatchupAssignment());
        }
    }

    // New coroutine to delay matchup assignment
    private IEnumerator DelayedMatchupAssignment()
    {
        // Wait for player states to stabilize
        yield return new WaitForSeconds(1.0f);
        
        // Log player count
        GameManager.Instance.LogManager.LogMessage($"Players registered with GameState: {_playerStates.Count}");
        foreach (var entry in _playerStates)
        {
            GameManager.Instance.LogManager.LogMessage($"  - Player {entry.Key}: {entry.Value.PlayerName}");
        }
        
        // Assign initial monster matchups
        AssignMonsterMatchups();
        
        // Use RPC to ensure all players draw cards
        RPC_DrawInitialCardsForAllPlayers();
        
        // Start every player's turn (no need for turns in sequence)
        if (HasStateAuthority)
        {
            foreach (var player in _allPlayers)
            {
                // Set this player's turn to active
                _isPlayerTurn[player] = true;
                _isMonsterTurn[player] = false;
                
                // Notify the player that it's their turn
                RPC_NotifyPlayerTurnState(player, true);
            }
        }
        
        // Notify about game active state change
        OnGameActiveChanged?.Invoke(true);
    }
    
    // New RPC method to handle turn end requests from any client
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEndTurn(PlayerRef player)
    {
        // Only process if we have state authority
        if (!HasStateAuthority)
        {
            GameManager.Instance.LogManager.LogError($"RPC_RequestEndTurn received but this client doesn't have state authority!");
            return;
        }
        
        GameManager.Instance.LogManager.LogMessage($"Processing turn end request for player {player}");
        
        // If it's currently the player's turn, switch to monster's turn
        if (_isPlayerTurn.ContainsKey(player) && _isPlayerTurn[player])
        {
            // Change to monster's turn
            _isPlayerTurn[player] = false;
            _isMonsterTurn[player] = true;
            
            // Notify all clients about the turn change
            RPC_NotifyPlayerTurnState(player, false);
            
            // Simulate monster's turn after a short delay
            StartCoroutine(SimulateMonsterTurn(player));
            
            GameManager.Instance.LogManager.LogMessage($"Player {player}'s turn ended, monster's turn started");
        }
        else
        {
            GameManager.Instance.LogManager.LogError($"Cannot end turn for player {player}: not currently in player turn state");
        }
    }
    
    // Notify player about their turn state
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerTurnState(PlayerRef player, bool isPlayerTurn)
    {
        // Update local dictionaries
        _isPlayerTurn[player] = isPlayerTurn;
        _isMonsterTurn[player] = !isPlayerTurn;
        
        // Notify UI through event
        OnPlayerTurnChanged?.Invoke(player, isPlayerTurn);
        
        GameManager.Instance.LogManager.LogMessage($"Turn changed for player {player}: {(isPlayerTurn ? "Player's Turn" : "Monster's Turn")}");
    }
    
    // RPC: Ensure all players draw cards
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DrawInitialCardsForAllPlayers()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LogManager.LogMessage("Drawing initial cards for all players");
        
        // First try the normal draw method for authority players
        foreach (var playerEntry in _playerStates)
        {
            playerEntry.Value.DrawInitialHand();
        }
        
        // Use this delayed call to ensure cards are drawn even for non-authority players
        StartCoroutine(DelayedForceCardDraw());
    }
    
    // Delayed force card draw to ensure all players get cards
    private IEnumerator DelayedForceCardDraw()
    {
        // Wait a short time to allow normal draws to complete
        yield return new WaitForSeconds(0.2f);
        
        // Force draw for all players to ensure everyone has cards
        foreach (var playerEntry in _playerStates)
        {
            // Call the force draw method that bypasses authority check
            playerEntry.Value.ForceDrawInitialHand();
        }
        
        if (GameManager.Instance != null)
            GameManager.Instance.LogManager.LogMessage("Forced initial card draw for all players");
    }

    private void AssignMonsterMatchups()
    {
        if (_playerStates.Count < 2)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage("Not enough players to assign matchups, adding AI opponent");
            
            // For single-player testing, create an AI opponent
            if (_playerStates.Count == 1 && HasStateAuthority)
            {
                // Just match the player against their own monster for now
                // In the future, you could create an AI player
                PlayerRef playerRef = _allPlayers[0];
                RPC_SetMonsterMatchup(playerRef, playerRef);
                GameManager.Instance.LogManager.LogMessage($"Single player mode: Player {playerRef} vs own monster");
                return;
            }
        }

        // Create a list of all players
        List<PlayerRef> players = new List<PlayerRef>(_playerStates.Keys);
        
        // Shuffle the players to create random matchups
        ShuffleList(players);
        
        for (int i = 0; i < players.Count; i++)
        {
            // Each player fights the next player's monster (circular)
            PlayerRef opponent = players[(i + 1) % players.Count];
            
            // Use RPCs to inform clients of their matchups
            RPC_SetMonsterMatchup(players[i], opponent);
            
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"Matchup: Player {players[i]} vs Monster from {opponent}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetMonsterMatchup(PlayerRef player, PlayerRef monsterOwner)
    {
        if (_playerStates.TryGetValue(player, out PlayerState playerState) && 
            _playerStates.TryGetValue(monsterOwner, out PlayerState monsterState))
        {
            // Set the opponent monster reference using the PlayerState reference
            playerState.SetOpponentMonster(monsterOwner, monsterState);
            
            GameManager.Instance.LogManager.LogMessage($"Set monster matchup for player {player}");
        }
        else
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"Could not find player states for matchup between {player} and {monsterOwner}");
        }
    }

    // FIXED: NextTurn now handles drawing properly regardless of player state authority
    // FIXED: Properly isolate player turn changes
// FIXED: Properly isolate player turn changes
public bool NextTurn(PlayerRef player)
{
    if (!_isSpawned || !HasStateAuthority || !GameActive)
    {
        GameManager.Instance.LogManager.LogError($"NextTurn precondition failed: Spawned={_isSpawned}, Authority={HasStateAuthority}, GameActive={GameActive}");
        return false;
    }
    
    // Check that we're only processing turn change for a known player
    if (!_playerStates.ContainsKey(player))
    {
        GameManager.Instance.LogManager.LogError($"NextTurn called for unknown player: {player}");
        return false;
    }
    
    // If it's currently the player's turn, switch to monster's turn
    if (_isPlayerTurn.ContainsKey(player) && _isPlayerTurn[player])
    {
        // Change to monster's turn FOR THIS PLAYER ONLY
        _isPlayerTurn[player] = false;
        _isMonsterTurn[player] = true;
        
        // Notify THIS PLAYER it's now the monster's turn
        RPC_NotifyPlayerTurnState(player, false);
        
        // Simulate monster's turn after a short delay FOR THIS PLAYER ONLY
        StartCoroutine(SimulateMonsterTurn(player));
        
        GameManager.Instance.LogManager.LogMessage($"Player {player}'s turn ended, monster's turn started");
        return true;
    }
    // If it's the monster's turn ending
    else if (_isMonsterTurn.ContainsKey(player) && _isMonsterTurn[player])
    {
        // IMPORTANT: This method should not be called directly for monster turn end
        // Instead, SimulateMonsterTurn handles this case to avoid turn handling issues
        GameManager.Instance.LogManager.LogMessage($"NextTurn called for monster turn end - should use SimulateMonsterTurn instead");
        
        // For backward compatibility, we'll handle it anyway but log the message
        
        // Increment player's turn count FOR THIS PLAYER ONLY
        if (_playerTurnCount.ContainsKey(player))
        {
            _playerTurnCount[player]++;
            GameManager.Instance.LogManager.LogMessage($"Player {player} starting turn #{_playerTurnCount[player]}");
        }
        
        // Set back to player's turn FOR THIS PLAYER ONLY
        _isPlayerTurn[player] = true;
        _isMonsterTurn[player] = false;
        
        // Refresh THIS player's resources for the next turn
        if (_playerStates.TryGetValue(player, out PlayerState playerState))
        {
            if (playerState.HasStateAuthority)
            {
                // Reset energy to max
                playerState.ModifyEnergy(playerState.MaxEnergy - playerState.Energy);
                
                // Draw new cards FOR THIS PLAYER ONLY
                GameManager.Instance.LogManager.LogMessage($"Drawing new hand ONLY for player {player}");
                RPC_DrawNewHandForSpecificPlayer(player);
            }
        }
        
        // Notify THIS player it's their turn again
        RPC_NotifyPlayerTurnState(player, true);
        
        GameManager.Instance.LogManager.LogMessage($"Monster's turn for player {player} ended, player's turn started");
        
        // Check if this completed a round for this player
        CheckPlayerRoundComplete(player);
        
        return true;
    }
    
    GameManager.Instance.LogManager.LogError($"Invalid turn state for player {player}");
    return false;
}
    
    // FIXED: Improved RPC to force drawing cards with better logging
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ForceDrawNewHandForPlayer(PlayerRef playerRef)
    {
        bool playerFound = false;
        
        // Try to find player in _playerStates dictionary
        if (_playerStates.TryGetValue(playerRef, out PlayerState playerState))
        {
            playerFound = true;
            GameManager.Instance.LogManager.LogMessage($"RPC_ForceDrawNewHandForPlayer: Drawing new hand for {playerState.PlayerName} (Player {playerRef})");
            
            // Bypass authority checks directly with specialized method
            playerState.ForceDrawNewHandDirectly();
        }
        
        // If not found through dictionary, try searching all player states
        if (!playerFound)
        {
            GameManager.Instance.LogManager.LogError($"RPC_ForceDrawNewHandForPlayer: Could not find PlayerState for Player {playerRef} in dictionary!");
            
            // Try to find the player another way - perhaps by searching all PlayerState objects
            PlayerState[] allPlayerStates = UnityEngine.Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            foreach (PlayerState ps in allPlayerStates)
            {
                if (ps.Object.InputAuthority == playerRef)
                {
                    GameManager.Instance.LogManager.LogMessage($"Found PlayerState through search: {ps.PlayerName}");
                    ps.ForceDrawNewHandDirectly();
                    playerFound = true;
                    break;
                }
            }
        }
        
        if (!playerFound)
        {
            GameManager.Instance.LogManager.LogError($"CRITICAL: Could not find PlayerState for {playerRef} by any method!");
        }
    }
    
    // Check if the player completed a round - you can define this based on turn count
    private void CheckPlayerRoundComplete(PlayerRef player)
    {
        // For demonstration, let's say a "round" for a player is 3 turns
        // This would be adjusted based on your game design
        if (_playerTurnCount.TryGetValue(player, out int turnCount) && turnCount % 3 == 0)
        {
            // Player completed a round
            GameManager.Instance.LogManager.LogMessage($"Player {player} completed a round!");
            _hasCompletedRound[player] = true;
            
            // Check if all players have completed this round
            CheckAllPlayersRoundComplete();
        }
    }
    
    // Check if all players have completed their rounds
    private void CheckAllPlayersRoundComplete()
    {
        if (!HasStateAuthority)
            return;
            
        // Count players who have completed their rounds
        int completedCount = 0;
        foreach (var entry in _hasCompletedRound)
        {
            if (entry.Value)
                completedCount++;
        }
        
        _playersCompletedThisRound = completedCount;
        
        // If all players have completed a round, advance the global round
        if (_playersCompletedThisRound >= _allPlayers.Count)
        {
            // All players have finished their rounds
            RoundComplete = true;
            RPC_TriggerRoundComplete();
            
            // Check if game should end
            CheckGameEnd();
            
            if (!GameActive)
            {
                return;
            }
            
            // Start draft phase - this is a global state
            DraftPhaseActive = true;
            OnDraftPhaseChanged?.Invoke(true);
            GenerateDraftOptions();
            
            // Reset counters for next round
            _playersCompletedThisRound = 0;
            foreach (var player in _allPlayers)
            {
                _hasCompletedRound[player] = false;
            }
        }
    }
    
    // FIXED: Improved monster turn simulation with explicit logging
    // FIXED: Improved monster turn simulation with proper player isolation
private IEnumerator SimulateMonsterTurn(PlayerRef player)
{
    GameManager.Instance.LogManager.LogMessage($"Starting monster turn simulation for player {player} ONLY");
    
    // Wait a short time to simulate monster thinking
    yield return new WaitForSeconds(1.0f);
    
    // For now, just immediately end the monster's turn
    // In the future, implement monster AI actions here
    GameManager.Instance.LogManager.LogMessage($"Monster turn simulation completed for player {player}");
    
    // CRITICAL FIX: Only send turn end for the specific player whose monster just acted
    if (HasStateAuthority)
    {
        // Do not call NextTurn here, which might be affecting all players
        // Instead, directly manage just this player's turn
        if (_isMonsterTurn.ContainsKey(player) && _isMonsterTurn[player])
        {
            // Increment player's turn count
            if (_playerTurnCount.ContainsKey(player))
            {
                _playerTurnCount[player]++;
                GameManager.Instance.LogManager.LogMessage($"Player {player} starting turn #{_playerTurnCount[player]}");
            }
            
            // Set back to player's turn for THIS PLAYER ONLY
            _isPlayerTurn[player] = true;
            _isMonsterTurn[player] = false;
            
            // Refresh THIS player's resources for their next turn
            if (_playerStates.TryGetValue(player, out PlayerState playerState))
            {
                if (playerState.HasStateAuthority)
                {
                    // Reset energy to max
                    playerState.ModifyEnergy(playerState.MaxEnergy - playerState.Energy);
                    
                    // Draw new cards for THIS PLAYER ONLY
                    GameManager.Instance.LogManager.LogMessage($"Drawing new hand ONLY for player {player} after their monster's turn");
                    RPC_DrawNewHandForSpecificPlayer(player);
                }
            }
            
            // Notify THIS player it's their turn again
            RPC_NotifyPlayerTurnState(player, true);
            
            GameManager.Instance.LogManager.LogMessage($"Monster's turn for player {player} ended, player's turn started");
            
            // Check if this completed a round for this player
            CheckPlayerRoundComplete(player);
        }
        else
        {
            GameManager.Instance.LogManager.LogError($"Cannot end monster turn for player {player}: not currently in monster turn state");
        }
    }
    else
    {
        GameManager.Instance.LogManager.LogError($"Cannot end monster turn: no state authority");
    }
}

// NEW: Draw cards for a specific player only - prevents drawing for all players
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_DrawNewHandForSpecificPlayer(PlayerRef playerRef)
{
    // Only process the card draw for the specified player
    if (_playerStates.TryGetValue(playerRef, out PlayerState playerState))
    {
        GameManager.Instance.LogManager.LogMessage($"RPC_DrawNewHandForSpecificPlayer: Drawing new hand ONLY for {playerState.PlayerName} (Player {playerRef})");
        
        // Call forced draw on this specific player only
        playerState.ForceDrawNewHandDirectly();
    }
    else
    {
        GameManager.Instance.LogManager.LogError($"RPC_DrawNewHandForSpecificPlayer: Could not find PlayerState for {playerRef}");
    }
}

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerRoundComplete()
    {
        OnRoundComplete?.Invoke();
    }

    private void CheckGameEnd()
    {
        foreach (var playerEntry in _playerStates)
        {
            if (playerEntry.Value.GetScore() >= RoundsToWin)
            {
                // Game over, this player won
                RPC_TriggerGameComplete(playerEntry.Key);
                GameActive = false;
                OnGameActiveChanged?.Invoke(false);
                break;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerGameComplete(PlayerRef winner)
    {
        OnGameComplete?.Invoke();
        if (GameManager.Instance != null)
            GameManager.Instance.LogManager.LogMessage($"Game complete! Player {winner} is the winner!");
    }

    private void GenerateDraftOptions()
    {
        // Generate draft options will be implemented later
        // For now it's just a placeholder
        
        // After draft phase completes:
        // DraftPhaseActive = false;
        // StartNextRound();
        
        // For now, just start next round immediately
        StartNextRound();
    }

    // StartNextRound advances the global round counter
    // StartNextRound advances the global round counter
private void StartNextRound()
{
    if (_isSpawned && HasStateAuthority)
    {
        CurrentRound++;
        RoundComplete = false;
        
        // Notify clients
        RPC_RoundChanged(CurrentRound);
        
        // Reset player state for new round
        foreach (var playerEntry in _playerStates)
        {
            // Reset monster health etc.
            playerEntry.Value.PrepareForNewRound();
            
            // Reset round completion tracking
            if (_hasCompletedRound.ContainsKey(playerEntry.Key))
            {
                _hasCompletedRound[playerEntry.Key] = false;
            }
        }
        
        // Assign new monster matchups
        AssignMonsterMatchups();
        
        // CRITICAL FIX: Draw cards for each player INDIVIDUALLY to prevent cross-talk
        // This ensures players get their initial hands without affecting each other
        foreach (var player in _allPlayers)
        {
            // Set each player to player turn
            _isPlayerTurn[player] = true;
            _isMonsterTurn[player] = false;
            
            // Draw cards for THIS PLAYER ONLY using new specific method
            RPC_DrawNewHandForSpecificPlayer(player);
            
            // Notify the player about their turn state
            RPC_NotifyPlayerTurnState(player, true);
            
            // Add small delay between player setups to avoid network contention
            StartCoroutine(DelayBetweenPlayerSetups());
        }
        
        GameManager.Instance.LogManager.LogMessage("All players initialized for new round with individual turns and card draws");
    }
}

// Helper coroutine to add slight delay between player turn setups
private IEnumerator DelayBetweenPlayerSetups()
{
    yield return new WaitForSeconds(0.1f);
}

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RoundChanged(int round)
    {
        OnRoundChanged?.Invoke(round);
    }

    private static void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    public PlayerState GetLocalPlayerState()
    {
        var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
        if (networkRunner != null)
        {
            PlayerRef localPlayerRef = networkRunner.LocalPlayer;
            if (_playerStates.TryGetValue(localPlayerRef, out PlayerState state))
            {
                return state;
            }
        }
        return null;
    }

    public Dictionary<PlayerRef, PlayerState> GetAllPlayerStates()
    {
        return new Dictionary<PlayerRef, PlayerState>(_playerStates);
    }

    public PlayerRef GetLocalPlayerRef()
    {
        var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
        return networkRunner?.LocalPlayer ?? default;
    }

    // Helper method to get a PlayerState by PlayerRef
    public PlayerState GetPlayerState(PlayerRef playerRef)
    {
        if (_playerStates.TryGetValue(playerRef, out PlayerState state))
        {
            return state;
        }
        return null;
    }

    // Check if it's the local player's turn based on player-specific turn state
    public bool IsLocalPlayerTurn()
    {
        if (!_isSpawned) return false;
        
        var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
        if (networkRunner == null) return false;
        
        PlayerRef localPlayer = networkRunner.LocalPlayer;
        
        // Check if it's this player's turn in our dictionary
        if (_isPlayerTurn.TryGetValue(localPlayer, out bool isPlayerTurn))
        {
            return isPlayerTurn;
        }
        
        // Default to allowing the turn if we're not sure
        // This helps during initialization and prevents blocks
        return true;
    }

    // Get the current turn number for a player
    public int GetPlayerTurnCount(PlayerRef player)
    {
        if (_playerTurnCount.TryGetValue(player, out int turnCount))
        {
            return turnCount;
        }
        return 1; // Default to 1 if not found
    }

    // Safe accessors for networked properties
    public int GetCurrentRound()
    {
        return _isSpawned ? CurrentRound : 1;
    }

    public bool IsDraftPhaseActive()
    {
        return _isSpawned && DraftPhaseActive;
    }

    public bool IsGameActive()
    {
        return _isSpawned && GameActive;
    }

    // Get turn state for a specific player
    public bool IsPlayerTurn(PlayerRef player)
    {
        if (_isPlayerTurn.TryGetValue(player, out bool isPlayerTurn))
        {
            return isPlayerTurn;
        }
        return false;
    }

    public bool IsSpawned()
    {
        return _isSpawned;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        
        // Only clear the instance if this is the current instance
        if (_instance == this)
        {
            _instance = null;
            _isSpawned = false;
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage("GameState instance cleared on despawn");
        }
    }
}