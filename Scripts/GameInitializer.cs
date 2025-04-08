using Fusion;
using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    // Game object references
    private GameObject _gameStateObject;
    private GameObject _gameUIObject;
    
    // Network objects
    private NetworkObject _gameStateNetworkObject;
    
    // Component references
    private GameState _gameState;
    private GameUI _gameUI;
    
    // Game state
    private bool _gameInitialized = false;
    private bool _gameplayStarted = false;

    public void InitializeGame()
    {
        if (_gameInitialized) return;
        
        GameManager.Instance.LogManager.LogMessage("Initializing game objects and systems...");
        
        // Get network runner
        NetworkRunner runner = GameManager.Instance.NetworkManager.GetRunner();
        if (runner == null)
        {
            GameManager.Instance.LogManager.LogError("NetworkRunner not available!");
            return;
        }
        
        // Create game state - this needs to be networked
        CreateGameState(runner);
        
        // Create game UI - this is local only
        CreateGameUI();
        
        _gameInitialized = true;
        GameManager.Instance.LogManager.LogMessage("Game initialized successfully");
    }

    private void CreateGameState(NetworkRunner runner)
    {
        // Create game state object
        _gameStateObject = new GameObject("GameState");
        DontDestroyOnLoad(_gameStateObject);
        
        // Add network object component
        _gameStateNetworkObject = _gameStateObject.AddComponent<NetworkObject>();
        
        // Spawn on the network
        _gameStateNetworkObject = runner.Spawn(_gameStateNetworkObject);
        
        // Get reference to game state component (added by network spawn)
        _gameState = _gameStateObject.GetComponent<GameState>();
        
        GameManager.Instance.LogManager.LogMessage("Game state created and spawned on network");
    }

    private void CreateGameUI()
    {
        // Create game UI object
        _gameUIObject = new GameObject("GameUI");
        DontDestroyOnLoad(_gameUIObject);
        
        // Add UI component
        _gameUI = _gameUIObject.AddComponent<GameUI>();
        
        // Initialize UI (will be deferred until GameState is available)
        _gameUI.Initialize();
        
        GameManager.Instance.LogManager.LogMessage("Game UI created");
    }

    // Start the gameplay once all setup is complete
    public void StartGameplay()
    {
        if (!_gameInitialized || _gameplayStarted || _gameState == null)
        {
            GameManager.Instance.LogManager.LogError("Cannot start gameplay: Game not properly initialized!");
            return;
        }
        
        _gameplayStarted = true;
        
        // Tell GameState to start the actual gameplay
        _gameState.StartGame();
        
        GameManager.Instance.LogManager.LogMessage("Gameplay started!");
    }
    
    // Clean up game objects and systems
    public void CleanupGame()
    {
        if (!_gameInitialized) return;
        
        GameManager.Instance.LogManager.LogMessage("Cleaning up game systems...");
        
        // Destroy game UI
        if (_gameUIObject != null)
        {
            Destroy(_gameUIObject);
            _gameUIObject = null;
            _gameUI = null;
        }
        
        // Despawn game state from network if we have authority
        NetworkRunner runner = GameManager.Instance.NetworkManager.GetRunner();
        if (runner != null && _gameStateNetworkObject != null)
        {
            if (_gameStateNetworkObject.HasStateAuthority)
            {
                runner.Despawn(_gameStateNetworkObject);
            }
            _gameStateNetworkObject = null;
        }
        else if (_gameStateObject != null)
        {
            // Destroy locally if we can't despawn
            Destroy(_gameStateObject);
        }
        _gameStateObject = null;
        _gameState = null;
        
        // Reset flags
        _gameInitialized = false;
        _gameplayStarted = false;
        
        GameManager.Instance.LogManager.LogMessage("Game systems cleaned up");
    }
    
    private void OnDestroy()
    {
        // Clean up any remaining objects
        CleanupGame();
    }
}