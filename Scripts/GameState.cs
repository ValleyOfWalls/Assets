using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// This class handles the networked game state
public class GameState : NetworkBehaviour
{
    // Singleton instance
    private static GameState _instance;
    public static GameState Instance => _instance;

    // Game progression
    [Networked] public int CurrentRound { get; set; }
    [Networked] public NetworkBool DraftPhaseActive { get; set; }
    [Networked] public NetworkBool GameActive { get; set; }
    [Networked] public NetworkBool RoundComplete { get; set; }
    [Networked] public int CurrentTurnPlayerIndex { get; set; }
    [Networked] public int RoundsToWin { get; set; }

    // Events
    public static event Action<int> OnRoundChanged;
    public static event Action<bool> OnDraftPhaseChanged;
    public static event Action<int> OnTurnChanged;
    public static event Action<bool> OnGameActiveChanged;
    public static event Action OnRoundComplete;
    public static event Action OnGameComplete;

    // Track previous values for change detection
    private int _previousTurnPlayerIndex;

    // Player references
    private Dictionary<PlayerRef, PlayerState> _playerStates = new Dictionary<PlayerRef, PlayerState>();
    private List<PlayerRef> _turnOrder = new List<PlayerRef>();

    // Card collections
    private List<CardData> _draftPool = new List<CardData>();
    
    // Flag to track if we're already networked
    private bool _isNetworked = false;

    // Called when the component is first initialized
    private void Awake()
    {
        // Set instance in Awake to make sure it's available early
        SetupSingleton();
    }

    // Also set the instance in OnEnable for redundancy
    private void OnEnable()
    {
        SetupSingleton();
    }

    // Set up the singleton instance
    private void SetupSingleton()
    {
        if (_instance == null)
        {
            _instance = this;
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            {
                GameManager.Instance.LogManager.LogMessage("GameState singleton instance set");
            }
        }
        else if (_instance != this)
        {
            // Instead of creating multiple instances, just log a warning and destroy this one
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            {
                GameManager.Instance.LogManager.LogMessage("Duplicate GameState detected - destroying duplicate");
            }
            
            // Destroy this instance since we already have a singleton
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
        }
    }
    
    // Call this when the game starts to network the GameState
    public void NetworkGameState()
    {
        if (_isNetworked) return;
        
        // Get network runner
        var runner = GameManager.Instance.NetworkManager.GetRunner();
        if (runner == null)
        {
            GameManager.Instance.LogManager.LogError("Cannot network GameState: NetworkRunner is null");
            return;
        }
        
        // Check if we already have a NetworkObject component
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            GameManager.Instance.LogManager.LogError("GameState is missing NetworkObject component!");
            return;
        }
        
        // Network this object
        if (runner.IsRunning)
        {
            try
            {
                runner.Spawn(networkObject);
                _isNetworked = true;
                GameManager.Instance.LogManager.LogMessage("GameState networked successfully");
            }
            catch (Exception ex)
            {
                GameManager.Instance.LogManager.LogError($"Failed to network GameState: {ex.Message}");
            }
        }
        else
        {
            GameManager.Instance.LogManager.LogError("Cannot network GameState: NetworkRunner is not running");
        }
    }

    public override void Spawned()
    {
        base.Spawned();
        
        // Ensure instance is set when spawned on the network
        // This is important because Spawned() might be called on a different instance
        // than the one we created in Awake() if we're using a prefab
        SetupSingleton();
        
        if (HasStateAuthority)
        {
            // Initialize game defaults
            CurrentRound = 1;
            RoundsToWin = 3;
            GameActive = false;
            DraftPhaseActive = false;
            RoundComplete = false;
            CurrentTurnPlayerIndex = 0;
        }
        
        // Initialize tracking variables
        _previousTurnPlayerIndex = CurrentTurnPlayerIndex;
        
        GameManager.Instance.LogManager.LogMessage("GameState spawned and initialized");
    }

    // Manual change detection in Render/FixedUpdateNetwork
    public override void Render()
    {
        base.Render();
        
        // Check for changes in turn player
        if (_previousTurnPlayerIndex != CurrentTurnPlayerIndex)
        {
            OnTurnChanged?.Invoke(CurrentTurnPlayerIndex);
            _previousTurnPlayerIndex = CurrentTurnPlayerIndex;
        }
    }

    public void RegisterPlayer(PlayerRef player, PlayerState state)
    {
        if (!_playerStates.ContainsKey(player))
        {
            _playerStates.Add(player, state);
            
            if (HasStateAuthority && !_turnOrder.Contains(player))
            {
                _turnOrder.Add(player);
            }
            
            GameManager.Instance.LogManager.LogMessage($"Player {player} registered with GameState");
        }
    }

    public void StartGame()
    {
        if (HasStateAuthority)
        {
            GameActive = true;
            CurrentRound = 1;
            GameManager.Instance.LogManager.LogMessage("Game starting!");
            
            // Assign initial monster matchups
            AssignMonsterMatchups();
            
            // Deal initial cards
            DealInitialCards();
        }
    }

    private void AssignMonsterMatchups()
    {
        if (_playerStates.Count < 2)
        {
            GameManager.Instance.LogManager.LogMessage("Not enough players to assign matchups");
            return;
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
            
            GameManager.Instance.LogManager.LogMessage($"Matchup: Player {players[i]} vs Monster from {opponent}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetMonsterMatchup(PlayerRef player, PlayerRef monsterOwner)
    {
        if (_playerStates.TryGetValue(player, out PlayerState playerState) && 
            _playerStates.TryGetValue(monsterOwner, out PlayerState monsterState))
        {
            playerState.SetOpponentMonster(monsterState.GetMonster());
            GameManager.Instance.LogManager.LogMessage($"Set monster matchup for player {player}");
        }
    }

    private void DealInitialCards()
    {
        foreach (var playerEntry in _playerStates)
        {
            // Deal initial cards to each player
            playerEntry.Value.DrawInitialHand();
        }
    }

    public void NextTurn()
    {
        if (HasStateAuthority)
        {
            // Check if all players have taken their turn
            int nextIndex = (CurrentTurnPlayerIndex + 1) % _turnOrder.Count;
            
            if (nextIndex == 0)
            {
                // All players have taken their turn
                RoundComplete = true;
                RPC_TriggerRoundComplete();
                
                // Check if game should end
                CheckGameEnd();
                
                if (!GameActive)
                {
                    return;
                }
                
                // Start draft phase
                DraftPhaseActive = true;
                GenerateDraftOptions();
            }
            else
            {
                // Move to next player's turn
                CurrentTurnPlayerIndex = nextIndex;
            }
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
                break;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerGameComplete(PlayerRef winner)
    {
        OnGameComplete?.Invoke();
        GameManager.Instance.LogManager.LogMessage($"Game complete! Player {winner} is the winner!");
    }

    private void GenerateDraftOptions()
    {
        // Generate draft options will be implemented later
        // For now it's just a placeholder
        
        // After draft phase completes:
        // DraftPhaseActive = false;
        // StartNextRound();
    }

    private void StartNextRound()
    {
        if (HasStateAuthority)
        {
            CurrentRound++;
            RoundComplete = false;
            CurrentTurnPlayerIndex = 0;
            
            // Notify clients
            RPC_RoundChanged(CurrentRound);
            
            // Reset player state for new round
            foreach (var playerEntry in _playerStates)
            {
                playerEntry.Value.PrepareForNewRound();
            }
            
            // Assign new monster matchups
            AssignMonsterMatchups();
        }
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
        var networkRunner = GameManager.Instance.NetworkManager.GetRunner();
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
        return _playerStates;
    }

    public PlayerRef GetLocalPlayerRef()
    {
        var networkRunner = GameManager.Instance.NetworkManager.GetRunner();
        return networkRunner?.LocalPlayer ?? default;
    }

    public bool IsLocalPlayerTurn()
    {
        if (_turnOrder.Count == 0) return false;
        
        var networkRunner = GameManager.Instance.NetworkManager.GetRunner();
        if (networkRunner == null) return false;
        
        PlayerRef localPlayer = networkRunner.LocalPlayer;
        return _turnOrder[CurrentTurnPlayerIndex] == localPlayer;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (_instance == this)
        {
            _instance = null;
            GameManager.Instance.LogManager.LogMessage("GameState instance cleared on despawn");
        }
    }
}