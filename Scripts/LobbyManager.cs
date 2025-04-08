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
    
    // Track if we've been spawned
    private bool _hasBeenSpawned = false;
    
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
        
        // Check for duplicate names and append a number if needed
        string uniqueName = playerName;
        int suffix = 1;
        while (_playersByName.ContainsKey(uniqueName) && _playersByName[uniqueName] != playerRef)
        {
            uniqueName = $"{playerName}_{suffix++}";
        }
        
        if (_playersByName.ContainsKey(uniqueName))
        {
            // This is a rejoining player, we already have their ref
            GameManager.Instance.LogManager.LogMessage($"Player {uniqueName} is rejoining");
            
            // Update the player reference (might have changed)
            _playersByName[uniqueName] = playerRef;
            
            // Make sure to restore ready status
            if (!_readyStatusByName.ContainsKey(uniqueName))
            {
                _readyStatusByName.Add(uniqueName, false);
            }
        }
        else
        {
            // New player joining
            GameManager.Instance.LogManager.LogMessage($"Registering player {uniqueName} with ID {playerRef.PlayerId}");
            _playersByName.Add(uniqueName, playerRef);
            _readyStatusByName.Add(uniqueName, false);
            
            // Initialize player data for possible rejoin later
            if (!_playerDataByName.ContainsKey(uniqueName))
            {
                _playerDataByName.Add(uniqueName, new PlayerData());
            }
        }
        
        // Update UI
        GameManager.Instance.UIManager.UpdatePlayersList();
    }
    
    public bool IsPlayerRegistered(string playerName)
    {
        return _playersByName.ContainsKey(playerName);
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
        if (_readyStatusByName.ContainsKey(playerName))
        {
            _readyStatusByName[playerName] = isReady;
            GameManager.Instance.LogManager.LogMessage($"Player {playerName} ready status: {isReady}");
            
            // Notify listeners about the status change
            OnPlayerReadyStatusChanged?.Invoke(playerName, isReady);
            
            // Check if all players are ready
            CheckAllPlayersReady();
        }
        else
        {
            GameManager.Instance.LogManager.LogError($"Player {playerName} not found in ready status dictionary");
        }
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
    
    public void StartCountdown()
    {
        // Use local variables if not spawned yet
        if (!_hasBeenSpawned)
        {
            if (_localCountdownActive)
                return;
                
            _localCountdownActive = true;
            _localCurrentCountdown = _localCountdownTime;
            
            GameManager.Instance.LogManager.LogMessage($"Starting countdown with local variables: {_localCountdownTime} seconds");
            return;
        }
        
        // Use networked variables if spawned
        if (_countdownActive)
            return;
            
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
            }
        }
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

// Class to store player data for rejoining
[System.Serializable]
public class PlayerData
{
    public Vector3 Position = Vector3.zero; // Changed to Vector3 for 3D
    public Color PlayerColor = Color.white;
    // Add any other player state that needs to be preserved
}