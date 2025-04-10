using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    // Core UI components
    private Canvas _gameCanvas;
    
    // UI controllers
    private GameUILayout _layoutManager;
    private PlayerUIController _playerUIController;
    private BattleUIController _battleUIController;
    private HandUIController _handUIController;
    private TurnInfoUIController _turnInfoUIController;
    private OpponentsUIController _opponentsUIController;
    
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
            
            // Create game canvas
            CreateGameCanvas();
            
            // Initialize layout manager
            _layoutManager = new GameUILayout(_gameCanvas.transform);
            
            // Initialize UI controllers
            _playerUIController = new PlayerUIController(_layoutManager.GetStatsPanel(), _localPlayerState);
            _battleUIController = new BattleUIController(_layoutManager.GetBattlePanel(), _layoutManager.GetPlayerMonsterPanel(), _localPlayerState);
            _handUIController = new HandUIController(_layoutManager.GetHandPanel(), _gameCanvas, _localPlayerState);
            _turnInfoUIController = new TurnInfoUIController(_layoutManager.GetTurnInfoPanel(), _localPlayerState);
            _opponentsUIController = new OpponentsUIController(_layoutManager.GetOpponentsPanel());
            
            // Subscribe to events
            SubscribeToEvents();
            
            // Wait a bit before the initial UI update to allow for network sync
            yield return new WaitForSeconds(0.5f);
            
            // Initial UI update
            UpdateAllUI();
            
            // Hide lobby UI
            HideLobbyUI();
            
            _initialized = true;
            _initializationInProgress = false;
            GameManager.Instance.LogManager.LogMessage("GameUI fully initialized");
            
            yield break; // Successful initialization, exit coroutine
        }
        
        // If we get here, initialization failed after max retries
        GameManager.Instance.LogManager.LogError("Failed to initialize GameUI after maximum retries");
        _initializationInProgress = false;
    }

    private void CreateGameCanvas()
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
        
        GameManager.Instance.LogManager.LogMessage("Game canvas created");
    }
    
    private void SubscribeToEvents()
    {
        // Subscribe to PlayerState events
        PlayerState.OnStatsChanged += HandleStatsChanged;
        PlayerState.OnHandChanged += HandleHandChanged;
        
        // Subscribe to GameState events
        GameState.OnRoundChanged += HandleRoundChanged;
        GameState.OnPlayerTurnChanged += HandlePlayerTurnChanged;
        
        // Subscribe to GameState player registration events
        GameState.OnPlayerStateAdded += HandlePlayerStateAdded;
        GameState.OnPlayerStateRemoved += HandlePlayerStateRemoved;
        
        // Subscribe to internal controller events
        _turnInfoUIController.OnEndTurnClicked += HandleEndTurnClicked;
        _handUIController.OnCardPlayed += HandleCardPlayed;
        
        GameManager.Instance.LogManager.LogMessage("GameUI subscribed to all events");
    }
    
    private void HandleStatsChanged(PlayerState playerState)
    {
        // Update appropriate UI based on which player's stats changed
        if (playerState == _localPlayerState)
        {
            _playerUIController.UpdateStats(playerState);
            _battleUIController.UpdatePlayerMonsterDisplay(playerState.GetMonster());
        }
        else
        {
            _opponentsUIController.UpdateOpponentStats(playerState);
            _battleUIController.UpdateOpponentMonsterDisplay(playerState.GetMonster());
        }
    }
    
    private void HandleHandChanged(PlayerState playerState, System.Collections.Generic.List<CardData> hand)
    {
        if (playerState == _localPlayerState)
        {
            _handUIController.UpdateHand(hand);
        }
    }
    
    private void HandleRoundChanged(int round)
    {
        _turnInfoUIController.UpdateRoundInfo(round);
    }
    
    private void HandlePlayerTurnChanged(Fusion.PlayerRef player, bool isPlayerTurn)
    {
        // Only update UI if this is the local player
        if (GameState.Instance != null && player == GameState.Instance.GetLocalPlayerRef())
        {
            _turnInfoUIController.UpdateTurnState(isPlayerTurn);
            _handUIController.SetCardsInteractable(isPlayerTurn);
        }
    }
    
    private void HandlePlayerStateAdded(Fusion.PlayerRef playerRef, PlayerState playerState)
    {
        if (playerRef != GameState.Instance.GetLocalPlayerRef())
        {
            _opponentsUIController.AddOpponentDisplay(playerRef, playerState);
        }
    }
    
    private void HandlePlayerStateRemoved(Fusion.PlayerRef playerRef, PlayerState playerState)
    {
        _opponentsUIController.RemoveOpponentDisplay(playerRef);
    }
    
    private void HandleEndTurnClicked()
    {
        if (_localPlayerState != null)
        {
            _localPlayerState.EndTurn();
        }
    }
    
    private void HandleCardPlayed(CardDisplay display, GameObject target)
    {
        if (_localPlayerState == null) return;
        
        CardData card = display.GetCardData();
        int cardIndex = display.GetCardIndex();
        
        // Find the monster target
        MonsterDisplay monsterDisplay = target?.GetComponent<MonsterDisplay>();
        if (monsterDisplay != null)
        {
            Monster targetMonster = null;
            
            if (monsterDisplay.IsPlayerMonster())
            {
                targetMonster = _localPlayerState.GetMonster();
            }
            else
            {
                targetMonster = _localPlayerState.GetOpponentMonster();
            }
            
            if (targetMonster != null)
            {
                _localPlayerState.PlayCard(cardIndex, targetMonster);
                
                // Play visual effect
                _battleUIController.PlayCardEffect(card, target);
            }
        }
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
            }
            catch (System.Exception ex)
            {
                GameManager.Instance.LogManager.LogError($"Error hiding lobby UI: {ex.Message}");
            }
        }
    }
    
    private void UpdateAllUI()
    {
        if (!_initialized) return;
        
        try {
            // Update all UI components
            _playerUIController.UpdateStats(_localPlayerState);
            _battleUIController.UpdatePlayerMonsterDisplay(_localPlayerState.GetMonster());
            _battleUIController.UpdateOpponentMonsterDisplay(_localPlayerState.GetOpponentMonster());
            _handUIController.UpdateHand(_localPlayerState.GetHand());
            _opponentsUIController.UpdateAllOpponents();
            
            // Update turn and round info
            if (GameState.Instance != null && GameState.Instance.IsSpawned())
            {
                _turnInfoUIController.UpdateRoundInfo(GameState.Instance.GetCurrentRound());
                _turnInfoUIController.UpdateTurnState(GameState.Instance.IsLocalPlayerTurn());
            }
        }
        catch (System.Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error updating UI: {ex.Message}");
        }
    }
    
    // Call this each frame to ensure the UI stays updated
    private void Update()
    {
        if (!_initialized && !_initializationInProgress) 
        {
            Initialize();
        }
        
        // Periodically update opponent displays as a fallback
        if (_initialized && Time.frameCount % 120 == 0) // Update every 120 frames (~2 seconds)
        {
            _opponentsUIController.UpdateAllOpponents();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        PlayerState.OnStatsChanged -= HandleStatsChanged;
        PlayerState.OnHandChanged -= HandleHandChanged;
        GameState.OnRoundChanged -= HandleRoundChanged;
        GameState.OnPlayerTurnChanged -= HandlePlayerTurnChanged;
        
        // Unsubscribe from player state events
        if (GameState.Instance != null)
        {
            GameState.OnPlayerStateAdded -= HandlePlayerStateAdded;
            GameState.OnPlayerStateRemoved -= HandlePlayerStateRemoved;
        }
        
        // Unsubscribe from internal controller events
        if (_turnInfoUIController != null)
        {
            _turnInfoUIController.OnEndTurnClicked -= HandleEndTurnClicked;
        }
        
        if (_handUIController != null)
        {
            _handUIController.OnCardPlayed -= HandleCardPlayed;
        }
    }
}