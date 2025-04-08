using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;  // Added Fusion namespace for PlayerRef
using System.Collections; // Added for coroutines

public class GameUI : MonoBehaviour
{
    // UI References
    private Canvas _gameCanvas;
    private GameObject _handContainer;
    private GameObject _statsPanel;
    private GameObject _opponentsPanel;
    private GameObject _monsterDisplay;
    private GameObject _roundInfoPanel;
    private Button _endTurnButton;
    
    // UI Prefabs
    private GameObject _cardPrefab;
    private GameObject _opponentStatsPrefab;
    
    // References
    private PlayerState _localPlayerState;
    private List<CardDisplay> _cardDisplays = new List<CardDisplay>();
    private Dictionary<PlayerRef, OpponentStatsDisplay> _opponentDisplays = new Dictionary<PlayerRef, OpponentStatsDisplay>();
    
    // Stats Text Elements
    private TMP_Text _healthText;
    private TMP_Text _energyText;
    private TMP_Text _scoreText;
    private TMP_Text _roundText;
    private TMP_Text _turnInfoText;
    
    private MonsterDisplay _playerMonsterDisplay;
    private MonsterDisplay _opponentMonsterDisplay;
    
    private bool _initialized = false;
    private bool _initializationInProgress = false;
    private float _initRetryInterval = 0.5f; // Retry every half second
    private int _maxInitRetries = 10; // Maximum number of retries

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
            // Wait for GameState to be available
            if (GameState.Instance == null)
            {
                GameManager.Instance.LogManager.LogMessage($"GameState not available, retrying ({retryCount+1}/{_maxInitRetries})");
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
            CreateHandArea();
            CreatePlayerStatsPanel();
            CreateOpponentsPanel();
            CreateMonsterDisplays();
            CreateRoundInfoPanel();
            CreateEndTurnButton();
            
            // Subscribe to events
            PlayerState.OnStatsChanged += UpdatePlayerStats;
            PlayerState.OnHandChanged += UpdateHand;
            GameState.OnRoundChanged += UpdateRoundInfo;
            GameState.OnTurnChanged += UpdateTurnInfo;
            
            _initialized = true;
            _initializationInProgress = false;
            GameManager.Instance.LogManager.LogMessage("GameUI fully initialized");
            
            // Hide lobby UI - IMPORTANT: This needs to happen before updating UI
            HideLobbyUI();
            
            // Initial UI update
            UpdateAllUI();
            
            yield break; // Successful initialization, exit coroutine
        }
        
        // If we get here, initialization failed after max retries
        GameManager.Instance.LogManager.LogError("Failed to initialize GameUI after maximum retries");
        _initializationInProgress = false;
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
        
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);
        
        GameManager.Instance.LogManager.LogMessage("Game canvas created");
    }

    private void CreateHandArea()
    {
        // Create the container for the player's hand
        _handContainer = CreateUIPanel("HandContainer", new Vector2(0.5f, 0), new Vector2(0.5f, 0), 
                                       new Vector2(0.1f, 0.01f), new Vector2(0.9f, 0.25f));
        
        // Create card prefab
        _cardPrefab = CreateCardPrefab();
        
        GameManager.Instance.LogManager.LogMessage("Hand area created");
    }

    private GameObject CreateCardPrefab()
    {
        GameObject cardObj = new GameObject("CardPrefab");
        cardObj.SetActive(false);
        
        // Card background
        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.2f, 0.2f, 0.2f);
        
        // Card layout
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(150, 200);
        
        // Add CardDisplay component
        CardDisplay display = cardObj.AddComponent<CardDisplay>();
        
        // Create title text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(cardObj.transform, false);
        TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 14;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.85f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        
        // Create cost text
        GameObject costObj = new GameObject("CostText");
        costObj.transform.SetParent(cardObj.transform, false);
        TMP_Text costText = costObj.AddComponent<TextMeshProUGUI>();
        costText.fontSize = 18;
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
        descObj.transform.SetParent(cardObj.transform, false);
        TMP_Text descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.fontSize = 12;
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
        Button cardButton = cardObj.AddComponent<Button>();
        ColorBlock colors = cardButton.colors;
        colors.highlightedColor = new Color(0.8f, 0.8f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.9f);
        cardButton.colors = colors;
        
        // Set up the button click handler
        display.SetButton(cardButton);
        
        return cardObj;
    }

    private void CreatePlayerStatsPanel()
    {
        // Create stats panel in the top left
        _statsPanel = CreateUIPanel("PlayerStatsPanel", new Vector2(0, 1), new Vector2(0, 1), 
                                   new Vector2(0.01f, 0.8f), new Vector2(0.25f, 0.99f));
        
        // Background
        Image bg = _statsPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Player name - safely get name to avoid errors with networked properties
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
        catch (System.Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Could not access PlayerName: {ex.Message}. Using default name.");
        }
        
        nameText.text = playerName;
        nameText.fontSize = 18;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Center;
        
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.85f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        
        // Health
        GameObject healthObj = CreateStatDisplay(_statsPanel, "Health", "Health: 50/50", 0.7f, 0.85f);
        _healthText = healthObj.GetComponent<TMP_Text>();
        
        // Energy
        GameObject energyObj = CreateStatDisplay(_statsPanel, "Energy", "Energy: 3/3", 0.55f, 0.7f);
        _energyText = energyObj.GetComponent<TMP_Text>();
        
        // Score
        GameObject scoreObj = CreateStatDisplay(_statsPanel, "Score", "Score: 0", 0.4f, 0.55f);
        _scoreText = scoreObj.GetComponent<TMP_Text>();
        
        GameManager.Instance.LogManager.LogMessage("Player stats panel created");
    }

    private GameObject CreateStatDisplay(GameObject parent, string name, string defaultText, float minY, float maxY)
    {
        GameObject statObj = new GameObject(name);
        statObj.transform.SetParent(parent.transform, false);
        TMP_Text statText = statObj.AddComponent<TextMeshProUGUI>();
        statText.text = defaultText;
        statText.fontSize = 16;
        statText.color = Color.white;
        statText.alignment = TextAlignmentOptions.Left;
        
        RectTransform statRect = statObj.GetComponent<RectTransform>();
        statRect.anchorMin = new Vector2(0.05f, minY);
        statRect.anchorMax = new Vector2(0.95f, maxY);
        statRect.offsetMin = Vector2.zero;
        statRect.offsetMax = Vector2.zero;
        
        return statObj;
    }

    private void CreateOpponentsPanel()
    {
        // Create panel on the right for opponents
        _opponentsPanel = CreateUIPanel("OpponentsPanel", new Vector2(1, 0.5f), new Vector2(1, 0.5f), 
                                       new Vector2(0.85f, 0.2f), new Vector2(0.99f, 0.8f));
        
        // Background
        Image bg = _opponentsPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Add vertical layout
        VerticalLayoutGroup layout = _opponentsPanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(5, 5, 5, 5);
        
        // Create opponent stats prefab
        _opponentStatsPrefab = CreateOpponentStatsPrefab();
        
        // Add all opponents
        UpdateOpponentDisplays();
        
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
        rect.sizeDelta = new Vector2(0, 80);
        
        // Add layout element for sizing
        LayoutElement layoutElement = opponentObj.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 80;
        layoutElement.flexibleWidth = 1;
        
        // Add stats display component
        OpponentStatsDisplay display = opponentObj.AddComponent<OpponentStatsDisplay>();
        
        // Name
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(opponentObj.transform, false);
        TMP_Text nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 16;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.7f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        
        // Health
        GameObject healthObj = new GameObject("Health");
        healthObj.transform.SetParent(opponentObj.transform, false);
        TMP_Text healthText = healthObj.AddComponent<TextMeshProUGUI>();
        healthText.fontSize = 14;
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
        scoreText.fontSize = 14;
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

    private void CreateMonsterDisplays()
    {
        // Create area for monster display in center
        _monsterDisplay = CreateUIPanel("MonsterDisplay", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 
                                       new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.7f));
        
        // Create player's monster
        GameObject playerMonsterObj = new GameObject("PlayerMonster");
        playerMonsterObj.transform.SetParent(_monsterDisplay.transform, false);
        _playerMonsterDisplay = playerMonsterObj.AddComponent<MonsterDisplay>();
        
        RectTransform playerMonsterRect = playerMonsterObj.GetComponent<RectTransform>();
        playerMonsterRect.anchorMin = new Vector2(0, 0);
        playerMonsterRect.anchorMax = new Vector2(0.45f, 1);
        playerMonsterRect.offsetMin = Vector2.zero;
        playerMonsterRect.offsetMax = Vector2.zero;
        
        // Create opponent's monster
        GameObject opponentMonsterObj = new GameObject("OpponentMonster");
        opponentMonsterObj.transform.SetParent(_monsterDisplay.transform, false);
        _opponentMonsterDisplay = opponentMonsterObj.AddComponent<MonsterDisplay>();
        
        RectTransform opponentMonsterRect = opponentMonsterObj.GetComponent<RectTransform>();
        opponentMonsterRect.anchorMin = new Vector2(0.55f, 0);
        opponentMonsterRect.anchorMax = new Vector2(1, 1);
        opponentMonsterRect.offsetMin = Vector2.zero;
        opponentMonsterRect.offsetMax = Vector2.zero;
        
        // Create VS text in middle
        GameObject vsObj = new GameObject("VS");
        vsObj.transform.SetParent(_monsterDisplay.transform, false);
        TMP_Text vsText = vsObj.AddComponent<TextMeshProUGUI>();
        vsText.text = "VS";
        vsText.fontSize = 24;
        vsText.alignment = TextAlignmentOptions.Center;
        vsText.color = Color.yellow;
        
        RectTransform vsRect = vsObj.GetComponent<RectTransform>();
        vsRect.anchorMin = new Vector2(0.45f, 0.4f);
        vsRect.anchorMax = new Vector2(0.55f, 0.6f);
        vsRect.offsetMin = Vector2.zero;
        vsRect.offsetMax = Vector2.zero;
        
        // Initialize the monster displays with default monsters (to be safe)
        UpdateMonsterDisplays();
        
        GameManager.Instance.LogManager.LogMessage("Monster displays created");
    }

    private void CreateRoundInfoPanel()
    {
        // Create round info panel at top center
        _roundInfoPanel = CreateUIPanel("RoundInfoPanel", new Vector2(0.5f, 1), new Vector2(0.5f, 1), 
                                        new Vector2(0.3f, 0.9f), new Vector2(0.7f, 0.99f));
        
        // Background
        Image bg = _roundInfoPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Round text
        GameObject roundObj = new GameObject("RoundText");
        roundObj.transform.SetParent(_roundInfoPanel.transform, false);
        _roundText = roundObj.AddComponent<TextMeshProUGUI>();
        _roundText.text = "Round 1";
        _roundText.fontSize = 18;
        _roundText.alignment = TextAlignmentOptions.Center;
        _roundText.color = Color.white;
        
        RectTransform roundRect = roundObj.GetComponent<RectTransform>();
        roundRect.anchorMin = new Vector2(0, 0.5f);
        roundRect.anchorMax = new Vector2(0.5f, 1);
        roundRect.offsetMin = Vector2.zero;
        roundRect.offsetMax = Vector2.zero;
        
        // Turn info text
        GameObject turnObj = new GameObject("TurnInfoText");
        turnObj.transform.SetParent(_roundInfoPanel.transform, false);
        _turnInfoText = turnObj.AddComponent<TextMeshProUGUI>();
        _turnInfoText.text = "Your Turn";
        _turnInfoText.fontSize = 18;
        _turnInfoText.alignment = TextAlignmentOptions.Center;
        _turnInfoText.color = Color.green;
        
        RectTransform turnRect = turnObj.GetComponent<RectTransform>();
        turnRect.anchorMin = new Vector2(0.5f, 0.5f);
        turnRect.anchorMax = new Vector2(1, 1);
        turnRect.offsetMin = Vector2.zero;
        turnRect.offsetMax = Vector2.zero;
        
        GameManager.Instance.LogManager.LogMessage("Round info panel created");
    }

    private void CreateEndTurnButton()
    {
        // Create end turn button at bottom right
        GameObject buttonObj = new GameObject("EndTurnButton");
        buttonObj.transform.SetParent(_gameCanvas.transform, false);
        _endTurnButton = buttonObj.AddComponent<Button>();
        
        // Image
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.7f, 0.2f, 0.2f);
        
        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TMP_Text buttonText = textObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "END TURN";
        buttonText.fontSize = 16;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.white;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Position
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.9f, 0.01f);
        buttonRect.anchorMax = new Vector2(0.99f, 0.1f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        
        // Event
        _endTurnButton.onClick.AddListener(OnEndTurnClicked);
        
        GameManager.Instance.LogManager.LogMessage("End turn button created");
    }

    private GameObject CreateUIPanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(_gameCanvas.transform, false);
        
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(offsetMin.x * Screen.width, offsetMin.y * Screen.height);
        rect.offsetMax = new Vector2(offsetMax.x * Screen.width - Screen.width, offsetMax.y * Screen.height - Screen.height);
        
        return panel;
    }

    private void HideLobbyUI()
    {
        // Hide the lobby UI elements when the game starts
        var uiManager = GameManager.Instance.UIManager;
        if (uiManager != null)
        {
            // FIXED: Don't call ShowConnectUI, instead directly hide both panels
            // Find the canvas
            Transform canvas = uiManager.transform.Find("UI Canvas");
            if (canvas != null)
            {
                Transform lobbyPanel = canvas.Find("Lobby Panel");
                if (lobbyPanel != null)
                {
                    lobbyPanel.gameObject.SetActive(false);
                    GameManager.Instance.LogManager.LogMessage("Lobby panel hidden");
                }
                
                Transform connectPanel = canvas.Find("Connect Panel");
                if (connectPanel != null)
                {
                    connectPanel.gameObject.SetActive(false);
                    GameManager.Instance.LogManager.LogMessage("Connect panel hidden");
                }
                
                // Also hide game started panel if it exists
                Transform gameStartedPanel = canvas.Find("Game Started Panel");
                if (gameStartedPanel != null)
                {
                    gameStartedPanel.gameObject.SetActive(false);
                    GameManager.Instance.LogManager.LogMessage("Game started panel hidden");
                }
            }
            else
            {
                GameManager.Instance.LogManager.LogError("UI Canvas not found!");
            }
            
            GameManager.Instance.LogManager.LogMessage("Lobby UI hidden");
        }
    }

    private void UpdateAllUI()
    {
        try {
            // Update all UI elements
            UpdatePlayerStats(_localPlayerState);
            UpdateHand(_localPlayerState, _localPlayerState.GetHand());
            UpdateOpponentDisplays();
            UpdateMonsterDisplays();
            
            if (GameState.Instance != null) {
                UpdateRoundInfo(GameState.Instance.CurrentRound);
                UpdateTurnInfo(GameState.Instance.CurrentTurnPlayerIndex);
            }
        }
        catch (System.Exception ex) {
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
        catch (System.Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error updating player stats: {ex.Message}");
            
            // Default values
            if (_healthText != null) _healthText.text = "Health: 50/50";
            if (_energyText != null) _energyText.text = "Energy: 3/3";
            if (_scoreText != null) _scoreText.text = "Score: 0";
        }
    }

    private void UpdateHand(PlayerState playerState, List<CardData> hand)
    {
        if (playerState != _localPlayerState) return;
        
        // Clear existing cards
        foreach (var display in _cardDisplays)
        {
            if (display != null && display.gameObject != null)
            {
                Destroy(display.gameObject);
            }
        }
        _cardDisplays.Clear();
        
        // No cards in hand
        if (hand == null || hand.Count == 0) return;
        
        // Calculate card layout
        float cardWidth = 160f;
        float spacing = 10f;
        float totalWidth = hand.Count * cardWidth + (hand.Count - 1) * spacing;
        float startX = -totalWidth / 2;
        
        // Create card displays
        for (int i = 0; i < hand.Count; i++)
        {
            // Create card instance
            GameObject cardObj = Instantiate(_cardPrefab, _handContainer.transform);
            cardObj.SetActive(true);
            
            // Position card
            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), 0);
            
            // Set card data
            CardDisplay display = cardObj.GetComponent<CardDisplay>();
            display.SetCardData(hand[i], i);
            
            // Add to list
            _cardDisplays.Add(display);
            
            // Add click handler
            display.CardClicked += OnCardClicked;
        }
        
        GameManager.Instance.LogManager.LogMessage($"Updated hand with {hand.Count} cards");
    }

    private void UpdateOpponentDisplays()
    {
        try {
            // Get all player states
            var playerStates = GameState.Instance?.GetAllPlayerStates();
            if (playerStates == null) return;
            
            PlayerRef localPlayerRef = GameState.Instance.GetLocalPlayerRef();
            
            // Clear existing displays
            foreach (var display in _opponentDisplays.Values)
            {
                if (display != null && display.gameObject != null)
                {
                    Destroy(display.gameObject);
                }
            }
            _opponentDisplays.Clear();
            
            // Create display for each opponent
            foreach (var entry in playerStates)
            {
                if (entry.Key != localPlayerRef)
                {
                    // Create opponent display
                    GameObject opponentObj = Instantiate(_opponentStatsPrefab, _opponentsPanel.transform);
                    opponentObj.SetActive(true);
                    
                    // Set data
                    OpponentStatsDisplay display = opponentObj.GetComponent<OpponentStatsDisplay>();
                    display.UpdateDisplay(entry.Value);
                    
                    // Add to dictionary
                    _opponentDisplays.Add(entry.Key, display);
                }
            }
            
            GameManager.Instance.LogManager.LogMessage($"Updated opponent displays for {_opponentDisplays.Count} opponents");
        }
        catch (System.Exception ex) {
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
            } catch (System.Exception) { }
            
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
            
            // Update opponent monster - similar approach with null checking
            Monster opponentMonster = null;
            try {
                opponentMonster = _localPlayerState.GetOpponentMonster();
            } catch (System.Exception) { }
            
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
        catch (System.Exception ex) {
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
        catch (System.Exception ex) {
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
        catch (System.Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error playing card: {ex.Message}");
        }
    }

    private void OnEndTurnClicked()
    {
        if (GameState.Instance == null || !GameState.Instance.IsLocalPlayerTurn()) return;
        
        try {
            _localPlayerState.EndTurn();
            GameManager.Instance.LogManager.LogMessage("Player ended turn");
        }
        catch (System.Exception ex) {
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
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        PlayerState.OnStatsChanged -= UpdatePlayerStats;
        PlayerState.OnHandChanged -= UpdateHand;
        GameState.OnRoundChanged -= UpdateRoundInfo;
        GameState.OnTurnChanged -= UpdateTurnInfo;
    }
}