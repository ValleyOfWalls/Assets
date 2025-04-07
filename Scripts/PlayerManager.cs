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
    
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        try 
        {
            GameManager.Instance.LogManager.LogMessage($"Player {player} joined - spawning player");
            
            if (_playerPrefab == null)
            {
                GameManager.Instance.LogManager.LogError("Player prefab is missing! Make sure to create the PlayerPrefab in Resources folder.");
                return;
            }
            
            // Position the player with a unique position based on player ID to avoid spawn collisions
            Vector3 spawnPosition = new Vector3(
                UnityEngine.Random.Range(-5, 5) + player.PlayerId, 
                1, 
                UnityEngine.Random.Range(-5, 5) + player.PlayerId
            );
            
            // Spawn the player for everyone in the session
            NetworkObject playerObject = runner.Spawn(_playerPrefab, position: spawnPosition, inputAuthority: player);
            
            // Keep track of spawned objects
            if (playerObject != null)
            {
                _players[player] = playerObject;
                GameManager.Instance.LogManager.LogMessage($"Player {player} spawned successfully with object ID: {playerObject.Id}");
                
                // Only create camera for the local player
                if (player == runner.LocalPlayer)
                {
                    GameManager.Instance.LogManager.LogMessage("Creating camera for local player");
                    GameManager.Instance.CameraManager.CreatePlayerCamera(playerObject.transform);
                    GameManager.Instance.UIManager.UpdateStatus($"You joined as Player {player.PlayerId}");
                }
                else
                {
                    GameManager.Instance.LogManager.LogMessage($"Remote player {player} joined");
                    GameManager.Instance.UIManager.UpdateStatus($"Player {player.PlayerId} joined the game");
                }
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
            GameManager.Instance.LogManager.LogError($"Error in OnPlayerJoined: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Player {player} left the game");
        
        // Clean up the player
        if (_players.TryGetValue(player, out NetworkObject playerObject))
        {
            if (playerObject != null)
            {
                runner.Despawn(playerObject);
                GameManager.Instance.LogManager.LogMessage($"Despawned player object for Player {player}");
            }
            _players.Remove(player);
        }
        
        GameManager.Instance.LogManager.LogMessage($"Players remaining in session: {_players.Count}");
        
        if (player != runner.LocalPlayer)
        {
            GameManager.Instance.UIManager.UpdateStatus($"Player {player.PlayerId} left the game");
        }
    }
    
    public void ClearPlayers()
    {
        _players.Clear();
        GameManager.Instance.LogManager.LogMessage("All players cleared");
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
}