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
            CurrentTurnPlayerIndex = 0;
            
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage("GameState initialized with default values");
        }
        
        // Initialize tracking variables
        _previousTurnPlayerIndex = CurrentTurnPlayerIndex;
    }

    // Manual change detection in Render
    public override void Render()
    {
        base.Render();
        
        // Check for changes in turn player
        if (_isSpawned && _previousTurnPlayerIndex != CurrentTurnPlayerIndex)
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
            
            if (_isSpawned && HasStateAuthority && !_turnOrder.Contains(player))
            {
                _turnOrder.Add(player);
            }
            
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"Player {player} registered with GameState");
        }
    }

    public void StartGame()
    {
        if (_isSpawned && HasStateAuthority)
        {
            GameActive = true;
            CurrentRound = 1;
            
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage("Game starting on network!");
            
            // Use coroutine with delay to ensure all players are properly initialized
            StartCoroutine(DelayedMatchupAssignment());
        }
    }

    // New coroutine to delay matchup assignment
    private IEnumerator DelayedMatchupAssignment()
    {
        // Wait a short time for all player states to initialize
        yield return new WaitForSeconds(1.0f);
        
        // Assign initial monster matchups
        AssignMonsterMatchups();
        
        // Deal initial cards
        DealInitialCards();
    }

    private void AssignMonsterMatchups()
    {
        if (_playerStates.Count < 2)
        {
            if (GameManager.Instance != null)
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
            Monster opponentMonster = monsterState.GetMonster();
            if (opponentMonster == null)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.LogManager.LogMessage($"Warning: Monster from player {monsterOwner} is null");
                
                // Create a temporary monster if needed
                opponentMonster = new Monster
                {
                    Name = $"Temporary Monster",
                    Health = 40,
                    MaxHealth = 40,
                    Attack = 5,
                    Defense = 3,
                    TintColor = Color.red
                };
            }
            
            playerState.SetOpponentMonster(opponentMonster);
            
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"Set monster matchup for player {player}");
        }
        else
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"Could not find player states for matchup between {player} and {monsterOwner}");
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
        if (_isSpawned && HasStateAuthority)
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
    }

    private void StartNextRound()
    {
        if (_isSpawned && HasStateAuthority)
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
        return _playerStates;
    }

    public PlayerRef GetLocalPlayerRef()
    {
        var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
        return networkRunner?.LocalPlayer ?? default;
    }

    public bool IsLocalPlayerTurn()
    {
        if (!_isSpawned || _turnOrder.Count == 0) return false;
        
        var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
        if (networkRunner == null) return false;
        
        PlayerRef localPlayer = networkRunner.LocalPlayer;
        return _turnOrder[CurrentTurnPlayerIndex] == localPlayer;
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

    public int GetCurrentTurnPlayerIndex()
    {
        return _isSpawned ? CurrentTurnPlayerIndex : 0;
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