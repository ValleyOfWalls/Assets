using Fusion;
using UnityEngine;
using System.Collections;

public class GameInitializer : MonoBehaviour
{
    // Game object references
    private GameObject _gameUIObject;
    
    // Component references
    private GameUI _gameUI;
    
    // Game state
    private bool _gameInitialized = false;
    private bool _gameplayStarted = false;
    private bool _initializationInProgress = false;

    public void InitializeGame()
    {
        if (_gameInitialized || _initializationInProgress) return;
        
        _initializationInProgress = true;
        GameManager.Instance.LogManager.LogMessage("Initializing game objects and systems...");
        
        // Start the initialization process
        StartCoroutine(InitializeGameSequence());
    }

    private IEnumerator InitializeGameSequence()
    {
        // Get network runner
        NetworkRunner runner = GameManager.Instance.NetworkManager.GetRunner();
        if (runner == null)
        {
            GameManager.Instance.LogManager.LogError("NetworkRunner not available!");
            _initializationInProgress = false;
            yield break;
        }
        
        // Wait for GameState.Instance to be available
        float timeoutSeconds = 10.0f;
        float startTime = Time.time;
        
        while (GameState.Instance == null)
        {
            if (Time.time - startTime > timeoutSeconds)
            {
                GameManager.Instance.LogManager.LogError("Timed out waiting for GameState.Instance to be available");
                _initializationInProgress = false;
                yield break;
            }
            
            GameManager.Instance.LogManager.LogMessage("Waiting for GameState.Instance to be available...");
            yield return new WaitForSeconds(0.2f);
        }
        
        // Make sure the GameState is spawned on the network
        timeoutSeconds = 5.0f;
        startTime = Time.time;
        
        while (!GameState.Instance.IsSpawned())
        {
            if (Time.time - startTime > timeoutSeconds)
            {
                GameManager.Instance.LogManager.LogError("GameState.Instance exists but is not spawned properly");
                _initializationInProgress = false;
                yield break;
            }
            
            GameManager.Instance.LogManager.LogMessage("Waiting for GameState to be fully spawned...");
            yield return new WaitForSeconds(0.2f);
        }
        
        // Wait for local player to be registered with GameState
        timeoutSeconds = 10.0f;
        startTime = Time.time;
        
        while (GameState.Instance.GetLocalPlayerState() == null)
        {
            if (Time.time - startTime > timeoutSeconds)
            {
                GameManager.Instance.LogManager.LogMessage("Local player not registered after timeout, creating a temporary player state");
                
                // Force player registration by creating a temporary PlayerState
                yield return StartCoroutine(CreateTemporaryPlayerState(runner));
                
                if (GameState.Instance.GetLocalPlayerState() == null)
                {
                    GameManager.Instance.LogManager.LogError("Failed to create local player state");
                    _initializationInProgress = false;
                    yield break;
                }
                break;
            }
            
            GameManager.Instance.LogManager.LogMessage("Waiting for local player state to be registered...");
            yield return new WaitForSeconds(0.2f);
        }
        
        // Once GameState.Instance is available and player state is registered, set flag
        _gameInitialized = true;
        
        // Create game UI - this is local only
        CreateGameUI();
        
        _initializationInProgress = false;
        GameManager.Instance.LogManager.LogMessage("Game initialized successfully");
        
        // Start gameplay immediately once initialization is complete
        StartGameplay();
    }

    // Helper class to track temporary player states
    private class TemporaryPlayerStateMarker : MonoBehaviour { }

    private IEnumerator CreateTemporaryPlayerState(NetworkRunner runner)
    {
        // Try to create a temporary player state
        GameObject playerStateObj = new GameObject("TemporaryPlayerState");
        DontDestroyOnLoad(playerStateObj);
        
        // Add network object component
        NetworkObject networkObject = playerStateObj.AddComponent<NetworkObject>();
        
        // Add player state component
        PlayerState playerState = playerStateObj.AddComponent<PlayerState>();
        
        // Add marker component for tracking
        playerStateObj.AddComponent<TemporaryPlayerStateMarker>();
        
        // Initialize temporary monster
        yield return StartCoroutine(InitializeTempPlayerMonster(playerState));
        
        // Register with GameState
        if (GameState.Instance != null)
        {
            GameState.Instance.RegisterPlayer(runner.LocalPlayer, playerState);
            GameManager.Instance.LogManager.LogMessage("Manually registered temporary player state");
            
            // Wait a frame for registration to complete
            yield return null;
        }
        else
        {
            GameManager.Instance.LogManager.LogError("GameState.Instance is null, cannot register player");
        }
    }

    // Initialize a default monster for the temporary player
    private IEnumerator InitializeTempPlayerMonster(PlayerState playerState)
    {
        try
        {
            // Use reflection to access and initialize the monster field directly
            System.Reflection.FieldInfo monsterField = typeof(PlayerState).GetField("_playerMonster", 
                                                    System.Reflection.BindingFlags.NonPublic | 
                                                    System.Reflection.BindingFlags.Instance);
            
            if (monsterField != null)
            {
                Monster monster = new Monster
                {
                    Name = "Temporary Monster",
                    Health = 40,
                    MaxHealth = 40,
                    Attack = 5,
                    Defense = 3,
                    TintColor = new Color(0.8f, 0.2f, 0.2f) // Red tint
                };
                
                monsterField.SetValue(playerState, monster);
                GameManager.Instance.LogManager.LogMessage("Created temporary monster for player");
            }
            else
            {
                GameManager.Instance.LogManager.LogMessage("Could not find _playerMonster field");
            }
        }
        catch (System.Exception ex)
        {
            GameManager.Instance.LogManager.LogMessage($"Failed to initialize monster: {ex.Message}");
        }
        
        yield return null;
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
        
        GameManager.Instance.LogManager.LogMessage("Game UI created and initialized");
    }

    // Start the gameplay once all setup is complete
    public void StartGameplay()
    {
        if (!_gameInitialized || _gameplayStarted)
        {
            GameManager.Instance.LogManager.LogError($"Cannot start gameplay: Initialized={_gameInitialized}, Already Started={_gameplayStarted}");
            return;
        }
        
        if (GameState.Instance == null)
        {
            GameManager.Instance.LogManager.LogError("Cannot start gameplay: GameState is null!");
            return;
        }
        
        if (!GameState.Instance.IsSpawned())
        {
            GameManager.Instance.LogManager.LogError("Cannot start gameplay: GameState is not spawned!");
            return;
        }
        
        _gameplayStarted = true;
        
        // Start the game with the GameState that was spawned on the network
        GameState.Instance.StartGame();
        
        GameManager.Instance.LogManager.LogMessage("Gameplay started!");
    }
    
    // Clean up game objects and systems
    public void CleanupGame()
    {
        if (!_gameInitialized) return;
        
        GameManager.Instance.LogManager.LogMessage("Cleaning up game systems...");
        
        StopAllCoroutines();
        
        // Destroy game UI
        if (_gameUIObject != null)
        {
            Destroy(_gameUIObject);
            _gameUIObject = null;
            _gameUI = null;
        }
        
        // Reset flags
        _gameInitialized = false;
        _gameplayStarted = false;
        _initializationInProgress = false;
        
        GameManager.Instance.LogManager.LogMessage("Game systems cleaned up");
    }
    
    private void OnDestroy()
    {
        // Clean up any remaining objects
        CleanupGame();
    }
}