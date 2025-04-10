using TMPro;
using UnityEngine;
using UnityEngine.UI; // Added missing namespace
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    // Core UI elements
    private Canvas _uiCanvas;
    
    // Controllers
    private ConnectUIController _connectUIController;
    private LobbyUIController _lobbyUIController;
    private GameStartUIController _gameStartUIController;
    
    // Player information
    private string _localPlayerName = "";
    
    // UI state
    private bool _isUIActive = true;
    
    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing UIManager...");
        
        // Create main canvas
        CreateMainCanvas();
        
        // Create controllers
        _connectUIController = new ConnectUIController(_uiCanvas.transform);
        _lobbyUIController = new LobbyUIController(_uiCanvas.transform);
        _gameStartUIController = new GameStartUIController(_uiCanvas.transform);
        
        // Set up UI event listeners
        SetupUIListeners();
        
        // Subscribe to LobbyManager events
        SubscribeToLobbyEvents();
        
        // Hide lobby and game started UI initially
        _lobbyUIController.HidePanel();
        _gameStartUIController.HidePanel();
        
        GameManager.Instance.LogManager.LogMessage("UIManager initialization complete");
    }
    
    private void CreateMainCanvas()
    {
        GameObject canvasObj = new GameObject("UI Canvas");
        _uiCanvas = canvasObj.AddComponent<Canvas>();
        _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);
    }
    
    private void SetupUIListeners()
    {
        // Connect UI event handlers
        _connectUIController.OnCreateRoomClicked += HandleCreateRoom;
        _connectUIController.OnJoinRoomClicked += HandleJoinRoom;
        
        // Lobby UI event handlers
        _lobbyUIController.OnReadyButtonClicked += HandleReadyButtonClicked;
        
        GameManager.Instance.LogManager.LogMessage("UI listeners set up");
    }
    
    private void SubscribeToLobbyEvents()
    {
        GameManager.Instance.LobbyManager.OnAllPlayersReady += HandleAllPlayersReady;
        GameManager.Instance.LobbyManager.OnCountdownComplete += HandleCountdownComplete;
        GameManager.Instance.LobbyManager.OnPlayerReadyStatusChanged += HandlePlayerReadyStatusChanged;
        GameManager.Instance.LobbyManager.OnGameStarted += HandleGameStarted;
    }
    
    private void HandleCreateRoom(string roomName, string playerName)
    {
        SaveLocalPlayerName(playerName);
        GameManager.Instance.NetworkManager.CreateRoom(roomName);
    }
    
    private void HandleJoinRoom(string roomName, string playerName)
    {
        SaveLocalPlayerName(playerName);
        GameManager.Instance.NetworkManager.JoinRoom(roomName);
    }
    
    private void SaveLocalPlayerName(string playerName)
    {
        _localPlayerName = playerName;
        if (string.IsNullOrEmpty(_localPlayerName))
        {
            _localPlayerName = "Player" + UnityEngine.Random.Range(1000, 10000);
        }
        
        GameManager.Instance.LogManager.LogMessage($"Saved local player name: {_localPlayerName}");
    }
    
    private void HandleReadyButtonClicked()
    {
        // Find local player object
        Player localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            // Toggle ready status
            bool currentStatus = localPlayer.GetReadyStatus();
            bool newStatus = !currentStatus;
            
            // Update player ready status
            localPlayer.SetReadyStatus(newStatus);
            
            // Direct call to LobbyManager to ensure ready status is set
            GameManager.Instance.LobbyManager.SetPlayerReadyStatus(localPlayer.GetPlayerName(), newStatus);
            
            GameManager.Instance.LogManager.LogMessage($"Local player ready status changed to: {newStatus}");
            
            // Update UI
            _lobbyUIController.UpdateReadyButtonState(newStatus);
            
            // Update player list
            UpdatePlayersList();
            
            // Force ready status check - only for single player testing
            if (GameManager.Instance.PlayerManager.GetPlayerCount() == 1)
            {
                GameManager.Instance.LogManager.LogMessage("Single player detected - directly triggering player ready check");
                GameManager.Instance.LobbyManager.DebugForceReadyCheck();
            }
        }
        else
        {
            GameManager.Instance.LogManager.LogMessage("Could not find local player");
        }
    }
    
    private Player FindLocalPlayer()
    {
        var networkRunner = GameManager.Instance.NetworkManager.GetRunner();
        if (networkRunner != null)
        {
            var localPlayerRef = networkRunner.LocalPlayer;
            var playerObject = GameManager.Instance.PlayerManager.GetPlayerObject(localPlayerRef);
            if (playerObject != null)
            {
                return playerObject.GetComponent<Player>();
            }
        }
        return null;
    }
    
    // Show/hide methods
    public void ShowConnectUI()
    {
        _connectUIController.ShowPanel();
        _lobbyUIController.HidePanel();
        _gameStartUIController.HidePanel();
        _isUIActive = true;
    }

    public void HideConnectUI()
    {
        _connectUIController.HidePanel();
        _lobbyUIController.ShowPanel();
        _gameStartUIController.HidePanel();
        
        // Update the room name in the lobby UI
        UpdateRoomInfo();
        
        _isUIActive = true;
    }
    
    public void HideAllUI()
    {
        _connectUIController.HidePanel();
        _lobbyUIController.HidePanel();
        _gameStartUIController.HidePanel();
        
        // Deactivate the entire canvas
        if (_uiCanvas != null)
        {
            _uiCanvas.gameObject.SetActive(false);
            GameManager.Instance.LogManager.LogMessage("Entire lobby UI canvas forcibly deactivated");
        }
        
        _isUIActive = false;
    }
    
    public void DestroyAllUI()
    {
        // Complete destruction of all UI elements
        if (_uiCanvas != null)
        {
            Destroy(_uiCanvas.gameObject);
            _uiCanvas = null;
            GameManager.Instance.LogManager.LogMessage("Destroyed entire UI canvas");
        }
        
        _isUIActive = false;
    }
    
    public void UpdateStatus(string message)
    {
        _connectUIController.UpdateStatus(message);
    }
    
    private void UpdateRoomInfo()
    {
        var runner = GameManager.Instance.NetworkManager.GetRunner();
        if (runner != null && runner.SessionInfo != null)
        {
            string roomName = runner.SessionInfo.Name;
            _lobbyUIController.UpdateRoomName(roomName);
        }
    }
    
    public void UpdatePlayersList()
    {
        List<string> playerNames = GameManager.Instance.LobbyManager.GetAllPlayerNames();
        Dictionary<string, bool> readyStatuses = new Dictionary<string, bool>();
        
        foreach (var playerName in playerNames)
        {
            bool isReady = GameManager.Instance.LobbyManager.GetPlayerReadyStatus(playerName);
            readyStatuses[playerName] = isReady;
        }
        
        _lobbyUIController.UpdatePlayersList(playerNames, readyStatuses);
    }
    
    // Event handlers
    private void HandleAllPlayersReady()
    {
        _lobbyUIController.SetCountdownText("All players ready! Game starting soon...");
        GameManager.Instance.LogManager.LogMessage("All players ready event received in UI");
    }
    
    private void HandleCountdownComplete()
    {
        _lobbyUIController.SetCountdownText("Game starting!");
        GameManager.Instance.LogManager.LogMessage("Countdown complete event received in UI");
    }
    
    private void HandleGameStarted()
    {
        GameManager.Instance.LogManager.LogMessage("Game started event received in UI");
        
        // Show the game started panel
        _gameStartUIController.ShowPanel();
        
        // Hide all UI after 2 seconds
        Invoke("HideAllUIDelayed", 2f);
        
        // Destroy all UI after 3 seconds
        Invoke("DestroyAllUI", 3f);
    }
    
    private void HideAllUIDelayed()
    {
        HideAllUI();
    }
    
    private void HandlePlayerReadyStatusChanged(string playerName, bool isReady)
    {
        UpdatePlayersList();
        GameManager.Instance.LogManager.LogMessage($"Player ready status changed: {playerName} - {isReady}");
    }
    
    private void Update()
    {
        if (!_isUIActive) return;
        
        if (GameManager.Instance.LobbyManager != null && GameManager.Instance.LobbyManager.IsCountdownActive())
        {
            float countdown = GameManager.Instance.LobbyManager.GetCurrentCountdown();
            _lobbyUIController.UpdateCountdown(countdown);
        }
    }
    
    public string GetLocalPlayerName()
    {
        return _localPlayerName;
    }
    
    public bool IsUIActive()
    {
        return _isUIActive;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (GameManager.Instance != null && GameManager.Instance.LobbyManager != null)
        {
            GameManager.Instance.LobbyManager.OnAllPlayersReady -= HandleAllPlayersReady;
            GameManager.Instance.LobbyManager.OnCountdownComplete -= HandleCountdownComplete;
            GameManager.Instance.LobbyManager.OnPlayerReadyStatusChanged -= HandlePlayerReadyStatusChanged;
            GameManager.Instance.LobbyManager.OnGameStarted -= HandleGameStarted;
        }
        
        // Unsubscribe from UI controller events
        if (_connectUIController != null)
        {
            _connectUIController.OnCreateRoomClicked -= HandleCreateRoom;
            _connectUIController.OnJoinRoomClicked -= HandleJoinRoom;
        }
        
        if (_lobbyUIController != null)
        {
            _lobbyUIController.OnReadyButtonClicked -= HandleReadyButtonClicked;
        }
    }
}