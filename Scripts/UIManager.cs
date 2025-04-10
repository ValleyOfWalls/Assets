using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    // UI references
    private Canvas _uiCanvas;
    private TMP_InputField _roomNameInput;
    private TMP_InputField _playerNameInput;
    private Button _createRoomButton;
    private Button _joinRoomButton;
    private TMP_Text _statusText;
    private GameObject _connectPanel;
    
    // Lobby UI references
    private GameObject _lobbyPanel;
    private Button _readyButton;
    private TMP_Text _countdownText;
    private Transform _playerListContent;
    private GameObject _playerListItemPrefab;
    
    // Game started UI
    private GameObject _gameStartedPanel;
    private TMP_Text _gameStartedText;
    
    // Player name tracking
    private string _localPlayerName = "";
    
    // Dictionary to keep track of player list items in UI
    private Dictionary<string, GameObject> _playerListItems = new Dictionary<string, GameObject>();
    
    // Flag to track if UI is active
    private bool _isUIActive = true;
    
    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing UIManager...");
        
        // Create UI
        CreateUI();
        SetupUIListeners();
        
        // Subscribe to LobbyManager events
        GameManager.Instance.LobbyManager.OnAllPlayersReady += HandleAllPlayersReady;
        GameManager.Instance.LobbyManager.OnCountdownComplete += HandleCountdownComplete;
        GameManager.Instance.LobbyManager.OnPlayerReadyStatusChanged += HandlePlayerReadyStatusChanged;
        GameManager.Instance.LobbyManager.OnGameStarted += HandleGameStarted;
        
        GameManager.Instance.LogManager.LogMessage("UIManager initialization complete");
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
        CreateConnectionPanel(canvasObj);
        
        // Create a panel for lobby UI
        CreateLobbyPanel(canvasObj);
        
        // Create a panel for game started UI
        CreateGameStartedPanel(canvasObj);
        
        // Hide lobby panel and game started panel initially
        _lobbyPanel.SetActive(false);
        _gameStartedPanel.SetActive(false);
        
        GameManager.Instance.LogManager.LogMessage("UI created successfully");
    }
    
    private void CreateConnectionPanel(GameObject parentCanvas)
    {
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

        // Add player name input
        GameObject nameInputObj = new GameObject("Player Name Input");
        nameInputObj.transform.SetParent(panelObj.transform, false);
        _playerNameInput = nameInputObj.AddComponent<TMP_InputField>();
        Image nameInputImage = nameInputObj.AddComponent<Image>();
        nameInputImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform nameInputRect = nameInputObj.GetComponent<RectTransform>();
        nameInputRect.anchorMin = new Vector2(0.1f, 0.7f);
        nameInputRect.anchorMax = new Vector2(0.9f, 0.85f);
        nameInputRect.offsetMin = Vector2.zero;
        nameInputRect.offsetMax = Vector2.zero;
        
        // Create text area for player name input field
        GameObject nameTextArea = new GameObject("Text Area");
        nameTextArea.transform.SetParent(nameInputObj.transform, false);
        TMP_Text nameInputText = nameTextArea.AddComponent<TextMeshProUGUI>();
        nameInputText.text = "";
        nameInputText.color = Color.white;
        nameInputText.fontSize = 18;
        nameInputText.alignment = TextAlignmentOptions.Left;
        RectTransform nameTextRect = nameTextArea.GetComponent<RectTransform>();
        nameTextRect.anchorMin = new Vector2(0.05f, 0.1f);
        nameTextRect.anchorMax = new Vector2(0.95f, 0.9f);
        nameTextRect.offsetMin = Vector2.zero;
        nameTextRect.offsetMax = Vector2.zero;
        
        // Create placeholder for player name input field
        GameObject namePlaceholder = new GameObject("Placeholder");
        namePlaceholder.transform.SetParent(nameInputObj.transform, false);
        TMP_Text namePlaceholderText = namePlaceholder.AddComponent<TextMeshProUGUI>();
        namePlaceholderText.text = "Enter Your Name";
        namePlaceholderText.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        namePlaceholderText.fontSize = 18;
        namePlaceholderText.alignment = TextAlignmentOptions.Left;
        RectTransform namePlaceholderRect = namePlaceholder.GetComponent<RectTransform>();
        namePlaceholderRect.anchorMin = new Vector2(0.05f, 0.1f);
        namePlaceholderRect.anchorMax = new Vector2(0.95f, 0.9f);
        namePlaceholderRect.offsetMin = Vector2.zero;
        namePlaceholderRect.offsetMax = Vector2.zero;
        
        // Connect the player name input field components
        _playerNameInput.textComponent = nameInputText;
        _playerNameInput.placeholder = namePlaceholderText;
        _playerNameInput.text = "Player" + UnityEngine.Random.Range(1000, 10000);

        // Add room name input
        GameObject inputObj = new GameObject("Room Name Input");
        inputObj.transform.SetParent(panelObj.transform, false);
        _roomNameInput = inputObj.AddComponent<TMP_InputField>();
        Image inputImage = inputObj.AddComponent<Image>();
        inputImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.1f, 0.55f);
        inputRect.anchorMax = new Vector2(0.9f, 0.7f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;
        
        // Create text area for room input field
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
        
        // Create placeholder for room input field
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
        
        // Connect the room input field components
        _roomNameInput.textComponent = inputText;
        _roomNameInput.placeholder = placeholderText;
        _roomNameInput.text = "asd"; // Default to "asd"

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
        _statusText.text = "Enter your name and a room name to create or join";
        _statusText.fontSize = 16;
        _statusText.alignment = TextAlignmentOptions.Center;
        _statusText.color = Color.white;
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.1f, 0.2f);
        statusRect.anchorMax = new Vector2(0.9f, 0.35f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;
    }
    
    private void CreateLobbyPanel(GameObject parentCanvas)
    {
        // Create lobby panel
        GameObject lobbyPanelObj = new GameObject("Lobby Panel");
        lobbyPanelObj.transform.SetParent(_uiCanvas.transform, false);
        _lobbyPanel = lobbyPanelObj;
        
        Image lobbyPanelImage = lobbyPanelObj.AddComponent<Image>();
        lobbyPanelImage.color = new Color(0, 0, 0, 0.8f);
        RectTransform lobbyPanelRect = lobbyPanelObj.GetComponent<RectTransform>();
        lobbyPanelRect.anchorMin = new Vector2(0, 0);
        lobbyPanelRect.anchorMax = new Vector2(0.3f, 1);
        lobbyPanelRect.offsetMin = Vector2.zero;
        lobbyPanelRect.offsetMax = Vector2.zero;
        
        // Add title text
        GameObject lobbyTitleObj = new GameObject("Lobby Title");
        lobbyTitleObj.transform.SetParent(lobbyPanelObj.transform, false);
        TMP_Text lobbyTitleText = lobbyTitleObj.AddComponent<TextMeshProUGUI>();
        lobbyTitleText.text = "Game Lobby";
        lobbyTitleText.fontSize = 24;
        lobbyTitleText.alignment = TextAlignmentOptions.Center;
        lobbyTitleText.color = Color.white;
        RectTransform lobbyTitleRect = lobbyTitleObj.GetComponent<RectTransform>();
        lobbyTitleRect.anchorMin = new Vector2(0.1f, 0.9f);
        lobbyTitleRect.anchorMax = new Vector2(0.9f, 1f);
        lobbyTitleRect.offsetMin = Vector2.zero;
        lobbyTitleRect.offsetMax = Vector2.zero;
        
        // Add room name display
        GameObject roomNameObj = new GameObject("Room Name Text");
        roomNameObj.transform.SetParent(lobbyPanelObj.transform, false);
        TMP_Text roomNameText = roomNameObj.AddComponent<TextMeshProUGUI>();
        roomNameText.text = "Room: asd";
        roomNameText.fontSize = 18;
        roomNameText.alignment = TextAlignmentOptions.Center;
        roomNameText.color = Color.white;
        RectTransform roomNameRect = roomNameObj.GetComponent<RectTransform>();
        roomNameRect.anchorMin = new Vector2(0.1f, 0.85f);
        roomNameRect.anchorMax = new Vector2(0.9f, 0.9f);
        roomNameRect.offsetMin = Vector2.zero;
        roomNameRect.offsetMax = Vector2.zero;
        
        // Add player list title
        GameObject playerListTitleObj = new GameObject("Player List Title");
        playerListTitleObj.transform.SetParent(lobbyPanelObj.transform, false);
        TMP_Text playerListTitleText = playerListTitleObj.AddComponent<TextMeshProUGUI>();
        playerListTitleText.text = "Players";
        playerListTitleText.fontSize = 18;
        playerListTitleText.alignment = TextAlignmentOptions.Center;
        playerListTitleText.color = Color.white;
        RectTransform playerListTitleRect = playerListTitleObj.GetComponent<RectTransform>();
        playerListTitleRect.anchorMin = new Vector2(0.1f, 0.8f);
        playerListTitleRect.anchorMax = new Vector2(0.9f, 0.85f);
        playerListTitleRect.offsetMin = Vector2.zero;
        playerListTitleRect.offsetMax = Vector2.zero;
        
        // Add simple player list
        GameObject playerListObj = new GameObject("Player List");
        playerListObj.transform.SetParent(lobbyPanelObj.transform, false);
        
        RectTransform playerListRect = playerListObj.AddComponent<RectTransform>();
        playerListRect.anchorMin = new Vector2(0.05f, 0.3f);
        playerListRect.anchorMax = new Vector2(0.95f, 0.8f);
        playerListRect.offsetMin = Vector2.zero;
        playerListRect.offsetMax = Vector2.zero;
        
        // Create content parent - this will hold our player items
        GameObject playerListContentObj = new GameObject("Player List Content");
        playerListContentObj.transform.SetParent(playerListObj.transform, false);
        
        // Add vertical layout group
        VerticalLayoutGroup verticalLayout = playerListContentObj.AddComponent<VerticalLayoutGroup>();
        verticalLayout.spacing = 5f;
        verticalLayout.padding = new RectOffset(5, 5, 5, 5);
        
        // Get reference to player list content
        _playerListContent = playerListContentObj.transform;
        
        RectTransform contentRect = playerListContentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        
        // Create player list item prefab
        _playerListItemPrefab = CreatePlayerListItemPrefab();
        
        // Add ready button
        GameObject readyButtonObj = new GameObject("Ready Button");
        readyButtonObj.transform.SetParent(lobbyPanelObj.transform, false);
        _readyButton = readyButtonObj.AddComponent<Button>();
        Image readyButtonImage = readyButtonObj.AddComponent<Image>();
        readyButtonImage.color = new Color(0.2f, 0.7f, 0.2f, 1f);
        RectTransform readyButtonRect = readyButtonObj.GetComponent<RectTransform>();
        readyButtonRect.anchorMin = new Vector2(0.1f, 0.2f);
        readyButtonRect.anchorMax = new Vector2(0.9f, 0.25f);
        readyButtonRect.offsetMin = Vector2.zero;
        readyButtonRect.offsetMax = Vector2.zero;
        
        GameObject readyButtonText = new GameObject("Text");
        readyButtonText.transform.SetParent(readyButtonObj.transform, false);
        TMP_Text readyText = readyButtonText.AddComponent<TextMeshProUGUI>();
        readyText.text = "Ready";
        readyText.fontSize = 18;
        readyText.alignment = TextAlignmentOptions.Center;
        readyText.color = Color.white;
        RectTransform readyTextRect = readyButtonText.GetComponent<RectTransform>();
        readyTextRect.anchorMin = Vector2.zero;
        readyTextRect.anchorMax = Vector2.one;
        readyTextRect.offsetMin = Vector2.zero;
        readyTextRect.offsetMax = Vector2.zero;
        
        // Add countdown text
        GameObject countdownObj = new GameObject("Countdown Text");
        countdownObj.transform.SetParent(lobbyPanelObj.transform, false);
        _countdownText = countdownObj.AddComponent<TextMeshProUGUI>();
        _countdownText.text = "";
        _countdownText.fontSize = 24;
        _countdownText.alignment = TextAlignmentOptions.Center;
        _countdownText.color = Color.yellow;
        RectTransform countdownRect = countdownObj.GetComponent<RectTransform>();
        countdownRect.anchorMin = new Vector2(0.1f, 0.1f);
        countdownRect.anchorMax = new Vector2(0.9f, 0.2f);
        countdownRect.offsetMin = Vector2.zero;
        countdownRect.offsetMax = Vector2.zero;
    }
    
    private GameObject CreatePlayerListItemPrefab()
    {
        GameObject itemObj = new GameObject("Player List Item");
        itemObj.SetActive(false); // This is a prefab
        
        // Add background image
        Image itemImage = itemObj.AddComponent<Image>();
        itemImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Set layout properties
        RectTransform itemRect = itemObj.GetComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0, 30);
        
        // Add horizontal layout group
        HorizontalLayoutGroup itemLayout = itemObj.AddComponent<HorizontalLayoutGroup>();
        itemLayout.padding = new RectOffset(5, 5, 5, 5);
        itemLayout.spacing = 5f;
        itemLayout.childAlignment = TextAnchor.MiddleLeft;
        
        // Add player name text
        GameObject nameTextObj = new GameObject("Player Name");
        nameTextObj.transform.SetParent(itemObj.transform, false);
        TMP_Text nameText = nameTextObj.AddComponent<TextMeshProUGUI>();
        nameText.text = "Player Name";
        nameText.fontSize = 14;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Left;
        
        // Add layout element to name
        LayoutElement nameLayout = nameTextObj.AddComponent<LayoutElement>();
        nameLayout.flexibleWidth = 1;
        
        // Add ready status text
        GameObject statusTextObj = new GameObject("Ready Status");
        statusTextObj.transform.SetParent(itemObj.transform, false);
        TMP_Text statusText = statusTextObj.AddComponent<TextMeshProUGUI>();
        statusText.text = "Not Ready";
        statusText.fontSize = 14;
        statusText.color = Color.red;
        statusText.alignment = TextAlignmentOptions.Right;
        
        // Add layout element to status
        LayoutElement statusLayout = statusTextObj.AddComponent<LayoutElement>();
        statusLayout.preferredWidth = 80;
        
        return itemObj;
    }
    
    private void CreateGameStartedPanel(GameObject parentCanvas)
    {
        // Create game started panel that overlays the center of the screen
        GameObject gameStartedPanelObj = new GameObject("Game Started Panel");
        gameStartedPanelObj.transform.SetParent(_uiCanvas.transform, false);
        _gameStartedPanel = gameStartedPanelObj;
        
        Image gameStartedPanelImage = gameStartedPanelObj.AddComponent<Image>();
        gameStartedPanelImage.color = new Color(0, 0, 0, 0.9f);
        RectTransform gameStartedPanelRect = gameStartedPanelObj.GetComponent<RectTransform>();
        gameStartedPanelRect.anchorMin = new Vector2(0.35f, 0.4f);
        gameStartedPanelRect.anchorMax = new Vector2(0.65f, 0.6f);
        gameStartedPanelRect.offsetMin = Vector2.zero;
        gameStartedPanelRect.offsetMax = Vector2.zero;
        
        // Add game started text
        GameObject gameStartedTextObj = new GameObject("Game Started Text");
        gameStartedTextObj.transform.SetParent(gameStartedPanelObj.transform, false);
        _gameStartedText = gameStartedTextObj.AddComponent<TextMeshProUGUI>();
        _gameStartedText.text = "GAME STARTED!";
        _gameStartedText.fontSize = 32;
        _gameStartedText.alignment = TextAlignmentOptions.Center;
        _gameStartedText.color = Color.green;
        RectTransform gameStartedTextRect = gameStartedTextObj.GetComponent<RectTransform>();
        gameStartedTextRect.anchorMin = Vector2.zero;
        gameStartedTextRect.anchorMax = Vector2.one;
        gameStartedTextRect.offsetMin = Vector2.zero;
        gameStartedTextRect.offsetMax = Vector2.zero;
    }

    private void SetupUIListeners()
    {
        if (_createRoomButton != null)
        {
            _createRoomButton.onClick.RemoveAllListeners();
            _createRoomButton.onClick.AddListener(() => {
                SaveLocalPlayerName();
                GameManager.Instance.NetworkManager.CreateRoom(_roomNameInput.text);
            });
        }

        if (_joinRoomButton != null)
        {
            _joinRoomButton.onClick.RemoveAllListeners();
            _joinRoomButton.onClick.AddListener(() => {
                SaveLocalPlayerName();
                GameManager.Instance.NetworkManager.JoinRoom(_roomNameInput.text);
            });
        }
        
        if (_readyButton != null)
        {
            _readyButton.onClick.RemoveAllListeners();
            _readyButton.onClick.AddListener(() => {
                SetLocalPlayerReady();
            });
        }
        
        GameManager.Instance.LogManager.LogMessage("UI listeners set up");
    }
    
    private void SaveLocalPlayerName()
    {
        if (_playerNameInput != null)
        {
            _localPlayerName = _playerNameInput.text;
            if (string.IsNullOrEmpty(_localPlayerName))
            {
                _localPlayerName = "Player" + UnityEngine.Random.Range(1000, 10000);
            }
            
            GameManager.Instance.LogManager.LogMessage($"Saved local player name: {_localPlayerName}");
        }
    }
    
    private void SetLocalPlayerReady()
    {
        // Find local player object
        Player localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            // Toggle ready status
            bool currentStatus = localPlayer.GetReadyStatus();
            bool newStatus = !currentStatus;
            localPlayer.SetReadyStatus(newStatus);
            
            // Direct call to LobbyManager to ensure ready status is set
            GameManager.Instance.LobbyManager.SetPlayerReadyStatus(localPlayer.GetPlayerName(), newStatus);
            
            GameManager.Instance.LogManager.LogMessage($"Local player ready status changed to: {newStatus}");
            
            // Update UI
            Button button = _readyButton;
            if (button != null)
            {
                if (newStatus)
                {
                    ColorBlock colors = button.colors;
                    colors.normalColor = new Color(0.7f, 0.2f, 0.2f, 1f);
                    button.colors = colors;
                    
                    TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();
                    if (buttonText != null)
                    {
                        buttonText.text = "Cancel Ready";
                    }
                }
                else
                {
                    ColorBlock colors = button.colors;
                    colors.normalColor = new Color(0.2f, 0.7f, 0.2f, 1f);
                    button.colors = colors;
                    
                    TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();
                    if (buttonText != null)
                    {
                        buttonText.text = "Ready";
                    }
                }
            }
            
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

    // Implement common UI actions
    public void ShowConnectUI()
    {
        if (_connectPanel != null)
        {
            _connectPanel.SetActive(true);
            GameManager.Instance.LogManager.LogMessage("Connect panel shown");
        }
        
        if (_lobbyPanel != null)
        {
            _lobbyPanel.SetActive(false);
            GameManager.Instance.LogManager.LogMessage("Lobby panel hidden");
        }
            
        if (_gameStartedPanel != null)
        {
            _gameStartedPanel.SetActive(false);
            GameManager.Instance.LogManager.LogMessage("Game started panel hidden");
        }
        
        _isUIActive = true;
    }

    public void HideConnectUI()
    {
        if (_connectPanel != null)
        {
            _connectPanel.SetActive(false);
            GameManager.Instance.LogManager.LogMessage("Connect panel hidden");
        }
        
        if (_lobbyPanel != null)
        {
            _lobbyPanel.SetActive(true);
            GameManager.Instance.LogManager.LogMessage("Lobby panel shown");
        }
            
        if (_gameStartedPanel != null)
        {
            _gameStartedPanel.SetActive(false);
            GameManager.Instance.LogManager.LogMessage("Game started panel hidden");
        }
        
        // Update the room name in the lobby UI
        UpdateRoomInfo();
        
        _isUIActive = true;
    }
    
    // New method to completely hide all UI
    public void HideAllUI()
    {
        if (_connectPanel != null)
        {
            _connectPanel.SetActive(false);
            GameManager.Instance.LogManager.LogMessage("Connect panel forcibly hidden");
        }
        
        if (_lobbyPanel != null)
        {
            _lobbyPanel.SetActive(false);
            GameManager.Instance.LogManager.LogMessage("Lobby panel forcibly hidden");
        }
            
        if (_gameStartedPanel != null)
        {
            _gameStartedPanel.SetActive(false);
            GameManager.Instance.LogManager.LogMessage("Game started panel forcibly hidden");
        }
        
        // Additionally, deactivate the entire canvas for lobby UI
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
        
        _connectPanel = null;
        _lobbyPanel = null;
        _gameStartedPanel = null;
        _playerListContent = null;
        _playerListItemPrefab = null;
        _isUIActive = false;
    }
    
    public void UpdateStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
        }
    }
    
    private void UpdateRoomInfo()
    {
        if (_lobbyPanel == null) return;
        
        var runner = GameManager.Instance.NetworkManager.GetRunner();
        if (runner != null && runner.SessionInfo != null)
        {
            TMP_Text roomNameText = _lobbyPanel.transform.Find("Room Name Text")?.GetComponent<TMP_Text>();
            if (roomNameText != null)
            {
                roomNameText.text = $"Room: {runner.SessionInfo.Name}";
            }
        }
    }
    
    public void UpdatePlayersList()
    {
        if (_playerListContent == null)
        {
            GameManager.Instance.LogManager.LogError("Player list content is null!");
            return;
        }
            
        GameManager.Instance.LogManager.LogMessage("Updating players list in UI");
            
        // Get all player names from LobbyManager
        List<string> playerNames = GameManager.Instance.LobbyManager.GetAllPlayerNames();
        
        GameManager.Instance.LogManager.LogMessage($"Found {playerNames.Count} players in lobby manager");
        
        // Remove any players that are no longer in the lobby
        List<string> playersToRemove = new List<string>();
        foreach (var entry in _playerListItems)
        {
            if (!playerNames.Contains(entry.Key))
            {
                playersToRemove.Add(entry.Key);
            }
        }
        
        foreach (var playerName in playersToRemove)
        {
            if (_playerListItems[playerName] != null)
            {
                Destroy(_playerListItems[playerName]);
            }
            _playerListItems.Remove(playerName);
            GameManager.Instance.LogManager.LogMessage($"Removed {playerName} from UI list");
        }
        
        // Add or update player items
        foreach (var playerName in playerNames)
        {
            GameObject playerItem;
            if (_playerListItems.ContainsKey(playerName))
            {
                playerItem = _playerListItems[playerName];
                if (playerItem == null)
                {
                    // Item was destroyed, recreate it
                    playerItem = Instantiate(_playerListItemPrefab, _playerListContent);
                    playerItem.SetActive(true);
                    _playerListItems[playerName] = playerItem;
                }
                GameManager.Instance.LogManager.LogMessage($"Updating existing player item: {playerName}");
            }
            else
            {
                // Create a new player item
                playerItem = Instantiate(_playerListItemPrefab, _playerListContent);
                playerItem.SetActive(true);
                _playerListItems.Add(playerName, playerItem);
                GameManager.Instance.LogManager.LogMessage($"Created new player item: {playerName}");
            }
            
            // Update player item UI
            TMP_Text nameText = playerItem.transform.Find("Player Name")?.GetComponent<TMP_Text>();
            TMP_Text statusText = playerItem.transform.Find("Ready Status")?.GetComponent<TMP_Text>();
            
            if (nameText != null)
            {
                nameText.text = playerName;
            }
            
            if (statusText != null)
            {
                bool isReady = GameManager.Instance.LobbyManager.GetPlayerReadyStatus(playerName);
                statusText.text = isReady ? "Ready" : "Not Ready";
                statusText.color = isReady ? Color.green : Color.red;
            }
        }
    }
    
    private void HandleAllPlayersReady()
    {
        if (_countdownText != null)
        {
            _countdownText.text = "All players ready! Game starting soon...";
        }
        
        GameManager.Instance.LogManager.LogMessage("All players ready event received in UI");
    }
    
    private void HandleCountdownComplete()
    {
        if (_countdownText != null)
        {
            _countdownText.text = "Game starting!";
        }
        
        GameManager.Instance.LogManager.LogMessage("Countdown complete event received in UI");
    }
    
    // Method to handle the game started event
    private void HandleGameStarted()
    {
        GameManager.Instance.LogManager.LogMessage("Game started event received in UI");
        
        // Show the game started panel
        if (_gameStartedPanel != null)
        {
            _gameStartedPanel.SetActive(true);
            
            // Hide all UI after 3 seconds
            Invoke("HideAllUIDelayed", 2f);
            
            // Destroy all UI after 3 seconds
            Invoke("DestroyAllUI", 3f);
        }
    }
    
    // Delayed method to hide UI
    private void HideAllUIDelayed()
    {
        HideAllUI();
    }
    
    private void HideGameStartedPanel()
    {
        if (_gameStartedPanel != null)
        {
            _gameStartedPanel.SetActive(false);
        }
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
            if (_countdownText != null)
            {
                float countdown = GameManager.Instance.LobbyManager.GetCurrentCountdown();
                _countdownText.text = $"Game starting in: {countdown:F1}";
            }
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
    }
}