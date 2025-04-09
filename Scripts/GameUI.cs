using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

public class GameUI : MonoBehaviour
{
    // UI References
    private Canvas _gameCanvas;
    
    // Main container panels
    private GameObject _mainLayout;
    private GameObject _handPanel;
    private GameObject _statsPanel;
    private GameObject _opponentsPanel;
    private GameObject _monsterPanel;
    private GameObject _turnInfoPanel;
    
    // Card references
    private GameObject _cardPrefab;
    private List<CardDisplay> _cardDisplays = new List<CardDisplay>();
    
    // Monster displays
    private MonsterDisplay _playerMonsterDisplay;
    private MonsterDisplay _opponentMonsterDisplay;
    
    // Stats Text Elements
    private TMP_Text _healthText;
    private TMP_Text _energyText;
    private TMP_Text _scoreText;
    private TMP_Text _roundText;
    private TMP_Text _turnInfoText;
    
    // Opponent references
    private GameObject _opponentStatsPrefab;
    private Dictionary<PlayerRef, OpponentStatsDisplay> _opponentDisplays = new Dictionary<PlayerRef, OpponentStatsDisplay>();
    
    // Button references
    private Button _endTurnButton;
    
    // References
    private PlayerState _localPlayerState;
    
    // Initialization state
    private bool _initialized = false;
    private bool _initializationInProgress = false;
    private float _initRetryInterval = 0.5f;
    private int _maxInitRetries = 20;

    public void Initialize()
    {
        if (_initialized || _initializationInProgress) return;
        
        GameManager.Instance.LogManager.LogMessage("Starting GameUI initialization");
        
        // Start initialization process with retries
        StartCoroutine(InitializeWithRetry());
    }

    private IEnumerator InitializeWithRetry()
    {
        _initializationInProgress = true;
        int retryCount = 0;
        
        while (retryCount < _maxInitRetries)
        {
            // Wait for GameState to be available and spawned
            if (GameState.Instance == null || !GameState.Instance.IsSpawned())
            {
                GameManager.Instance.LogManager.LogMessage($"GameState not available or not spawned, retrying ({retryCount+1}/{_maxInitRetries})");
                retryCount++;
                yield return new WaitForSeconds(_initRetryInterval);
                continue;
            }
            
            // Get local player state
            _localPlayerState = GameState.Instance.GetLocalPlayerState();
            if (_localPlayerState == null)
            {
                GameManager.Instance.LogManager.LogMessage($"LocalPlayerState not available, retrying ({retryCount+1}/{_maxInitRetries})");
                retryCount++;
                yield return new WaitForSeconds(_initRetryInterval);
                continue;
            }
            
            // If we got here, we can initialize
            GameManager.Instance.LogManager.LogMessage("GameState and LocalPlayerState available, proceeding with initialization");
            
            // Create UI
            CreateMainCanvas();
            CreateMainLayout();
            CreateCardPrefab();
            CreatePlayerUI();
            CreateOpponentsUI();
            CreateMonsterUI();
            CreateTurnInfoUI();
            CreateHandUI();
            
            // Subscribe to events
            PlayerState.OnStatsChanged += UpdatePlayerStats;
            PlayerState.OnHandChanged += UpdateHand;
            GameState.OnRoundChanged += UpdateRoundInfo;
            GameState.OnTurnChanged += UpdateTurnInfo;
            
            // Subscribe to GameState player registration events
            GameState.OnPlayerStateAdded += SubscribeToOpponentState;
            GameState.OnPlayerStateRemoved += UnsubscribeFromOpponentState;
            
            _initialized = true;
            _initializationInProgress = false;
            GameManager.Instance.LogManager.LogMessage("GameUI fully initialized");
            
            // Hide lobby UI
            HideLobbyUI();
            
            // Initial UI update
            UpdateAllUI();
            
            // Do an immediate attempt to find and subscribe to all opponent states
            InitializeOpponentStates();
            
            yield break; // Successful initialization, exit coroutine
        }
        
        // If we get here, initialization failed after max retries
        GameManager.Instance.LogManager.LogError("Failed to initialize GameUI after maximum retries");
        _initializationInProgress = false;
    }

    // Subscribe to all existing opponent states
    private void InitializeOpponentStates()
    {
        if (GameState.Instance == null) return;
        
        GameManager.Instance.LogManager.LogMessage("Initializing opponent states");
        
        var allPlayerStates = GameState.Instance.GetAllPlayerStates();
        PlayerRef localPlayerRef = GameState.Instance.GetLocalPlayerRef();
        
        foreach (var entry in allPlayerStates)
        {
            if (entry.Key != localPlayerRef)
            {
                SubscribeToOpponentState(entry.Key, entry.Value);
            }
        }
    }
    
    // Subscribe to a specific opponent's state
    private void SubscribeToOpponentState(PlayerRef playerRef, PlayerState playerState)
    {
        if (GameState.Instance == null) return;
        if (playerRef == GameState.Instance.GetLocalPlayerRef()) return;
        
        GameManager.Instance.LogManager.LogMessage($"Subscribing to opponent state: {playerState.PlayerName}");
        
        // Create the opponent display if it doesn't exist
        if (!_opponentDisplays.ContainsKey(playerRef))
        {
            CreateOpponentDisplay(playerRef, playerState);
        }
        
        // Update the display with current data
        UpdateOpponentDisplay(playerRef, playerState);
    }
    
    // Unsubscribe from a specific opponent's state
    private void UnsubscribeFromOpponentState(PlayerRef playerRef, PlayerState playerState)
    {
        if (_opponentDisplays.ContainsKey(playerRef))
        {
            GameManager.Instance.LogManager.LogMessage($"Unsubscribing from opponent state: {playerState.PlayerName}");
            
            // Destroy the display object
            if (_opponentDisplays[playerRef] != null && _opponentDisplays[playerRef].gameObject != null)
            {
                Destroy(_opponentDisplays[playerRef].gameObject);
            }
            
            // Remove from the dictionary
            _opponentDisplays.Remove(playerRef);
        }
    }
    
    // Create display for a specific opponent
    private void CreateOpponentDisplay(PlayerRef playerRef, PlayerState playerState)
    {
        if (_opponentsPanel == null || _opponentStatsPrefab == null) {
            GameManager.Instance.LogManager.LogError("Cannot create opponent display: panel or prefab is null");
            return;
        }
        
        string playerName = playerState?.PlayerName.ToString() ?? "Unknown";
        GameManager.Instance.LogManager.LogMessage($"Creating opponent display for {playerName}");
        
        // Create new display
        GameObject opponentObj = Instantiate(_opponentStatsPrefab, _opponentsPanel.transform);
        opponentObj.SetActive(true);
        opponentObj.name = $"OpponentDisplay_{playerName}";
        
        OpponentStatsDisplay display = opponentObj.GetComponent<OpponentStatsDisplay>();
        if (display != null)
        {
            // Force create text elements if needed
            display.ForceCreateTextElements();
            
            // Store in the dictionary
            _opponentDisplays[playerRef] = display;
            
            // Update with player state
            display.UpdateDisplay(playerState);
        }
        else
        {
            GameManager.Instance.LogManager.LogError("OpponentStatsDisplay component not found on instantiated prefab");
        }
    }
    
    // Update a specific opponent's display
    private void UpdateOpponentDisplay(PlayerRef playerRef, PlayerState playerState)
    {
        if (!_opponentDisplays.ContainsKey(playerRef) || _opponentDisplays[playerRef] == null)
        {
            CreateOpponentDisplay(playerRef, playerState);
        }
        
        if (_opponentDisplays.ContainsKey(playerRef) && _opponentDisplays[playerRef] != null)
        {
            _opponentDisplays[playerRef].UpdateDisplay(playerState);
            GameManager.Instance.LogManager.LogMessage($"Updated opponent display for {playerState.PlayerName}");
        }
    }

private void CreateMainCanvas()
{
    // Create main canvas for game UI
    GameObject canvasObj = new GameObject("GameCanvas");
    _gameCanvas = canvasObj.AddComponent<Canvas>();
    _gameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
    _gameCanvas.sortingOrder = 10; // Ensure it's above other UI
    
    CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920, 1080);
    scaler.matchWidthOrHeight = 0.5f; // Balance width and height
    
    // Make sure the GraphicRaycaster is added - essential for drag operations!
    GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
    
    DontDestroyOnLoad(canvasObj);
    
    // Add a debug log to verify canvas creation
    Debug.Log($"Game canvas created: {_gameCanvas.name}");
    
    GameManager.Instance.LogManager.LogMessage("Game canvas created");
}


    private void CreateMainLayout()
    {
        // Create a main layout container that will hold all UI elements
        _mainLayout = new GameObject("MainLayout");
        _mainLayout.transform.SetParent(_gameCanvas.transform, false);
        
        // Add a background image
        Image background = _mainLayout.AddComponent<Image>();
        background.color = new Color(0.05f, 0.05f, 0.1f, 0.8f); // Dark blue semi-transparent background
        
        // Set to full screen
        RectTransform mainRect = _mainLayout.GetComponent<RectTransform>();
        mainRect.anchorMin = Vector2.zero;
        mainRect.anchorMax = Vector2.one;
        mainRect.offsetMin = Vector2.zero;
        mainRect.offsetMax = Vector2.zero;
        
        GameManager.Instance.LogManager.LogMessage("Main layout created");
    }
    
    private void CreateCardPrefab()
{
    // Create card prefab for hand display
    _cardPrefab = new GameObject("CardPrefab");
    _cardPrefab.SetActive(false);
    
    // Add RectTransform - essential for UI elements
    RectTransform cardRect = _cardPrefab.AddComponent<RectTransform>();
    cardRect.sizeDelta = new Vector2(180, 250);
    
    // Card background
    Image cardBg = _cardPrefab.AddComponent<Image>();
    cardBg.color = new Color(0.2f, 0.2f, 0.2f);
    
    // Add CardDisplay component
    CardDisplay display = _cardPrefab.AddComponent<CardDisplay>();
    
    // Add CanvasGroup - required for drag operations
    CanvasGroup canvasGroup = _cardPrefab.AddComponent<CanvasGroup>();
    
    // Create title text
    GameObject titleObj = new GameObject("TitleText");
    titleObj.transform.SetParent(_cardPrefab.transform, false);
    TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
    titleText.fontSize = 16;
    titleText.alignment = TextAlignmentOptions.Center;
    titleText.color = Color.white;
    
    RectTransform titleRect = titleObj.GetComponent<RectTransform>();
    titleRect.anchorMin = new Vector2(0, 0.85f);
    titleRect.anchorMax = new Vector2(1, 1);
    titleRect.offsetMin = Vector2.zero;
    titleRect.offsetMax = Vector2.zero;
    
    // Create cost text
    GameObject costObj = new GameObject("CostText");
    costObj.transform.SetParent(_cardPrefab.transform, false);
    TMP_Text costText = costObj.AddComponent<TextMeshProUGUI>();
    costText.fontSize = 20;
    costText.alignment = TextAlignmentOptions.Center;
    costText.color = Color.yellow;
    
    // Add background circle for cost
    GameObject costBgObj = new GameObject("CostBg");
    costBgObj.transform.SetParent(costObj.transform, false);
    costBgObj.transform.SetSiblingIndex(0);
    Image costBg = costBgObj.AddComponent<Image>();
    costBg.color = new Color(0.1f, 0.1f, 0.3f);
    
    RectTransform costBgRect = costBgObj.GetComponent<RectTransform>();
    costBgRect.anchorMin = Vector2.zero;
    costBgRect.anchorMax = Vector2.one;
    costBgRect.offsetMin = new Vector2(-5, -5);
    costBgRect.offsetMax = new Vector2(5, 5);
    
    RectTransform costRect = costObj.GetComponent<RectTransform>();
    costRect.anchorMin = new Vector2(0, 0.9f);
    costRect.anchorMax = new Vector2(0.2f, 1);
    costRect.offsetMin = Vector2.zero;
    costRect.offsetMax = Vector2.zero;
    
    // Create description text
    GameObject descObj = new GameObject("DescriptionText");
    descObj.transform.SetParent(_cardPrefab.transform, false);
    TMP_Text descText = descObj.AddComponent<TextMeshProUGUI>();
    descText.fontSize = 14;
    descText.alignment = TextAlignmentOptions.Center;
    descText.color = Color.white;
    
    RectTransform descRect = descObj.GetComponent<RectTransform>();
    descRect.anchorMin = new Vector2(0.1f, 0.2f);
    descRect.anchorMax = new Vector2(0.9f, 0.6f);
    descRect.offsetMin = Vector2.zero;
    descRect.offsetMax = Vector2.zero;
    
    // Set references in CardDisplay
    display.SetTextElements(titleText, costText, descText);
    
    // Add button for interactivity
    Button cardButton = _cardPrefab.AddComponent<Button>();
    ColorBlock colors = cardButton.colors;
    colors.highlightedColor = new Color(0.8f, 0.8f, 1f);
    colors.pressedColor = new Color(0.7f, 0.7f, 0.9f);
    cardButton.colors = colors;
    
    // Set up the button click handler
    display.SetButton(cardButton);
    
    GameManager.Instance.LogManager.LogMessage("Card prefab created");
}

    
    // New method to set up drag-and-drop on card prefab
    // This is a partial update with just the method that needs fixing in GameUI.cs

// Replace the old SetupCardDragAndDrop method with this one
private void SetupCardDragAndDrop(CardDisplay display, GameObject cardObj)
{
    // Use the new method name: InitializeDragOperation instead of SetCanvas
    if (_gameCanvas != null)
    {
        display.InitializeDragOperation(_gameCanvas);
        Debug.Log($"Drag operation initialized for card {cardObj.name}");
    }
    else
    {
        Debug.LogError("Game canvas is null when setting up card drag!");
        
        // Try to find the canvas as a fallback
        Canvas foundCanvas = FindObjectOfType<Canvas>();
        if (foundCanvas != null)
        {
            display.InitializeDragOperation(foundCanvas);
            Debug.Log("Using fallback canvas for card drag operations");
        }
        else
        {
            Debug.LogError("No canvas found in the scene. Drag operations will fail!");
        }
    }
    
    // Subscribe to card played event
    display.CardPlayed += OnCardPlayedOnTarget;
}

    private void CreatePlayerUI()
    {
        // Create player stats panel
        _statsPanel = new GameObject("PlayerStats");
        _statsPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image statsBg = _statsPanel.AddComponent<Image>();
        statsBg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Position in top left
        RectTransform statsRect = _statsPanel.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0, 1);
        statsRect.anchorMax = new Vector2(0.2f, 1);
        statsRect.pivot = new Vector2(0, 1);
        statsRect.offsetMin = new Vector2(20, -200);
        statsRect.offsetMax = new Vector2(-20, -20);
        
        // Add player name
        GameObject nameObj = new GameObject("PlayerName");
        nameObj.transform.SetParent(_statsPanel.transform, false);
        TMP_Text nameText = nameObj.AddComponent<TextMeshProUGUI>();
        
        // Safely get player name or use default
        string playerName = "Player";
        try {
            if (_localPlayerState != null) {
                playerName = _localPlayerState.PlayerName.ToString();
            }
        }
        catch (Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Could not access PlayerName: {ex.Message}. Using default name.");
        }
        
        nameText.text = playerName;
        nameText.fontSize = 20;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontStyle = FontStyles.Bold;
        
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.85f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(10, 0);
        nameRect.offsetMax = new Vector2(-10, -5);
        
        // Add health
        _healthText = CreateStatText(_statsPanel, "Health", "Health: 50/50", 0.7f, 0.85f);
        
        // Add energy
        _energyText = CreateStatText(_statsPanel, "Energy", "Energy: 3/3", 0.55f, 0.7f);
        
        // Add score
        _scoreText = CreateStatText(_statsPanel, "Score", "Score: 0", 0.4f, 0.55f);
        
        GameManager.Instance.LogManager.LogMessage("Player stats panel created");
    }

    private TMP_Text CreateStatText(GameObject parent, string name, string defaultText, float minY, float maxY)
    {
        GameObject statObj = new GameObject(name);
        statObj.transform.SetParent(parent.transform, false);
        TMP_Text statText = statObj.AddComponent<TextMeshProUGUI>();
        statText.text = defaultText;
        statText.fontSize = 18;
        statText.color = Color.white;
        statText.alignment = TextAlignmentOptions.Left;
        
        RectTransform statRect = statObj.GetComponent<RectTransform>();
        statRect.anchorMin = new Vector2(0.05f, minY);
        statRect.anchorMax = new Vector2(0.95f, maxY);
        statRect.offsetMin = Vector2.zero;
        statRect.offsetMax = Vector2.zero;
        
        return statText;
    }

    private void CreateOpponentsUI()
    {
        // Create opponents panel on the right
        _opponentsPanel = new GameObject("OpponentsPanel");
        _opponentsPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image opponentsBg = _opponentsPanel.AddComponent<Image>();
        opponentsBg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Position on the right
        RectTransform opponentsRect = _opponentsPanel.GetComponent<RectTransform>();
        opponentsRect.anchorMin = new Vector2(0.8f, 0.3f);
        opponentsRect.anchorMax = new Vector2(1, 0.9f);
        opponentsRect.pivot = new Vector2(1, 0.5f);
        opponentsRect.offsetMin = new Vector2(20, 0);
        opponentsRect.offsetMax = new Vector2(-20, 0);
        
        // Add vertical layout
        VerticalLayoutGroup layout = _opponentsPanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(10, 10, 10, 10);
        
        // Create opponent stats prefab
        _opponentStatsPrefab = CreateOpponentStatsPrefab();
        
        // Don't add opponents here, we'll do it in SubscribeToOpponentState
        
        GameManager.Instance.LogManager.LogMessage("Opponents panel created");
    }

    private GameObject CreateOpponentStatsPrefab()
    {
        GameObject opponentObj = new GameObject("OpponentStatsPrefab");
        opponentObj.SetActive(false);
        
        // Background
        Image bg = opponentObj.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
        
        // Layout
        RectTransform rect = opponentObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 100);
        
        // Add layout element for sizing
        LayoutElement layoutElement = opponentObj.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 100;
        layoutElement.flexibleWidth = 1;
        
        // Add stats display component
        OpponentStatsDisplay display = opponentObj.AddComponent<OpponentStatsDisplay>();
        
        // Name
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(opponentObj.transform, false);
        TMP_Text nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 18;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        nameText.fontStyle = FontStyles.Bold;
        
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.7f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(5, 0);
        nameRect.offsetMax = new Vector2(-5, -5);
        
        // Health
        GameObject healthObj = new GameObject("Health");
        healthObj.transform.SetParent(opponentObj.transform, false);
        TMP_Text healthText = healthObj.AddComponent<TextMeshProUGUI>();
        healthText.fontSize = 16;
        healthText.alignment = TextAlignmentOptions.Left;
        healthText.color = Color.red;
        
        RectTransform healthRect = healthObj.GetComponent<RectTransform>();
        healthRect.anchorMin = new Vector2(0.05f, 0.35f);
        healthRect.anchorMax = new Vector2(0.95f, 0.65f);
        healthRect.offsetMin = Vector2.zero;
        healthRect.offsetMax = Vector2.zero;
        
        // Score
        GameObject scoreObj = new GameObject("Score");
        scoreObj.transform.SetParent(opponentObj.transform, false);
        TMP_Text scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
        scoreText.fontSize = 16;
        scoreText.alignment = TextAlignmentOptions.Left;
        scoreText.color = Color.yellow;
        
        RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0.05f, 0.05f);
        scoreRect.anchorMax = new Vector2(0.95f, 0.35f);
        scoreRect.offsetMin = Vector2.zero;
        scoreRect.offsetMax = Vector2.zero;
        
        // Set references
        display.SetTextElements(nameText, healthText, scoreText);
        
        return opponentObj;
    }

    private void CreateMonsterUI()
    {
        // Create monster area in the center
        _monsterPanel = new GameObject("MonsterPanel");
        _monsterPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image monsterBg = _monsterPanel.AddComponent<Image>();
        monsterBg.color = new Color(0.1f, 0.1f, 0.2f, 0.4f); // More transparent
        
        // Position in center
        RectTransform monsterRect = _monsterPanel.GetComponent<RectTransform>();
        monsterRect.anchorMin = new Vector2(0.2f, 0.3f);
        monsterRect.anchorMax = new Vector2(0.8f, 0.8f);
        monsterRect.offsetMin = Vector2.zero;
        monsterRect.offsetMax = Vector2.zero;
        
        // Create player's monster on left
        GameObject playerMonsterObj = new GameObject("PlayerMonster");
        playerMonsterObj.transform.SetParent(_monsterPanel.transform, false);
        _playerMonsterDisplay = playerMonsterObj.AddComponent<MonsterDisplay>();
        
        RectTransform playerMonsterRect = playerMonsterObj.GetComponent<RectTransform>();
        playerMonsterRect.anchorMin = new Vector2(0, 0);
        playerMonsterRect.anchorMax = new Vector2(0.45f, 1);
        playerMonsterRect.offsetMin = new Vector2(20, 20);
        playerMonsterRect.offsetMax = new Vector2(-10, -20);
        
        // Create opponent's monster on right
        GameObject opponentMonsterObj = new GameObject("OpponentMonster");
        opponentMonsterObj.transform.SetParent(_monsterPanel.transform, false);
        _opponentMonsterDisplay = opponentMonsterObj.AddComponent<MonsterDisplay>();
        
        RectTransform opponentMonsterRect = opponentMonsterObj.GetComponent<RectTransform>();
        opponentMonsterRect.anchorMin = new Vector2(0.55f, 0);
        opponentMonsterRect.anchorMax = new Vector2(1, 1);
        opponentMonsterRect.offsetMin = new Vector2(10, 20);
        opponentMonsterRect.offsetMax = new Vector2(-20, -20);
        
        // Create VS text in middle
        GameObject vsObj = new GameObject("VS");
        vsObj.transform.SetParent(_monsterPanel.transform, false);
        TMP_Text vsText = vsObj.AddComponent<TextMeshProUGUI>();
        vsText.text = "VS";
        vsText.fontSize = 36;
        vsText.fontStyle = FontStyles.Bold;
        vsText.alignment = TextAlignmentOptions.Center;
        vsText.color = Color.yellow;
        
        RectTransform vsRect = vsObj.GetComponent<RectTransform>();
        vsRect.anchorMin = new Vector2(0.45f, 0.4f);
        vsRect.anchorMax = new Vector2(0.55f, 0.6f);
        vsRect.offsetMin = Vector2.zero;
        vsRect.offsetMax = Vector2.zero;
        
        // Initialize the monster displays with default monsters
        UpdateMonsterDisplays();
        
        GameManager.Instance.LogManager.LogMessage("Monster displays created");
    }

    private void CreateTurnInfoUI()
    {
        // Create turn info panel at top
        _turnInfoPanel = new GameObject("TurnInfoPanel");
        _turnInfoPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image turnInfoBg = _turnInfoPanel.AddComponent<Image>();
        turnInfoBg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Position at top
        RectTransform turnInfoRect = _turnInfoPanel.GetComponent<RectTransform>();
        turnInfoRect.anchorMin = new Vector2(0.25f, 1);
        turnInfoRect.anchorMax = new Vector2(0.75f, 1);
        turnInfoRect.pivot = new Vector2(0.5f, 1);
        turnInfoRect.offsetMin = new Vector2(0, -80);
        turnInfoRect.offsetMax = new Vector2(0, -20);
        
        // Round text on left
        GameObject roundObj = new GameObject("RoundText");
        roundObj.transform.SetParent(_turnInfoPanel.transform, false);
        _roundText = roundObj.AddComponent<TextMeshProUGUI>();
        _roundText.text = "Round 1";
        _roundText.fontSize = 24;
        _roundText.fontStyle = FontStyles.Bold;
        _roundText.alignment = TextAlignmentOptions.Center;
        _roundText.color = Color.white;
        
        RectTransform roundRect = roundObj.GetComponent<RectTransform>();
        roundRect.anchorMin = new Vector2(0, 0);
        roundRect.anchorMax = new Vector2(0.5f, 1);
        roundRect.offsetMin = new Vector2(20, 10);
        roundRect.offsetMax = new Vector2(-10, -10);
        
        // Turn info on right
        GameObject turnObj = new GameObject("TurnInfoText");
        turnObj.transform.SetParent(_turnInfoPanel.transform, false);
        _turnInfoText = turnObj.AddComponent<TextMeshProUGUI>();
        _turnInfoText.text = "Your Turn";
        _turnInfoText.fontSize = 24;
        _turnInfoText.fontStyle = FontStyles.Bold;
        _turnInfoText.alignment = TextAlignmentOptions.Center;
        _turnInfoText.color = Color.green;
        
        RectTransform turnRect = turnObj.GetComponent<RectTransform>();
        turnRect.anchorMin = new Vector2(0.5f, 0);
        turnRect.anchorMax = new Vector2(1, 1);
        turnRect.offsetMin = new Vector2(10, 10);
        turnRect.offsetMax = new Vector2(-20, -10);
        
        // End turn button
        GameObject endTurnObj = new GameObject("EndTurnButton");
        endTurnObj.transform.SetParent(_mainLayout.transform, false);
        _endTurnButton = endTurnObj.AddComponent<Button>();
        
        // Button image
        Image endTurnImage = endTurnObj.AddComponent<Image>();
        endTurnImage.color = new Color(0.7f, 0.2f, 0.2f);
        
        // Button text
        GameObject endTurnTextObj = new GameObject("Text");
        endTurnTextObj.transform.SetParent(endTurnObj.transform, false);
        TMP_Text endTurnText = endTurnTextObj.AddComponent<TextMeshProUGUI>();
        endTurnText.text = "END TURN";
        endTurnText.fontSize = 18;
        endTurnText.fontStyle = FontStyles.Bold;
        endTurnText.alignment = TextAlignmentOptions.Center;
        endTurnText.color = Color.white;
        
        RectTransform endTurnTextRect = endTurnTextObj.GetComponent<RectTransform>();
        endTurnTextRect.anchorMin = Vector2.zero;
        endTurnTextRect.anchorMax = Vector2.one;
        endTurnTextRect.offsetMin = new Vector2(5, 5);
        endTurnTextRect.offsetMax = new Vector2(-5, -5);
        
        // Position the button
        RectTransform endTurnRect = endTurnObj.GetComponent<RectTransform>();
        endTurnRect.anchorMin = new Vector2(1, 0);
        endTurnRect.anchorMax = new Vector2(1, 0);
        endTurnRect.pivot = new Vector2(1, 0);
        endTurnRect.sizeDelta = new Vector2(180, 60);
        endTurnRect.anchoredPosition = new Vector2(-40, 40);
        
        // Button click event
        _endTurnButton.onClick.AddListener(OnEndTurnClicked);
        
        GameManager.Instance.LogManager.LogMessage("Turn info panel created");
    }

    private void CreateHandUI()
    {
        // Create hand panel at bottom
        _handPanel = new GameObject("HandPanel");
        _handPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image handBg = _handPanel.AddComponent<Image>();
        handBg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Position at bottom
        RectTransform handRect = _handPanel.GetComponent<RectTransform>();
        handRect.anchorMin = new Vector2(0, 0);
        handRect.anchorMax = new Vector2(1, 0);
        handRect.pivot = new Vector2(0.5f, 0);
        handRect.offsetMin = new Vector2(100, 20);
        handRect.offsetMax = new Vector2(-100, 220);
        
        // Horizontal layout for cards
        HorizontalLayoutGroup layout = _handPanel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(20, 20, 10, 10);
        layout.childAlignment = TextAnchor.MiddleCenter;
        
        // Content size fitter to adjust based on card count
        ContentSizeFitter fitter = _handPanel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        GameManager.Instance.LogManager.LogMessage("Hand area created");
    }

    private void HideLobbyUI()
    {
        // Hide the lobby UI elements when the game starts
        var uiManager = GameManager.Instance.UIManager;
        if (uiManager != null)
        {
            try
            {
                // Force hide all UI through the UIManager
                uiManager.HideAllUI();
                GameManager.Instance.LogManager.LogMessage("Forced hiding of all lobby UI components");
                
                // Additionally, find and destroy all lobby canvases
                Canvas[] allCanvases = FindObjectsOfType<Canvas>();
                foreach (Canvas canvas in allCanvases)
                {
                    if (canvas.gameObject != _gameCanvas.gameObject && 
                        (canvas.name.StartsWith("UI Canvas") || canvas.name.StartsWith("UI_Canvas")))
                    {
                        GameManager.Instance.LogManager.LogMessage($"Destroying lobby UI canvas: {canvas.name}");
                        Destroy(canvas.gameObject);
                    }
                }
                
                // Also try to find specific panels by name
                string[] panelNames = {"Lobby Panel", "Connect Panel", "Game Started Panel", "LobbyPanel", "ConnectPanel"};
                foreach (string panelName in panelNames)
                {
                    GameObject panel = GameObject.Find(panelName);
                    if (panel != null)
                    {
                        GameManager.Instance.LogManager.LogMessage($"Found and destroying panel: {panel.name}");
                        Destroy(panel);
                    }
                }
            }
            catch (System.Exception ex)
            {
                GameManager.Instance.LogManager.LogError($"Error hiding lobby UI: {ex.Message}");
            }
            
            GameManager.Instance.LogManager.LogMessage("Lobby UI complete destruction procedure completed");
        }
    }

    private void UpdateAllUI()
    {
        try {
            // Update all UI elements
            UpdatePlayerStats(_localPlayerState);
            UpdateHand(_localPlayerState, _localPlayerState?.GetHand() ?? new List<CardData>());
            UpdateOpponentDisplays();
            UpdateMonsterDisplays();
            
            if (GameState.Instance != null && GameState.Instance.IsSpawned())
            {
                UpdateRoundInfo(GameState.Instance.GetCurrentRound());
                UpdateTurnInfo(GameState.Instance.GetCurrentTurnPlayerIndex());
            }
            else
            {
                // Default values if GameState isn't available
                UpdateRoundInfo(1);
                UpdateTurnInfo(0);
            }
        }
        catch (Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error updating UI: {ex.Message}");
        }
    }

    private void UpdatePlayerStats(PlayerState playerState)
    {
        if (playerState != _localPlayerState) return;
        
        try {
            // Update health
            if (_healthText != null)
            {
                _healthText.text = $"Health: {playerState.Health}/{playerState.MaxHealth}";
            }
            
            // Update energy
            if (_energyText != null)
            {
                _energyText.text = $"Energy: {playerState.Energy}/{playerState.MaxEnergy}";
            }
            
            // Update score
            if (_scoreText != null)
            {
                _scoreText.text = $"Score: {playerState.GetScore()}";
            }
        } 
        catch (Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error updating player stats: {ex.Message}");
            
            // Default values
            if (_healthText != null) _healthText.text = "Health: 50/50";
            if (_energyText != null) _energyText.text = "Energy: 3/3";
            if (_scoreText != null) _scoreText.text = "Score: 0";
        }
    }

    private void UpdateHand(PlayerState playerState, List<CardData> hand)
{
    if (playerState != _localPlayerState || _handPanel == null) return;
    
    // Clear existing cards
    foreach (var display in _cardDisplays)
    {
        if (display != null && display.gameObject != null)
        {
            // Unsubscribe from events
            display.CardClicked -= OnCardClicked;
            display.CardPlayed -= OnCardPlayedOnTarget;
            
            Destroy(display.gameObject);
        }
    }
    _cardDisplays.Clear();
    
    // No cards in hand
    if (hand == null || hand.Count == 0) return;
    
    // Log for debugging
    Debug.Log($"Creating {hand.Count} cards in hand. Canvas: {(_gameCanvas != null ? _gameCanvas.name : "null")}");
    
    // Create card displays
    for (int i = 0; i < hand.Count; i++)
    {
        // Create card instance
        GameObject cardObj = Instantiate(_cardPrefab, _handPanel.transform);
        cardObj.SetActive(true);
        cardObj.name = $"Card_{hand[i].Name}_{i}";  // Naming for debugging
        
        // Set card data
        CardDisplay display = cardObj.GetComponent<CardDisplay>();
        
        // Critical: Properly initialize the drag operations FIRST
        InitializeCardDragOperation(display);
        
        // Then set the data
        display.SetCardData(hand[i], i);
        
        // Add to list
        _cardDisplays.Add(display);
        
        // Add click handler
        display.CardClicked += OnCardClicked;
        display.CardPlayed += OnCardPlayedOnTarget;
        
        // Add layout element to control card size
        LayoutElement layoutElement = cardObj.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = cardObj.AddComponent<LayoutElement>();
        }
        layoutElement.preferredWidth = 180;
        layoutElement.preferredHeight = 250;
        layoutElement.flexibleWidth = 0;
        
        // Debug verification
        Debug.Log($"Card {cardObj.name} created and initialized");
    }
    
    GameManager.Instance.LogManager.LogMessage($"Updated hand with {hand.Count} cards");
}


private void InitializeCardDragOperation(CardDisplay display)
{
    if (display == null)
    {
        Debug.LogError("Attempted to initialize null CardDisplay");
        return;
    }
    
    if (_gameCanvas == null)
    {
        Debug.LogError("Game canvas is null when initializing card drag!");
        // Try to find the canvas
        _gameCanvas = FindObjectOfType<Canvas>();
        if (_gameCanvas == null)
        {
            Debug.LogError("Could not find any canvas in the scene. Drag operations will fail!");
            return;
        }
    }
    
    // Initialize the card drag operation with the proper canvas
    display.InitializeDragOperation(_gameCanvas);
    
    // Debug log
    Debug.Log($"Card drag initialized with canvas: {_gameCanvas.name}");
}


    private void UpdateOpponentDisplays()
    {
        try {
            if (GameState.Instance == null) return;
            
            // Get all player states
            var playerStates = GameState.Instance.GetAllPlayerStates();
            if (playerStates == null || playerStates.Count == 0)
            {
                GameManager.Instance.LogManager.LogMessage("No player states found for opponent displays");
                return;
            }
            
            PlayerRef localPlayerRef = GameState.Instance.GetLocalPlayerRef();
            
            // Remove opponents that aren't in the current game
            List<PlayerRef> playersToRemove = new List<PlayerRef>();
            foreach (var playerRef in _opponentDisplays.Keys)
            {
                if (!playerStates.ContainsKey(playerRef))
                {
                    playersToRemove.Add(playerRef);
                }
            }
            
            foreach (var playerRef in playersToRemove)
            {
                UnsubscribeFromOpponentState(playerRef, null);
            }
            
            // Update or create displays for each opponent
            foreach (var entry in playerStates)
            {
                if (entry.Key != localPlayerRef)
                {
                    // Update display if it exists, create it if it doesn't
                    UpdateOpponentDisplay(entry.Key, entry.Value);
                }
            }
            
            GameManager.Instance.LogManager.LogMessage($"Updated opponent displays for {_opponentDisplays.Count} opponents");
        }
        catch (Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error updating opponent displays: {ex.Message}");
        }
    }

    private void UpdateMonsterDisplays()
    {
        if (_localPlayerState == null) return;
        
        try {
            // Update player monster
            Monster playerMonster = null;
            try { 
                playerMonster = _localPlayerState.GetMonster();
            } catch (Exception) { }
            
            if (_playerMonsterDisplay != null && playerMonster != null)
            {
                _playerMonsterDisplay.SetMonster(playerMonster);
            }
            else if (_playerMonsterDisplay != null)
            {
                // Create default monster if needed
                Monster defaultMonster = new Monster
                {
                    Name = "Player Monster",
                    Health = 40,
                    MaxHealth = 40,
                    Attack = 5,
                    Defense = 3,
                    TintColor = Color.blue
                };
                _playerMonsterDisplay.SetMonster(defaultMonster);
            }
            
            // Update opponent monster
            Monster opponentMonster = null;
            try {
                opponentMonster = _localPlayerState.GetOpponentMonster();
            } catch (Exception) { }
            
            if (_opponentMonsterDisplay != null && opponentMonster != null)
            {
                _opponentMonsterDisplay.SetMonster(opponentMonster);
            }
            else if (_opponentMonsterDisplay != null)
            {
                // Create default opponent monster if needed
                Monster defaultOpponent = new Monster
                {
                    Name = "Opponent Monster",
                    Health = 40,
                    MaxHealth = 40,
                    Attack = 5,
                    Defense = 3,
                    TintColor = Color.red
                };
                _opponentMonsterDisplay.SetMonster(defaultOpponent);
            }
        }
        catch (Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error updating monster displays: {ex.Message}");
        }
    }

    private void UpdateRoundInfo(int round)
    {
        if (_roundText != null)
        {
            _roundText.text = $"Round {round}";
        }
    }

    private void UpdateTurnInfo(int turnPlayerIndex)
    {
        if (_turnInfoText == null || GameState.Instance == null) return;
        
        try {
            bool isLocalPlayerTurn = GameState.Instance.IsLocalPlayerTurn();
            
            if (isLocalPlayerTurn)
            {
                _turnInfoText.text = "Your Turn";
                _turnInfoText.color = Color.green;
                
                // Enable end turn button
                if (_endTurnButton != null)
                {
                    _endTurnButton.interactable = true;
                }
            }
            else
            {
                _turnInfoText.text = "Opponent's Turn";
                _turnInfoText.color = Color.red;
                
                // Disable end turn button
                if (_endTurnButton != null)
                {
                    _endTurnButton.interactable = false;
                }
            }
        }
        catch (Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error updating turn info: {ex.Message}");
            
            // Default to not player's turn when error occurs
            _turnInfoText.text = "Waiting...";
            _turnInfoText.color = Color.yellow;
            if (_endTurnButton != null) {
                _endTurnButton.interactable = false;
            }
        }
    }

    private void OnCardClicked(CardDisplay display)
    {
        if (GameState.Instance == null || !GameState.Instance.IsLocalPlayerTurn()) return;
        
        CardData card = display.GetCardData();
        int cardIndex = display.GetCardIndex();
        
        // Determine target based on card target type
        Monster target = null;
        
        switch (card.Target)
        {
            case CardTarget.Enemy:
            case CardTarget.AllEnemies:
                target = _localPlayerState.GetOpponentMonster();
                break;
                
            case CardTarget.Self:
            case CardTarget.All:
                // Self-targeted cards don't need a target reference
                break;
        }
        
        // Play the card
        try {
            _localPlayerState.PlayCard(cardIndex, target);
            GameManager.Instance.LogManager.LogMessage($"Card {card.Name} played against {(target != null ? target.Name : "self")}");
        }
        catch (Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error playing card: {ex.Message}");
        }
    }
    
    // New method to handle cards played on targets via drag-and-drop
    private void OnCardPlayedOnTarget(CardDisplay display, GameObject targetObj)
    {
        if (GameState.Instance == null || !GameState.Instance.IsLocalPlayerTurn()) return;
        
        CardData card = display.GetCardData();
        int cardIndex = display.GetCardIndex();
        
        // Determine the target based on the target object
        Monster target = null;
        
        // Handle monster targets
        MonsterDisplay monsterDisplay = targetObj.GetComponent<MonsterDisplay>();
        if (monsterDisplay != null)
        {
            // Determine if it's the player's monster or opponent's monster
            bool isOpponentMonster = targetObj.name.Contains("Opponent");
            
            if (isOpponentMonster)
            {
                // Target is the opponent's monster
                target = _localPlayerState.GetOpponentMonster();
            }
            else
            {
                // Target is the player's own monster
                target = _localPlayerState.GetMonster();
            }
        }
        
        // Play the card if we have a valid target
        if ((target != null && (card.Target == CardTarget.Enemy || card.Target == CardTarget.AllEnemies || 
                           card.Target == CardTarget.All)) ||
        (card.Target == CardTarget.Self || card.Target == CardTarget.All))
        {
            try {
                _localPlayerState.PlayCard(cardIndex, target);
                GameManager.Instance.LogManager.LogMessage($"Card {card.Name} played against {(target != null ? target.Name : "self")}");
                
                // Play visual effect
                PlayCardVisualEffect(display, targetObj);
            }
            catch (Exception ex) {
                GameManager.Instance.LogManager.LogMessage($"Error playing card: {ex.Message}");
            }
        }
    }
    
    // New method to create visual effects when playing cards
    private void PlayCardVisualEffect(CardDisplay display, GameObject targetObj)
    {
        CardData card = display.GetCardData();
        
        // Create a visual effect based on card type
        GameObject effectObj = new GameObject("CardEffect");
        effectObj.transform.SetParent(_gameCanvas.transform, false);
        
        // Position at the target
        RectTransform effectRect = effectObj.AddComponent<RectTransform>();
        effectRect.position = targetObj.transform.position;
        effectRect.sizeDelta = new Vector2(100, 100);
        
        // Add image component for the effect
        Image effectImage = effectObj.AddComponent<Image>();
        
        // Set color based on card type
        switch (card.Type)
        {
            case CardType.Attack:
                effectImage.color = new Color(1f, 0.3f, 0.3f, 0.7f);
                break;
            case CardType.Skill:
                effectImage.color = new Color(0.3f, 0.7f, 1f, 0.7f);
                break;
            case CardType.Power:
                effectImage.color = new Color(0.8f, 0.5f, 1f, 0.7f);
                break;
        }
        
        // Add text to show the amount
        GameObject textObj = new GameObject("EffectText");
        textObj.transform.SetParent(effectObj.transform, false);
        
        TMP_Text effectText = textObj.AddComponent<TextMeshProUGUI>();
        effectText.alignment = TextAlignmentOptions.Center;
        effectText.fontSize = 24;
        effectText.fontStyle = FontStyles.Bold;
        
        // Set the text based on card effect
        if (card.DamageAmount > 0)
        {
            effectText.text = $"-{card.DamageAmount}";
            effectText.color = Color.white;
        }
        else if (card.BlockAmount > 0)
        {
            effectText.text = $"+{card.BlockAmount}";
            effectText.color = Color.cyan;
        }
        else if (card.HealAmount > 0)
        {
            effectText.text = $"+{card.HealAmount}";
            effectText.color = Color.green;
        }
        
        // Position the text
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Animate and destroy the effect
        StartCoroutine(AnimateCardEffect(effectObj));
    }
    
    // New coroutine to animate the card effect
    private IEnumerator AnimateCardEffect(GameObject effectObj)
    {
        RectTransform rectTransform = effectObj.GetComponent<RectTransform>();
        Image image = effectObj.GetComponent<Image>();
        
        float duration = 1.0f;
        float elapsed = 0f;
        Vector2 startSize = rectTransform.sizeDelta;
        Vector2 endSize = startSize * 1.5f;
        Color startColor = image.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            
            // Scale up
            rectTransform.sizeDelta = Vector2.Lerp(startSize, endSize, t);
            
            // Fade out
            image.color = Color.Lerp(startColor, endColor, t);
            
            // Update text color too
            TMP_Text text = effectObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                Color textColor = text.color;
                text.color = new Color(textColor.r, textColor.g, textColor.b, 1f - t);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Destroy the effect object
        Destroy(effectObj);
    }

    private void OnEndTurnClicked()
    {
        if (GameState.Instance == null || !GameState.Instance.IsLocalPlayerTurn()) return;
        
        try {
            _localPlayerState.EndTurn();
            GameManager.Instance.LogManager.LogMessage("Player ended turn");
        }
        catch (Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error ending turn: {ex.Message}");
        }
    }

    // Call this each frame to ensure the UI stays updated
    public void Update()
    {
        if (!_initialized && !_initializationInProgress) 
        {
            Initialize();
        }
        
        // Periodically update UI as a fallback but much less frequently
        if (_initialized && Time.frameCount % 120 == 0) // Update every 120 frames (~2 seconds)
        {
            UpdateOpponentDisplays();
            UpdateMonsterDisplays();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        PlayerState.OnStatsChanged -= UpdatePlayerStats;
        PlayerState.OnHandChanged -= UpdateHand;
        GameState.OnRoundChanged -= UpdateRoundInfo;
        GameState.OnTurnChanged -= UpdateTurnInfo;
        
        // Unsubscribe from player state events
        if (GameState.Instance != null)
        {
            GameState.OnPlayerStateAdded -= SubscribeToOpponentState;
            GameState.OnPlayerStateRemoved -= UnsubscribeFromOpponentState;
        }
    }
}