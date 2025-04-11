using Fusion;
using Fusion.Sockets;
using System;
using System.Collections; // Added for IEnumerator
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    // Network runner and player tracking
    private NetworkRunner _runner;
    private bool _isConnecting = false;

    // Session info
    private string _currentRoomName = "asd"; // Default room name
    public bool IsConnected => _runner != null && _runner.IsRunning;

    // Connection settings
    private const int MAX_PLAYERS = 4;

    // Reference to GameState prefab - ADDED
    private NetworkObject _gameStatePrefab;

    // Dictionary to store player name by player ref for rejoining
    private Dictionary<string, PlayerRef> _playerRefsByName = new Dictionary<string, PlayerRef>();

    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing NetworkManager...");
        CreateNetworkRunner();
        LoadGameStatePrefab(); // Load GameState prefab here
    }

    // ADDED: Method to load the GameState prefab
    private void LoadGameStatePrefab()
    {
        if (_gameStatePrefab != null) return;
        _gameStatePrefab = Resources.Load<NetworkObject>("GameStatePrefab");
        if (_gameStatePrefab == null)
            GameManager.Instance.LogManager.LogError("NetworkManager: GameStatePrefab not found in Resources folder! Critical Error.");
        else
            GameManager.Instance.LogManager.LogMessage("NetworkManager: GameStatePrefab loaded successfully.");
    }


    public void Shutdown()
    {
        if (_runner != null)
        {
            GameManager.Instance.LogManager.LogMessage("Shutting down NetworkRunner...");
            _runner.Shutdown();
            _runner = null;
        }
    }

    private void CreateNetworkRunner()
    {
        GameObject runnerObj = new GameObject("NetworkRunner");
        DontDestroyOnLoad(runnerObj);

        // Add required components
        _runner = runnerObj.AddComponent<NetworkRunner>();
        runnerObj.AddComponent<NetworkSceneManagerDefault>();

        // Configure runner
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        GameManager.Instance.LogManager.LogMessage("NetworkRunner created successfully");
    }

     public void LogNetworkProjectConfig()
    {
        // Check if config exists first
        if (NetworkProjectConfig.Global == null) {
            GameManager.Instance.LogManager.LogError("NetworkProjectConfig.Global is null!");
            return;
        }
        var config = NetworkProjectConfig.Global;
        GameManager.Instance.LogManager.LogMessage("=== Network Project Config ===");
        GameManager.Instance.LogManager.LogMessage($"PeerMode: {config.PeerMode}");
        GameManager.Instance.LogManager.LogMessage("=============================");
    }

    public async void CreateRoom(string roomName)
    {
        if (_isConnecting)
        {
            GameManager.Instance.LogManager.LogMessage("Already connecting to a room");
            return;
        }

        if (string.IsNullOrEmpty(roomName))
        {
            roomName = "asd"; // Default to "asd" if empty
        }

        _currentRoomName = roomName;
        GameManager.Instance.LogManager.LogMessage($"Creating room: {roomName}");
        GameManager.Instance.UIManager.UpdateStatus($"Creating room: {roomName}...");
        _isConnecting = true;

        // Create and join a shared mode session
        try
        {
            // Create session properties to help identify the session
            Dictionary<string, SessionProperty> customProps = new Dictionary<string, SessionProperty>()
            {
                { "CreatedTime", (SessionProperty)DateTime.UtcNow.Ticks },
                { "GameVersion", (SessionProperty)"1.0" }
            };

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Shared, // THIS IS CRITICAL - use shared mode
                SessionName = roomName,
                SessionProperties = customProps,
                SceneManager = _runner.GetComponent<NetworkSceneManagerDefault>(),
                PlayerCount = MAX_PLAYERS
            };

            GameManager.Instance.LogManager.LogMessage($"StartGame Args: GameMode={startGameArgs.GameMode}, SessionName={startGameArgs.SessionName}");

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                GameManager.Instance.LogManager.LogMessage($"Room created successfully: {roomName}");
                GameManager.Instance.LogManager.LogMessage($"Local player ID: {_runner.LocalPlayer.PlayerId}");
                GameManager.Instance.UIManager.UpdateStatus($"Room '{roomName}' created!");

                // Corrected: Show Lobby UI instead of hiding Connect UI
                GameManager.Instance.UIManager.ShowLobbyUI();

                // --- Spawn GameState immediately if Authority ---
                if (_runner.IsServer || _runner.IsSharedModeMasterClient)
                {
                    SpawnGameStateIfMissing();
                }
                // -----------------------------------------------------
            }
            else
            {
                GameManager.Instance.LogManager.LogError($"Failed to create room: {result.ShutdownReason} - {result.ErrorMessage}");
                GameManager.Instance.UIManager.UpdateStatus($"Failed: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            GameManager.Instance.LogManager.LogError($"Error creating room: {e.Message}\n{e.StackTrace}");
            GameManager.Instance.UIManager.UpdateStatus("Error creating room");
        }

        _isConnecting = false;
    }

    public async void JoinRoom(string roomName)
    {
        if (_isConnecting)
        {
            GameManager.Instance.LogManager.LogMessage("Already connecting to a room");
            return;
        }

        if (string.IsNullOrEmpty(roomName))
        {
            roomName = "asd"; // Default to "asd" if empty
        }

        _currentRoomName = roomName;
        GameManager.Instance.LogManager.LogMessage($"Joining room: {roomName}");
        GameManager.Instance.UIManager.UpdateStatus($"Joining room: {roomName}...");
        _isConnecting = true;

        // Join the shared mode session
        try
        {
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Shared, // THIS IS CRITICAL - use shared mode
                SessionName = roomName,
                SceneManager = _runner.GetComponent<NetworkSceneManagerDefault>()
            };

            GameManager.Instance.LogManager.LogMessage($"StartGame Args: GameMode={startGameArgs.GameMode}, SessionName={startGameArgs.SessionName}");

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                GameManager.Instance.LogManager.LogMessage($"Joined room successfully: {roomName}");
                GameManager.Instance.LogManager.LogMessage($"Local player ID: {_runner.LocalPlayer.PlayerId}");
                GameManager.Instance.LogManager.LogMessage($"Players in session: {_runner.ActivePlayers.Count()}");
                GameManager.Instance.UIManager.UpdateStatus($"Joined room: {roomName}");

                // Corrected: Show Lobby UI instead of hiding Connect UI
                GameManager.Instance.UIManager.ShowLobbyUI();

                 // --- Attempt to Spawn GameState if Authority ---
                if (_runner.IsServer || _runner.IsSharedModeMasterClient)
                {
                    SpawnGameStateIfMissing();
                }
                 // -----------------------------------------------------
            }
            else
            {
                GameManager.Instance.LogManager.LogError($"Failed to join room: {result.ShutdownReason} - {result.ErrorMessage}");
                GameManager.Instance.UIManager.UpdateStatus($"Failed: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            GameManager.Instance.LogManager.LogError($"Error joining room: {e.Message}\n{e.StackTrace}");
            GameManager.Instance.UIManager.UpdateStatus("Error joining room");
        }

        _isConnecting = false;
    }

    // --- ADDED: Helper method to spawn GameState ---
    private void SpawnGameStateIfMissing()
    {
        if (_runner == null || !_runner.IsRunning || !(_runner.IsServer || _runner.IsSharedModeMasterClient))
        {
            // Only authority can spawn
            return;
        }

        // Check if GameState already exists
        if (GameState.Instance != null && GameState.Instance.IsSpawned())
        {
            GameManager.Instance.LogManager.LogMessage("NetworkManager: GameState already exists, skipping spawn.");
            return;
        }

        if (_gameStatePrefab == null)
        {
            GameManager.Instance.LogManager.LogError("NetworkManager: Cannot spawn GameState - prefab not loaded!");
            return;
        }

        GameManager.Instance.LogManager.LogMessage("NetworkManager: Authority spawning GameState...");
        try
        {
            _runner.Spawn(_gameStatePrefab, Vector3.zero, Quaternion.identity, null); // Spawn with no specific input authority
            GameManager.Instance.LogManager.LogMessage("NetworkManager: GameState spawn command issued.");
        }
        catch (Exception ex)
        {
            GameManager.Instance.LogManager.LogError($"NetworkManager: Failed to spawn GameState: {ex.Message}\n{ex.StackTrace}");
        }
    }
    // --------------------------------------------

    public NetworkRunner GetRunner()
    {
        return _runner;
    }

    public bool CheckIsRejoining(string playerName)
    {
        // Check if this player name was previously in the session
        return GameManager.Instance.LobbyManager.IsPlayerRegistered(playerName);
    }

    #region INetworkRunnerCallbacks Implementation

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Player {player} joined the network");

        // --- Authority checks if GameState needs spawning when *any* player joins ---
        if (runner.IsServer || runner.IsSharedModeMasterClient)
        {
            StartCoroutine(DelayedSpawnGameStateCheck()); // Use coroutine for slight delay
        }
        // --------------------------------------------------------------------------------

        // Update all clients about the player count
        string playerCountMessage = $"Players in room: {runner.ActivePlayers.Count()}";
        GameManager.Instance.LogManager.LogMessage(playerCountMessage);

        // Call player manager to handle the new player (spawns Player and PlayerState)
        if (player == runner.LocalPlayer)
        {
            // Only spawn our own player object sequence
            GameManager.Instance.PlayerManager.OnLocalPlayerJoined(runner, player);
        }
        else
        {
            // Just track remote players - their objects will be spawned and detected via OnPlayerObjectSpawned
            GameManager.Instance.PlayerManager.OnRemotePlayerJoined(runner, player);
        }
    }

    // Added Coroutine for delayed check
    private IEnumerator DelayedSpawnGameStateCheck() {
        yield return null; // Wait one frame
        SpawnGameStateIfMissing();
    }


    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Player {player} left the network");
        // Update player manager
        GameManager.Instance.PlayerManager.OnPlayerLeft(runner, player);
        // Update all clients about the player count
        string playerCountMessage = $"Players in room: {runner.ActivePlayers.Count()}";
        GameManager.Instance.LogManager.LogMessage(playerCountMessage);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Create and provide input data from the local player
        var data = new NetworkInputData
        {
            horizontal = Input.GetAxis("Horizontal"),
            vertical = Input.GetAxis("Vertical"),
            buttons = new NetworkButtons()
        };

        // Add button presses if needed
        if (Input.GetKeyDown(KeyCode.Space))
            data.buttons.Set(0, true); // Jump
        if (Input.GetKeyDown(KeyCode.R))
            data.buttons.Set(1, true); // Ready toggle

        // Set the input data
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        GameManager.Instance.LogManager.LogMessage($"Network shutdown: {shutdownReason}");
        // Clear the players
        GameManager.Instance.PlayerManager.ClearPlayers();
        // Reset lobby
        GameManager.Instance.LobbyManager.Reset();
        // Show the connection UI
        GameManager.Instance.UIManager.ShowConnectUI();
        GameManager.Instance.UIManager.UpdateStatus($"Disconnected: {shutdownReason}");
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        GameManager.Instance.LogManager.LogMessage("Connected to server");
         // --- Attempt to Spawn GameState if Authority ---
        if (runner.IsServer || runner.IsSharedModeMasterClient)
        {
            SpawnGameStateIfMissing();
        }
         // -----------------------------------------------------
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        GameManager.Instance.LogManager.LogMessage($"Disconnected from server: {reason}");
        GameManager.Instance.UIManager.ShowConnectUI();
        GameManager.Instance.UIManager.UpdateStatus($"Disconnected: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        GameManager.Instance.LogManager.LogMessage($"Connect request received from {request.RemoteAddress}");
        // Always accept connection requests in this simple implementation
        request.Accept(); // Explicitly accept
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        GameManager.Instance.LogManager.LogError($"Connect failed: {reason}");
        GameManager.Instance.UIManager.UpdateStatus($"Connect failed: {reason}");
        GameManager.Instance.UIManager.ShowConnectUI();
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        int sessionCount = sessionList?.Count ?? 0;
        GameManager.Instance.LogManager.LogMessage($"Session list updated: {sessionCount} sessions");

        if (sessionList != null && sessionList.Count > 0)
        {
            foreach (var session in sessionList)
            {
                GameManager.Instance.LogManager.LogMessage($"Session: {session.Name}, Players: {session.PlayerCount}/{session.MaxPlayers}");
            }
        }
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {
        GameManager.Instance.LogManager.LogMessage("Host migration occurred");
        // ADDED: After host migration, the new host should ensure GameState exists
        if (runner.IsServer || runner.IsSharedModeMasterClient) {
            SpawnGameStateIfMissing();
        }
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        // GameManager.Instance.LogManager.LogMessage($"Reliable data received from player {player}"); // Can be spammy
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        // Only log significant progress to avoid spam
        // if (progress == 1.0f)
        // {
        //     GameManager.Instance.LogManager.LogMessage($"Reliable data transfer complete for player {player}");
        // }
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        // GameManager.Instance.LogManager.LogMessage($"Object {obj.Id} entered AOI for player {player}"); // Can be spammy
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        // GameManager.Instance.LogManager.LogMessage($"Object {obj.Id} exited AOI for player {player}"); // Can be spammy
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        GameManager.Instance.LogManager.LogMessage("Scene load completed");
        // ADDED: After scene load, authority should ensure GameState exists
        if (runner.IsServer || runner.IsSharedModeMasterClient) {
            SpawnGameStateIfMissing();
        }
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        GameManager.Instance.LogManager.LogMessage("Scene load started");
    }

    #endregion
}