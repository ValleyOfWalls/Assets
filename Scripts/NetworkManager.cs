using Fusion;
using Fusion.Sockets;
using System;
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
    
    // Dictionary to store player name by player ref for rejoining
    private Dictionary<string, PlayerRef> _playerRefsByName = new Dictionary<string, PlayerRef>();

    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing NetworkManager...");
        CreateNetworkRunner();
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
                
                // Hide the connection panel
                GameManager.Instance.UIManager.HideConnectUI();
            }
            else
            {
                GameManager.Instance.LogManager.LogError($"Failed to create room: {result.ShutdownReason} - {result.ErrorMessage}");
                GameManager.Instance.UIManager.UpdateStatus($"Failed: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            GameManager.Instance.LogManager.LogError($"Error creating room: {e.Message}");
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
                
                // Hide the connection panel
                GameManager.Instance.UIManager.HideConnectUI();
            }
            else
            {
                GameManager.Instance.LogManager.LogError($"Failed to join room: {result.ShutdownReason} - {result.ErrorMessage}");
                GameManager.Instance.UIManager.UpdateStatus($"Failed: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            GameManager.Instance.LogManager.LogError($"Error joining room: {e.Message}");
            GameManager.Instance.UIManager.UpdateStatus("Error joining room");
        }

        _isConnecting = false;
    }

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
        
        // Update all clients about the player count
        string playerCountMessage = $"Players in room: {runner.ActivePlayers.Count()}";
        GameManager.Instance.LogManager.LogMessage(playerCountMessage);
        
        // Call player manager to handle the new player
        GameManager.Instance.PlayerManager.OnPlayerJoined(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Player {player} left the network");
        
        // Get player name before removing
        string playerName = GameManager.Instance.LobbyManager.GetPlayerName(player);
        
        // Update player manager
        GameManager.Instance.PlayerManager.OnPlayerLeft(runner, player);
        
        // Update all clients about the player count
        string playerCountMessage = $"Players in room: {runner.ActivePlayers.Count()}";
        GameManager.Instance.LogManager.LogMessage(playerCountMessage);
        
        // Update lobby (don't remove data to allow rejoining)
        if (!string.IsNullOrEmpty(playerName))
        {
            // We don't remove the player from LobbyManager to allow for rejoin
            // But we do need to update the UI
            GameManager.Instance.UIManager.UpdatePlayersList();
        }
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
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        GameManager.Instance.LogManager.LogMessage($"Reliable data received from player {player}");
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        // Only log significant progress to avoid spam
        if (progress == 1.0f)
        {
            GameManager.Instance.LogManager.LogMessage($"Reliable data transfer complete for player {player}");
        }
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Object {obj.Id} entered AOI for player {player}");
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        GameManager.Instance.LogManager.LogMessage($"Object {obj.Id} exited AOI for player {player}");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        GameManager.Instance.LogManager.LogMessage("Scene load completed");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        GameManager.Instance.LogManager.LogMessage("Scene load started");
    }

    #endregion
}