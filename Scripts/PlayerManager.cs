using Fusion;
using System;
using System.Collections; // Added for Coroutines
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private NetworkObject _playerPrefab;
    private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();
    
    // Event fired when player is spawned and ready
    public event Action<PlayerRef, Player> OnPlayerSpawned;
    
    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing PlayerManager...");
        
        // Find the player prefab if not assigned
        if (_playerPrefab == null)
        {
            _playerPrefab = Resources.Load<NetworkObject>("PlayerPrefab");
            if (_playerPrefab == null)
                GameManager.Instance.LogManager.LogError("PlayerPrefab not found! Please create it and place it in a Resources folder.");
            else
                GameManager.Instance.LogManager.LogMessage("PlayerPrefab loaded from Resources folder");
        }
    }

    // This method now just starts the coroutine
    public void OnLocalPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"OnLocalPlayerJoined called for {player}. Starting spawn coroutine.");
        StartCoroutine(SpawnPlayerAfterDelay(runner, player));
    }

    // Coroutine to handle the actual spawning after a delay
    private IEnumerator SpawnPlayerAfterDelay(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"SpawnPlayerAfterDelay coroutine started for {player}. Waiting one frame...");
        yield return null; // Wait for one frame

        GameManager.Instance.LogManager.LogMessage($"SpawnPlayerAfterDelay continuing for {player} after delay.");
        try
        {
            // Only the local player should spawn their own character
            if (_players.ContainsKey(player))
            {
                GameManager.Instance.LogManager.LogMessage($"Player {player} is already spawned");
                
                // Notify UI if player is already spawned but UI hasn't been updated
                Player playerComponent = _players[player].GetComponent<Player>();
                if (playerComponent != null)
                 {
                     OnPlayerSpawned?.Invoke(player, playerComponent);
                 }
                 yield break;
             }
             
            GameManager.Instance.LogManager.LogMessage($"Local player {player} joined - spawning player");
            
            if (_playerPrefab == null)
            {
                // Try to load the prefab if it's not assigned
                _playerPrefab = Resources.Load<NetworkObject>("PlayerPrefab");
                
                if (_playerPrefab == null)
                 {
                     GameManager.Instance.LogManager.LogError("Player prefab is missing! Make sure to create the PlayerPrefab in Resources folder.");
                     yield break;
                 }
             }
            
            // Get player name from UI
            string playerName = GameManager.Instance.UIManager.GetLocalPlayerName();
            
            // Position the player with a unique position
            Vector2 spawnPosition = new Vector2(
                UnityEngine.Random.Range(-5, 5) + player.PlayerId, 
                UnityEngine.Random.Range(-5, 5) + player.PlayerId
            );
            
            GameManager.Instance.LogManager.LogMessage($"Spawning player with Game Mode: {runner.GameMode}");
            
            GameManager.Instance.LogManager.LogMessage($"Attempting SpawnAsync for player {player}..."); // <-- ADDED LOG
            // Use SpawnAsync, and handle the callback properly
            runner.SpawnAsync(_playerPrefab, spawnPosition, Quaternion.identity, player, 
                (runner, playerObject) => {
                    GameManager.Instance.LogManager.LogMessage($"SpawnAsync callback entered for player {player}. PlayerObject is null: {playerObject == null}"); // <-- ADDED LOG
                    // This callback is executed when the spawn succeeds
                    if (playerObject != null)
                    {
                        // Store the player object
                        _players[player] = playerObject;
                        
                        // Initialize player name
                        Player playerComponent = playerObject.GetComponent<Player>();
                        if (playerComponent != null)
                        {
                            playerComponent.SetPlayerName(playerName);
                            
                            // Register with lobby manager
                            GameManager.Instance.LogManager.LogMessage($"Player {playerName} spawned successfully with object ID: {playerObject.Id}");
                            
                            // Notify listeners that player is ready
                            OnPlayerSpawned?.Invoke(player, playerComponent);
                        }
                        else
                        {
                            GameManager.Instance.LogManager.LogError("Player component not found on spawned object!");
                        }
                        
                        // Create camera for the local player
                        GameManager.Instance.CameraManager.CreatePlayerCamera(playerObject.transform);
                    }
                    else
                    {
                        GameManager.Instance.LogManager.LogError($"Player object failed to spawn! SpawnAsync returned null for player {player}."); // <-- ADDED LOG
                    }
                });
        }
        catch (Exception ex)
        {
            GameManager.Instance.LogManager.LogError($"Error in SpawnPlayerAfterDelay: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Called when a network player object spawns
    public void OnPlayerObjectSpawned(NetworkRunner runner, NetworkObject playerObject, PlayerRef player)
    {
        if (!_players.ContainsKey(player))
        {
            _players[player] = playerObject;
            GameManager.Instance.LogManager.LogMessage($"Tracking new remote player object: {playerObject.Id} for player {player}");
            
            // Get player component to check if it has a name
            Player playerComponent = playerObject.GetComponent<Player>();
            if (playerComponent != null)
            {
                string playerName = playerComponent.GetPlayerName();
                if (!string.IsNullOrEmpty(playerName))
                {
                    // Ensure this player is in the lobby manager
                    GameManager.Instance.LogManager.LogMessage($"Ensuring player {playerName} is in lobby manager");
                    GameManager.Instance.LobbyManager.RegisterPlayer(playerName, player);
                    
                    // Notify UI
                    OnPlayerSpawned?.Invoke(player, playerComponent);
                    
                    // Update UI
                    GameManager.Instance.UIManager.UpdatePlayersList();
                }
            }
        }
    }
    
    public void OnRemotePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Remote player {player} joined - waiting for their character to spawn");
    }
    
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Player {player} left the game");
        
        // Get player name before cleanup
        string playerName = "";
        if (_players.TryGetValue(player, out NetworkObject playerObject))
        {
            if (playerObject != null)
            {
                Player playerComponent = playerObject.GetComponent<Player>();
                if (playerComponent != null)
                {
                    playerName = playerComponent.GetPlayerName();
                    
                    // Don't remove from lobby manager to allow rejoining later
                    // But do mark them as not ready if they were ready
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        GameManager.Instance.LobbyManager.SetPlayerReadyStatus(playerName, false);
                    }
                }
            }
        }
        
        // Clean up the player object
        if (_players.TryGetValue(player, out NetworkObject obj))
        {
            if (obj != null)
            {
                runner.Despawn(obj);
                GameManager.Instance.LogManager.LogMessage($"Despawned player object for Player {player}");
            }
            _players.Remove(player);
        }
        
        GameManager.Instance.LogManager.LogMessage($"Players remaining in session: {_players.Count}");
        
        // Update lobby UI
        GameManager.Instance.UIManager.UpdatePlayersList();
        
        if (player != runner.LocalPlayer)
        {
            if (!string.IsNullOrEmpty(playerName))
            {
                GameManager.Instance.UIManager.UpdateStatus($"Player {playerName} left the game");
            }
            else
            {
                GameManager.Instance.UIManager.UpdateStatus($"Player {player.PlayerId} left the game");
            }
        }
    }
    
    public void ClearPlayers()
    {
        _players.Clear();
        GameManager.Instance.LogManager.LogMessage("All players cleared");
        
        // Update UI
        GameManager.Instance.UIManager.UpdatePlayersList();
    }
    
    public NetworkObject GetPlayerObject(PlayerRef player)
    {
        if (_players.TryGetValue(player, out NetworkObject playerObject))
        {
            return playerObject;
        }
        return null;
    }
    
    public int GetPlayerCount()
    {
        return _players.Count;
    }
    
    public List<PlayerRef> GetAllPlayers()
    {
        return new List<PlayerRef>(_players.Keys);
    }
    
    public Player GetPlayerByName(string playerName)
    {
        foreach (var playerObj in _players.Values)
        {
            if (playerObj == null) continue;
            
            Player player = playerObj.GetComponent<Player>();
            if (player != null && player.GetPlayerName() == playerName)
            {
                return player;
            }
        }
        return null;
    }
}
