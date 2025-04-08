using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    // Dictionary to keep track of players in the lobby by name
    private Dictionary<string, PlayerRef> _playersByName = new Dictionary<string, PlayerRef>();
    // Dictionary to keep track of ready status by player name
    private Dictionary<string, bool> _readyStatusByName = new Dictionary<string, bool>();
    // Dictionary to store player data for rejoining
    private Dictionary<string, PlayerData> _playerDataByName = new Dictionary<string, PlayerData>();
    
    // Non-networked backup properties for initialization
    private bool _localCountdownActive = false;
    private float _localCountdownTime = 3.0f;
    private float _localCurrentCountdown = 0f;
    private bool _localGameStarted = false;
    
    // Networked properties - only access these after spawned
    [Networked] private NetworkBool _countdownActive { get; set; }
    [Networked] private float _countdownTime { get; set; }
    [Networked] private float _currentCountdown { get; set; }
    [Networked] public NetworkBool GameStarted { get; set; }
    
    // Event raised when all players are ready
    public event Action OnAllPlayersReady;
    // Event raised when the countdown is complete
    public event Action OnCountdownComplete;
    // Event raised when player ready status changes
    public event Action<string, bool> OnPlayerReadyStatusChanged;
    // Event raised when the game actually starts
    public event Action OnGameStarted;
    
    // Track if we've been spawned
    private bool _hasBeenSpawned = false;
    
    // For single player testing
    private bool _singlePlayerDebugMode = true;
    
    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing LobbyManager...");
        
        // Initialize local variables only, not networked ones
        _localCountdownActive = false;
        _localCountdownTime = 3.0f;
        _localCurrentCountdown = 0f;
        _localGameStarted = false;
        
        GameManager.Instance.LogManager.LogMessage("LobbyManager initialized with local values");
    }
    
    public override void Spawned()
    {
        base.Spawned();
        
        // Now we can safely access networked properties
        if (HasStateAuthority)
        {
            _countdownActive = _localCountdownActive;
            _countdownTime = _localCountdownTime;
            _currentCountdown = _localCurrentCountdown;
            GameStarted = _localGameStarted;
        }
        
        _hasBeenSpawned = true;
        GameManager.Instance.LogManager.LogMessage("LobbyManager spawned, networked properties initialized");
    }
    
    public void RegisterPlayer(string playerName, PlayerRef playerRef)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = $"Player_{playerRef.PlayerId}";
        }
        
        // Log initial registration attempt
        GameManager.Instance.LogManager.LogMessage($"Trying to register player {playerName} with ID {playerRef.PlayerId}");
        
        // Check for duplicate names
        if (_playersByName.ContainsKey(playerName) && _playersByName[playerName] != playerRef)
        {
            // Add player ID to make name unique
            playerName = $"{playerName}_{playerRef.PlayerId}";
            GameManager.Instance.LogManager.LogMessage($"Name already taken, using unique name: {playerName}");
        }
        
        if (_playersByName.ContainsKey(playerName))
        {
            // This is a rejoining player or duplicate register call
            if (_playersByName[playerName] == playerRef)
            {
                GameManager.Instance.LogManager.LogMessage($"Player {playerName} is already registered with same PlayerRef");
                return; // Already registered with same PlayerRef
            }
            
            // Update the player reference (might have changed)
            GameManager.Instance.LogManager.LogMessage($"Player {playerName} is rejoining with new PlayerRef");
            _playersByName[playerName] = playerRef;
            
            // Make sure to restore ready status
            if (!_readyStatusByName.ContainsKey(playerName))
            {
                _readyStatusByName.Add(playerName, false);
            }
        }
        else
        {
            // New player joining
            GameManager.Instance.LogManager.LogMessage($"Registering new player {playerName} with ID {playerRef.PlayerId}");
            _playersByName.Add(playerName, playerRef);
            _readyStatusByName.Add(playerName, false);
            
            // Initialize player data for possible rejoin later
            if (!_playerDataByName.ContainsKey(playerName))
            {
                _playerDataByName.Add(playerName, new PlayerData());
            }
        }
        
        // Debug output of all registered players
        GameManager.Instance.LogManager.LogMessage($"Current player count: {_playersByName.Count}");
        foreach (var name in _playersByName.Keys)
        {
            GameManager.Instance.LogManager.LogMessage($"  - Player: {name}, ID: {_playersByName[name].PlayerId}");
        }
        
        // Update UI
        GameManager.Instance.UIManager.UpdatePlayersList();
    }
    
    public bool IsPlayerRegistered(string playerName)
    {
        return _playersByName.ContainsKey(playerName);
    }
    
    public bool CheckIsRejoining(string playerName)
    {
        return _playerDataByName.ContainsKey(playerName);
    }
    
    public void RemovePlayer(string playerName)
    {
        if (_playersByName.ContainsKey(playerName))
        {
            // Don't remove player data to allow rejoining
            _playersByName.Remove(playerName);
            _readyStatusByName.Remove(playerName);
            
            GameManager.Instance.LogManager.LogMessage($"Player {playerName} removed from lobby");
            
            // Update UI
            GameManager.Instance.UIManager.UpdatePlayersList();
        }
    }
    
    public void SetPlayerReadyStatus(string playerName, bool isReady)
    {
        GameManager.Instance.LogManager.LogMessage($"Setting ready status for {playerName}: {isReady}");
        
        if (_readyStatusByName.ContainsKey(playerName))
        {
            _readyStatusByName[playerName] = isReady;
            GameManager.Instance.LogManager.LogMessage($"Player {playerName} ready status: {isReady}");
            
            // Notify listeners about the status change
            OnPlayerReadyStatusChanged?.Invoke(playerName, isReady);
            
            // Check if all players are ready
            CheckAllPlayersReady();
        }
        else if (!string.IsNullOrEmpty(playerName))
        {
            // Add the player to ready status if not there
            _readyStatusByName.Add(playerName, isReady);
            GameManager.Instance.LogManager.LogMessage($"Added new player {playerName} with ready status: {isReady}");
            
            // Notify listeners
            OnPlayerReadyStatusChanged?.Invoke(playerName, isReady);
            
            // Check if all players are ready
            CheckAllPlayersReady();
        }
        else
        {
            GameManager.Instance.LogManager.LogError("Attempted to set ready status for a null or empty player name");
        }
        
        // Update UI for all clients
        GameManager.Instance.UIManager.UpdatePlayersList();
    }
    
    public bool GetPlayerReadyStatus(string playerName)
    {
        if (_readyStatusByName.ContainsKey(playerName))
        {
            return _readyStatusByName[playerName];
        }
        return false;
    }
    
    public bool AreAllPlayersReady()
    {
        if (_playersByName.Count == 0 || _readyStatusByName.Count == 0)
            return false;
        
        GameManager.Instance.LogManager.LogMessage($"Checking if all players are ready. Players: {_playersByName.Count}, Ready statuses: {_readyStatusByName.Count}");
        
        foreach (var kvp in _readyStatusByName)
        {
            GameManager.Instance.LogManager.LogMessage($"Player: {kvp.Key}, Ready: {kvp.Value}");
            if (!kvp.Value)
                return false;
        }
        
        return true;
    }
    
    public string GetPlayerName(PlayerRef playerRef)
    {
        foreach (var kvp in _playersByName)
        {
            if (kvp.Value == playerRef)
                return kvp.Key;
        }
        
        return null;
    }
    
    private void CheckAllPlayersReady()
    {
        bool allReady = AreAllPlayersReady();
        GameManager.Instance.LogManager.LogMessage($"All players ready check: {allReady}");
        
        if (allReady && _playersByName.Count >= 1)
        {
            GameManager.Instance.LogManager.LogMessage("All players are ready! Starting countdown...");
            
            // Notify listeners that all players are ready
            OnAllPlayersReady?.Invoke();
            
            // Start countdown
            StartCountdown();
        }
    }
    
    // Special method for single player testing
    public void DebugForceReadyCheck()
    {
        if (_singlePlayerDebugMode && _playersByName.Count == 1)
        {
            GameManager.Instance.LogManager.LogMessage("DEBUG: Forcing all players ready in single player mode");
            
            // Force notify listeners
            OnAllPlayersReady?.Invoke();
            
            // Start countdown
            StartCountdown();
        }
    }
    
    public void StartCountdown()
    {
        GameManager.Instance.LogManager.LogMessage("StartCountdown called");
        
        // Use local variables if not spawned yet
        if (!_hasBeenSpawned)
        {
            if (_localCountdownActive)
            {
                GameManager.Instance.LogManager.LogMessage("Countdown already active (local)");
                return;
            }
                
            _localCountdownActive = true;
            _localCurrentCountdown = _localCountdownTime;
            
            GameManager.Instance.LogManager.LogMessage($"Starting countdown with local variables: {_localCountdownTime} seconds");
            return;
        }
        
        // Use networked variables if spawned
        if (_countdownActive)
        {
            GameManager.Instance.LogManager.LogMessage("Countdown already active (networked)");
            return;
        }
            
        if (HasStateAuthority)
        {
            _countdownActive = true;
            _currentCountdown = _countdownTime;
            GameManager.Instance.LogManager.LogMessage($"Starting countdown with networked variables: {_countdownTime} seconds");
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        if (!_hasBeenSpawned)
            return;
            
        if (_countdownActive)
        {
            if (HasStateAuthority)
            {
                _currentCountdown -= Runner.DeltaTime;
                
                if (_currentCountdown <= 0)
                {
                    _countdownActive = false;
                    GameManager.Instance.LogManager.LogMessage("Countdown complete!");
                    
                    // Set the networked game started flag
                    GameStarted = true;
                    
                    // Notify listeners that countdown is complete
                    OnCountdownComplete?.Invoke();
                    
                    // Start the game after a short delay
                    StartGame();
                }
            }
        }
    }
    
    // For regular updates (UI, etc)
    private void Update()
    {
        // Handle local countdown if not spawned
        if (!_hasBeenSpawned && _localCountdownActive)
        {
            _localCurrentCountdown -= Time.deltaTime;
            
            if (_localCurrentCountdown <= 0)
            {
                _localCountdownActive = false;
                _localGameStarted = true;
                
                GameManager.Instance.LogManager.LogMessage("Local countdown complete!");
                OnCountdownComplete?.Invoke();
                
                // Start the game after a short delay
                StartGame();
            }
        }
    }
    
    private void StartGame()
    {
        if (!IsGameStarted())
        {
            GameManager.Instance.LogManager.LogError("StartGame called but IsGameStarted() returned false");
            return;
        }
            
        // Trigger the game started event
        GameManager.Instance.LogManager.LogMessage("GAME STARTING NOW!");
        
        // Ensure we're properly initialized for an RPC
        if (!_hasBeenSpawned)
        {
            GameManager.Instance.LogManager.LogMessage("StartGame called before spawned, will trigger locally");
            
            // Directly notify listeners since we can't use an RPC
            OnGameStarted?.Invoke();
            
            // Let GameManager know the game has started
            GameManager.Instance.StartGame();
        }
        else
        {
            // Notify all clients that the game has started
            if (HasStateAuthority)
            {
                GameManager.Instance.LogManager.LogMessage("Sending RPC_TriggerGameStart to all clients");
                RPC_TriggerGameStart();
            }
            else
            {
                GameManager.Instance.LogManager.LogMessage("Not state authority, cannot send RPC_TriggerGameStart");
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerGameStart()
    {
        // Log receipt of RPC
        GameManager.Instance.LogManager.LogMessage("RPC_TriggerGameStart received");
        
        // Invoke the game started event for all clients
        OnGameStarted?.Invoke();
        
        // Let GameManager know the game has started
        GameManager.Instance.StartGame();
    }
    
    public float GetCurrentCountdown()
    {
        if (!_hasBeenSpawned)
            return _localCurrentCountdown;
            
        return _currentCountdown;
    }
    
    public bool IsCountdownActive()
    {
        if (!_hasBeenSpawned)
            return _localCountdownActive;
            
        return _countdownActive;
    }
    
    public bool IsGameStarted()
    {
        if (!_hasBeenSpawned)
            return _localGameStarted;
            
        return GameStarted;
    }
    
    public List<string> GetAllPlayerNames()
    {
        GameManager.Instance.LogManager.LogMessage($"GetAllPlayerNames called, found {_playersByName.Count} players");
        foreach (var name in _playersByName.Keys)
        {
            GameManager.Instance.LogManager.LogMessage($"Player in list: {name}");
        }
        return new List<string>(_playersByName.Keys);
    }
    
    public PlayerData GetPlayerData(string playerName)
    {
        if (_playerDataByName.ContainsKey(playerName))
        {
            return _playerDataByName[playerName];
        }
        
        return null;
    }
    
    public void UpdatePlayerData(string playerName, PlayerData data)
    {
        if (_playerDataByName.ContainsKey(playerName))
        {
            _playerDataByName[playerName] = data;
        }
        else if (!string.IsNullOrEmpty(playerName))
        {
            _playerDataByName.Add(playerName, data);
        }
    }
    
    public Dictionary<string, PlayerRef> GetPlayerRefsByName()
    {
        return new Dictionary<string, PlayerRef>(_playersByName);
    }
    
    public void Reset()
    {
        _playersByName.Clear();
        _readyStatusByName.Clear();
        // Keep player data for rejoining
        
        // Reset local variables
        _localCountdownActive = false;
        _localGameStarted = false;
        
        // Reset networked variables if spawned
        if (_hasBeenSpawned && HasStateAuthority)
        {
            _countdownActive = false;
            GameStarted = false;
        }
        
        GameManager.Instance.LogManager.LogMessage("Lobby reset");
    }
    
    public void DebugPrintState()
    {
        GameManager.Instance.LogManager.LogMessage("--- LobbyManager Debug State ---");
        GameManager.Instance.LogManager.LogMessage($"Players count: {_playersByName.Count}");
        
        foreach (var player in _playersByName)
        {
            bool isReady = _readyStatusByName.ContainsKey(player.Key) ? _readyStatusByName[player.Key] : false;
            GameManager.Instance.LogManager.LogMessage($"Player: {player.Key}, ID: {player.Value.PlayerId}, Ready: {isReady}");
        }
        
        GameManager.Instance.LogManager.LogMessage($"All ready: {AreAllPlayersReady()}");
        GameManager.Instance.LogManager.LogMessage($"Countdown active: {IsCountdownActive()}");
        GameManager.Instance.LogManager.LogMessage($"Countdown time: {GetCurrentCountdown()}");
        GameManager.Instance.LogManager.LogMessage($"Game started: {IsGameStarted()}");
        GameManager.Instance.LogManager.LogMessage("-------------------------------");
    }
}