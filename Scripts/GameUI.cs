using System;
using System.Collections;
using System.Collections.Generic; // Added for List<>
using UnityEngine;
using UnityEngine.UI;
using Fusion; // Added for PlayerRef

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
    private PlayerState _localPlayerState; // Cached reference to the local player's state

    // Initialization state
    private bool _initialized = false;
    private bool _initializationInProgress = false;
    private float _initRetryInterval = 0.5f;
    private int _maxInitRetries = 20;

    // Flag to prevent double cleanup
    private bool _cleanedUp = false;


    // Called by GameInitializer
    public void Initialize()
    {
        if (_initialized || _initializationInProgress) return;
        GameManager.Instance?.LogManager?.LogMessage("Starting GameUI initialization...");
        StartCoroutine(InitializeWithRetry());
    }

    private IEnumerator InitializeWithRetry()
    {
        _initializationInProgress = true;
        int retryCount = 0;

        while (retryCount < _maxInitRetries)
        {
            // *** FIXED: Access Runner via NetworkManager ***
            NetworkRunner runner = GameManager.Instance?.NetworkManager?.GetRunner();

            // Wait for GameState and NetworkRunner
            // Check GameState first as PlayerState depends on it
            if (GameState.Instance == null || !GameState.Instance.IsSpawned() || runner == null || !runner.IsRunning)
            {
                // GameManager.Instance?.LogManager?.LogMessage($"GameUI Init: Waiting for GameState/Runner ({retryCount+1}/{_maxInitRetries})");
                retryCount++;
                yield return new WaitForSeconds(_initRetryInterval);
                continue;
            }

            // Get local player state using GameState instance (which requires a runner)
            _localPlayerState = GameState.Instance.GetLocalPlayerState();
            if (_localPlayerState == null)
            {
                // GameManager.Instance?.LogManager?.LogMessage($"GameUI Init: LocalPlayerState not available yet, retrying ({retryCount+1}/{_maxInitRetries})");
                retryCount++;
                yield return new WaitForSeconds(_initRetryInterval);
                continue;
            }

            // --- Initialization successful ---
            GameManager.Instance?.LogManager?.LogMessage("GameUI: GameState and LocalPlayerState available, proceeding with initialization.");

            // Create game canvas and layout
            CreateGameCanvas();
             if (_gameCanvas == null) // Ensure canvas was created/found
             {
                 GameManager.Instance?.LogManager?.LogError("GameUI: Failed to create or find game canvas. Aborting initialization.");
                 _initializationInProgress = false;
                 yield break;
             }
            _layoutManager = new GameUILayout(_gameCanvas.transform);

            // Initialize UI controllers with required references
            _playerUIController = new PlayerUIController(_layoutManager.GetStatsPanel(), _localPlayerState);
            _battleUIController = new BattleUIController(_layoutManager.GetBattlePanel(), _layoutManager.GetPlayerMonsterPanel(), _localPlayerState);
            _handUIController = new HandUIController(_layoutManager.GetHandPanel(), _gameCanvas, _localPlayerState);
            _turnInfoUIController = new TurnInfoUIController(_layoutManager.GetTurnInfoPanel(), _localPlayerState);
            _opponentsUIController = new OpponentsUIController(_layoutManager.GetOpponentsPanel());

            // Subscribe to necessary events
            SubscribeToEvents();

            // Wait briefly before the first full UI update
            yield return new WaitForSeconds(0.2f);

            // Initial UI update based on current state
            UpdateAllUI();

            // Hide lobby UI elements managed by UIManager
            HideLobbyUI();

            _initialized = true;
            _initializationInProgress = false;
            GameManager.Instance?.LogManager?.LogMessage("GameUI fully initialized and Lobby UI hidden.");

            yield break; // Successful initialization, exit coroutine
        }

        // If we get here, initialization failed after max retries
        GameManager.Instance?.LogManager?.LogError("Failed to initialize GameUI after maximum retries. Check GameState and PlayerState spawning.");
        _initializationInProgress = false;
    }

    private void CreateGameCanvas()
    {
        // Check if a canvas already exists (e.g., from UIManager)
        Canvas existingCanvas = FindObjectOfType<Canvas>();
        if (existingCanvas != null && existingCanvas.renderMode == RenderMode.ScreenSpaceOverlay) // Accept any ScreenSpace Overlay canvas
        {
            _gameCanvas = existingCanvas;
            // GameManager.Instance?.LogManager?.LogMessage("GameUI: Using existing ScreenSpace Overlay Canvas.");
        }
        else
        {
            // Create main canvas for game UI if none suitable exists
            GameObject canvasObj = new GameObject("GameUICanvas"); // Specific name
            _gameCanvas = canvasObj.AddComponent<Canvas>();
            _gameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _gameCanvas.sortingOrder = 10; // Ensure it's above other UI if needed

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>(); // Essential for UI interactions

            // Make sure it persists if created dynamically here
            // Only call DontDestroyOnLoad if this script manages its lifecycle fully
             // DontDestroyOnLoad(canvasObj);
             // GameManager.Instance?.LogManager?.LogMessage("GameUI: Created new Game canvas.");
        }

    }

    private void SubscribeToEvents()
    {
        // Subscribe to PlayerState LOCAL events for the local player
        PlayerState.OnStatsChanged += HandlePlayerStatsChanged;
        PlayerState.OnHandChanged += HandleLocalHandChanged;
        PlayerState.OnPlayerMonsterChanged += HandlePlayerMonsterChanged;
        PlayerState.OnOpponentMonsterChanged += HandleOpponentMonsterChanged;
        PlayerState.OnLocalTurnStateChanged += HandleLocalTurnStateChanged;
        PlayerState.OnLocalFightOver += HandleLocalFightOver;


        // Subscribe to GameState global events
        GameState.OnRoundChanged += HandleRoundChanged;
        GameState.OnPlayerStateAdded += HandlePlayerStateAdded; // For opponent UI
        GameState.OnPlayerStateRemoved += HandlePlayerStateRemoved; // For opponent UI
        GameState.OnPlayerFightCompletionUpdated += HandlePlayerFightCompletionUpdated; // For opponent UI status


        // Subscribe to events from our OWN UI controllers
        if (_turnInfoUIController != null)
        {
            _turnInfoUIController.OnEndTurnClicked += HandleEndTurnClicked;
        }
        if (_handUIController != null)
        {
            _handUIController.OnCardPlayedRequested += HandleCardPlayedRequested;
        }

        // GameManager.Instance?.LogManager?.LogMessage("GameUI subscribed to events.");
    }

    private void UnsubscribeFromEvents()
    {
        PlayerState.OnStatsChanged -= HandlePlayerStatsChanged;
        PlayerState.OnHandChanged -= HandleLocalHandChanged;
        PlayerState.OnPlayerMonsterChanged -= HandlePlayerMonsterChanged;
        PlayerState.OnOpponentMonsterChanged -= HandleOpponentMonsterChanged;
        PlayerState.OnLocalTurnStateChanged -= HandleLocalTurnStateChanged;
        PlayerState.OnLocalFightOver -= HandleLocalFightOver;

        // Check GameState exists before unsubscribing
        if (GameState.Instance != null)
        {
            GameState.OnRoundChanged -= HandleRoundChanged;
            GameState.OnPlayerStateAdded -= HandlePlayerStateAdded;
            GameState.OnPlayerStateRemoved -= HandlePlayerStateRemoved;
            GameState.OnPlayerFightCompletionUpdated -= HandlePlayerFightCompletionUpdated;
        }


        if (_turnInfoUIController != null)
        {
            _turnInfoUIController.OnEndTurnClicked -= HandleEndTurnClicked;
        }
        if (_handUIController != null)
        {
            _handUIController.OnCardPlayedRequested -= HandleCardPlayedRequested;
        }
        // GameManager.Instance?.LogManager?.LogMessage("GameUI unsubscribed from events.");
    }

    // --- Event Handlers ---

    private void HandlePlayerStatsChanged(PlayerState playerState)
    {
        if (!_initialized || playerState == null) return;
        // Update UI only if the change is for the local player this UI represents OR an opponent
        if (playerState == _localPlayerState)
        {
            _playerUIController?.UpdateStats(playerState);
        }
        else
        {
            _opponentsUIController?.UpdateOpponentStats(playerState);
        }
    }

    private void HandleLocalHandChanged(PlayerState playerState, List<CardData> hand)
    {
        if (!_initialized) return;
        if (playerState == _localPlayerState) // Ensure it's for the local player
        {
            _handUIController?.UpdateHand(hand);
        }
    }

    private void HandlePlayerMonsterChanged(Monster monster)
    {
        if (!_initialized) return;
        _battleUIController?.UpdatePlayerMonsterDisplay(monster);
         if(_localPlayerState != null) _playerUIController?.UpdateStats(_localPlayerState); // Update general stats if monster affects them
    }

    private void HandleOpponentMonsterChanged(Monster opponentMonster)
    {
        if (!_initialized) return;
        _battleUIController?.UpdateOpponentMonsterDisplay(opponentMonster);
    }

    private void HandleLocalTurnStateChanged(bool isPlayerTurnActive)
    {
        if (!_initialized) return;
        _turnInfoUIController?.UpdateTurnState(isPlayerTurnActive);
        _handUIController?.SetCardsInteractable(isPlayerTurnActive);
    }

    private void HandleLocalFightOver(bool isFightOver)
    {
        if (!_initialized) return;
        if(isFightOver && _turnInfoUIController != null)
        {
            _turnInfoUIController.UpdateTurnState(false); // Show inactive state
            // TODO: Update turn info text to "Fight Over" or similar via TurnInfoUIController method
        }
    }

    private void HandleRoundChanged(int round)
    {
        if (!_initialized) return;
        _turnInfoUIController?.UpdateRoundInfo(round);
        _battleUIController?.ResetCachedHealth(); // Reset visual health tracking for new opponent
    }

    private void HandlePlayerStateAdded(PlayerRef playerRef, PlayerState playerState)
    {
        if (!_initialized || playerState == null) return;
        // Add to opponent display if it's not the local player
        if (playerRef != _localPlayerState?.Object?.InputAuthority)
        {
            _opponentsUIController?.AddOpponentDisplay(playerRef, playerState);
        }
    }

    private void HandlePlayerStateRemoved(PlayerRef playerRef, PlayerState playerState)
    {
        if (!_initialized) return;
        // Remove from opponent display if it's not the local player
        // (Technically removing local player state might error elsewhere, but OpponentsUI should handle it)
        _opponentsUIController?.RemoveOpponentDisplay(playerRef);
    }

    // Handles updates to any player's fight completion status
    private void HandlePlayerFightCompletionUpdated(PlayerRef playerRef, bool isComplete)
    {
        if (!_initialized) return;
        // Update opponent UI to show waiting status, etc.
        // *** Assuming OpponentsUIController will have this method added ***
        _opponentsUIController?.UpdateOpponentFightStatus(playerRef, isComplete);
    }


    // Handles the local "End Turn" button click
    private void HandleEndTurnClicked()
    {
        if (!_initialized || _localPlayerState == null) return;
        _localPlayerState.EndPlayerTurnLocally();
    }

    // Handles the event from HandUIController when a card is dropped on a target
    private void HandleCardPlayedRequested(CardDisplay display, GameObject target)
    {
        if (!_initialized || _localPlayerState == null || display == null || target == null) return;

        CardData card = display.GetCardData();
        int cardIndex = display.GetCardIndex();
        MonsterDisplay monsterDisplay = target.GetComponent<MonsterDisplay>();
        Monster targetMonster = monsterDisplay?.GetMonster(); // Get the LOCAL monster representation

        if (monsterDisplay == null) // Target wasn't a monster display
        {
             // If card targets self and wasn't dropped on own monster, target self implicitly
             if(card.Target == CardTarget.Self)
             {
                  targetMonster = _localPlayerState.GetMonster();
             }
        }

        if (targetMonster != null)
        {
            _localPlayerState.PlayCardLocally(cardIndex, targetMonster);
            _battleUIController?.PlayCardEffect(card, target); // Play visual effect immediately
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"GameUI: Could not determine valid monster target for card {card.Name}. Play cancelled."); }
    }

    // Hides UI elements managed by UIManager (Connect/Lobby)
    private void HideLobbyUI()
    {
        GameManager.Instance?.UIManager?.HideAllUI();
    }

    // Updates all major UI components based on the current local player state
    private void UpdateAllUI()
    {
        if (!_initialized || _localPlayerState == null) return;
        try
        {
            _playerUIController?.UpdateStats(_localPlayerState);
            _battleUIController?.UpdatePlayerMonsterDisplay(_localPlayerState.GetMonster());
            _battleUIController?.UpdateOpponentMonsterDisplay(_localPlayerState.GetOpponentMonster());
            _handUIController?.UpdateHand(_localPlayerState.GetHand());
            _opponentsUIController?.UpdateAllOpponents();

            if (GameState.Instance != null && GameState.Instance.IsSpawned())
            {
                _turnInfoUIController?.UpdateRoundInfo(GameState.Instance.GetCurrentRound());
                _turnInfoUIController?.UpdateTurnState(_localPlayerState.GetIsLocalPlayerTurn());
            }
            else // Fallback if GameState not ready
            {
                _turnInfoUIController?.UpdateRoundInfo(1);
                _turnInfoUIController?.UpdateTurnState(false);
            }
        }
        catch (System.Exception ex) { GameManager.Instance?.LogManager?.LogError($"Error updating UI in UpdateAllUI: {ex.Message}\n{ex.StackTrace}"); }
    }

    private void Update()
    {
        if (!_initialized && !_initializationInProgress && GameManager.Instance != null)
        {
            if (GameState.Instance != null && GameState.Instance.IsSpawned()) Initialize();
        }
    }

    public BattleUIController GetBattleUIController() => _battleUIController;

    private void OnDestroy()
    {
        if (!_cleanedUp)
        {
            // GameManager.Instance?.LogManager?.LogMessage("GameUI OnDestroy called. Cleaning up...");
            UnsubscribeFromEvents();
            _handUIController?.Cleanup(); // Call cleanup on HandUIController
            _cleanedUp = true;
        }
    }
}