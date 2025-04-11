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

    // Reference to GameState prefab
    private NetworkObject _gameStatePrefab;
    // Flag to prevent multiple spawn attempts
    private bool _gameStateSpawnInitiated = false;

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

        LoadGameStatePrefab();
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

    private void LoadGameStatePrefab()
    {
        if (_gameStatePrefab != null) return;
        _gameStatePrefab = Resources.Load<NetworkObject>("GameStatePrefab");
        if (_gameStatePrefab == null) LogManager.LogError("GameStatePrefab not found in Resources folder! Critical Error.");
        else LogManager.LogMessage("GameStatePrefab loaded successfully.");
    }

    public void StartGame()
    {
        if (_isGameStartingOrStarted)
        {
            LogManager.LogMessage("Game is already starting or started. Ignoring duplicate StartGame call.");
            return;
        }
        LogManager.LogMessage("GameManager: StartGame called!");
        _isGameStartingOrStarted = true;
        _gameStateSpawnInitiated = false;
        StartCoroutine(GameStartupSequence());
    }

    private IEnumerator GameStartupSequence()
    {
        LogManager.LogMessage("GameStartupSequence: Starting...");
        yield return StartCoroutine(EnsureGameStateSpawned());

        if (GameState.Instance == null)
        {
            LogManager.LogError("GameStartupSequence failed: GameState could not be spawned or found.");
            _isGameStartingOrStarted = false;
            yield break;
        }
        LogManager.LogMessage("GameStartupSequence: GameState is ready.");

        LogManager.LogMessage("GameStartupSequence: Initializing GameInitializer...");
        GameInitializer.InitializeGame();
        LogManager.LogMessage("GameStartupSequence: Handed off to GameInitializer.");
    }

    private IEnumerator EnsureGameStateSpawned()
    {
        LogManager.LogMessage("EnsureGameStateSpawned: Starting check/spawn process...");

        // --- Check 1: Is GameState already spawned? ---
        // *** Line ~105 Error Fix: Access .IsSpawned as property ***
        if (GameState.Instance != null && GameState.Instance.IsSpawned)
        {
            LogManager.LogMessage("EnsureGameStateSpawned: GameState already exists and is spawned.");
            _gameStateSpawnInitiated = true;
            yield break;
        }

        // --- Check 2: Has spawn been initiated but not completed? ---
        if (_gameStateSpawnInitiated)
        {
            LogManager.LogMessage("EnsureGameStateSpawned: GameState spawn already initiated, waiting...");
        }
        // --- Check 3: Initiate spawn if needed and possible ---
        else
        {
            if (_gameStatePrefab == null) { LogManager.LogError("EnsureGameStateSpawned: GameStatePrefab is null!"); yield break; }

            NetworkRunner runner = NetworkManager?.GetRunner();
            if (runner == null || !runner.IsRunning) { LogManager.LogError("EnsureGameStateSpawned: NetworkRunner is not running!"); yield break; }

            if (runner.IsServer || runner.IsSharedModeMasterClient)
            {
                LogManager.LogMessage("EnsureGameStateSpawned: Conditions met, attempting to spawn GameState (Authority).");
                try
                {
                    runner.Spawn(_gameStatePrefab, Vector3.zero, Quaternion.identity, null);
                    _gameStateSpawnInitiated = true;
                    LogManager.LogMessage("EnsureGameStateSpawned: GameState spawn command issued.");
                }
                catch (System.Exception ex) { LogManager.LogError($"EnsureGameStateSpawned: Failed to spawn GameState: {ex.Message}"); yield break; }
            }
            else
            {
                LogManager.LogMessage("EnsureGameStateSpawned: Not State Authority. Will wait for GameState.");
                _gameStateSpawnInitiated = true;
            }
        }

        // --- Check 4: Wait for instance to become valid and spawned ---
        float timer = 0f;
        float timeout = 15f;
        // *** Line ~147 Error Fix: Access .IsSpawned as property ***
        while (GameState.Instance == null || !GameState.Instance.IsSpawned)
        {
            timer += Time.deltaTime;
            if (timer > timeout)
            {
                LogManager.LogError("EnsureGameStateSpawned: Timed out waiting for GameState.Instance to become valid and spawned.");
                yield break;
            }
            yield return null;
        }
        LogManager.LogMessage($"EnsureGameStateSpawned: GameState.Instance is valid and spawned (ID: {GameState.Instance.Id}).");
    }

    // Public method to check game status
    public bool IsGameStarted()
    {
        // *** Line ~165 Error Fix: Access .IsGameActive as property ***
        return GameState.Instance != null ? GameState.Instance.IsGameActive : _isGameStartingOrStarted;
    }

    public void EndGame()
    {
        if (!_isGameStartingOrStarted) return;
        LogManager.LogMessage("Game is ending!");

        _isGameStartingOrStarted = false;
        _gameStateSpawnInitiated = false;

        GameInitializer?.CleanupGame();

        NetworkRunner runner = NetworkManager?.GetRunner();
        // *** Line ~181 Error Fix: Access .IsSpawned as property ***
        // Combined checks carefully
        if (runner != null && runner.IsRunning &&
            (runner.IsServer || runner.IsSharedModeMasterClient) &&
            GameState.Instance != null && GameState.Instance.Object != null &&
            GameState.Instance.IsSpawned) // Check IsSpawned as property
        {
            LogManager.LogMessage("Authority despawning GameState.");
            runner.Despawn(GameState.Instance.Object);
        }

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