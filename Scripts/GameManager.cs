using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fusion;
using Fusion.Sockets;
using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour, INetworkRunnerCallbacks
{
    // Singleton instance
    public static GameManager Instance { get; private set; }

    // NetworkRunner reference
    private NetworkRunner _runner;

    // UI Elements
    [Header("UI Elements")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private TMP_InputField createRoomInput;
    [SerializeField] private TMP_InputField joinRoomInput;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text statusText;

    // Game Settings
    [Header("Game Settings")]
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private int maxPlayers = 12;

    // Current session info
    private string _roomCode = "";
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Create UI if not assigned
        if (connectionPanel == null || lobbyPanel == null || roomPanel == null)
        {
            CreateUI();
        }
        
        // Log network status
        Debug.Log("GameManager initialized. Make sure your Photon AppId is set in the PhotonAppSettings asset.");
    }

    #region UI Creation

    private void CreateUI()
    {
        // Create Canvas if doesn't exist - using FindFirstObjectByType to fix the warning
        Canvas mainCanvas = FindFirstObjectByType<Canvas>();
        if (mainCanvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create connection panel
        connectionPanel = CreatePanel("ConnectionPanel", mainCanvas.transform);
        CreateText("StatusText", connectionPanel.transform, "Connecting to server...", new Vector2(0, 0));
        statusText = connectionPanel.GetComponentInChildren<TMP_Text>();

        // Create lobby panel
        lobbyPanel = CreatePanel("LobbyPanel", mainCanvas.transform);
        CreateText("TitleText", lobbyPanel.transform, "Game Lobby", new Vector2(0, 150));
        createRoomInput = CreateInputField("CreateRoomInput", lobbyPanel.transform, "Room Code (optional)", new Vector2(-150, 70));
        CreateButton("CreateButton", lobbyPanel.transform, "Create Room", new Vector2(-150, 20), OnCreateRoomClicked);
        joinRoomInput = CreateInputField("JoinRoomInput", lobbyPanel.transform, "Room Code", new Vector2(150, 70));
        CreateButton("JoinButton", lobbyPanel.transform, "Join Room", new Vector2(150, 20), OnJoinRoomClicked);

        // Create room panel
        roomPanel = CreatePanel("RoomPanel", mainCanvas.transform);
        CreateText("RoomTitleText", roomPanel.transform, "Room Code:", new Vector2(-100, 150));
        roomCodeText = CreateText("RoomCodeText", roomPanel.transform, "", new Vector2(50, 150));
        playerCountText = CreateText("PlayerCountText", roomPanel.transform, "Players: 0/" + maxPlayers, new Vector2(0, 100));
        CreateButton("LeaveButton", roomPanel.transform, "Leave Room", new Vector2(0, -150), OnLeaveRoomClicked);

        // Set initial UI state
        ShowPanel(lobbyPanel);
    }

    private GameObject CreatePanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        
        panel.AddComponent<CanvasRenderer>();
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        
        panel.SetActive(false);
        return panel;
    }

    private TMP_Text CreateText(string name, Transform parent, string text, Vector2 position)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 50);
        rect.anchoredPosition = position;
        
        TMP_Text tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.fontSize = 24;
        
        return tmpText;
    }

    private Button CreateButton(string name, Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);
        
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 50);
        rect.anchoredPosition = position;
        
        buttonObj.AddComponent<CanvasRenderer>();
        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.8f);
        
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        
        // Add text to button
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        TMP_Text tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
        
        return button;
    }

    private TMP_InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 position)
    {
        GameObject inputObj = new GameObject(name);
        inputObj.transform.SetParent(parent, false);
        
        RectTransform rect = inputObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 50);
        rect.anchoredPosition = position;
        
        inputObj.AddComponent<CanvasRenderer>();
        Image image = inputObj.AddComponent<Image>();
        image.color = Color.white;
        
        // Create text area
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputObj.transform, false);
        
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);
        
        // Create text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(textArea.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        TMP_Text tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.color = Color.black;
        tmpText.alignment = TextAlignmentOptions.Left;
        
        // Create placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textArea.transform, false);
        
        RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        
        TMP_Text placeholderText = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholderText.text = placeholder;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f);
        placeholderText.alignment = TextAlignmentOptions.Left;
        
        // Set up input field
        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        inputField.textComponent = tmpText;
        inputField.placeholder = placeholderText;
        inputField.caretWidth = 2;
        inputField.caretColor = Color.black;
        
        return inputField;
    }

    private void ShowPanel(GameObject panel)
    {
        if (connectionPanel != null) connectionPanel.SetActive(panel == connectionPanel);
        if (lobbyPanel != null) lobbyPanel.SetActive(panel == lobbyPanel);
        if (roomPanel != null) roomPanel.SetActive(panel == roomPanel);
    }

    #endregion

    #region Button Handlers

    public void OnCreateRoomClicked()
    {
        string roomName = string.IsNullOrEmpty(createRoomInput.text) 
            ? GenerateRandomRoomCode() 
            : createRoomInput.text;
        
        Debug.Log($"Attempting to create room: {roomName}");
        StartGame(GameMode.Host, roomName);
    }

    public void OnJoinRoomClicked()
    {
        string roomName = joinRoomInput.text;
        
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("Room code cannot be empty");
            return;
        }
        
        Debug.Log($"Attempting to join room: {roomName}");
        StartGame(GameMode.Client, roomName);
    }

    public void OnLeaveRoomClicked()
    {
        LeaveGame();
    }

    #endregion

    #region Networking

    public async void StartGame(GameMode mode, string roomName = null)
    {
        try
        {
            // Check if AppIdFusion is set in PhotonAppSettings
            var appSettings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings;
            if (string.IsNullOrEmpty(appSettings.AppIdFusion))
            {
                Debug.LogError("Photon AppId not set! Please set your Photon AppId in the PhotonAppSettings asset (Tools/Fusion/Realtime Settings)");
                statusText.text = "Error: Photon AppId not set!";
                Invoke("ReturnToLobby", 3f);
                return;
            }

            // Create network runner if it doesn't exist
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
                _runner.ProvideInput = true;
            }

            // Create SceneRef from the current scene's build index
            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

            // Start connection and create/join session
            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                Scene = scene,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
                PlayerCount = maxPlayers
            };

            // Show connecting status
            statusText.text = (mode == GameMode.Host) 
                ? "Creating room..." 
                : "Joining room...";
            ShowPanel(connectionPanel);

            Debug.Log($"Starting {mode} mode with room name: {roomName}");

            // Start or join game session
            var result = await _runner.StartGame(startGameArgs);
            
            // Check the result
            Debug.Log($"Connection result: {result.Ok}, {result.ShutdownReason}");
            
            if (!result.Ok)
            {
                // Handle failed connection
                Debug.LogError($"Failed to connect: {result.ShutdownReason}");
                statusText.text = $"Connection failed: {result.ShutdownReason}";
                Invoke("ReturnToLobby", 3f);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error starting game: {ex.Message}");
            statusText.text = $"Error: {ex.Message}";
            Invoke("ReturnToLobby", 3f);
        }
    }

    private void ReturnToLobby()
    {
        ShowPanel(lobbyPanel);
    }

    public void LeaveGame()
    {
        Debug.Log("Leaving game session");
        if (_runner != null)
        {
            // Shutdown the network runner
            _runner.Shutdown();
        }

        // Return to lobby
        ShowPanel(lobbyPanel);
    }

    private string GenerateRandomRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[6];
        
        for (int i = 0; i < 6; i++)
        {
            code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        }
        
        return new string(code);
    }

    private void UpdatePlayerCount()
    {
        if (_runner != null && _runner.IsRunning)
        {
            int playerCount = _spawnedCharacters.Count;
            playerCountText.text = $"Players: {playerCount}/{maxPlayers}";
        }
    }

    #endregion

    #region INetworkRunnerCallbacks

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} joined");
        
        // Only spawn player objects if we're the host or if we are the joining player
        if (runner.IsServer || player == runner.LocalPlayer)
        {
            // Spawn player object for this player
            Vector3 spawnPosition = new Vector3(UnityEngine.Random.Range(-5f, 5f), 1f, UnityEngine.Random.Range(-5f, 5f));
            NetworkObject networkPlayerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
            
            // Store reference to the player object
            _spawnedCharacters[player] = networkPlayerObject;
            
            // Update UI
            UpdatePlayerCount();
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} left");
        
        // Find and remove the player object
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
        
        // Update UI
        UpdatePlayerCount();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Create new input data
        var data = new NetworkInputData();
        
        // Get player input
        if (Input.GetKey(KeyCode.W))
            data.direction.z = 1;
        if (Input.GetKey(KeyCode.S))
            data.direction.z = -1;
        if (Input.GetKey(KeyCode.A))
            data.direction.x = -1;
        if (Input.GetKey(KeyCode.D))
            data.direction.x = 1;
        
        // Submit input data to the runner
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    
    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log($"Connected to server, SessionInfo: {(runner.SessionInfo != null ? runner.SessionInfo.Name : "null")}");
        
        // If we've successfully connected, show the room panel
        if (runner.SessionInfo != null)
        {
            _roomCode = runner.SessionInfo.Name;
            roomCodeText.text = _roomCode;
            ShowPanel(roomPanel);
        }
        else
        {
            Debug.LogWarning("Connected to server but SessionInfo is null");
            statusText.text = "Connected but no room info available";
            Invoke("ReturnToLobby", 3f);
        }
    }
    
    // Updated method signature to match newer Fusion API
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from server: {reason}");
        statusText.text = $"Disconnected: {reason}";
        Invoke("ReturnToLobby", 3f);
    }
    
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        // Auto-accept connection requests
        Debug.Log($"Connection request from: {request.RemoteAddress}");
        request.Accept();
    }
    
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"Connect failed: {reason}, Address: {remoteAddress}");
        statusText.text = $"Connection failed: {reason}";
        Invoke("ReturnToLobby", 3f);
    }
    
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) 
    {
        Debug.Log($"Session list updated: {sessionList.Count} sessions found");
    }
    
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    
    // Updated method signature to match newer Fusion API
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    // New method required by newer Fusion API
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    
    public void OnSceneLoadDone(NetworkRunner runner) { }
    
    public void OnSceneLoadStart(NetworkRunner runner) { }
    
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Shutdown: {shutdownReason}");
        
        // Clear players dictionary
        _spawnedCharacters.Clear();
        
        // Return to lobby
        ShowPanel(lobbyPanel);
    }

    // New methods required by newer Fusion API for Area of Interest management
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    #endregion
}

// Define the network input data structure
public struct NetworkInputData : INetworkInput
{
    public Vector3 direction;
}