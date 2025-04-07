using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour, INetworkRunnerCallbacks
{
    // Singleton instance
    public static GameManager Instance { get; private set; }

    [Header("Network References")]
    [SerializeField] private NetworkObject _playerPrefab;
    [SerializeField] private GameObject _cameraPrefab;

    // Runtime references
    private NetworkRunner _runner;
    private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();
    private bool _isConnecting = false;
    private List<string> _logMessages = new List<string>();
    private int _maxLogMessages = 20;

    // UI references
    private Canvas _uiCanvas;
    private TMP_InputField _roomNameInput;
    private Button _createRoomButton;
    private Button _joinRoomButton;
    private TMP_Text _statusText;
    private TMP_Text _debugText;
    private GameObject _connectPanel;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Find the player prefab if not assigned
        if (_playerPrefab == null)
        {
            _playerPrefab = Resources.Load<NetworkObject>("PlayerPrefab");
            if (_playerPrefab == null)
                Debug.LogError("PlayerPrefab not found! Please create it and place it in a Resources folder.");
        }
    }

    private void Start()
    {
        // Create NetworkRunner
        CreateNetworkRunner();
        
        // Create UI
        CreateUI();
        SetupUIListeners();
        
        // Log initial configuration
        LogNetworkProjectConfig();
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
        
        // Create camera prefab if needed
        if (_cameraPrefab == null)
        {
            _cameraPrefab = new GameObject("Camera_Prefab");
            _cameraPrefab.SetActive(false);
            DontDestroyOnLoad(_cameraPrefab);
            
            // Add camera component
            Camera cam = _cameraPrefab.AddComponent<Camera>();
            _cameraPrefab.AddComponent<AudioListener>();
            
            // Add follow script
            FollowCamera follow = _cameraPrefab.AddComponent<FollowCamera>();
            follow.offset = new Vector3(0, 3, -5);
            follow.smoothSpeed = 0.125f;
        }
        
        Debug.Log("NetworkRunner created successfully");
    }

    private void CreateUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("UI Canvas");
        _uiCanvas = canvasObj.AddComponent<Canvas>();
        _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        // Create a panel for connection UI
        GameObject panelObj = new GameObject("Connect Panel");
        panelObj.transform.SetParent(_uiCanvas.transform, false);
        _connectPanel = panelObj;
        
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.3f, 0.3f);
        panelRect.anchorMax = new Vector2(0.7f, 0.7f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Add title text
        GameObject titleObj = new GameObject("Title Text");
        titleObj.transform.SetParent(panelObj.transform, false);
        TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Photon Fusion Lobby";
        titleText.fontSize = 24;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.8f);
        titleRect.anchorMax = new Vector2(0.9f, 0.95f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // Add room name input
        GameObject inputObj = new GameObject("Room Name Input");
        inputObj.transform.SetParent(panelObj.transform, false);
        _roomNameInput = inputObj.AddComponent<TMP_InputField>();
        Image inputImage = inputObj.AddComponent<Image>();
        inputImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.1f, 0.6f);
        inputRect.anchorMax = new Vector2(0.9f, 0.75f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;
        
        // Create text area for input field
        GameObject textArea = new GameObject("Text Area");
        textArea.transform.SetParent(inputObj.transform, false);
        TMP_Text inputText = textArea.AddComponent<TextMeshProUGUI>();
        inputText.text = "";
        inputText.color = Color.white;
        inputText.fontSize = 18;
        inputText.alignment = TextAlignmentOptions.Left;
        RectTransform textRect = textArea.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0.1f);
        textRect.anchorMax = new Vector2(0.95f, 0.9f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Create placeholder for input field
        GameObject placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(inputObj.transform, false);
        TMP_Text placeholderText = placeholder.AddComponent<TextMeshProUGUI>();
        placeholderText.text = "Enter Room Name";
        placeholderText.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        placeholderText.fontSize = 18;
        placeholderText.alignment = TextAlignmentOptions.Left;
        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = new Vector2(0.05f, 0.1f);
        placeholderRect.anchorMax = new Vector2(0.95f, 0.9f);
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        
        // Connect the input field components
        _roomNameInput.textComponent = inputText;
        _roomNameInput.placeholder = placeholderText;
        _roomNameInput.text = "Room" + UnityEngine.Random.Range(1000, 10000);

        // Add create room button
        GameObject createButtonObj = new GameObject("Create Room Button");
        createButtonObj.transform.SetParent(panelObj.transform, false);
        _createRoomButton = createButtonObj.AddComponent<Button>();
        Image createButtonImage = createButtonObj.AddComponent<Image>();
        createButtonImage.color = new Color(0.2f, 0.7f, 0.2f, 1f);
        RectTransform createButtonRect = createButtonObj.GetComponent<RectTransform>();
        createButtonRect.anchorMin = new Vector2(0.1f, 0.4f);
        createButtonRect.anchorMax = new Vector2(0.45f, 0.55f);
        createButtonRect.offsetMin = Vector2.zero;
        createButtonRect.offsetMax = Vector2.zero;
        
        GameObject createButtonText = new GameObject("Text");
        createButtonText.transform.SetParent(createButtonObj.transform, false);
        TMP_Text createText = createButtonText.AddComponent<TextMeshProUGUI>();
        createText.text = "Create Room";
        createText.fontSize = 18;
        createText.alignment = TextAlignmentOptions.Center;
        createText.color = Color.white;
        RectTransform createTextRect = createButtonText.GetComponent<RectTransform>();
        createTextRect.anchorMin = Vector2.zero;
        createTextRect.anchorMax = Vector2.one;
        createTextRect.offsetMin = Vector2.zero;
        createTextRect.offsetMax = Vector2.zero;

        // Add join room button
        GameObject joinButtonObj = new GameObject("Join Room Button");
        joinButtonObj.transform.SetParent(panelObj.transform, false);
        _joinRoomButton = joinButtonObj.AddComponent<Button>();
        Image joinButtonImage = joinButtonObj.AddComponent<Image>();
        joinButtonImage.color = new Color(0.2f, 0.2f, 0.7f, 1f);
        RectTransform joinButtonRect = joinButtonObj.GetComponent<RectTransform>();
        joinButtonRect.anchorMin = new Vector2(0.55f, 0.4f);
        joinButtonRect.anchorMax = new Vector2(0.9f, 0.55f);
        joinButtonRect.offsetMin = Vector2.zero;
        joinButtonRect.offsetMax = Vector2.zero;
        
        GameObject joinButtonText = new GameObject("Text");
        joinButtonText.transform.SetParent(joinButtonObj.transform, false);
        TMP_Text joinText = joinButtonText.AddComponent<TextMeshProUGUI>();
        joinText.text = "Join Room";
        joinText.fontSize = 18;
        joinText.alignment = TextAlignmentOptions.Center;
        joinText.color = Color.white;
        RectTransform joinTextRect = joinButtonText.GetComponent<RectTransform>();
        joinTextRect.anchorMin = Vector2.zero;
        joinTextRect.anchorMax = Vector2.one;
        joinTextRect.offsetMin = Vector2.zero;
        joinTextRect.offsetMax = Vector2.zero;

        // Add status text
        GameObject statusObj = new GameObject("Status Text");
        statusObj.transform.SetParent(panelObj.transform, false);
        _statusText = statusObj.AddComponent<TextMeshProUGUI>();
        _statusText.text = "Enter a room name and create or join a room";
        _statusText.fontSize = 16;
        _statusText.alignment = TextAlignmentOptions.Center;
        _statusText.color = Color.white;
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.1f, 0.2f);
        statusRect.anchorMax = new Vector2(0.9f, 0.35f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;

        // Add debug text
        GameObject debugObj = new GameObject("Debug Text");
        debugObj.transform.SetParent(_uiCanvas.transform, false);
        _debugText = debugObj.AddComponent<TextMeshProUGUI>();
        _debugText.text = "";
        _debugText.fontSize = 12;
        _debugText.alignment = TextAlignmentOptions.Left;
        _debugText.color = Color.white;
        RectTransform debugRect = debugObj.GetComponent<RectTransform>();
        debugRect.anchorMin = new Vector2(0.01f, 0.01f);
        debugRect.anchorMax = new Vector2(0.4f, 0.25f);
        debugRect.offsetMin = Vector2.zero;
        debugRect.offsetMax = Vector2.zero;
        
        Debug.Log("UI created successfully");
    }

    private void SetupUIListeners()
    {
        if (_createRoomButton != null)
        {
            _createRoomButton.onClick.RemoveAllListeners();
            _createRoomButton.onClick.AddListener(CreateRoom);
        }

        if (_joinRoomButton != null)
        {
            _joinRoomButton.onClick.RemoveAllListeners();
            _joinRoomButton.onClick.AddListener(JoinRoom);
        }
        
        Debug.Log("UI listeners set up");
    }

    /// <summary>
    /// Logs the current Network Project Config settings
    /// </summary>
    private void LogNetworkProjectConfig()
    {
        var config = NetworkProjectConfig.Global;
        LogMessage("=== Network Project Config ===");
        LogMessage($"PeerMode: {config.PeerMode}");
        LogMessage("=============================");
    }

    /// <summary>
    /// Creates and joins a new room as host
    /// </summary>
    public async void CreateRoom()
    {
        if (_isConnecting)
        {
            LogMessage("Already connecting to a room");
            return;
        }

        string roomName = string.IsNullOrEmpty(_roomNameInput?.text) 
            ? $"Room_{UnityEngine.Random.Range(0, 10000)}" 
            : _roomNameInput.text;

        LogMessage($"Creating room: {roomName}");
        _isConnecting = true;
        UpdateStatus($"Creating room: {roomName}...");

        // Create and join a shared mode session
        try
        {
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Shared, // THIS IS CRITICAL - use shared mode
                SessionName = roomName,
                SceneManager = _runner.GetComponent<NetworkSceneManagerDefault>(),
                PlayerCount = 4 // Specify player count (optional)
            };

            LogMessage($"StartGame Args: GameMode={startGameArgs.GameMode}, SessionName={startGameArgs.SessionName}");
            
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                LogMessage($"Room created successfully: {roomName}");
                UpdateStatus($"Room '{roomName}' created!");
                
                // Hide the connection panel
                if (_connectPanel != null)
                    _connectPanel.SetActive(false);
            }
            else
            {
                LogError($"Failed to create room: {result.ShutdownReason} - {result.ErrorMessage}");
                UpdateStatus($"Failed: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            LogError($"Error creating room: {e.Message}");
            UpdateStatus("Error creating room");
        }

        _isConnecting = false;
    }

    /// <summary>
    /// Joins an existing room
    /// </summary>
    public async void JoinRoom()
    {
        if (_isConnecting)
        {
            LogMessage("Already connecting to a room");
            return;
        }

        if (string.IsNullOrEmpty(_roomNameInput?.text))
        {
            LogMessage("Room name is empty");
            UpdateStatus("Please enter a room name");
            return;
        }

        string roomName = _roomNameInput.text;
        LogMessage($"Joining room: {roomName}");
        UpdateStatus($"Joining room: {roomName}...");
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

            LogMessage($"StartGame Args: GameMode={startGameArgs.GameMode}, SessionName={startGameArgs.SessionName}");
            
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                LogMessage($"Joined room successfully: {roomName}");
                UpdateStatus($"Joined room: {roomName}");
                
                // Hide the connection panel
                if (_connectPanel != null)
                    _connectPanel.SetActive(false);
            }
            else
            {
                LogError($"Failed to join room: {result.ShutdownReason} - {result.ErrorMessage}");
                UpdateStatus($"Failed: {result.ErrorMessage}");
            }
        }
        catch (Exception e)
        {
            LogError($"Error joining room: {e.Message}");
            UpdateStatus("Error joining room");
        }

        _isConnecting = false;
    }

    private void UpdateStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
        }
    }

    private void LogMessage(string message)
    {
        Debug.Log($"[GameManager] {message}");
        
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _logMessages.Add($"[{timestamp}] {message}");
        
        // Keep log at reasonable size
        while (_logMessages.Count > _maxLogMessages)
            _logMessages.RemoveAt(0);
            
        UpdateDebugText();
    }

    private void LogError(string message)
    {
        Debug.LogError($"[GameManager] {message}");
        
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _logMessages.Add($"[{timestamp}] ERROR: {message}");
        
        // Keep log at reasonable size
        while (_logMessages.Count > _maxLogMessages)
            _logMessages.RemoveAt(0);
            
        UpdateDebugText();
    }

    private void UpdateDebugText()
    {
        if (_debugText != null)
        {
            _debugText.text = string.Join("\n", _logMessages);
        }
    }

    // Implement common UI actions
    public void ShowConnectUI()
    {
        if (_connectPanel != null)
            _connectPanel.SetActive(true);
    }

    public void HideConnectUI()
    {
        if (_connectPanel != null)
            _connectPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        // Clean up network runner
        if (_runner != null)
        {
            _runner.Shutdown();
        }
    }

    // Register the player prefab
    private void RegisterPlayerPrefab()
    {
        if (_playerPrefab == null)
        {
            LogError("Cannot register player prefab: prefab is null");
            return;
        }

        try
        {
            // If your version of Fusion 2 supports it, you can add this code to manually register the prefab
            // Not all versions of Fusion 2 have this API, so we're keeping it commented out
            /*
            if (_runner.NetworkPrefabTable != null)
            {
                if (!_runner.NetworkPrefabTable.Contains(_playerPrefab))
                {
                    _runner.NetworkPrefabTable.Add(_playerPrefab);
                    LogMessage("Player prefab registered manually");
                }
            }
            */
            
            // For most Fusion 2 versions, placing the prefab in Resources is sufficient
            LogMessage("Player prefab found in Resources and will be auto-registered");
        }
        catch (Exception ex)
        {
            LogError($"Error registering player prefab: {ex.Message}");
        }
    }

    #region INetworkRunnerCallbacks Implementation

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        LogMessage($"Player {player} joined");
        
        try 
        {
            // Only spawn objects for the local player
            if (player == runner.LocalPlayer)
            {
                LogMessage("Spawning local player");
                
                if (_playerPrefab == null)
                {
                    LogError("Player prefab is missing! Make sure to create the PlayerPrefab in Resources folder.");
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
                    
                    // Spawn a camera that follows the player
                    GameObject camera = Instantiate(_cameraPrefab);
                    camera.SetActive(true);
                    FollowCamera followCam = camera.GetComponent<FollowCamera>();
                    if (followCam != null)
                    {
                        followCam.SetTarget(playerObject.transform);
                    }
                }
                else
                {
                    LogError("Failed to spawn player object!");
                }
            }
        }
        catch (Exception ex) 
        {
            LogError($"Error in OnPlayerJoined: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        LogMessage($"Player {player} left");
        
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
        if (Input.GetKey(KeyCode.Space))
            data.buttons.Set(0, true); // Jump
        if (Input.GetKey(KeyCode.Mouse0))
            data.buttons.Set(1, true); // Fire
            
        // Set the input data
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        LogMessage($"Shutdown: {shutdownReason}");
        
        // Clear the players dictionary
        _players.Clear();
        
        // Show the connection UI
        ShowConnectUI();
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        LogMessage("Connected to server");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        LogMessage($"Disconnected from server: {reason}");
        ShowConnectUI();
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        LogMessage($"Connect request received");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        LogError($"Connect failed: {reason}");
        UpdateStatus($"Connect failed: {reason}");
        ShowConnectUI();
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        LogMessage($"Session list updated: {sessionList.Count} sessions");
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        LogMessage($"Reliable data received from player {player}");
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        // Only log significant progress to avoid spam
        if (progress == 1.0f)
        {
            LogMessage($"Reliable data transfer complete for player {player}");
        }
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        LogMessage($"Object {obj.Id} entered AOI for player {player}");
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        LogMessage($"Object {obj.Id} exited AOI for player {player}");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        LogMessage("Scene load completed");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        LogMessage("Scene load started");
    }

    #endregion
}