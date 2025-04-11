using UnityEngine;
using Fusion;
using System.Threading.Tasks;
using System.Collections;

public class GameManager : MonoBehaviour
{
    // Singleton instance
    public static GameManager Instance { get; private set; }

    // Component references
    [HideInInspector] public NetworkManager NetworkManager { get; private set; }
    [HideInInspector] public UIManager UIManager { get; private set; }
    [HideInInspector] public LogManager LogManager { get; private set; }
    [HideInInspector] public PlayerManager PlayerManager { get; private set; }
    [HideInInspector] public CameraManager CameraManager { get; private set; }
    [HideInInspector] public LobbyManager LobbyManager { get; private set; }
    [HideInInspector] public GameInitializer GameInitializer { get; private set; }

    // Reference to GameState prefab (REMOVED - NetworkManager handles prefab loading/spawning)
    // private NetworkObject _gameStatePrefab;

    // Flag to prevent multiple spawn attempts (REMOVED - Not needed here anymore)
    // private bool _gameStateSpawnInitiated = false;

    // Game state flag
    private bool _isGameStartingOrStarted = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize components
        LogManager = gameObject.AddComponent<LogManager>();
        LogManager.Initialize();
        NetworkManager = gameObject.AddComponent<NetworkManager>();
        PlayerManager = gameObject.AddComponent<PlayerManager>();
        CameraManager = gameObject.AddComponent<CameraManager>();
        LobbyManager = gameObject.AddComponent<LobbyManager>();
        UIManager = gameObject.AddComponent<UIManager>();
        GameInitializer = gameObject.AddComponent<GameInitializer>();

        // LoadGameStatePrefab(); // REMOVED - NetworkManager handles this
        LogManager.LogMessage("GameManager Singleton Initialized");
    }

    private void Start()
    {
        LogManager.LogMessage("Starting secondary manager initialization...");
        NetworkManager.Initialize();
        PlayerManager.Initialize();
        CameraManager.Initialize();
        LobbyManager.Initialize();
        UIManager.Initialize();
        NetworkManager.LogNetworkProjectConfig();
        LogManager.LogMessage("All managers initialized.");
    }

    // REMOVED LoadGameStatePrefab() - NetworkManager handles loading

    // This method is now called by LobbyManager when the lobby phase is complete.
    // It assumes GameState ALREADY exists.
    public void StartGame()
    {
        if (_isGameStartingOrStarted)
        {
            LogManager.LogMessage("Game is already starting or started. Ignoring duplicate StartGame call.");
            return;
        }
        LogManager.LogMessage("GameManager: StartGame called (triggered by Lobby/Debug).");
        _isGameStartingOrStarted = true;
        // _gameStateSpawnInitiated = false; // Flag removed

        // Directly start the GameInitializer sequence.
        // We ASSUME GameState has already been spawned by NetworkManager earlier.
        StartCoroutine(GameStartupSequence());
    }

    private IEnumerator GameStartupSequence()
    {
        LogManager.LogMessage("GameStartupSequence: Starting...");

        // --- Step 1: Verify GameState Exists ---
        // Add a check here to ensure GameState is indeed ready before proceeding.
        if (GameState.Instance == null || !GameState.Instance.IsSpawned())
        {
             LogManager.LogError("GameStartupSequence ERROR: GameState is not valid or spawned! Cannot start game logic.");
             _isGameStartingOrStarted = false; // Allow trying again
             // TODO: Handle this failure (e.g., return to lobby, show error)
             yield break;
        }
         LogManager.LogMessage("GameStartupSequence: GameState verified.");


        // --- Step 2: Hand off to GameInitializer ---
        // GameInitializer will wait for PlayerState registration etc.
        LogManager.LogMessage("GameStartupSequence: Initializing GameInitializer...");
        GameInitializer.InitializeGame();
        LogManager.LogMessage("GameStartupSequence: Handed off to GameInitializer.");
        yield return null; // Ensure coroutine doesn't end immediately
    }

    // This coroutine is now just a utility function to WAIT for GameState,
    // it no longer attempts to spawn it. It might be called by GameInitializer
    // or other systems if they need to explicitly wait.
    public IEnumerator EnsureGameStateSpawned() // Renamed slightly
    {
        LogManager.LogMessage("EnsureGameStateReady: Waiting for GameState.Instance...");
        NetworkRunner runner = NetworkManager?.GetRunner();

        // --- Validate Runner ---
        if (runner == null || !runner.IsRunning)
        {
            LogManager.LogError("EnsureGameStateReady: NetworkRunner is null or not running!");
            yield break; // Cannot proceed
        }

        // --- Wait for instance to become valid and spawned ---
        float timer = 0f;
        float timeout = 20f;
        while (GameState.Instance == null || !GameState.Instance.IsSpawned()) // Check IsSpawned property
        {
             // Re-check runner status during wait
             if (!runner.IsRunning)
             {
                 LogManager.LogError("EnsureGameStateReady: NetworkRunner stopped while waiting for GameState.");
                 yield break;
             }

            timer += Time.deltaTime;
            if (timer > timeout)
            {
                LogManager.LogError($"EnsureGameStateReady: Timed out ({timeout}s) waiting for GameState.Instance. Instance: {GameState.Instance}, IsSpawned: {GameState.Instance?.IsSpawned()}.");
                yield break; // Stop waiting on timeout
            }
            yield return null; // Wait for the next frame
        }

        LogManager.LogMessage($"EnsureGameStateReady: GameState.Instance is valid and spawned (ID: {GameState.Instance.Id}).");
    }


    // Public method to check game status
    public bool IsGameStarted()
    {
        // Use the IsGameActive property from the GameState if available, otherwise fallback
        return GameState.Instance != null && GameState.Instance.IsSpawned() ?
             GameState.Instance.IsGameActive() : _isGameStartingOrStarted;
    }

    public void EndGame()
    {
        if (!_isGameStartingOrStarted) return; // Only end if started/starting
        LogManager.LogMessage("Game is ending!");

        _isGameStartingOrStarted = false; // Reset game state flag

        // Cleanup game initializer systems (UI, etc.)
        GameInitializer?.CleanupGame();

        NetworkRunner runner = NetworkManager?.GetRunner();

        // --- Despawn GameState only if Authority ---
         if (runner != null && runner.IsRunning &&
             (runner.IsServer || runner.IsSharedModeMasterClient) && // Check authority
             GameState.Instance != null && GameState.Instance.Object != null &&
             GameState.Instance.IsSpawned()) // Check IsSpawned property
        {
             LogManager.LogMessage("Authority despawning GameState.");
            runner.Despawn(GameState.Instance.Object);
        }
        // Reset Lobby and show Connect UI
        LobbyManager?.Reset();
        UIManager?.ShowConnectUI();
    }

    private void OnDestroy()
    {
        if (NetworkManager != null)
        {
            NetworkRunner runner = NetworkManager.GetRunner();
            if (runner != null && runner.IsRunning)
            {
                LogManager?.LogMessage("GameManager OnDestroy: Shutting down NetworkRunner.");
                runner.Shutdown();
            }
        }
        if (Instance == this) Instance = null;
    }
}