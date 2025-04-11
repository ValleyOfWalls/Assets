using Fusion;
using UnityEngine;
using System.Collections;

public class GameInitializer : MonoBehaviour
{
    // Game object references
    private GameObject _gameUIObject;
    // Component references
    private GameUI _gameUI;

    // Game state flags
    private bool _gameInitialized = false; // Has initialization sequence completed successfully?
    private bool _gameplayStarted = false; // Has StartGameplay been called successfully?
    private bool _initializationInProgress = false; // Coroutine running?

    // Initialization retry settings
    private float _initWaitInterval = 0.5f; // How often to check conditions
    private float _registrationTimeout = 20.0f; // Max time to wait for GameState/PlayerState

    public void InitializeGame()
    {
        if (_gameInitialized || _initializationInProgress)
        {
             GameManager.Instance?.LogManager?.LogMessage($"GameInitializer: InitializeGame called but already initialized ({_gameInitialized}) or in progress ({_initializationInProgress}). Ignoring.");
             return;
        }

        GameManager.Instance?.LogManager?.LogMessage("GameInitializer: Starting Initialization Sequence...");
        StartCoroutine(InitializeGameSequence());
    }

    private IEnumerator InitializeGameSequence()
    {
        _initializationInProgress = true;
        float startTime = Time.time;
        string sequenceId = $"InitSeq-{UnityEngine.Random.Range(1000, 9999)}"; // For tracking logs

        GameManager.Instance?.LogManager?.LogMessage($"[{sequenceId}] Starting...");

        // --- 1. Wait for Network Runner ---
        NetworkRunner runner = null;
        while (runner == null)
        {
             if (Time.time - startTime > _registrationTimeout) {
                 GameManager.Instance?.LogManager?.LogError($"[{sequenceId}] Timed out waiting for NetworkRunner.");
                 _initializationInProgress = false;
                 yield break; // Abort
             }
             runner = GameManager.Instance?.NetworkManager?.GetRunner();
            yield return new WaitForSeconds(_initWaitInterval);
        }
        GameManager.Instance?.LogManager?.LogMessage($"[{sequenceId}] NetworkRunner ready.");


        // --- 2. Wait for GameState Instance to be Spawned ---
        while (GameState.Instance == null || !GameState.Instance.IsSpawned())
        {
            if (Time.time - startTime > _registrationTimeout)
            {
                GameManager.Instance?.LogManager?.LogError($"[{sequenceId}] Timed out waiting for GameState.Instance to be available and spawned.");
                _initializationInProgress = false;
                yield break; // Abort initialization
            }
            // GameManager.Instance?.LogManager?.LogMessage($"[{sequenceId}] Waiting for GameState.Instance to be spawned...");
            yield return new WaitForSeconds(_initWaitInterval);
        }
        GameManager.Instance?.LogManager?.LogMessage($"[{sequenceId}] GameState.Instance is valid and spawned (ID: {GameState.Instance.Id}).");


        // --- 3. Wait for Local PlayerState to be Registered with GameState ---
        PlayerState localPlayerState = null;
        while (localPlayerState == null)
        {
            if (Time.time - startTime > _registrationTimeout)
            {
                 // *** REMOVED Temporary PlayerState Creation Logic ***
                 // If we time out here, it's a real problem.
                GameManager.Instance?.LogManager?.LogError($"[{sequenceId}] Timed out waiting for local PlayerState to be registered with GameState.");
                _initializationInProgress = false;
                // TODO: Handle this failure case (e.g., return to lobby, show error message)
                yield break; // Abort initialization
            }

            // Attempt to get the local player state via GameState
            localPlayerState = GameState.Instance.GetLocalPlayerState();

            if (localPlayerState == null)
            {
                 // GameManager.Instance?.LogManager?.LogMessage($"[{sequenceId}] Waiting for local PlayerState registration...");
                 yield return new WaitForSeconds(_initWaitInterval);
            }
        }
        GameManager.Instance?.LogManager?.LogMessage($"[{sequenceId}] Local PlayerState found and registered (ID: {localPlayerState.Id}).");

        // --- 4. Initialization Steps Successful - Create UI ---
         GameManager.Instance?.LogManager?.LogMessage($"[{sequenceId}] All prerequisites met. Creating Game UI...");
        CreateGameUI(); // Create UI only AFTER player state is confirmed

        _gameInitialized = true;
        _initializationInProgress = false;
        GameManager.Instance?.LogManager?.LogMessage($"[{sequenceId}] Game initialized successfully. Ready to start gameplay.");

        // --- 5. Automatically Start Gameplay (Optional) ---
        // Decide if gameplay should start immediately after initialization
        // This was previously called here, keeping it for now.
         StartGameplay(); // Proceed to start the actual game logic
    }


    // Removed temporary state creation methods:
    // private IEnumerator CreateTemporaryPlayerState(NetworkRunner runner) { ... }
    // private IEnumerator InitializeTempPlayerMonster(PlayerState playerState) { ... }


    // Creates the GameUI object and initializes it
    private void CreateGameUI()
    {
         if (_gameUIObject != null)
         {
              GameManager.Instance?.LogManager?.LogMessage("GameUI object already exists.");
              return; // Avoid creating multiple
         }
        // Create game UI root object
        _gameUIObject = new GameObject("GameUI_Root"); // Specific name
        // Consider parenting strategy if needed, e.g., under GameManager?
        // DontDestroyOnLoad(_gameUIObject); // Manage lifecycle carefully

        // Add the GameUI component which handles its own setup
        _gameUI = _gameUIObject.AddComponent<GameUI>();
        _gameUI.Initialize(); // GameUI has its own internal initialization checks

        GameManager.Instance?.LogManager?.LogMessage("GameUI object created and Initialize() called.");
    }

    // Starts the actual gameplay logic via GameState
    public void StartGameplay()
    {
         // Ensure initialization completed and gameplay hasn't already started
        if (!_gameInitialized || _gameplayStarted)
        {
            GameManager.Instance?.LogManager?.LogError($"Cannot start gameplay: Initialized={_gameInitialized}, Already Started={_gameplayStarted}");
            return;
        }

        if (GameState.Instance == null || !GameState.Instance.IsSpawned())
        {
            GameManager.Instance?.LogManager?.LogError("Cannot start gameplay: GameState is null or not spawned!");
            return;
        }

        _gameplayStarted = true;
        // Tell the authoritative GameState to start the game logic (assign matchups, etc.)
        GameState.Instance.StartGame(); // GameState.StartGame should handle authority check
        GameManager.Instance?.LogManager?.LogMessage("Gameplay start initiated via GameState.StartGame()!");
    }

    // Clean up game objects and systems when game ends or returns to lobby
    public void CleanupGame()
    {
        GameManager.Instance?.LogManager?.LogMessage("Cleaning up GameInitializer systems...");
        StopAllCoroutines(); // Stop initialization coroutine if running

        // Destroy game UI if it exists
        if (_gameUIObject != null)
        {
            Destroy(_gameUIObject);
            _gameUIObject = null;
            _gameUI = null;
            GameManager.Instance?.LogManager?.LogMessage("Destroyed GameUI_Root object.");
        }

        // Reset flags
        _gameInitialized = false;
        _gameplayStarted = false;
        _initializationInProgress = false;

        GameManager.Instance?.LogManager?.LogMessage("GameInitializer systems cleaned up.");
    }

    private void OnDestroy()
    {
        // Ensure cleanup happens if the initializer object itself is destroyed
        CleanupGame();
    }
}