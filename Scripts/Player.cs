using Fusion;
using UnityEngine;
using TMPro;

public class Player : NetworkBehaviour
{
    [Networked] public Color PlayerColor { get; set; }
    [Networked] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    
    // Add networked position to ensure synchronization between clients
    [Networked] private Vector3 NetworkedPosition { get; set; }
    
    // Visual components - references only, created programmatically
    private SpriteRenderer _spriteRenderer;
    private TextMeshPro _nameText;
    private CharacterController _characterController;
    
    // Movement speed
    private float _moveSpeed = 5f;
    
    // Screen position management
    private Vector2 _screenBounds;
    
    // Track if components were created
    private bool _componentsCreated = false;
    
    // Whether the player is on the ground
    private bool _isGrounded = false;
    
    // Fixed Y position for 2D movement
    private float _fixedYPosition = 1.0f;

    private void Awake()
    {
        // Create components immediately when the object is instantiated
        CreateAllComponents();
        
        // Calculate screen bounds
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float height = mainCamera.orthographicSize * 2;
            float width = height * mainCamera.aspect;
            _screenBounds = new Vector2(width / 2, height / 2);
        }
    }

    // Create all required components
    private void CreateAllComponents()
    {
        if (_componentsCreated)
            return;

        if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            GameManager.Instance.LogManager.LogMessage($"Creating components for player gameObject: {gameObject.name}");
        
        // Create CharacterController
        CreateCharacterController();
        
        // Create visuals
        CreateVisualComponents();
        
        _componentsCreated = true;
        
        if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            GameManager.Instance.LogManager.LogMessage("All components created for player");
    }
    
    private void CreateCharacterController()
    {
        // Check if character controller exists
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
        {
            // Add character controller
            _characterController = gameObject.AddComponent<CharacterController>();
            _characterController.radius = 0.5f;
            _characterController.height = 0.2f; // Make it flatter for 2D-like movement
            _characterController.center = new Vector3(0, 0.1f, 0); // Lower center for stability
            
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                GameManager.Instance.LogManager.LogMessage("Created CharacterController for player");
        }
    }
    
    private void CreateVisualComponents()
    {
        // Create sprite
        CreateSpriteRenderer();
        
        // Create name text
        CreateNameText();
    }
    
    private void CreateSpriteRenderer()
    {
        // Check for existing sprite renderer on children
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer == null)
        {
            // Create a child object for the sprite
            GameObject spriteObj = new GameObject("PlayerSprite");
            spriteObj.transform.SetParent(transform, false);
            spriteObj.transform.localPosition = new Vector3(0, 0.1f, 0); // Slightly above ground
            
            // Add sprite renderer
            _spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = CreateDefaultSprite();
            _spriteRenderer.sortingOrder = 1;
            
            // Rotate sprite to be visible from top-down camera
            spriteObj.transform.rotation = Quaternion.Euler(90, 0, 0);
            
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                GameManager.Instance.LogManager.LogMessage("Created sprite renderer for player");
        }
    }
    
    private void CreateNameText()
    {
        // Check for existing name text on children
        _nameText = GetComponentInChildren<TextMeshPro>();
        if (_nameText == null)
        {
            // Create a child object for the text
            GameObject textObj = new GameObject("PlayerNameText");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = new Vector3(0, 1.5f, 0);
            
            // Add text mesh pro component
            _nameText = textObj.AddComponent<TextMeshPro>();
            _nameText.fontSize = 4;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.color = Color.white;
            _nameText.text = "Player";
            
            // Make text face the camera
            textObj.transform.rotation = Quaternion.Euler(90, 0, 0);
            
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                GameManager.Instance.LogManager.LogMessage("Created text mesh for player name");
        }
    }
    
    private Sprite CreateDefaultSprite()
    {
        // Create a default circle sprite
        Texture2D texture = new Texture2D(128, 128);
        Color[] colors = new Color[128 * 128];
        
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                float dx = x - 64;
                float dy = y - 64;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                if (dist < 60)
                    colors[y * 128 + x] = Color.white;
                else
                    colors[y * 128 + x] = Color.clear;
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
    }

    public override void Spawned()
    {
        base.Spawned();
        
        if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            GameManager.Instance.LogManager.LogMessage($"Player Spawned: HasStateAuthority={HasStateAuthority}, HasInputAuthority={HasInputAuthority}, ID={Object.Id}, InputAuthority={Object.InputAuthority}");
        
        // Double-check components were created (they should have been in Awake)
        if (!_componentsCreated)
        {
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                GameManager.Instance.LogManager.LogMessage("Components not created in Awake, creating now");
            CreateAllComponents();
        }
        
        // Set a random color for the player when it's first spawned
        if (HasStateAuthority)
        {
            // Only set color if it hasn't been set (for rejoining players)
            if (PlayerColor.r == 0 && PlayerColor.g == 0 && PlayerColor.b == 0)
            {
                PlayerColor = new Color(
                    UnityEngine.Random.Range(0.5f, 1f),
                    UnityEngine.Random.Range(0.5f, 1f),
                    UnityEngine.Random.Range(0.5f, 1f)
                );
            }
            
            // Position the player in a unique space
            PositionPlayerInUniqueSpace();
            
            // Initialize networked position
            NetworkedPosition = transform.position;
            
            // Check if player is rejoining
            string playerName = PlayerName.ToString();
            if (!string.IsNullOrEmpty(playerName))
            {
                PlayerData savedData = GameManager.Instance.LobbyManager.GetPlayerData(playerName);
                if (savedData != null)
                {
                    // Restore player state, but maintain Y position
                    Vector3 position = savedData.Position;
                    position.y = _fixedYPosition;
                    transform.position = position;
                    NetworkedPosition = position; // Important: Update networked position too
                    PlayerColor = savedData.PlayerColor;
                    
                    if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                        GameManager.Instance.LogManager.LogMessage($"Restored state for player {playerName}");
                }
            }
        }
        
        // Update visuals based on networked properties
        UpdateVisuals();
        
        // Subscribe to game start event
        GameManager.Instance.LobbyManager.OnGameStarted += HandleGameStarted;
        
        // If this is a newly spawned network object from another player, track it
        if (!HasInputAuthority && Runner != null)
        {
            GameManager.Instance.PlayerManager.OnPlayerObjectSpawned(Runner, Object, Object.InputAuthority);
        }
        
        // If this is the local player, register with the lobby manager
        if (HasInputAuthority)
        {
            string playerName = PlayerName.ToString();
            if (string.IsNullOrEmpty(playerName))
            {
                // Get name from UIManager if not set
                playerName = GameManager.Instance.UIManager.GetLocalPlayerName();
                PlayerName = playerName;
            }
            
            if (!string.IsNullOrEmpty(playerName))
            {
                // Register on all clients - IMPORTANT: This ensures player is added to lobby manager
                RPC_RegisterPlayer(playerName, Object.InputAuthority);
                
                if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                    GameManager.Instance.LogManager.LogMessage($"Sent RPC to register player: {playerName}");
            }
        }
        
        Debug.Log($"Player spawned with ID: {Object.Id} and name: {PlayerName}");
    }
    
    private void PositionPlayerInUniqueSpace()
    {
        // Create a grid-based positioning system
        int playerCount = GameManager.Instance.PlayerManager.GetPlayerCount();
        int index = playerCount - 1; // 0-based index
        
        // Define grid dimensions based on player count
        int cols = Mathf.CeilToInt(Mathf.Sqrt(playerCount));
        int rows = Mathf.CeilToInt((float)playerCount / cols);
        
        // Calculate row and column for this player
        int row = index / cols;
        int col = index % cols;
        
        // Calculate position based on grid
        float spacing = 4f; // Space between players
        float startX = -((cols - 1) * spacing) / 2;
        float startZ = -((rows - 1) * spacing) / 2;
        
        // Use fixed Y position
        Vector3 position = new Vector3(
            startX + col * spacing,
            _fixedYPosition, // Keep y at fixed position
            startZ + row * spacing
        );
        
        transform.position = position;
        NetworkedPosition = position; // Initialize networked position
        
        if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            GameManager.Instance.LogManager.LogMessage($"Positioned player at {position}");
    }
    
    private void UpdateVisuals()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = PlayerColor;
        }
        
        if (_nameText != null)
        {
            _nameText.text = PlayerName.ToString();
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        if (GameManager.Instance.LobbyManager.IsGameStarted() || !GameManager.Instance.NetworkManager.IsConnected)
            return;
            
        // For StateAuthority (server/host), apply position from networked value
        if (HasStateAuthority && !HasInputAuthority) 
        {
            transform.position = NetworkedPosition;
        }
        
        // For InputAuthority (local player), apply inputs and update networked position
        if (GetInput(out NetworkInputData data))
        {
            // Apply movement only when we have input authority
            if (HasInputAuthority)
            {
                // 2D-like movement in 3D space using CharacterController
                Vector3 move = new Vector3(data.horizontal, 0, data.vertical);
                move = move.normalized * _moveSpeed * Runner.DeltaTime;
                
                if (_characterController != null)
                {
                    // Apply movement
                    _characterController.Move(move);
                    
                    // Check if grounded
                    _isGrounded = _characterController.isGrounded;
                    
                    // Apply gravity only if not grounded
                    if (!_isGrounded)
                    {
                        // Apply reduced gravity
                        _characterController.Move(Vector3.down * 9.81f * 0.3f * Runner.DeltaTime);
                    }
                    
                    // Maintain fixed Y position
                    if (Mathf.Abs(transform.position.y - _fixedYPosition) > 0.1f)
                    {
                        Vector3 correctedPosition = transform.position;
                        correctedPosition.y = _fixedYPosition;
                        transform.position = correctedPosition;
                    }
                    
                    // Update the networked position for synchronization
                    if (HasStateAuthority)
                    {
                        NetworkedPosition = transform.position;
                    }
                }
                
                // Save player position for rejoining
                SavePlayerState();
            }
        }
        
        // If we're not the InputAuthority, but we're the StateAuthority, sync position
        else if (HasStateAuthority)
        {
            transform.position = NetworkedPosition;
        }
        
        // Check ready status button press
        if (HasInputAuthority && GetInput(out NetworkInputData readyInput))
        {
            // Check if R key is pressed (button 1 in our input mapping)
            if (readyInput.buttons.IsSet(1)) 
            {
                // Toggle ready state
                ToggleReady();
            }
        }
    }
    
    // For non-network authority objects, use Render which is called after FixedUpdateNetwork 
    // to ensure smooth position interpolation
    public override void Render()
    {
        if (!HasStateAuthority)
        {
            // For all other clients, interpolate to the networked position for smooth movement
            transform.position = Vector3.Lerp(transform.position, NetworkedPosition, Runner.DeltaTime * 10f);
            
            // Ensure fixed Y position
            if (Mathf.Abs(transform.position.y - _fixedYPosition) > 0.05f)
            {
                Vector3 position = transform.position;
                position.y = _fixedYPosition;
                transform.position = position;
            }
        }
        
        // Update visuals if color or name has changed over the network
        UpdateVisuals();
    }
    
    private void ToggleReady()
    {
        if (HasStateAuthority)
        {
            // Toggle ready state directly
            IsReady = !IsReady;
            
            // Update lobby manager via RPC to ensure all clients know
            RPC_SetReadyStatus(PlayerName.ToString(), IsReady);
            
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                GameManager.Instance.LogManager.LogMessage($"Player {PlayerName} ready status changed to: {IsReady}");
        }
    }
    
    private void SavePlayerState()
    {
        string playerName = PlayerName.ToString();
        if (string.IsNullOrEmpty(playerName))
            return;
            
        PlayerData data = new PlayerData
        {
            Position = transform.position,
            PlayerColor = PlayerColor
        };
        
        GameManager.Instance.LobbyManager.UpdatePlayerData(playerName, data);
    }
    
    private void HandleGameStarted()
    {
        if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            GameManager.Instance.LogManager.LogMessage($"Player {PlayerName} received game started event");
        // When the game starts, we can perform player-specific initialization here
        // For now, we'll just log the event
    }
    
    public void SetPlayerName(string name)
    {
        if (HasStateAuthority)
        {
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                GameManager.Instance.LogManager.LogMessage($"Setting player name to: {name}");
            PlayerName = name;
            UpdateVisuals();
        }
    }
    
    public string GetPlayerName()
    {
        return PlayerName.ToString();
    }
    
    public void SetReadyStatus(bool isReady)
    {
        if (HasStateAuthority)
        {
            IsReady = isReady;
            // Update all clients via RPC
            RPC_SetReadyStatus(PlayerName.ToString(), isReady);
            
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                GameManager.Instance.LogManager.LogMessage($"Player {PlayerName} ready status set to {isReady} via SetReadyStatus method");
        }
    }
    
    public bool GetReadyStatus()
    {
        return IsReady;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RegisterPlayer(string playerName, PlayerRef playerRef)
    {
        // This RPC ensures all clients register the player
        if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            GameManager.Instance.LogManager.LogMessage($"RPC received to register player: {playerName}");
        GameManager.Instance.LobbyManager.RegisterPlayer(playerName, playerRef);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetReadyStatus(string playerName, bool isReady)
    {
        // This RPC ensures all clients update the ready status
        if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            GameManager.Instance.LogManager.LogMessage($"RPC received to set ready status: {playerName} = {isReady}");
        GameManager.Instance.LobbyManager.SetPlayerReadyStatus(playerName, isReady);
        
        // Update UI on all clients
        GameManager.Instance.UIManager.UpdatePlayersList();
        
        // Force ready status check for single player mode
        if (GameManager.Instance.PlayerManager.GetPlayerCount() == 1)
        {
            GameManager.Instance.LobbyManager.DebugForceReadyCheck();
        }
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        
        // Unsubscribe from events
        GameManager.Instance.LobbyManager.OnGameStarted -= HandleGameStarted;
    }
}