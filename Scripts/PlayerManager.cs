// valleyofwalls-assets/Scripts/PlayerManager.cs
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine; // Added for FindObjectsByType

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private NetworkObject _playerPrefab;
    [SerializeField] private NetworkObject _playerStatePrefab;
    private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();

    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing PlayerManager...");
        if (_playerPrefab == null) {
            _playerPrefab = Resources.Load<NetworkObject>("PlayerPrefab");
            if (_playerPrefab == null) GameManager.Instance.LogManager.LogError("PlayerPrefab not found! Place in Resources.");
            // else GameManager.Instance.LogManager.LogMessage("PlayerPrefab loaded from Resources.");
        }
        if (_playerStatePrefab == null) {
            _playerStatePrefab = Resources.Load<NetworkObject>("PlayerStatePrefab");
            if (_playerStatePrefab == null) GameManager.Instance.LogManager.LogError("PlayerStatePrefab not found! Place in Resources.");
            // else GameManager.Instance.LogManager.LogMessage("PlayerStatePrefab loaded from Resources.");
        }
    }

    // Only spawns the local player's objects
    public void OnLocalPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (_players.ContainsKey(player)) {
            GameManager.Instance.LogManager.LogMessage($"Player {player} already has objects spawned. Skipping.");
            return;
        }
        // GameManager.Instance.LogManager.LogMessage($"Local player {player} joined - starting spawn sequence.");
        StartCoroutine(SpawnLocalPlayerSequence(runner, player));
    }

    private IEnumerator SpawnLocalPlayerSequence(NetworkRunner runner, PlayerRef player)
    {
        NetworkObject playerObject = null;

        // --- Step 1: Spawn Player Object ---
        // GameManager.Instance.LogManager.LogMessage($"Player {player}: Attempting to spawn Player object...");
        if (_playerPrefab == null) { GameManager.Instance.LogManager.LogError("Player prefab missing!"); yield break; }
        try {
            string playerName = GameManager.Instance.UIManager.GetLocalPlayerName();
            Vector2 spawnPosition = new Vector2(UnityEngine.Random.Range(-5, 5), UnityEngine.Random.Range(-5, 5));
            playerObject = runner.Spawn(_playerPrefab, position: spawnPosition, inputAuthority: player);
            if (playerObject != null) {
                _players[player] = playerObject;
                Player playerComponent = playerObject.GetComponent<Player>();
                if (playerComponent != null) {
                    playerComponent.SetPlayerName(playerName);
                    // GameManager.Instance.LogManager.LogMessage($"Player object spawned for {playerName} ({player}, ID: {playerObject.Id}).");
                    GameManager.Instance.LobbyManager.RegisterPlayer(playerName, player);
                } else { GameManager.Instance.LogManager.LogError("Spawned Player object missing Player component!"); }
                GameManager.Instance.CameraManager.CreatePlayerCamera(playerObject.transform);
            } else { GameManager.Instance.LogManager.LogError($"Failed to spawn Player object for player {player}."); yield break; }
        } catch (Exception ex) { GameManager.Instance.LogManager.LogError($"Error spawning Player object for {player}: {ex.Message}\n{ex.StackTrace}"); yield break; }

        // --- Step 2: Wait for GameState ---
        // GameManager.Instance.LogManager.LogMessage($"Player {player} waiting for GameState before spawning PlayerState...");
        float timer = 0f, timeout = 25f;
        while (GameState.Instance == null || !GameState.Instance.IsSpawned()) {
             if (runner == null || !runner.IsRunning) { /* Log & break */ yield break; }
            timer += Time.deltaTime;
            if (timer > timeout) { GameManager.Instance.LogManager.LogError($"Player {player} timed out waiting for GameState."); yield break; }
            yield return null;
        }
        // GameManager.Instance.LogManager.LogMessage($"Player {player}: GameState ready. Spawning PlayerState object.");

        // --- Step 3: Spawn PlayerState Object ---
        if (_playerStatePrefab == null) { GameManager.Instance.LogManager.LogError("PlayerState prefab missing!"); yield break; }
        try {
            NetworkObject stateObj = runner.Spawn(_playerStatePrefab, inputAuthority: player);
            if (stateObj != null) {
                PlayerState playerStateComponent = stateObj.GetComponent<PlayerState>();
                if(playerStateComponent != null) { /* GameManager.Instance.LogManager.LogMessage($"PlayerState object spawned for player {player} (ID: {stateObj.Id})."); */ }
                else { GameManager.Instance.LogManager.LogError("Spawned PlayerState object missing PlayerState component!"); }
            } else { GameManager.Instance.LogManager.LogError($"Failed to spawn PlayerState object for player {player}."); }
        } catch (Exception ex) { GameManager.Instance.LogManager.LogError($"Error spawning PlayerState object for {player}: {ex.Message}\n{ex.StackTrace}"); }

        // GameManager.Instance.LogManager.LogMessage($"Spawn sequence complete for local player {player}.");
    }

    // Track remote players
    public void OnRemotePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // GameManager.Instance.LogManager.LogMessage($"Remote player {player} joined - waiting for their character to spawn");
    }

    // Called when any player's network object spawns
    public void OnPlayerObjectSpawned(NetworkRunner runner, NetworkObject playerObject, PlayerRef player)
    {
         if (!_players.ContainsKey(player)) {
            _players[player] = playerObject;
            // GameManager.Instance.LogManager.LogMessage($"Tracking remote player object: {playerObject.Id} for player {player}");
            Player playerComponent = playerObject.GetComponent<Player>();
            if (playerComponent != null) StartCoroutine(RegisterRemotePlayerWhenReady(playerComponent, player));
        }
         else if (_players[player] != playerObject) {
             // GameManager.Instance.LogManager.LogMessage($"Updating tracked object for Player {player} to new object ID: {playerObject.Id}");
             _players[player] = playerObject;
         }
    }

     // Wait for remote player name before registering with lobby
     private IEnumerator RegisterRemotePlayerWhenReady(Player playerComponent, PlayerRef player)
     {
         float timer = 0f, timeout = 10f; string playerName = "";
         while (string.IsNullOrEmpty(playerName) && timer < timeout) {
             if (playerComponent?.Object?.IsValid ?? false) { try { playerName = playerComponent.GetPlayerName(); } catch {} }
             if (string.IsNullOrEmpty(playerName)) { timer += Time.deltaTime; yield return null; }
         }
         if (!string.IsNullOrEmpty(playerName)) {
             // GameManager.Instance.LogManager.LogMessage($"Remote player {player}'s name '{playerName}' ready. Registering.");
             GameManager.Instance.LobbyManager.RegisterPlayer(playerName, player);
             GameManager.Instance.UIManager.UpdatePlayersList();
         } else { /* Log name timeout */ }
     }

    // Handle player leaving
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // GameManager.Instance.LogManager.LogMessage($"Player {player} left the game");
        string playerName = "";
        if (_players.TryGetValue(player, out NetworkObject playerObjectToDespawn) && playerObjectToDespawn != null) {
            Player playerComponent = playerObjectToDespawn.GetComponent<Player>();
            if (playerComponent != null) playerName = playerComponent.GetPlayerName();
        }

        if (!string.IsNullOrEmpty(playerName)) {
            GameManager.Instance.LobbyManager.SetPlayerReadyStatus(playerName, false); // Mark as not ready
            // GameManager.Instance.LogManager.LogMessage($"Marked leaving player {playerName} as not ready.");
        }

        // --- Despawn Associated Objects ---
        // Despawn Player Object
        if (_players.TryGetValue(player, out NetworkObject playerObj)) {
            if (playerObj?.IsValid ?? false) {
                if (playerObj.HasStateAuthority || player == runner.LocalPlayer) {
                    // GameManager.Instance.LogManager.LogMessage($"Despawning Player object (ID: {playerObj.Id}) for player {player}.");
                    runner.Despawn(playerObj);
                } else { /* Log skip despawn */ }
            }
             _players.Remove(player);
        }

        // Despawn PlayerState Object
        // ** FIXED: Use FindObjectsByType **
         var playerStates = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
         foreach (var state in playerStates) {
             if (state?.Object?.InputAuthority == player) {
                 if (state.Object.IsValid) {
                     if (state.Object.HasStateAuthority || player == runner.LocalPlayer) {
                         // GameManager.Instance.LogManager.LogMessage($"Despawning PlayerState object (ID: {state.Object.Id}) for player {player}.");
                         runner.Despawn(state.Object);
                     } else { /* Log skip despawn */ }
                 }
                 break; // Found the state object
             }
         }

        // GameManager.Instance.LogManager.LogMessage($"Players remaining: {_players.Count}");
        GameManager.Instance.UIManager.UpdatePlayersList(); // Update UI
        if (player != runner.LocalPlayer) {
            GameManager.Instance.UIManager.UpdateStatus(!string.IsNullOrEmpty(playerName) ? $"Player {playerName} left." : $"Player {player.PlayerId} left.");
        }
    }

    // Clear tracking dictionary
    public void ClearPlayers()
    {
        _players.Clear();
        // GameManager.Instance.LogManager.LogMessage("Player tracking dictionary cleared.");
        GameManager.Instance.UIManager.UpdatePlayersList();
    }

    // Getters
    public NetworkObject GetPlayerObject(PlayerRef player) { _players.TryGetValue(player, out NetworkObject p); return p; }
    public int GetPlayerCount() => _players.Count;
    public List<PlayerRef> GetAllPlayers() => new List<PlayerRef>(_players.Keys);
    public Player GetPlayerByName(string playerName) {
        foreach (var playerObj in _players.Values) {
            if (playerObj == null) continue;
            Player player = playerObj.GetComponent<Player>();
            if (player != null && player.GetPlayerName() == playerName) return player;
        }
        return null;
    }
}