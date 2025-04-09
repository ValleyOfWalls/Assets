using UnityEngine;
using Fusion;

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

    // Reference to GameState
    private NetworkObject _gameStatePrefab;
    private bool _gameStateSpawned = false;

    // Game state
    private bool _gameStarted = false;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize components in the correct order
        LogManager = gameObject.AddComponent<LogManager>();
        LogManager.Initialize();
        
        NetworkManager = gameObject.AddComponent<NetworkManager>();
        PlayerManager = gameObject.AddComponent<PlayerManager>();
        CameraManager = gameObject.AddComponent<CameraManager>();
        LobbyManager = gameObject.AddComponent<LobbyManager>();
        UIManager = gameObject.AddComponent<UIManager>();
        GameInitializer = gameObject.AddComponent<GameInitializer>();
        
        // Find the GameState prefab in Resources
        LoadGameStatePrefab();
        
        // Log initial message
        LogManager.LogMessage("GameManager initialized successfully");
    }

    private void Start()
    {
        // Initialize all managers in the correct order
        LogManager.LogMessage("Starting manager initialization...");
        
        // First initialize network and player managers
        NetworkManager.Initialize();
        PlayerManager.Initialize();
        
        // Then initialize camera and lobby managers
        CameraManager.Initialize();
        LobbyManager.Initialize();
        
        // Initialize UI last so it can subscribe to all events
        UIManager.Initialize();
        
        // Log network configuration
        NetworkManager.LogNetworkProjectConfig();
        
        // Log completion
        LogManager.LogMessage("All managers initialized successfully");
    }
    
    private void LoadGameStatePrefab()
    {
        // Don't load if already loaded
        if (_gameStatePrefab != null)
            return;

        // Load the GameState prefab from Resources
        _gameStatePrefab = Resources.Load<NetworkObject>("GameStatePrefab");
        
        if (_gameStatePrefab == null)
        {
            LogManager.LogError("GameStatePrefab not found in Resources folder! You need to create a GameState prefab with NetworkObject and GameState components and place it in a Resources folder.");
        }
        else
        {
            LogManager.LogMessage("GameStatePrefab loaded from Resources folder");
        }
    }
    
    // Handle game state transitions
    public void StartGame()
    {
        if (_gameStarted)
        {
            LogManager.LogMessage("Game already started, ignoring StartGame call");
            return;
        }
            
        LogManager.LogMessage("Game is starting!");
        
        // Set game started flag
        _gameStarted = true;
        
        // Spawn GameState if not already spawned
        SpawnGameState();
        
        // Initialize game systems through the GameInitializer
        GameInitializer.InitializeGame();
    }
    
    private void SpawnGameState()
    {
        if (_gameStateSpawned)
        {
            LogManager.LogMessage("GameState already spawned, not spawning again");
            return;
        }
            
        if (_gameStatePrefab == null)
        {
            LogManager.LogError("GameStatePrefab is null! Cannot spawn GameState");
            return;
        }
            
        NetworkRunner runner = NetworkManager.GetRunner();
        if (runner == null || !runner.IsRunning)
        {
            LogManager.LogError("NetworkRunner is not running! Cannot spawn GameState");
            return;
        }
            
        // Only spawn if we have state authority (the host/server)
        if (runner.IsServer || runner.IsSharedModeMasterClient)
        {
            try
            {
                // Only spawn once
                if (GameState.Instance == null)
                {
                    // Spawn the GameState on the network
                    runner.Spawn(_gameStatePrefab);
                    _gameStateSpawned = true;
                    LogManager.LogMessage("GameState spawned on network");
                }
                else
                {
                    LogManager.LogMessage("GameState.Instance already exists, not spawning again");
                    _gameStateSpawned = true;
                }
            }
            catch (System.Exception ex)
            {
                LogManager.LogError($"Failed to spawn GameState: {ex.Message}");
            }
        }
        else
        {
            LogManager.LogMessage("Not spawning GameState as this client is not the host/master");
            // We'll still mark as spawned since the host will have spawned it
            _gameStateSpawned = true;
        }
    }
    
    public bool IsGameStarted()
    {
        return _gameStarted;
    }
    
    public void EndGame()
    {
        if (!_gameStarted)
            return;
            
        LogManager.LogMessage("Game is ending!");
        
        // Reset game state
        _gameStarted = false;
        _gameStateSpawned = false;
        
        // Clean up game systems
        GameInitializer.CleanupGame();
        
        // Return to lobby state
        if (LobbyManager != null)
        {
            LobbyManager.Reset();
        }
        
        // Show lobby UI
        if (UIManager != null)
        {
            UIManager.ShowConnectUI();
        }
    }

    private void OnDestroy()
    {
        // Clean up network connection when game is closed
        if (NetworkManager != null)
        {
            NetworkManager.Shutdown();
        }
    }
}