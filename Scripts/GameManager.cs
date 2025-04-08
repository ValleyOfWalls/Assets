using UnityEngine;

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
    
    // Handle game state transitions
    public void StartGame()
    {
        if (_gameStarted)
            return;
            
        LogManager.LogMessage("Game is starting!");
        
        // Set game started flag
        _gameStarted = true;
        
        // Initialize game systems through the GameInitializer
        GameInitializer.InitializeGame();
        
        // Start the actual gameplay
        GameInitializer.StartGameplay();
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