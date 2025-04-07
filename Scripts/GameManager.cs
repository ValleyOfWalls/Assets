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
        
        // Initialize components
        LogManager = gameObject.AddComponent<LogManager>();
        LogManager.Initialize();
        
        NetworkManager = gameObject.AddComponent<NetworkManager>();
        PlayerManager = gameObject.AddComponent<PlayerManager>();
        CameraManager = gameObject.AddComponent<CameraManager>();
        UIManager = gameObject.AddComponent<UIManager>();
        
        // Log initial message
        LogManager.LogMessage("GameManager initialized successfully");
    }

    private void Start()
    {
        // Initialize all managers
        NetworkManager.Initialize();
        PlayerManager.Initialize();
        CameraManager.Initialize();
        UIManager.Initialize();
        
        // Log network configuration
        NetworkManager.LogNetworkProjectConfig();
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