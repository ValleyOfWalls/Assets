// valleyofwalls-assets/Scripts/PlayerManager.cs
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// Added for FindObjectsByType

public class PlayerManager : MonoBehaviour
{
    // Make prefabs assignable in Inspector or ensure they are loaded correctly
    [SerializeField] private NetworkObject _playerPrefab;
    [SerializeField] private NetworkObject _playerStatePrefab;

    // Dictionary to keep track of player NetworkObjects by their PlayerRef
    private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();

    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing PlayerManager...");
        // Load prefabs from Resources if not assigned in Inspector
        if (_playerPrefab == null) {
            _playerPrefab = Resources.Load<NetworkObject>("PlayerPrefab");
            if (_playerPrefab == null) GameManager.Instance.LogManager.LogError("PlayerPrefab not found! Place in Resources folder named 'PlayerPrefab'.");
        }
        if (_playerStatePrefab == null) {
            _playerStatePrefab = Resources.Load<NetworkObject>("PlayerStatePrefab");
            if (_playerStatePrefab == null) GameManager.Instance.LogManager.LogError("PlayerStatePrefab not found! Place in Resources folder named 'PlayerStatePrefab'.");
        }
    }

    // Called by NetworkManager when the LOCAL player joins the session
    public void OnLocalPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (_players.ContainsKey(player)) {
            GameManager.Instance.LogManager.LogMessage($"Player {player} already has objects spawned. Skipping spawn sequence.");
            return;
        }
        GameManager.Instance.LogManager.LogMessage($"Local player {player} joined - starting spawn sequence.");
        StartCoroutine(SpawnLocalPlayerSequence(runner, player));
    }

    // Coroutine to spawn Player and PlayerState objects for the local player
    private IEnumerator SpawnLocalPlayerSequence(NetworkRunner runner, PlayerRef player)
    {
        NetworkObject playerObject = null;

        // --- Step 1: Spawn Player Object ---
        GameManager.Instance.LogManager.LogMessage($"Player {player}: Attempting to spawn Player object...");
        if (_playerPrefab == null) { GameManager.Instance.LogManager.LogError("Player prefab missing! Cannot spawn player."); yield break; }

        try {
            string playerName = GameManager.Instance.UIManager.GetLocalPlayerName();
            Vector2 spawnPosition = new Vector2(UnityEngine.Random.Range(-5, 5), UnityEngine.Random.Range(-5, 5));
            playerObject = runner.Spawn(_playerPrefab, position: spawnPosition, inputAuthority: player);

            if (playerObject != null) {
                _players[player] = playerObject;
                Player playerComponent = playerObject.GetComponent<Player>();
                if (playerComponent != null) {
                    playerComponent.SetPlayerName(playerName);
                    GameManager.Instance.LogManager.LogMessage($"Player object spawned for {playerName} ({player}, ID: {playerObject.Id}).");
                    GameManager.Instance.LobbyManager.RegisterPlayer(playerName, player);
                } else { GameManager.Instance.LogManager.LogError("Spawned Player object missing Player component!"); }
                GameManager.Instance.CameraManager.CreatePlayerCamera(playerObject.transform);
            } else { GameManager.Instance.LogManager.LogError($"Failed to spawn Player object for player {player}. Aborting sequence."); yield break; }
        } catch (Exception ex) { GameManager.Instance.LogManager.LogError($"Error spawning Player object for {player}: {ex.Message}\n{ex.StackTrace}"); yield break; }

        // --- Step 2: Wait for GameState ---
        GameManager.Instance.LogManager.LogMessage($"Player {player} waiting for GameState before spawning PlayerState...");
        float timer = 0f, timeout = 25f;
        while (GameState.Instance == null || !GameState.Instance.IsSpawned()) {
             if (runner == null || !runner.IsRunning) { GameManager.Instance.LogManager.LogError($"Runner stopped while waiting for GameState for Player {player}. Aborting spawn."); yield break; }
            timer += Time.deltaTime;
            if (timer > timeout) { GameManager.Instance.LogManager.LogError($"Player {player} timed out waiting for GameState. Cannot spawn PlayerState."); yield break; }
            yield return null;
        }
        GameManager.Instance.LogManager.LogMessage($"Player {player}: GameState ready. Spawning PlayerState object.");

        // --- Step 3: Spawn PlayerState Object ---
        if (_playerStatePrefab == null) { GameManager.Instance.LogManager.LogError("PlayerState prefab missing! Cannot spawn player state."); yield break; }
        try {
            // *** MODIFIED: Removed the onBeforeSpawned delegate ***
            NetworkObject stateObj = runner.Spawn(
                _playerStatePrefab,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                inputAuthority: player // Assign Input Authority (State Authority should follow by default in Shared Mode)
                );

            if (stateObj != null) {
                PlayerState playerStateComponent = stateObj.GetComponent<PlayerState>();
                if(playerStateComponent != null) { GameManager.Instance.LogManager.LogMessage($"PlayerState object spawned for player {player} (ID: {stateObj.Id})."); }
                else { GameManager.Instance.LogManager.LogError("Spawned PlayerState object missing PlayerState component!"); }
            } else { GameManager.Instance.LogManager.LogError($"Failed to spawn PlayerState object for player {player}."); }
        } catch (Exception ex) { GameManager.Instance.LogManager.LogError($"Error spawning PlayerState object for {player}: {ex.Message}\n{ex.StackTrace}"); }

        GameManager.Instance.LogManager.LogMessage($"Spawn sequence complete for local player {player}.");
    }

    // Called by NetworkManager when a REMOTE player joins
    public void OnRemotePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Remote player {player} joined - waiting for their character/state objects to spawn");
    }

    // Called when ANY player object spawns (local or remote)
    public void OnPlayerObjectSpawned(NetworkRunner runner, NetworkObject playerObject, PlayerRef player)
    {
         if (!_players.ContainsKey(player)) {
            _players[player] = playerObject;
            GameManager.Instance.LogManager.LogMessage($"Tracking remote player object: {playerObject.Id} for player {player}");
            Player playerComponent = playerObject.GetComponent<Player>();
            if (playerComponent != null) StartCoroutine(RegisterRemotePlayerWhenReady(playerComponent, player));
         }
         else if (_players[player] != playerObject) {
             GameManager.Instance.LogManager.LogMessage($"Updating tracked object for Player {player} from ID: {_players[player]?.Id.ToString() ?? "null"} to new object ID: {playerObject.Id}");
             _players[player] = playerObject;
         }
    }

     // Coroutine to wait until a remote player's name is available before registering with the lobby
     private IEnumerator RegisterRemotePlayerWhenReady(Player playerComponent, PlayerRef player)
     {
         float timer = 0f, timeout = 10f;
         string playerName = "";
         while (string.IsNullOrEmpty(playerName) && timer < timeout) {
             if (playerComponent?.Object?.IsValid ?? false) {
                 try { playerName = playerComponent.GetPlayerName(); } catch {}
             } else {
                 // *** FIXED: Changed LogWarning to LogError ***
                 GameManager.Instance?.LogManager?.LogError($"Remote player {player} object became invalid while waiting for name. Aborting registration.");
                 yield break;
             }
             if (string.IsNullOrEmpty(playerName)) { timer += Time.deltaTime; yield return null; }
         }
         if (!string.IsNullOrEmpty(playerName)) {
             GameManager.Instance.LogManager.LogMessage($"Remote player {player}'s name '{playerName}' synced. Registering with LobbyManager.");
             GameManager.Instance.LobbyManager.RegisterPlayer(playerName, player);
             GameManager.Instance.UIManager.UpdatePlayersList();
         } else {
              GameManager.Instance.LogManager.LogError($"Timed out waiting for remote player {player}'s name to sync.");
         }
     }

    // Called by NetworkManager when any player leaves
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Player {player} left the game");
        string playerName = "";

        if (_players.TryGetValue(player, out NetworkObject playerObjectToDespawn) && playerObjectToDespawn != null) {
            Player playerComponent = playerObjectToDespawn.GetComponent<Player>();
            if (playerComponent != null) playerName = playerComponent.GetPlayerName();
            if (playerObjectToDespawn.HasStateAuthority) { runner.Despawn(playerObjectToDespawn); } else { /* Log lack of authority */ }
        }
        _players.Remove(player);

        var playerStates = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
        foreach (var state in playerStates) {
             if (state?.Object?.InputAuthority == player) {
                 if (state.Object.IsValid) { if (state.Object.HasStateAuthority) { runner.Despawn(state.Object); } else { /* Log lack of authority */ } }
                 break;
             }
         }

        if (!string.IsNullOrEmpty(playerName)) {
            GameManager.Instance.LobbyManager.SetPlayerReadyStatus(playerName, false);
            GameManager.Instance.LobbyManager.RemovePlayer(playerName);
            GameManager.Instance.LogManager.LogMessage($"Removed leaving player {playerName} from lobby.");
        } else {
             // *** FIXED: Changed LogWarning to LogMessage ***
             GameManager.Instance.LogManager.LogMessage($"Could not find name for leaving player {player} to fully remove from lobby.");
        }

        GameManager.Instance.LogManager.LogMessage($"Players remaining: {_players.Count}");
        GameManager.Instance.UIManager.UpdatePlayersList();
        if (player != runner.LocalPlayer) { GameManager.Instance.UIManager.UpdateStatus(!string.IsNullOrEmpty(playerName) ? $"Player {playerName} left." : $"Player {player.PlayerId} left."); }
    }

    // Clear tracking dictionary
    public void ClearPlayers() { _players.Clear(); GameManager.Instance.LogManager.LogMessage("Player tracking dictionary cleared."); GameManager.Instance.UIManager.UpdatePlayersList(); }

    // --- Getters ---
    public NetworkObject GetPlayerObject(PlayerRef player) { _players.TryGetValue(player, out NetworkObject p); return p; }
    public int GetPlayerCount() => _players.Count;
    public List<PlayerRef> GetAllPlayers() => new List<PlayerRef>(_players.Keys);
    public Player GetPlayerByName(string playerName) { foreach (var playerObj in _players.Values) { if (playerObj == null) continue; Player player = playerObj.GetComponent<Player>(); if (player != null && player.GetPlayerName() == playerName) return player; } return null; }
}