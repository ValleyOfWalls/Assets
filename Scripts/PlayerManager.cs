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
            // Only spawn objects for the local player
            if (player == runner.LocalPlayer)
            {
                GameManager.Instance.LogManager.LogMessage("Spawning local player");
                
                if (_playerPrefab == null)
                {
                    GameManager.Instance.LogManager.LogError("Player prefab is missing! Make sure to create the PlayerPrefab in Resources folder.");
                    return;
                }
                
                // Position the player
                Vector3 spawnPosition = new Vector3(UnityEngine.Random.Range(-5, 5), 1, UnityEngine.Random.Range(-5, 5));
                
                // Spawn the prefab - this automatically registers it with Fusion
                NetworkObject playerObject = runner.Spawn(_playerPrefab, position: spawnPosition, inputAuthority: player);
                
                // Keep track of spawned objects
                if (playerObject != null)
                {
                    _players[player] = playerObject;
                    
                    // Create camera for this player
                    GameManager.Instance.CameraManager.CreatePlayerCamera(playerObject.transform);
                }
                else
                {
                    GameManager.Instance.LogManager.LogError("Failed to spawn player object!");
                }
            }
        }
        catch (Exception ex) 
        {
            GameManager.Instance.LogManager.LogError($"Error in OnPlayerJoined: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // Clean up the player
        if (_players.TryGetValue(player, out NetworkObject playerObject))
        {
            if (playerObject != null)
            {
                runner.Despawn(playerObject);
            }
            _players.Remove(player);
        }
    }
    
    public void ClearPlayers()
    {
        _players.Clear();
    }
    
    public NetworkObject GetPlayerObject(PlayerRef player)
    {
        if (_players.TryGetValue(player, out NetworkObject playerObject))
        {
            return playerObject;
        }
        return null;
    }
}