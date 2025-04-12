// valleyofwalls-assets/Scripts/GameUI.cs
using System;
using System.Collections;
using System.Collections.Generic; // Added for List<>
using UnityEngine;
using UnityEngine.UI;
using Fusion;
// Added for PlayerRef

public class GameUI : MonoBehaviour // GameUI should be a MonoBehaviour to manage its own lifecycle and coroutines
{
    // Core UI components
    private Canvas _gameCanvas; // Canvas specifically for Game UI elements

    // UI controllers
    private GameUILayout _layoutManager; //
    private PlayerUIController _playerUIController; //
    private BattleUIController _battleUIController; //
    private HandUIController _handUIController; //
    private TurnInfoUIController _turnInfoUIController; //
    private OpponentsUIController _opponentsUIController; //

    // References
    private PlayerState _localPlayerState; // Cached reference to the local player's state

    // Initialization state
    private bool _initialized = false; //
    private bool _initializationInProgress = false; //
    private float _initRetryInterval = 0.5f; //
    private int _maxInitRetries = 20; //
    private bool _cleanedUp = false; // Flag to prevent double cleanup

    // Called by GameInitializer
    public void Initialize() //
    {
        if (_initialized || _initializationInProgress) return; //
        GameManager.Instance?.LogManager?.LogMessage("Starting GameUI initialization..."); //
        StartCoroutine(InitializeWithRetry()); // Start coroutine on this MonoBehaviour
    }

    private IEnumerator InitializeWithRetry() //
    {
        _initializationInProgress = true; //
        int retryCount = 0; //

        while (retryCount < _maxInitRetries) //
        {
            NetworkRunner runner = GameManager.Instance?.NetworkManager?.GetRunner(); // Access Runner via NetworkManager
            if (GameState.Instance == null || !GameState.Instance.IsSpawned() || runner == null || !runner.IsRunning) // Wait for GameState and NetworkRunner
            {
                retryCount++; //
                yield return new WaitForSeconds(_initRetryInterval); //
                continue; //
            }

            _localPlayerState = GameState.Instance.GetLocalPlayerState(); // Get local player state using GameState instance
            if (_localPlayerState == null || !_localPlayerState.Object.IsValid) // Also check if state object is valid
            {
                retryCount++; //
                yield return new WaitForSeconds(_initRetryInterval); //
                continue; //
            }

            // --- Initialization successful ---
            GameManager.Instance?.LogManager?.LogMessage("GameUI: GameState and LocalPlayerState available, proceeding with initialization."); //
            CreateGameCanvas(); // Create or find the game canvas *FIRST*
            if (_gameCanvas == null) //
             {
                 GameManager.Instance?.LogManager?.LogError("GameUI: Failed to create or find game canvas. Aborting initialization."); //
                 _initializationInProgress = false; //
                 yield break; // Stop if canvas fails
             }

            _layoutManager = new GameUILayout(_gameCanvas.transform); // Create layout manager using the game canvas transform
            // Initialize UI controllers with required references, parenting them under the correct layout panels
            _playerUIController = new PlayerUIController(_layoutManager.GetStatsPanel(), _localPlayerState); //
            _battleUIController = new BattleUIController(_layoutManager.GetBattlePanel(), _layoutManager.GetPlayerMonsterPanel(), _localPlayerState); //
            _handUIController = new HandUIController(_layoutManager.GetHandPanel(), _gameCanvas, _localPlayerState); // Hand needs main canvas for drag overlay
            _turnInfoUIController = new TurnInfoUIController(_layoutManager.GetTurnInfoPanel(), _localPlayerState); //
            _opponentsUIController = new OpponentsUIController(_layoutManager.GetOpponentsPanel()); //

            SubscribeToEvents(); // Subscribe to necessary events
            UpdateAllUI(); // Initial UI update based on current state
            HideLobbyUI(); // Call the method to hide UIManager's elements

            _initialized = true; //
            _initializationInProgress = false; //
            GameManager.Instance?.LogManager?.LogMessage("GameUI fully initialized and Lobby UI hidden."); //

            yield break; // Successful initialization, exit coroutine
        }

        // If we get here, initialization failed after max retries
        GameManager.Instance?.LogManager?.LogError("Failed to initialize GameUI after maximum retries. Check GameState and PlayerState spawning."); //
        _initializationInProgress = false; //
    }

     private void CreateGameCanvas() //
    {
        if (_gameCanvas != null && _gameCanvas.gameObject != null) // Check if we already have a reference
        {
            GameManager.Instance.LogManager.LogMessage("GameUI reusing existing _gameCanvas reference."); //
            _gameCanvas.gameObject.SetActive(true); // Ensure it's active
            return; //
        }

        GameObject existingCanvasObj = GameObject.Find("GameUICanvas"); // Try finding an *existing* GameObject named "GameUICanvas"
        if (existingCanvasObj != null) //
        {
            _gameCanvas = existingCanvasObj.GetComponent<Canvas>(); //
            if (_gameCanvas != null) //
            {
                 GameManager.Instance.LogManager.LogMessage("GameUI found existing 'GameUICanvas' GameObject."); //
                 _gameCanvas.gameObject.SetActive(true); // Ensure it's active
                 return; //
            }
             else { GameManager.Instance.LogManager.LogError("Found 'GameUICanvas' GameObject but it lacks a Canvas component!"); } //
        }

        GameManager.Instance.LogManager.LogMessage("GameUI creating new GameUICanvas."); // If still no canvas, create a new one
        GameObject canvasObj = new GameObject("GameUICanvas"); // Use a specific name
        _gameCanvas = canvasObj.AddComponent<Canvas>(); // Assign to instance variable
        _gameCanvas.renderMode = RenderMode.ScreenSpaceOverlay; //
        _gameCanvas.sortingOrder = 10; // Ensure it renders above potential lobby UI (sortingOrder 0)
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>(); //
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; //
        scaler.referenceResolution = new Vector2(1920, 1080); //
        scaler.matchWidthOrHeight = 0.5f; //
        canvasObj.AddComponent<GraphicRaycaster>(); // Essential for UI interactions
         _gameCanvas.gameObject.SetActive(true); // Make sure it's active
    }


    private void SubscribeToEvents() //
    {
        if (_localPlayerState != null) // Check if state is valid before subscribing
        {
            PlayerState.OnStatsChanged -= HandlePlayerStatsChanged; PlayerState.OnStatsChanged += HandlePlayerStatsChanged; //
            PlayerState.OnHandChanged -= HandleLocalHandChanged; PlayerState.OnHandChanged += HandleLocalHandChanged; //
            PlayerState.OnPlayerMonsterChanged -= HandlePlayerMonsterChanged; PlayerState.OnPlayerMonsterChanged += HandlePlayerMonsterChanged; //
            PlayerState.OnOpponentMonsterChanged -= HandleOpponentMonsterChanged; PlayerState.OnOpponentMonsterChanged += HandleOpponentMonsterChanged; //
            PlayerState.OnLocalTurnStateChanged -= HandleLocalTurnStateChanged; PlayerState.OnLocalTurnStateChanged += HandleLocalTurnStateChanged; //
            PlayerState.OnLocalFightOver -= HandleLocalFightOver; PlayerState.OnLocalFightOver += HandleLocalFightOver; //
        } else { GameManager.Instance?.LogManager?.LogError("GameUI: Cannot subscribe to PlayerState events - localPlayerState is null!"); } //

         if (GameState.Instance != null) // Check if GameState exists
         {
            GameState.OnRoundChanged -= HandleRoundChanged; GameState.OnRoundChanged += HandleRoundChanged; //
            GameState.OnPlayerStateAdded -= HandlePlayerStateAdded; GameState.OnPlayerStateAdded += HandlePlayerStateAdded; //
            GameState.OnPlayerStateRemoved -= HandlePlayerStateRemoved; GameState.OnPlayerStateRemoved += HandlePlayerStateRemoved; //
            GameState.OnPlayerFightCompletionUpdated -= HandlePlayerFightCompletionUpdated; GameState.OnPlayerFightCompletionUpdated += HandlePlayerFightCompletionUpdated; //
         } else { GameManager.Instance?.LogManager?.LogError("GameUI: Cannot subscribe to GameState events - GameState.Instance is null!"); } //

        if (_turnInfoUIController != null) { _turnInfoUIController.OnEndTurnClicked -= HandleEndTurnClicked; _turnInfoUIController.OnEndTurnClicked += HandleEndTurnClicked; } //
        if (_handUIController != null) { _handUIController.OnCardPlayedRequested -= HandleCardPlayedRequested; _handUIController.OnCardPlayedRequested += HandleCardPlayedRequested; } //

        GameManager.Instance?.LogManager?.LogMessage("GameUI subscribed to events."); //
    }

    private void UnsubscribeFromEvents() //
    {
        PlayerState.OnStatsChanged -= HandlePlayerStatsChanged; //
        PlayerState.OnHandChanged -= HandleLocalHandChanged; //
        PlayerState.OnPlayerMonsterChanged -= HandlePlayerMonsterChanged; //
        PlayerState.OnOpponentMonsterChanged -= HandleOpponentMonsterChanged; //
        PlayerState.OnLocalTurnStateChanged -= HandleLocalTurnStateChanged; //
        PlayerState.OnLocalFightOver -= HandleLocalFightOver; //
        if (GameState.Instance != null) // Check GameState exists before unsubscribing
        {
            GameState.OnRoundChanged -= HandleRoundChanged; //
            GameState.OnPlayerStateAdded -= HandlePlayerStateAdded; //
            GameState.OnPlayerStateRemoved -= HandlePlayerStateRemoved; //
            GameState.OnPlayerFightCompletionUpdated -= HandlePlayerFightCompletionUpdated; //
        }
        if (_turnInfoUIController != null) { _turnInfoUIController.OnEndTurnClicked -= HandleEndTurnClicked; } //
        if (_handUIController != null) { _handUIController.OnCardPlayedRequested -= HandleCardPlayedRequested; } //
        GameManager.Instance?.LogManager?.LogMessage("GameUI unsubscribed from events."); //
    }

    // --- Event Handlers ---

    private void HandlePlayerStatsChanged(PlayerState playerState) //
    {
        if (!_initialized || playerState == null) return; //
        if (playerState == _localPlayerState) { _playerUIController?.UpdateStats(playerState); } // Update local player UI
        else if (_opponentsUIController != null && _localPlayerState != null && playerState.Object != null && playerState.Object.InputAuthority == _localPlayerState.GetOpponentPlayerRef()) { _opponentsUIController?.UpdateOpponentStats(playerState); } // Update specific opponent UI
    }

    private void HandleLocalHandChanged(PlayerState playerState, List<CardData> hand) //
    {
        if (!_initialized || playerState != _localPlayerState) return; // Ensure it's for the local player
        _handUIController?.UpdateHand(hand); //
    }

    private void HandlePlayerMonsterChanged(Monster monster) //
    {
        if (!_initialized) return; //
        _battleUIController?.UpdatePlayerMonsterDisplay(monster); //
         if(_localPlayerState != null) _playerUIController?.UpdateStats(_localPlayerState); // Update general stats if monster affects them
    }

    private void HandleOpponentMonsterChanged(Monster opponentMonster) //
    {
        if (!_initialized) return; //
        _battleUIController?.UpdateOpponentMonsterDisplay(opponentMonster); //
    }

    private void HandleLocalTurnStateChanged(bool isPlayerTurnActive) //
    {
        if (!_initialized) return; //
        _turnInfoUIController?.UpdateTurnState(isPlayerTurnActive); //
        _handUIController?.SetCardsInteractable(isPlayerTurnActive); //
    }

    private void HandleLocalFightOver(bool isFightOver) //
    {
        if (!_initialized) return; //
        if(isFightOver && _turnInfoUIController != null) //
        {
            _turnInfoUIController.UpdateTurnState(false); // Show inactive state
        }
    }

    private void HandleRoundChanged(int round) //
    {
        if (!_initialized) return; //
        _turnInfoUIController?.UpdateRoundInfo(round); //
        _battleUIController?.ResetCachedHealth(); // Reset visual health tracking for new opponent
    }

    private void HandlePlayerStateAdded(PlayerRef playerRef, PlayerState playerState) // Called by GameState event when ANY PlayerState is added
    {
        if (!_initialized || playerState == null || _localPlayerState == null || _localPlayerState.Object == null) return; // Added null check for localPlayerState.Object
        if (playerRef != _localPlayerState.Object.InputAuthority) // Add to opponent display if it's NOT the local player
        {
            _opponentsUIController?.AddOpponentDisplay(playerRef, playerState); //
        }
         UpdateAllUI(); // Update our own UI in case our opponent assignment changed etc.
    }

    private void HandlePlayerStateRemoved(PlayerRef playerRef, PlayerState playerState) // Called by GameState event when ANY PlayerState is removed
    {
        if (!_initialized) return; //
        _opponentsUIController?.RemoveOpponentDisplay(playerRef); // Remove from opponent display
         if(_localPlayerState != null && playerRef == _localPlayerState.GetOpponentPlayerRef()) { UpdateAllUI(); } // If the removed player was our opponent, update our UI
    }

    private void HandlePlayerFightCompletionUpdated(PlayerRef playerRef, bool isComplete) // Handles updates to any player's fight completion status
    {
        if (!_initialized) return; //
        _opponentsUIController?.UpdateOpponentFightStatus(playerRef, isComplete); // Update opponent UI to show waiting status, etc.
    }

    private void HandleEndTurnClicked() // Handles the local "End Turn" button click
    {
        if (!_initialized || _localPlayerState == null) return; //
        _localPlayerState.EndPlayerTurnLocally(); //
    }

    // Handles the event from HandUIController when a card is dropped on a target
    private void HandleCardPlayedRequested(CardDisplay display, GameObject target) //
    {
        if (!_initialized || _localPlayerState == null || display == null || target == null) return; //
        CardData card = display.GetCardData(); //
        int cardIndex = display.GetCardIndex(); //
        MonsterDisplay monsterDisplay = target.GetComponent<MonsterDisplay>(); //
        // Monster targetMonster = monsterDisplay?.GetMonster(); // Don't use this directly, resolve logically below

        // *** ADD LOGGING HERE ***
        bool? isPlayerMonster = monsterDisplay?.IsPlayerMonster(); // Nullable bool
        GameManager.Instance?.LogManager?.LogMessage($"HandleCardPlayedRequested (UI): Card='{card?.Name}', Target Obj='{target?.name}', IsPlayerMonster={isPlayerMonster?.ToString() ?? "N/A (Not MonsterDisplay)"}");

        // Determine logical target monster based on the drop target
        Monster resolvedTargetMonsterForPlay = null;
        if (monsterDisplay != null) { // Dropped onto a monster display
            if (monsterDisplay.IsPlayerMonster()) {
                resolvedTargetMonsterForPlay = _localPlayerState.GetMonster(); // Target is logically own monster
                GameManager.Instance?.LogManager?.LogMessage($"-- Target Resolved As: Own Monster ({resolvedTargetMonsterForPlay?.Name})");
            } else {
                resolvedTargetMonsterForPlay = _localPlayerState.GetOpponentMonster(); // Target is logically opponent representation
                GameManager.Instance?.LogManager?.LogMessage($"-- Target Resolved As: Opponent Monster Representation ({resolvedTargetMonsterForPlay?.Name})");
            }
        } else { // Dropped elsewhere, infer from card type maybe? (Or handle non-targeted cards)
            if (card.Target == CardTarget.Self) {
                resolvedTargetMonsterForPlay = _localPlayerState.GetMonster();
                 GameManager.Instance?.LogManager?.LogMessage($"-- Target Resolved As (Self Card): Own Monster ({resolvedTargetMonsterForPlay?.Name})");
            } else if (card.Target == CardTarget.Enemy || card.Target == CardTarget.AllEnemies) {
                 resolvedTargetMonsterForPlay = _localPlayerState.GetOpponentMonster();
                 GameManager.Instance?.LogManager?.LogMessage($"-- Target Resolved As (Enemy Card): Opponent Monster Representation ({resolvedTargetMonsterForPlay?.Name})");
            } else {
                 GameManager.Instance?.LogManager?.LogMessage($"-- Target Resolved As: None (Card Type: {card.Type}, Target Type: {card.Target})");
                 // resolvedTargetMonsterForPlay remains null for non-monster targeted cards
            }
        }


        // Check if the play is valid (either resolved target is non-null OR card doesn't need a monster)
        if (resolvedTargetMonsterForPlay != null || (card.Target != CardTarget.Self && card.Target != CardTarget.Enemy && card.Target != CardTarget.AllEnemies && card.Target != CardTarget.All)) //
        {
            // Call PlayerState's method with the LOGICALLY resolved monster target
            _localPlayerState.PlayCardLocally(cardIndex, resolvedTargetMonsterForPlay); // Pass the resolved logical monster
            // Play visual effect immediately if target is a valid GameObject display
            if (target != null && monsterDisplay != null) // Ensure target has a visual representation
            {
                _battleUIController?.PlayCardEffect(card, target); // Play effect on the actual dropped-on object
            }
        } else {
             GameManager.Instance?.LogManager?.LogMessage($"GameUI: Could not determine valid logical target for card {card.Name}. Play cancelled."); //
        }
    }

    private void HideLobbyUI() // Hides UI elements managed by UIManager (Connect/Lobby)
    {
        GameManager.Instance?.UIManager?.HideLobbyAndConnectUI(); // Call the appropriate method on UIManager instance
    }

    private void UpdateAllUI() // Updates all major UI components based on the current local player state
    {
        if (!_initialized || _localPlayerState == null || !_localPlayerState.Object.IsValid) return; // Added validity check
        try {
            _playerUIController?.UpdateStats(_localPlayerState); //
            _battleUIController?.UpdatePlayerMonsterDisplay(_localPlayerState.GetMonster()); //
            _battleUIController?.UpdateOpponentMonsterDisplay(_localPlayerState.GetOpponentMonster()); //
            _handUIController?.UpdateHand(_localPlayerState.GetHand()); //
            _opponentsUIController?.UpdateAllOpponents(); //

            if (GameState.Instance != null && GameState.Instance.IsSpawned()) {
                _turnInfoUIController?.UpdateRoundInfo(GameState.Instance.GetCurrentRound()); //
                _turnInfoUIController?.UpdateTurnState(_localPlayerState.GetIsLocalPlayerTurn()); //
            } else { // Fallback if GameState not ready
                _turnInfoUIController?.UpdateRoundInfo(1); //
                _turnInfoUIController?.UpdateTurnState(false); // Assume not player's turn if GameState is missing
            }
        } catch (System.Exception ex) { GameManager.Instance?.LogManager?.LogError($"Error updating UI in GameUI.UpdateAllUI: {ex.Message}\n{ex.StackTrace}"); } //
    }

    public BattleUIController GetBattleUIController() => _battleUIController; //
    private void OnDestroy() //
    {
        if (!_cleanedUp) //
        {
            GameManager.Instance?.LogManager?.LogMessage("GameUI OnDestroy called. Cleaning up..."); //
            UnsubscribeFromEvents(); //
            _handUIController?.Cleanup(); // Call cleanup on HandUIController
            _cleanedUp = true; //
        }
    }
}