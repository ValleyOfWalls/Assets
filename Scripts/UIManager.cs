using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Fusion; // Added for PlayerRef

public class UIManager : MonoBehaviour
{
    // Core UI elements
    private Canvas _uiCanvas; // This canvas should primarily hold Connect/Lobby UI now

    // Controllers
    private ConnectUIController _connectUIController;
    private LobbyUIController _lobbyUIController;
    private GameStartUIController _gameStartUIController; // Displays "Game Started!" briefly

    // Player information
    private string _localPlayerName = "";

    // UI state
    private bool _isLobbyUIActive = true; // Track lobby UI state specifically

    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing UIManager...");
        // Create main canvas for Connect/Lobby
        CreateMainCanvas(); // Ensures _uiCanvas is assigned

        // Create controllers *after* canvas is guaranteed to exist
        if (_uiCanvas != null)
        {
            _connectUIController = new ConnectUIController(_uiCanvas.transform);
            _lobbyUIController = new LobbyUIController(_uiCanvas.transform);
            _gameStartUIController = new GameStartUIController(_uiCanvas.transform);

            // Set up UI event listeners
            SetupUIListeners();
            // Subscribe to LobbyManager events
            SubscribeToLobbyEvents();

             // Show Connect UI initially, hide others
            _connectUIController.ShowPanel();
            _lobbyUIController.HidePanel();
            _gameStartUIController.HidePanel();

            GameManager.Instance.LogManager.LogMessage("UIManager initialization complete");
        } else {
             GameManager.Instance.LogManager.LogError("UIManager Initialization failed: Could not create or find LobbyUI Canvas.");
        }

    }

    private void CreateMainCanvas()
    {
        // If the canvas assigned to this UIManager already exists (from a previous scene load, etc.)
        // and is valid, reuse it.
        if (_uiCanvas != null && _uiCanvas.gameObject != null)
        {
            GameManager.Instance.LogManager.LogMessage("UIManager reusing existing _uiCanvas reference.");
            // Ensure it's active, as it might have been deactivated
             _uiCanvas.gameObject.SetActive(true);
            return; // Already have a canvas
        }

        // --- Removed Tag Finding Logic ---
        // GameObject existingCanvasObj = GameObject.FindGameObjectWithTag("LobbyCanvas"); // REMOVED

        // If no existing reference, create a new canvas
        GameManager.Instance.LogManager.LogMessage("UIManager creating new LobbyUI Canvas.");
        GameObject canvasObj = new GameObject("LobbyUI Canvas"); // Specific name
        // canvasObj.tag = "LobbyCanvas"; // REMOVED Tag assignment

        _uiCanvas = canvasObj.AddComponent<Canvas>(); // Assign to the instance variable
        _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _uiCanvas.sortingOrder = 0; // Keep lobby UI behind potential Game UI

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); // Example resolution
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Since UIManager lives on the persistent GameManager, this canvas should persist too.
        DontDestroyOnLoad(canvasObj);
    }


    private void SetupUIListeners()
    {
        // Ensure controllers exist before subscribing
        if(_connectUIController != null) {
            _connectUIController.OnCreateRoomClicked -= HandleCreateRoom; // Prevent double subscription
            _connectUIController.OnCreateRoomClicked += HandleCreateRoom;
            _connectUIController.OnJoinRoomClicked -= HandleJoinRoom;
            _connectUIController.OnJoinRoomClicked += HandleJoinRoom;
        }

        if(_lobbyUIController != null) {
             _lobbyUIController.OnReadyButtonClicked -= HandleReadyButtonClicked; // Prevent double subscription
             _lobbyUIController.OnReadyButtonClicked += HandleReadyButtonClicked;
        }
        GameManager.Instance.LogManager.LogMessage("UIManager listeners set up");
    }

    private void SubscribeToLobbyEvents()
    {
        // Ensure LobbyManager exists before subscribing
        if (GameManager.Instance.LobbyManager != null)
        {
            // Unsubscribe first to prevent duplicates if Initialize is called multiple times
             GameManager.Instance.LobbyManager.OnAllPlayersReady -= HandleAllPlayersReady;
             GameManager.Instance.LobbyManager.OnCountdownComplete -= HandleCountdownComplete;
             GameManager.Instance.LobbyManager.OnPlayerReadyStatusChanged -= HandlePlayerReadyStatusChanged;
             GameManager.Instance.LobbyManager.OnGameStarted -= HandleGameStarted;

            // Subscribe
            GameManager.Instance.LobbyManager.OnAllPlayersReady += HandleAllPlayersReady;
            GameManager.Instance.LobbyManager.OnCountdownComplete += HandleCountdownComplete;
            GameManager.Instance.LobbyManager.OnPlayerReadyStatusChanged += HandlePlayerReadyStatusChanged;
            GameManager.Instance.LobbyManager.OnGameStarted += HandleGameStarted;
        } else {
            GameManager.Instance.LogManager.LogError("UIManager: LobbyManager is null, cannot subscribe to events!");
        }
    }

     // Event handler method for player ready status changes
    private void HandlePlayerReadyStatusChanged(string playerName, bool isReady)
    {
        UpdatePlayersList(); // Update the list when status changes
        GameManager.Instance.LogManager.LogMessage($"UIManager received ready status change: {playerName} = {isReady}");
        // Optionally update the local player's button state if it's them
        Player localPlayer = FindLocalPlayer();
        if (localPlayer != null && localPlayer.GetPlayerName() == playerName) {
            _lobbyUIController?.UpdateReadyButtonState(isReady);
        }
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

            // Update player ready status (this should trigger RPC via Player script)
            localPlayer.SetReadyStatus(newStatus);

            GameManager.Instance.LogManager.LogMessage($"Local player ready status toggled via UI: {newStatus}");

            // Update UI immediately based on the toggle action (RPC might have slight delay)
            _lobbyUIController.UpdateReadyButtonState(newStatus);
            // The list itself will update via the HandlePlayerReadyStatusChanged event handler
        }
        else
        {
            GameManager.Instance.LogManager.LogMessage("Could not find local player to toggle ready status");
        }
    }


    private Player FindLocalPlayer()
    {
        var networkRunner = GameManager.Instance?.NetworkManager?.GetRunner();
        // Corrected: Check if LocalPlayer is default (invalid) instead of using .IsValid
        if (networkRunner != null && networkRunner.LocalPlayer != default)
        {
            var localPlayerRef = networkRunner.LocalPlayer;
            var playerObject = GameManager.Instance?.PlayerManager?.GetPlayerObject(localPlayerRef);
            if (playerObject != null)
            {
                return playerObject.GetComponent<Player>();
            }
        }
        return null;
    }

    // Show/hide methods for Connect/Lobby flow
    public void ShowConnectUI()
    {
        // Ensure canvas is active first
        if (_uiCanvas != null) _uiCanvas.gameObject.SetActive(true);
        else { CreateMainCanvas(); } // Create if missing

        _connectUIController?.ShowPanel();
        _lobbyUIController?.HidePanel();
        _gameStartUIController?.HidePanel();
        _isLobbyUIActive = true; // Considered active during connection phase
         GameManager.Instance.LogManager.LogMessage("Showing Connect UI.");
    }

    // Called by NetworkManager after successful connection
    public void ShowLobbyUI()
    {
         if (_uiCanvas != null) _uiCanvas.gameObject.SetActive(true);
         else { CreateMainCanvas(); } // Create if missing

        _connectUIController?.HidePanel();
        _lobbyUIController?.ShowPanel();
        _gameStartUIController?.HidePanel();

        // Update the room name in the lobby UI
        UpdateRoomInfo();
        // Update player list immediately
        UpdatePlayersList();
        // Update local player's ready button state
        Player localPlayer = FindLocalPlayer();
        if(localPlayer != null) {
            _lobbyUIController?.UpdateReadyButtonState(localPlayer.GetReadyStatus());
        } else {
             // If local player not found yet, maybe default button to 'Ready' state
             _lobbyUIController?.UpdateReadyButtonState(false);
        }

        _isLobbyUIActive = true;
         GameManager.Instance.LogManager.LogMessage("Showing Lobby UI.");
    }

     // Called by GameUI or when game ends to hide non-gameplay UI elements
     public void HideLobbyAndConnectUI()
     {
          _connectUIController?.HidePanel();
          _lobbyUIController?.HidePanel();
          _gameStartUIController?.HidePanel();

          // Optionally deactivate the canvas if GameUI uses a separate one
          // If the GameUI uses the *same* canvas, we should NOT deactivate it here.
          // Assuming GameUI handles its own visibility/canvas for now.
          // if (_uiCanvas != null) _uiCanvas.gameObject.SetActive(false);

          _isLobbyUIActive = false;
          GameManager.Instance.LogManager.LogMessage("Hiding Lobby & Connect UI.");
     }


    // This should ONLY affect the lobby UI, not the GameUI
    public void HideAllLobbyUIElements()
    {
        _connectUIController?.HidePanel();
        _lobbyUIController?.HidePanel();
        _gameStartUIController?.HidePanel(); // Hide this too

         // If the LobbyUI canvas should be hidden completely (e.g., GameUI uses separate canvas)
         if (_uiCanvas != null) {
             _uiCanvas.gameObject.SetActive(false);
             GameManager.Instance.LogManager.LogMessage("Lobby UI Canvas deactivated.");
         }

        _isLobbyUIActive = false;
    }

    // Specific method to update status text on ConnectUI
    public void UpdateStatus(string message)
    {
        _connectUIController?.UpdateStatus(message);
    }

    private void UpdateRoomInfo()
    {
        var runner = GameManager.Instance?.NetworkManager?.GetRunner();
        if (runner != null && runner.SessionInfo != null)
        {
            string roomName = runner.SessionInfo.Name;
            _lobbyUIController?.UpdateRoomName(roomName);
        }
    }

    public void UpdatePlayersList()
    {
         if (GameManager.Instance?.LobbyManager == null) return; // Safety check

        List<string> playerNames = GameManager.Instance.LobbyManager.GetAllPlayerNames();
        Dictionary<string, bool> readyStatuses = new Dictionary<string, bool>();

        foreach (var playerName in playerNames)
        {
            // Fetch status directly from LobbyManager's cache
            bool isReady = GameManager.Instance.LobbyManager.GetPlayerReadyStatus(playerName);
            readyStatuses[playerName] = isReady;
        }

        _lobbyUIController?.UpdatePlayersList(playerNames, readyStatuses);
    }

    // Event handlers
    private void HandleAllPlayersReady()
    {
        _lobbyUIController?.SetCountdownText("All players ready! Starting soon...");
        GameManager.Instance.LogManager.LogMessage("UIManager: All players ready event received.");
    }

    private void HandleCountdownComplete()
    {
        _lobbyUIController?.SetCountdownText("Game starting!");
        GameManager.Instance.LogManager.LogMessage("UIManager: Countdown complete event received.");
    }

     // This is triggered by LobbyManager when the *game logic* should start
    private void HandleGameStarted()
    {
        GameManager.Instance.LogManager.LogMessage("UIManager: GameStarted event received. Hiding lobby UI.");
        // Show the brief "Game Started!" message
        _gameStartUIController?.ShowPanel();

        // Hide the main Lobby UI elements after a short delay
        Invoke(nameof(HideLobbyUIElementsDelayed), 1.0f); // Hide lobby panels
    }

     // Renamed methods for clarity
     private void HideLobbyUIElementsDelayed()
     {
         HideAllLobbyUIElements(); // Hide connect/lobby/start panels, possibly the canvas
         // GameStart panel should also be hidden now or shortly after
          _gameStartUIController?.HidePanel();
     }


    // Update countdown text if lobby UI is active
    private void Update()
    {
        if (!_isLobbyUIActive || GameManager.Instance?.LobbyManager == null) return;

        if (GameManager.Instance.LobbyManager.IsCountdownActive())
        {
            float countdown = GameManager.Instance.LobbyManager.GetCurrentCountdown();
            _lobbyUIController?.UpdateCountdown(countdown);
        }
        // Maybe clear countdown text if not active?
        // else if (_lobbyUIController != null && _lobbyUIController.gameObject.activeInHierarchy) {
        //     // Check if countdown text isn't already empty to avoid setting it every frame
        //      var countdownTMP = _lobbyUIController.GetComponentInChildren<TMP_Text>(true); // Find inactive too? Check specific name maybe
        //      if (countdownTMP != null && countdownTMP.name == "Countdown Text" && !string.IsNullOrEmpty(countdownTMP.text)) {
        //         _lobbyUIController.SetCountdownText("");
        //      }
        // }
    }

    public string GetLocalPlayerName()
    {
        return _localPlayerName;
    }

    public bool IsLobbyUIActive()
    {
        return _isLobbyUIActive;
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
         // Cancel invokes if the object is destroyed
         CancelInvoke();
    }
}