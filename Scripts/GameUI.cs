using System;
using System.Collections;
using System.Collections.Generic; // Added for List<>
using UnityEngine;
using UnityEngine.UI;
using Fusion; // Added for PlayerRef

public class GameUI : MonoBehaviour // GameUI should be a MonoBehaviour to manage its own lifecycle and coroutines
{
    // Core UI components
    private Canvas _gameCanvas; // Canvas specifically for Game UI elements

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
        // This method is called by GameInitializer, which is a MonoBehaviour.
        // GameUI itself should also be a MonoBehaviour to handle its UI elements properly.
        if (_initialized || _initializationInProgress) return;
        GameManager.Instance?.LogManager?.LogMessage("Starting GameUI initialization...");
        StartCoroutine(InitializeWithRetry()); // Start coroutine on this MonoBehaviour
    }

    private IEnumerator InitializeWithRetry()
    {
        _initializationInProgress = true;
        int retryCount = 0;

        while (retryCount < _maxInitRetries)
        {
            // Access Runner via NetworkManager
            NetworkRunner runner = GameManager.Instance?.NetworkManager?.GetRunner();
            // Wait for GameState and NetworkRunner
            if (GameState.Instance == null || !GameState.Instance.IsSpawned() || runner == null || !runner.IsRunning)
            {
                // GameManager.Instance?.LogManager?.LogMessage($"GameUI Init: Waiting for GameState/Runner ({retryCount+1}/{_maxInitRetries})");
                retryCount++;
                yield return new WaitForSeconds(_initRetryInterval);
                continue;
            }

            // Get local player state using GameState instance
            _localPlayerState = GameState.Instance.GetLocalPlayerState();
            if (_localPlayerState == null || !_localPlayerState.Object.IsValid) // Also check if state object is valid
            {
                // GameManager.Instance?.LogManager?.LogMessage($"GameUI Init: LocalPlayerState not available/valid yet, retrying ({retryCount+1}/{_maxInitRetries})");
                retryCount++;
                yield return new WaitForSeconds(_initRetryInterval);
                continue;
            }

            // --- Initialization successful ---
            GameManager.Instance?.LogManager?.LogMessage("GameUI: GameState and LocalPlayerState available, proceeding with initialization.");

            // Create or find the game canvas *FIRST*
            CreateGameCanvas();
            if (_gameCanvas == null)
             {
                 GameManager.Instance?.LogManager?.LogError("GameUI: Failed to create or find game canvas. Aborting initialization.");
                 _initializationInProgress = false;
                 yield break; // Stop if canvas fails
             }

            // Create layout manager using the game canvas transform
            _layoutManager = new GameUILayout(_gameCanvas.transform);

            // Initialize UI controllers with required references, parenting them under the correct layout panels
            _playerUIController = new PlayerUIController(_layoutManager.GetStatsPanel(), _localPlayerState);
            _battleUIController = new BattleUIController(_layoutManager.GetBattlePanel(), _layoutManager.GetPlayerMonsterPanel(), _localPlayerState);
            _handUIController = new HandUIController(_layoutManager.GetHandPanel(), _gameCanvas, _localPlayerState); // Hand needs main canvas for drag overlay
            _turnInfoUIController = new TurnInfoUIController(_layoutManager.GetTurnInfoPanel(), _localPlayerState);
            _opponentsUIController = new OpponentsUIController(_layoutManager.GetOpponentsPanel());

            // Subscribe to necessary events
            SubscribeToEvents();

            // Initial UI update based on current state
            UpdateAllUI();

            // Hide lobby UI elements managed by UIManager
            HideLobbyUI(); // Call the method to hide UIManager's elements

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
        // 1. Check if we already have a reference from a previous initialization
        if (_gameCanvas != null && _gameCanvas.gameObject != null)
        {
            GameManager.Instance.LogManager.LogMessage("GameUI reusing existing _gameCanvas reference.");
             _gameCanvas.gameObject.SetActive(true); // Ensure it's active
            return;
        }

        // 2. If no reference, try finding an *existing* GameObject named "GameUICanvas"
        GameObject existingCanvasObj = GameObject.Find("GameUICanvas");
        if (existingCanvasObj != null)
        {
            _gameCanvas = existingCanvasObj.GetComponent<Canvas>();
            if (_gameCanvas != null)
            {
                 GameManager.Instance.LogManager.LogMessage("GameUI found existing 'GameUICanvas' GameObject.");
                 _gameCanvas.gameObject.SetActive(true); // Ensure it's active
                 // Optionally ensure DontDestroyOnLoad if necessary, though GameInitializer might handle lifecycle
                 // DontDestroyOnLoad(_gameCanvas.gameObject);
                 return;
            }
             else {
                  GameManager.Instance.LogManager.LogError("Found 'GameUICanvas' GameObject but it lacks a Canvas component!");
                  // Proceed to create a new one
             }
        }

        // 3. If still no canvas, create a new one
        GameManager.Instance.LogManager.LogMessage("GameUI creating new GameUICanvas.");
        GameObject canvasObj = new GameObject("GameUICanvas"); // Use a specific name
        // canvasObj.tag = "GameCanvas"; // REMOVED Tag usage

        _gameCanvas = canvasObj.AddComponent<Canvas>(); // Assign to instance variable
        _gameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _gameCanvas.sortingOrder = 10; // Ensure it renders above potential lobby UI (sortingOrder 0)

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>(); // Essential for UI interactions

        // Decide lifecycle - often GameUI is destroyed when returning to lobby/menu
        // DontDestroyOnLoad(canvasObj); // Only use if GameUI should persist across scenes independently

         _gameCanvas.gameObject.SetActive(true); // Make sure it's active
    }


    private void SubscribeToEvents()
    {
        // Subscribe to PlayerState LOCAL events for the local player
        if (_localPlayerState != null) // Check if state is valid before subscribing
        {
            PlayerState.OnStatsChanged -= HandlePlayerStatsChanged; // Prevent duplicates
            PlayerState.OnStatsChanged += HandlePlayerStatsChanged;

            PlayerState.OnHandChanged -= HandleLocalHandChanged;
            PlayerState.OnHandChanged += HandleLocalHandChanged;

            PlayerState.OnPlayerMonsterChanged -= HandlePlayerMonsterChanged;
            PlayerState.OnPlayerMonsterChanged += HandlePlayerMonsterChanged;

            PlayerState.OnOpponentMonsterChanged -= HandleOpponentMonsterChanged;
            PlayerState.OnOpponentMonsterChanged += HandleOpponentMonsterChanged;

            PlayerState.OnLocalTurnStateChanged -= HandleLocalTurnStateChanged;
            PlayerState.OnLocalTurnStateChanged += HandleLocalTurnStateChanged;

            PlayerState.OnLocalFightOver -= HandleLocalFightOver;
            PlayerState.OnLocalFightOver += HandleLocalFightOver;
        } else {
             GameManager.Instance?.LogManager?.LogError("GameUI: Cannot subscribe to PlayerState events - localPlayerState is null!");
        }

        // Subscribe to GameState global events
         if (GameState.Instance != null) // Check if GameState exists
         {
            GameState.OnRoundChanged -= HandleRoundChanged; // Prevent duplicates
            GameState.OnRoundChanged += HandleRoundChanged;

            GameState.OnPlayerStateAdded -= HandlePlayerStateAdded;
            GameState.OnPlayerStateAdded += HandlePlayerStateAdded;

            GameState.OnPlayerStateRemoved -= HandlePlayerStateRemoved;
            GameState.OnPlayerStateRemoved += HandlePlayerStateRemoved;

            GameState.OnPlayerFightCompletionUpdated -= HandlePlayerFightCompletionUpdated;
            GameState.OnPlayerFightCompletionUpdated += HandlePlayerFightCompletionUpdated;
         } else {
             GameManager.Instance?.LogManager?.LogError("GameUI: Cannot subscribe to GameState events - GameState.Instance is null!");
         }


        // Subscribe to events from our OWN UI controllers
        if (_turnInfoUIController != null)
        {
             _turnInfoUIController.OnEndTurnClicked -= HandleEndTurnClicked; // Prevent duplicates
             _turnInfoUIController.OnEndTurnClicked += HandleEndTurnClicked;
        }
        if (_handUIController != null)
        {
             _handUIController.OnCardPlayedRequested -= HandleCardPlayedRequested; // Prevent duplicates
             _handUIController.OnCardPlayedRequested += HandleCardPlayedRequested;
        }

        GameManager.Instance?.LogManager?.LogMessage("GameUI subscribed to events.");
    }

    private void UnsubscribeFromEvents()
    {
        // Unsubscribe from PlayerState events
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

        // Unsubscribe from local UI controllers
        if (_turnInfoUIController != null)
        {
            _turnInfoUIController.OnEndTurnClicked -= HandleEndTurnClicked;
        }
        if (_handUIController != null)
        {
            _handUIController.OnCardPlayedRequested -= HandleCardPlayedRequested;
        }
        GameManager.Instance?.LogManager?.LogMessage("GameUI unsubscribed from events.");
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
        else if (_opponentsUIController != null && _localPlayerState != null && playerState.Object != null && playerState.Object.InputAuthority == _localPlayerState.GetOpponentPlayerRef()) // Check opponent ref
        {
            _opponentsUIController?.UpdateOpponentStats(playerState);
        }
        // Could potentially update other opponents if the UI shows more than one
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
            // _turnInfoUIController.SetFightStatusText("Fight Over!");
        }
         // Optionally re-enable turn UI if fight ends and a new round starts (handled by HandleRoundChanged/PrepareForNewRound)
    }


    private void HandleRoundChanged(int round)
    {
        if (!_initialized) return;
        _turnInfoUIController?.UpdateRoundInfo(round);
        _battleUIController?.ResetCachedHealth(); // Reset visual health tracking for new opponent
    }

     // Called by GameState event when ANY PlayerState is added
    private void HandlePlayerStateAdded(PlayerRef playerRef, PlayerState playerState)
    {
        if (!_initialized || playerState == null || _localPlayerState == null || _localPlayerState.Object == null) return; // Added null check for localPlayerState.Object
        // Add to opponent display if it's NOT the local player
        if (playerRef != _localPlayerState.Object.InputAuthority)
        {
            _opponentsUIController?.AddOpponentDisplay(playerRef, playerState);
        }
         // Update our own UI in case our opponent assignment changed etc.
         UpdateAllUI();
    }

     // Called by GameState event when ANY PlayerState is removed
    private void HandlePlayerStateRemoved(PlayerRef playerRef, PlayerState playerState)
    {
        if (!_initialized) return;
        // Remove from opponent display
        _opponentsUIController?.RemoveOpponentDisplay(playerRef);
         // If the removed player was our opponent, update our UI
         if(_localPlayerState != null && playerRef == _localPlayerState.GetOpponentPlayerRef()) {
             UpdateAllUI(); // Re-render to potentially show no opponent
         }
    }

    // Handles updates to any player's fight completion status
    private void HandlePlayerFightCompletionUpdated(PlayerRef playerRef, bool isComplete)
    {
        if (!_initialized) return;
        // Update opponent UI to show waiting status, etc.
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
        Monster targetMonster = monsterDisplay?.GetMonster();

         // Determine if the target is the player's own monster or the opponent's representation
         Monster ownMonster = _localPlayerState.GetMonster();
         Monster opponentMonster = _localPlayerState.GetOpponentMonster(); // Get the local representation
         bool isTargetingOwn = (monsterDisplay != null && monsterDisplay.IsPlayerMonster());
         bool isTargetingOpponent = (monsterDisplay != null && !monsterDisplay.IsPlayerMonster());


         // If the drop target wasn't a monster display, infer target based on card type
         if (monsterDisplay == null) {
             if (card.Target == CardTarget.Self) {
                 targetMonster = ownMonster;
                 isTargetingOwn = true;
                 isTargetingOpponent = false;
                 GameManager.Instance?.LogManager?.LogMessage($"Card {card.Name} targets self, applying to player monster.");
             } else if (card.Target == CardTarget.Enemy || card.Target == CardTarget.AllEnemies) {
                  targetMonster = opponentMonster; // Use the representation
                  isTargetingOwn = false;
                  isTargetingOpponent = true; // Mark as targeting opponent contextually
                  GameManager.Instance?.LogManager?.LogMessage($"Card {card.Name} targets enemy, applying to opponent monster representation.");
             } else {
                  // Card might not need a monster target (e.g. Draw Card)
                  GameManager.Instance?.LogManager?.LogMessage($"Card {card.Name} played without monster target. Applying effects to player state.");
                  // Let PlayCardLocally handle non-monster effects
                  targetMonster = null; // Indicate no specific monster target
             }
         } else {
             // Use the monster from the display that was hit
              targetMonster = monsterDisplay.GetMonster();
         }


         // Call PlayerState's method to handle the play logic (energy check, RPCs, effects)
         if (targetMonster != null || (card.Target != CardTarget.Self && card.Target != CardTarget.Enemy && card.Target != CardTarget.AllEnemies && card.Target != CardTarget.All)) // Allow play if target is valid OR card doesn't strictly need a monster
         {
             _localPlayerState.PlayCardLocally(cardIndex, targetMonster);
             // Play visual effect immediately if target is a valid GameObject
             if (target != null && monsterDisplay != null) // Ensure target has a visual representation
             {
                 _battleUIController?.PlayCardEffect(card, target);
             }
         } else {
             GameManager.Instance?.LogManager?.LogMessage($"GameUI: Could not determine valid target for card {card.Name}. Play cancelled.");
         }
    }


    // Hides UI elements managed by UIManager (Connect/Lobby)
    private void HideLobbyUI()
    {
        // Call the appropriate method on UIManager instance
        GameManager.Instance?.UIManager?.HideLobbyAndConnectUI();
    }

    // Updates all major UI components based on the current local player state
    private void UpdateAllUI()
    {
        if (!_initialized || _localPlayerState == null || !_localPlayerState.Object.IsValid) return; // Added validity check

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
                _turnInfoUIController?.UpdateTurnState(false); // Assume not player's turn if GameState is missing
            }
        }
        catch (System.Exception ex) { GameManager.Instance?.LogManager?.LogError($"Error updating UI in GameUI.UpdateAllUI: {ex.Message}\n{ex.StackTrace}"); }
    }

    public BattleUIController GetBattleUIController() => _battleUIController;

    private void OnDestroy()
    {
        if (!_cleanedUp)
        {
            GameManager.Instance?.LogManager?.LogMessage("GameUI OnDestroy called. Cleaning up...");
            UnsubscribeFromEvents();
            _handUIController?.Cleanup(); // Call cleanup on HandUIController

            // Optionally destroy the canvas if this GameUI owned it
            // if (_gameCanvas != null) {
            //    Destroy(_gameCanvas.gameObject);
            //}

            _cleanedUp = true;
        }
    }
}