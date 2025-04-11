using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private NetworkObject _playerPrefab;
    [SerializeField] private NetworkObject _playerStatePrefab; // Added reference for PlayerState prefab
    private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();

    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing PlayerManager...");
        // Load Player Prefab
        if (_playerPrefab == null)
        {
            _playerPrefab = Resources.Load<NetworkObject>("PlayerPrefab");
            if (_playerPrefab == null)
                GameManager.Instance.LogManager.LogError("PlayerPrefab not found! Place in Resources.");
            else
                GameManager.Instance.LogManager.LogMessage("PlayerPrefab loaded from Resources.");
        }
         // Load PlayerState Prefab
        if (_playerStatePrefab == null)
        {
            _playerStatePrefab = Resources.Load<NetworkObject>("PlayerStatePrefab");
            if (_playerStatePrefab == null)
                GameManager.Instance.LogManager.LogError("PlayerStatePrefab not found! Place in Resources.");
            else
                GameManager.Instance.LogManager.LogMessage("PlayerStatePrefab loaded from Resources.");
        }
    }

    // Only spawns the local player's objects (Player and PlayerState)
    public void OnLocalPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // Prevent duplicate spawning for the same player reference
        if (_players.ContainsKey(player))
        {
            GameManager.Instance.LogManager.LogMessage($"Player {player} already has objects spawned. Skipping.");
            return;
        }

        GameManager.Instance.LogManager.LogMessage($"Local player {player} joined - starting spawn sequence.");
        StartCoroutine(SpawnLocalPlayerSequence(runner, player));
    }

     // Coroutine to handle the spawn sequence ensuring correct order
    private IEnumerator SpawnLocalPlayerSequence(NetworkRunner runner, PlayerRef player)
    {
        NetworkObject playerObject = null; // Define playerObject here to access later

         // --- Step 1: Spawn Player Object ---
         GameManager.Instance.LogManager.LogMessage($"Player {player}: Attempting to spawn Player object...");
         if (_playerPrefab == null)
         {
             GameManager.Instance.LogManager.LogError("Player prefab is missing! Cannot spawn Player.");
             yield break;
         }

        try
        {
            string playerName = GameManager.Instance.UIManager.GetLocalPlayerName();
            Vector2 spawnPosition = new Vector2(UnityEngine.Random.Range(-5, 5), UnityEngine.Random.Range(-5, 5));

            // Spawn the Player object, granting input authority to the joining player
            playerObject = runner.Spawn(_playerPrefab, position: spawnPosition, inputAuthority: player);

            if (playerObject != null)
            {
                _players[player] = playerObject; // Track the player object
                Player playerComponent = playerObject.GetComponent<Player>();
                if (playerComponent != null)
                {
                    playerComponent.SetPlayerName(playerName); // Set name immediately
                    GameManager.Instance.LogManager.LogMessage($"Player object spawned for {playerName} ({player}, ID: {playerObject.Id}).");

                    // Ensure registration with LobbyManager
                    GameManager.Instance.LobbyManager.RegisterPlayer(playerName, player);
                 }
                 else
                 {
                     GameManager.Instance.LogManager.LogError("Spawned Player object is missing Player component!");
                 }
                 // Create camera AFTER spawning player object
                 GameManager.Instance.CameraManager.CreatePlayerCamera(playerObject.transform);
            }
            else
            {
                GameManager.Instance.LogManager.LogError($"Failed to spawn Player object for player {player}. Aborting sequence.");
                yield break; // Stop if player object spawn fails
            }
        }
        catch (Exception ex)
        {
            GameManager.Instance.LogManager.LogError($"Error spawning Player object for {player}: {ex.Message}\n{ex.StackTrace}");
            yield break; // Stop on error
        }

        // --- Step 2: Wait for GameState ---
        // Wait here until GameState is confirmed spawned and ready (spawned by authority in NetworkManager).
        GameManager.Instance.LogManager.LogMessage($"Player {player} waiting for GameState to be ready before spawning PlayerState...");
        float timer = 0f;
        float timeout = 25f; // Slightly increased timeout
        while (GameState.Instance == null || !GameState.Instance.IsSpawned()) // Check IsSpawned property
        {
             // Check if the runner is still active during the wait
             if (runner == null || !runner.IsRunning)
             {
                 GameManager.Instance.LogManager.LogMessage($"Player {player}: NetworkRunner stopped while waiting for GameState. Aborting PlayerState spawn.");
                 yield break;
             }

            timer += Time.deltaTime;
            if (timer > timeout)
            {
                GameManager.Instance.LogManager.LogError($"Player {player} timed out ({timeout}s) waiting for GameState before spawning PlayerState. GameState.Instance IsNull: {GameState.Instance == null}, IsSpawned: {GameState.Instance?.IsSpawned()}. Aborting.");
                yield break; // Stop the coroutine if timeout occurs
            }
            yield return null; // Wait for the next frame
        }
        GameManager.Instance.LogManager.LogMessage($"Player {player}: GameState found and ready (ID: {GameState.Instance.Id}). Proceeding to spawn PlayerState object.");


        // --- Step 3: Spawn PlayerState Object ---
        if (_playerStatePrefab == null)
        {
            GameManager.Instance.LogManager.LogError("PlayerState prefab is missing! Cannot spawn PlayerState.");
            yield break;
        }

        try
        {
            // Spawn the PlayerState, granting input authority to the same player
            NetworkObject stateObj = runner.Spawn(_playerStatePrefab, inputAuthority: player);

            if (stateObj != null)
            {
                PlayerState playerStateComponent = stateObj.GetComponent<PlayerState>();
                if(playerStateComponent != null)
                {
                    GameManager.Instance.LogManager.LogMessage($"PlayerState object spawned for player {player} (ID: {stateObj.Id}).");
                    // PlayerState's Spawned() method will handle registration with GameState
                }
                else
                {
                    GameManager.Instance.LogManager.LogError("Spawned PlayerState object is missing PlayerState component!");
                }
            }
            else
            {
                GameManager.Instance.LogManager.LogError($"Failed to spawn PlayerState object for player {player}.");
            }
        }
        catch (Exception ex)
        {
            GameManager.Instance.LogManager.LogError($"Error spawning PlayerState object for {player}: {ex.Message}\n{ex.StackTrace}");
        }

        GameManager.Instance.LogManager.LogMessage($"Spawn sequence complete for local player {player}.");
    }


    // Just track remote players that joined (their clients will spawn their characters)
    public void OnRemotePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Remote player {player} joined - waiting for their character to spawn");
        // Remote players' objects are tracked via OnPlayerObjectSpawned when they appear.
    }

    // Called when any player's network object spawns and becomes known locally
    public void OnPlayerObjectSpawned(NetworkRunner runner, NetworkObject playerObject, PlayerRef player)
    {
        // Track the object if it's not the local player's or if local player reconnected
         if (!_players.ContainsKey(player))
        {
            _players[player] = playerObject;
            GameManager.Instance.LogManager.LogMessage($"Tracking remote player object: {playerObject.Id} for player {player}");

            // Attempt to register with LobbyManager based on the name from the Player component
            Player playerComponent = playerObject.GetComponent<Player>();
            if (playerComponent != null)
            {
                 StartCoroutine(RegisterRemotePlayerWhenReady(playerComponent, player));
            }
        }
         else if (_players[player] != playerObject) // Handle case where PlayerRef exists but object is new (reconnect?)
         {
             GameManager.Instance.LogManager.LogMessage($"Updating tracked object for Player {player} to new object ID: {playerObject.Id}");
             _players[player] = playerObject;
         }
    }

     // Coroutine to wait until a remote player's name is available before registering
     private IEnumerator RegisterRemotePlayerWhenReady(Player playerComponent, PlayerRef player)
     {
         float timer = 0f;
         float timeout = 10f;
         string playerName = "";

         while (string.IsNullOrEmpty(playerName) && timer < timeout)
         {
             if (playerComponent != null && playerComponent.Object != null && playerComponent.Object.IsValid) // Check if object is valid
             {
                try {
                     playerName = playerComponent.GetPlayerName(); // Safely attempt to get name
                } catch (InvalidOperationException) {
                     // Ignore, networked property might not be ready yet
                }
             }
             if (string.IsNullOrEmpty(playerName))
             {
                 timer += Time.deltaTime;
                 yield return null; // Wait a frame
             }
         }

         if (!string.IsNullOrEmpty(playerName))
         {
             GameManager.Instance.LogManager.LogMessage($"Remote player {player}'s name '{playerName}' is ready. Registering with LobbyManager.");
             GameManager.Instance.LobbyManager.RegisterPlayer(playerName, player);
             GameManager.Instance.UIManager.UpdatePlayersList(); // Update UI now that name is known
         }
         else
         {
              GameManager.Instance.LogManager.LogMessage($"Could not get name for remote player {player} after timeout. Lobby registration might be incomplete.");
         }
     }


    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Player {player} left the game");

        // Get player name BEFORE despawning the object
        string playerName = "";
        if (_players.TryGetValue(player, out NetworkObject playerObjectToDespawn) && playerObjectToDespawn != null)
        {
            Player playerComponent = playerObjectToDespawn.GetComponent<Player>();
            if (playerComponent != null)
            {
                playerName = playerComponent.GetPlayerName();
            }
        }

        // Inform LobbyManager WITHOUT removing player data (for rejoin)
        // Mark them as not ready if they were previously ready
        if (!string.IsNullOrEmpty(playerName))
        {
            GameManager.Instance.LobbyManager.SetPlayerReadyStatus(playerName, false); // Mark as not ready upon leaving
            GameManager.Instance.LogManager.LogMessage($"Marked leaving player {playerName} as not ready.");
        }


        // --- Despawn Associated Objects ---
        // Find and Despawn Player Object
        if (_players.TryGetValue(player, out NetworkObject playerObj))
        {
            if (playerObj != null && playerObj.IsValid) // Check if valid before despawn
            {
                 // Only despawn if the runner has state authority over the object OR if it's the local player's object leaving
                if (playerObj.HasStateAuthority || player == runner.LocalPlayer)
                {
                    GameManager.Instance.LogManager.LogMessage($"Despawning Player object (ID: {playerObj.Id}) for player {player}.");
                    runner.Despawn(playerObj);
                } else {
                     GameManager.Instance.LogManager.LogMessage($"Skipping despawn of Player object (ID: {playerObj.Id}) for player {player} - No State Authority.");
                }
            }
             _players.Remove(player); // Remove from tracking dictionary
        }

        // Find and Despawn PlayerState Object (Iterate through all PlayerStates)
        // This is less direct but necessary if PlayerState isn't explicitly tracked here
         var playerStates = FindObjectsOfType<PlayerState>(); // Find all active PlayerState components
         foreach (var state in playerStates)
         {
             if (state.Object != null && state.Object.InputAuthority == player) // Find the one matching the leaving player's authority
             {
                 if (state.Object.IsValid) // Check if valid before despawn
                 {
                     // Only despawn if the runner has state authority over the object OR if it's the local player's object leaving
                     if (state.Object.HasStateAuthority || player == runner.LocalPlayer)
                     {
                         GameManager.Instance.LogManager.LogMessage($"Despawning PlayerState object (ID: {state.Object.Id}) for player {player}.");
                         runner.Despawn(state.Object);
                     } else {
                         GameManager.Instance.LogManager.LogMessage($"Skipping despawn of PlayerState object (ID: {state.Object.Id}) for player {player} - No State Authority.");
                     }
                 }
                 break; // Found and processed the state object for the player
             }
         }


        GameManager.Instance.LogManager.LogMessage($"Players remaining: {_players.Count}");
        GameManager.Instance.UIManager.UpdatePlayersList(); // Update UI

        // Update status message for others
        if (player != runner.LocalPlayer)
        {
            GameManager.Instance.UIManager.UpdateStatus(!string.IsNullOrEmpty(playerName) ? $"Player {playerName} left." : $"Player {player.PlayerId} left.");
        }
    }

    public void ClearPlayers()
    {
        // Note: This should generally not despawn objects unless shutting down.
        // Rely on OnPlayerLeft for individual despawns.
        _players.Clear();
        GameManager.Instance.LogManager.LogMessage("Player tracking dictionary cleared.");
        GameManager.Instance.UIManager.UpdatePlayersList();
    }

    public NetworkObject GetPlayerObject(PlayerRef player)
    {
        _players.TryGetValue(player, out NetworkObject playerObject);
        return playerObject;
    }

    public int GetPlayerCount()
    {
        // Returns the count of tracked player objects
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
            if (playerObj == null) continue; // Skip if object became null
            Player player = playerObj.GetComponent<Player>();
            if (player != null && player.GetPlayerName() == playerName)
            {
                return player;
            }
        }
        return null;
    }
}