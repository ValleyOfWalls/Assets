using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private NetworkObject _playerPrefab;
    private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();
    
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
    
    // Only spawns the local player
    public void OnLocalPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        try 
        {
            // Only the local player should spawn their own character
            if (_players.ContainsKey(player))
            {
                GameManager.Instance.LogManager.LogMessage($"Player {player} is already spawned");
                return;
            }
            
            GameManager.Instance.LogManager.LogMessage($"Local player {player} joined - spawning player");
            
            if (_playerPrefab == null)
            {
                GameManager.Instance.LogManager.LogError("Player prefab is missing! Make sure to create the PlayerPrefab in Resources folder.");
                return;
            }
            
            // Get player name from UI
            string playerName = GameManager.Instance.UIManager.GetLocalPlayerName();
            
            // Check if this player is rejoining
            bool isRejoining = !string.IsNullOrEmpty(playerName) && 
                               GameManager.Instance.LobbyManager.CheckIsRejoining(playerName);
            
            // Position the player with a unique position based on player ID to avoid spawn collisions
            Vector2 spawnPosition = new Vector2(
                UnityEngine.Random.Range(-5, 5) + player.PlayerId, 
                UnityEngine.Random.Range(-5, 5) + player.PlayerId
            );
            
            // Spawn the player for everyone in the session
            NetworkObject playerObject = runner.Spawn(_playerPrefab, position: spawnPosition, inputAuthority: player);
            
            // Keep track of spawned objects
            if (playerObject != null)
            {
                _players[player] = playerObject;
                
                // Initialize player name
                Player playerComponent = playerObject.GetComponent<Player>();
                if (playerComponent != null)
                {
                    // Set player name before any RPC calls
                    playerComponent.SetPlayerName(playerName);
                    
                    if (isRejoining)
                    {
                        GameManager.Instance.LogManager.LogMessage($"Player {playerName} is rejoining the game");
                        GameManager.Instance.UIManager.UpdateStatus($"Rejoined as {playerName}");
                    }
                    else
                    {
                        GameManager.Instance.LogManager.LogMessage($"Player {playerName} joined the game");
                        GameManager.Instance.UIManager.UpdateStatus($"You joined as {playerName}");
                    }
                }
                
                GameManager.Instance.LogManager.LogMessage($"Local player {player} spawned successfully with object ID: {playerObject.Id}");
                
                // Explicitly ensure this player is registered in the lobby
                if (!string.IsNullOrEmpty(playerName))
                {
                    // This direct call helps ensure registration happens
                    GameManager.Instance.LobbyManager.RegisterPlayer(playerName, player);
                    GameManager.Instance.LogManager.LogMessage($"Directly registering player {playerName} with lobby manager");
                }
                
                // Create camera for the local player
                GameManager.Instance.LogManager.LogMessage("Creating camera for local player");
                GameManager.Instance.CameraManager.CreatePlayerCamera(playerObject.transform);
            }
            else
            {
                GameManager.Instance.LogManager.LogError("Failed to spawn player object!");
            }
            
            // Log the total number of players in the session
            GameManager.Instance.LogManager.LogMessage($"Total players in session: {_players.Count}");
        }
        catch (Exception ex) 
        {
            GameManager.Instance.LogManager.LogError($"Error in OnLocalPlayerJoined: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    // Just track remote players that joined (their clients will spawn their characters)
    public void OnRemotePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Remote player {player} joined - waiting for their character to spawn");
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
                    
                    // Update UI
                    GameManager.Instance.UIManager.UpdatePlayersList();
                }
            }
        }
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
            Player player = playerObj.GetComponent<Player>();
            if (player != null && player.GetPlayerName() == playerName)
            {
                return player;
            }
        }
        return null;
    }
}