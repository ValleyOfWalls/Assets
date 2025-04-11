using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// This class handles the networked game state: Rounds, Matchups, Fight Completion, Draft Phase
public class GameState : NetworkBehaviour
{
    // Singleton instance - only valid when spawned
    private static GameState _instance;
    public static GameState Instance => _instance;

    // Game progression
    [Networked] public int CurrentRound { get; set; }
    [Networked] public NetworkBool DraftPhaseActive { get; set; }
    [Networked] public NetworkBool GameActive { get; set; }
    [Networked] public int RoundsToWin { get; set; }

    // --- Fight Completion Tracking ---
    // This dictionary is managed locally on the State Authority and synced via RPCs
    // Key: PlayerRef, Value: bool (true if fight is complete for the round)
    private Dictionary<PlayerRef, bool> _playerFightCompletion = new Dictionary<PlayerRef, bool>();

    // --- Events ---
    // Network-synced state changes often trigger local events for UI/Logic
    public static event Action<int> OnRoundChanged;              // Fired when CurrentRound changes
    public static event Action<bool> OnDraftPhaseChanged;        // Fired when DraftPhaseActive changes
    public static event Action<bool> OnGameActiveChanged;        // Fired when GameActive changes
    public static event Action OnAllFightsCompleteForRound; // Fired when all players complete their fight
    public static event Action OnGameComplete;              // Fired when a player wins the game
    public static event Action<PlayerRef, PlayerState> OnPlayerStateAdded;    // Fired when a PlayerState registers
    public static event Action<PlayerRef, PlayerState> OnPlayerStateRemoved;  // Fired when a PlayerState unregisters
    public static event Action<PlayerRef, bool> OnPlayerFightCompletionUpdated; // Fired LOCALLY when a player's completion status is updated

    // --- Player References ---
    private Dictionary<PlayerRef, PlayerState> _playerStates = new Dictionary<PlayerRef, PlayerState>();
    private List<PlayerRef> _allPlayers = new List<PlayerRef>(); // Keeps track of active player refs

    // Card collections (for draft phase - currently unused placeholder)
    private List<CardData> _draftPool = new List<CardData>();

    // Flag to track spawned status
    private bool _isSpawned = false;

    // Initialization
    private void Awake()
    {
        // Instance will be set in Spawned
        GameManager.Instance?.LogManager?.LogMessage("GameState Awake called");
    }

    public override void Spawned()
    {
        base.Spawned();
        _instance = this;
        _isSpawned = true;

        GameManager.Instance?.LogManager?.LogMessage("GameState spawned and singleton set");

        if (HasStateAuthority)
        {
            // Initialize game defaults only on State Authority
            CurrentRound = 1;
            RoundsToWin = 3; // Example value
            GameActive = false;
            DraftPhaseActive = false;
            _playerFightCompletion.Clear(); // Clear dictionary on spawn
            GameManager.Instance?.LogManager?.LogMessage("GameState (Authority) initialized default network properties.");
        }
        // All clients need to potentially react to network changes, so event subscriptions happen regardless of authority
    }

    // --- Player Registration ---

    public void RegisterPlayer(PlayerRef player, PlayerState state)
    {
        if (!_playerStates.ContainsKey(player))
        {
            _playerStates.Add(player, state);
            GameManager.Instance?.LogManager?.LogMessage($"Player {player} registered PlayerState {state.Id}.");

            // Only State Authority manages the list of active players and completion status
            if (HasStateAuthority)
            {
                 if (!_allPlayers.Contains(player))
                 {
                     _allPlayers.Add(player);
                      // Initialize fight completion tracking for this new player
                     _playerFightCompletion[player] = false;
                     // Notify all clients about the new player's fight status via RPC
                     RPC_UpdatePlayerFightCompletion(player, false);
                      GameManager.Instance?.LogManager?.LogMessage($"Authority added Player {player} to active list and set fight complete=false.");
                 }
            }
            OnPlayerStateAdded?.Invoke(player, state); // Notify local listeners
        }
         else
        {
             // GameManager.Instance?.LogManager?.LogMessage($"Player {player} attempted to register PlayerState {state.Id}, but was already registered.");
        }
    }

    public void UnregisterPlayer(PlayerRef player)
    {
        if (_playerStates.TryGetValue(player, out PlayerState state))
        {
            OnPlayerStateRemoved?.Invoke(player, state); // Notify local listeners first
            _playerStates.Remove(player);
            GameManager.Instance?.LogManager?.LogMessage($"Player {player} unregistered PlayerState {state?.Id}.");

            // State Authority removes from tracking lists
            if (HasStateAuthority)
            {
                _allPlayers.Remove(player);
                if (_playerFightCompletion.Remove(player))
                {
                     GameManager.Instance?.LogManager?.LogMessage($"Authority removed Player {player} from active list and completion tracking.");
                }
                 // Optional: Could send an RPC to explicitly tell clients to remove the player's UI if needed
            }
        }
    }

    // --- Game Flow ---

    public void StartGame()
    {
        if (!HasStateAuthority) return; // Only authority can start the game
        if (!_isSpawned || GameActive) return; // Don't start if not spawned or already active

        GameActive = true;
        CurrentRound = 1;
        DraftPhaseActive = false; // Ensure draft phase is off

        // Reset fight completion tracker for all currently registered players
        // Create a copy of keys to avoid modification during iteration issues
        List<PlayerRef> playersToReset = new List<PlayerRef>(_playerFightCompletion.Keys);
        foreach (var player in playersToReset)
        {
            _playerFightCompletion[player] = false;
            // Notify clients of the reset status
            RPC_UpdatePlayerFightCompletion(player, false);
        }

        GameManager.Instance?.LogManager?.LogMessage("Authority: Game starting! Fight completion reset for all players.");
        OnGameActiveChanged?.Invoke(true); // Trigger local event (authority only initially)

        // Assign matchups and trigger initial draws after a short delay
        StartCoroutine(DelayedMatchupAssignmentAndDraw());
    }

    private IEnumerator DelayedMatchupAssignmentAndDraw()
    {
        yield return new WaitForSeconds(1.0f); // Allow time for players to potentially register

        if (!GameActive) yield break; // Check if game was stopped during delay

        GameManager.Instance?.LogManager?.LogMessage($"Authority: Assigning matchups for {_allPlayers.Count} players.");
        AssignMonsterMatchups(); // Assign initial monster matchups

        // Trigger local card draw for all players
        foreach (var player in _allPlayers)
        {
            RPC_TriggerLocalDraw(player);
        }
        GameManager.Instance?.LogManager?.LogMessage($"Authority: Triggered initial local draw for all players.");
    }

    // RPC to tell a specific client to perform a local card draw at game start
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerLocalDraw(PlayerRef playerRef, RpcInfo info = default)
    {
        // Check if this RPC is targeted at the local player machine
        if (Runner != null && playerRef == Runner.LocalPlayer)
        {
            PlayerState localPlayerState = GetLocalPlayerState();
            if (localPlayerState != null)
            {
                // GameManager.Instance?.LogManager?.LogMessage($"RPC_TriggerLocalDraw received for local player {playerRef}. Telling PlayerState to draw.");
                localPlayerState.DrawInitialHandLocally(); // Call the local draw method
            }
             else { /* Log warning if needed */ }
        }
    }

    // RPC to tell a specific client to draw for a new round
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerLocalNewRoundDraw(PlayerRef playerRef, RpcInfo info = default)
    {
        if (Runner != null && playerRef == Runner.LocalPlayer)
        {
            PlayerState localPlayerState = GetLocalPlayerState();
            if (localPlayerState != null)
            {
                // GameManager.Instance?.LogManager?.LogMessage($"RPC_TriggerLocalNewRoundDraw received for local player {playerRef}. Telling PlayerState to draw new hand.");
                localPlayerState.DrawNewHandLocally(); // Call the local new hand draw method
            }
             else { /* Log warning if needed */ }
        }
    }

    // --- Fight Completion Handling ---

    // RPC Called by a client's PlayerState (via InputAuthority) when they finish their fight locally
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_NotifyFightComplete(PlayerRef player, RpcInfo info = default)
    {
        if (!HasStateAuthority) return;

        if (_playerFightCompletion.ContainsKey(player))
        {
            if (!_playerFightCompletion[player]) // Only process if not already complete
            {
                GameManager.Instance?.LogManager?.LogMessage($"Authority: Fight completion received from player {player}.");
                _playerFightCompletion[player] = true;

                // Notify all clients about this player's completion
                RPC_UpdatePlayerFightCompletion(player, true);

                // Check if all players have now completed their fights
                CheckAllFightsComplete();
            }
             //else { GameManager.Instance?.LogManager?.LogMessage($"Authority: Fight completion received from player {player}, but they were already marked complete."); }
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"Warning: Authority: Fight completion received from unknown or unregistered player {player}."); }
    }

    // RPC Called by State Authority to update the fight completion status on all clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdatePlayerFightCompletion(PlayerRef player, bool isComplete, RpcInfo info = default)
    {
         // Update local dictionary on all clients
        _playerFightCompletion[player] = isComplete;
        OnPlayerFightCompletionUpdated?.Invoke(player, isComplete); // Notify local listeners (e.g., UI)
        // GameManager.Instance?.LogManager?.LogMessage($"Client: Fight completion status updated for player {player} to {isComplete}.");
    }

    // Checks if all active players have completed their fights for the current round
    private void CheckAllFightsComplete()
    {
        if (!HasStateAuthority || !GameActive) return; // Only authority checks during active game

        int completedCount = 0;
        int requiredCount = _allPlayers.Count; // Number of players expected to complete

        if (requiredCount == 0) return; // No players, nothing to check

        foreach (var player in _allPlayers)
        {
            if (_playerFightCompletion.TryGetValue(player, out bool isComplete) && isComplete)
            {
                completedCount++;
            }
        }

        // GameManager.Instance?.LogManager?.LogMessage($"Authority: Checking fight completion: {completedCount}/{requiredCount} players complete.");

        if (completedCount >= requiredCount)
        {
            GameManager.Instance?.LogManager?.LogMessage("Authority: All players have completed their fights for this round.");
            RPC_TriggerAllFightsCompleteForRound(); // Notify clients

            // Check if game should end based on score
            CheckGameEnd();
            if (!GameActive) return; // Stop if game ended

            // Start draft phase (or next round if draft is skipped)
            StartDraftPhase();
        }
    }

     // RPC to notify clients that all fights for the round are complete
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerAllFightsCompleteForRound(RpcInfo info = default)
    {
        OnAllFightsCompleteForRound?.Invoke(); // Trigger local event
        // GameManager.Instance?.LogManager?.LogMessage($"Client: RPC_TriggerAllFightsCompleteForRound received.");
    }


    // --- Matchmaking & Round Progression ---

    private void AssignMonsterMatchups()
    {
        if (!HasStateAuthority) return;

        if (_playerStates.Count < 2)
        {
            GameManager.Instance?.LogManager?.LogMessage("Authority: Not enough players for standard matchups.");
            if (_playerStates.Count == 1)
            {
                PlayerRef playerRef = _allPlayers[0];
                RPC_SetMonsterMatchup(playerRef, playerRef); // Player fights their own monster
                GameManager.Instance?.LogManager?.LogMessage($"Authority: Single player mode: Player {playerRef} vs own monster");
            }
            return;
        }

        List<PlayerRef> players = new List<PlayerRef>(_allPlayers); // Use the tracked list
        ShuffleList(players);
        for (int i = 0; i < players.Count; i++)
        {
            PlayerRef opponent = players[(i + 1) % players.Count]; // Circular assignment
            RPC_SetMonsterMatchup(players[i], opponent);
            // GameManager.Instance?.LogManager?.LogMessage($"Authority: Matchup: Player {players[i]} vs Monster from {opponent}");
        }
    }

    // RPC to tell clients who their opponent is for the round
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetMonsterMatchup(PlayerRef player, PlayerRef monsterOwner, RpcInfo info = default)
    {
        if (_playerStates.TryGetValue(player, out PlayerState playerState) &&
            _playerStates.TryGetValue(monsterOwner, out PlayerState monsterState))
        {
            playerState.SetOpponentMonsterLocally(monsterOwner, monsterState); // Use local method on PlayerState
            // GameManager.Instance?.LogManager?.LogMessage($"Client: Set monster matchup locally for player {player} (Opponent: {monsterOwner})");
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"Client: Could not find player states for matchup RPC between {player} and {monsterOwner}"); }
    }


    private void StartDraftPhase()
    {
        if (!HasStateAuthority || !GameActive) return;

        DraftPhaseActive = true;
        RPC_DraftPhaseChanged(true); // Notify clients
        GameManager.Instance?.LogManager?.LogMessage("Authority: Starting Draft Phase.");

        // --- Draft Logic Placeholder ---
        // GenerateDraftOptions();
        // HandleDraftSelections(); // This would likely involve more RPCs
        // --- End Placeholder ---

        // For now, skip draft and immediately end it to start the next round
        StartCoroutine(SimulateDraftPhase()); // Simulate a delay for draft
    }

    private IEnumerator SimulateDraftPhase()
    {
         yield return new WaitForSeconds(2.0f); // Placeholder delay for draft
         EndDraftPhase();
    }


    private void EndDraftPhase()
    {
        if (!HasStateAuthority || !GameActive) return;

        DraftPhaseActive = false;
        RPC_DraftPhaseChanged(false); // Notify clients
        GameManager.Instance?.LogManager?.LogMessage("Authority: Ending Draft Phase.");

        // Start the next round
        StartNextRound();
    }

    // RPC to notify clients about draft phase state changes
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DraftPhaseChanged(bool isActive, RpcInfo info = default)
    {
        OnDraftPhaseChanged?.Invoke(isActive);
        // GameManager.Instance?.LogManager?.LogMessage($"Client: RPC_DraftPhaseChanged received: {isActive}");
    }


    private void StartNextRound()
    {
        if (!HasStateAuthority || !GameActive) return;

        CurrentRound++;
        RPC_RoundChanged(CurrentRound); // Notify clients of the new round number

        // Reset player states and fight completion for the new round
        // Create a copy of keys to avoid modification during iteration issues
        List<PlayerRef> playersToPrepare = new List<PlayerRef>(_playerFightCompletion.Keys);
        foreach (var playerRef in playersToPrepare)
        {
            // Tell the authoritative PlayerState to prepare for the new round via RPC
             if(_playerStates.TryGetValue(playerRef, out PlayerState playerState))
             {
                 playerState.RPC_PrepareForNewRound();
             }

            // Reset fight completion status locally on the server
            _playerFightCompletion[playerRef] = false;
            // Notify all clients of the reset status
            RPC_UpdatePlayerFightCompletion(playerRef, false);
        }

        // Assign new monster matchups
        AssignMonsterMatchups();

        // Trigger local card draw for all players for the new round
        foreach (var player in _allPlayers)
        {
            RPC_TriggerLocalNewRoundDraw(player);
        }

        GameManager.Instance?.LogManager?.LogMessage($"Authority: Starting Round {CurrentRound}. Matchups assigned, players reset, draw triggered.");
    }

    // RPC to update round number on clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RoundChanged(int round, RpcInfo info = default)
    {
        OnRoundChanged?.Invoke(round);
        // GameManager.Instance?.LogManager?.LogMessage($"Client: RPC_RoundChanged received: Round {round}");
    }

    // --- Game End Condition ---

    private void CheckGameEnd()
    {
        if (!HasStateAuthority || !GameActive) return;

        foreach (var playerEntry in _playerStates)
        {
            // Use GetScore() which accesses the networked property
            if (playerEntry.Value.GetScore() >= RoundsToWin)
            {
                GameManager.Instance?.LogManager?.LogMessage($"Authority: Player {playerEntry.Key} reached score limit ({RoundsToWin})!");
                RPC_TriggerGameComplete(playerEntry.Key); // Notify all clients of winner
                GameActive = false; // Stop the game on authority
                OnGameActiveChanged?.Invoke(false); // Trigger local event
                return; // Exit once a winner is found
            }
        }
    }

    // RPC to notify all clients that the game is over and who won
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerGameComplete(PlayerRef winner, RpcInfo info = default)
    {
        OnGameComplete?.Invoke(); // Trigger local game complete event
        // GameManager.Instance?.LogManager?.LogMessage($"Client: RPC_TriggerGameComplete received! Winner: Player {winner}");
        // UI can react to OnGameComplete event
    }


    // --- Utility & Accessors ---

    private static void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        System.Random rng = new System.Random(); // Consider seeding if needed
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]); // Swap using tuple deconstruction
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
        return new Dictionary<PlayerRef, PlayerState>(_playerStates); // Return a copy
    }

    public PlayerRef GetLocalPlayerRef()
    {
        var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
        return networkRunner?.LocalPlayer ?? default;
    }

    public PlayerState GetPlayerState(PlayerRef playerRef)
    {
        _playerStates.TryGetValue(playerRef, out PlayerState state);
        return state;
    }

    // Method to check fight completion status LOCALLY for a specific player
    public bool IsPlayerFightComplete(PlayerRef playerRef)
    {
        // Reads the local dictionary which is kept in sync by RPC_UpdatePlayerFightCompletion
        return _playerFightCompletion.TryGetValue(playerRef, out bool isComplete) && isComplete;
    }

    // Safe accessors for networked properties
    public int GetCurrentRound() => _isSpawned ? CurrentRound : 1;
    public bool IsDraftPhaseActive() => _isSpawned && DraftPhaseActive;
    public bool IsGameActive() => _isSpawned && GameActive;
    public bool IsSpawned() => _isSpawned;


    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (_instance == this)
        {
            _instance = null;
            _isSpawned = false;
            // Cleanup if necessary
            _playerStates.Clear();
            _allPlayers.Clear();
            _playerFightCompletion.Clear();
            GameManager.Instance?.LogManager?.LogMessage("GameState instance cleared and collections reset on despawn");
        }
    }
}