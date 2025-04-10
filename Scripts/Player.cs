using Fusion;
using UnityEngine;
using TMPro;

public class Player : NetworkBehaviour
{
    [Networked] public Color PlayerColor { get; set; }
    [Networked] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] private Vector2 NetworkedPosition { get; set; }
    
    // Visual components
    private SpriteRenderer _spriteRenderer;
    private TextMeshPro _nameText;
    
    // Physics components for 2D
    private CircleCollider2D _collider;
    private Rigidbody2D _rigidbody;
    
    // Movement speed
    private float _moveSpeed = 5f;
    
    // Screen bounds
    private Vector2 _screenBounds;
    
    // Track if components were created
    private bool _componentsCreated = false;

    private void Awake()
    {
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

    private void CreateAllComponents()
    {
        if (_componentsCreated)
            return;

        if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            GameManager.Instance.LogManager.LogMessage($"Creating components for player gameObject: {gameObject.name}");
        
        // Create 2D physics components
        Create2DPhysicsComponents();
        
        // Create visuals
        CreateVisualComponents();
        
        _componentsCreated = true;
        
        if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
            GameManager.Instance.LogManager.LogMessage("All components created for player");
    }
    
    private void Create2DPhysicsComponents()
    {
        // Check if collider exists
        _collider = GetComponent<CircleCollider2D>();
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<CircleCollider2D>();
            _collider.radius = 0.5f;
            
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                GameManager.Instance.LogManager.LogMessage("Created CircleCollider2D for player");
        }
        
        // Check if rigidbody exists
        _rigidbody = GetComponent<Rigidbody2D>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody2D>();
            _rigidbody.gravityScale = 0f; // No gravity in 2D
            _rigidbody.linearDamping = 5f; // Add some drag for smoother movement
            _rigidbody.angularDamping = 5f;
            _rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation; // Prevent rotation
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            
            if (GameManager.Instance != null && GameManager.Instance.LogManager != null)
                GameManager.Instance.LogManager.LogMessage("Created Rigidbody2D for player");
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
            spriteObj.transform.localPosition = Vector3.zero;
            
            // Add sprite renderer
            _spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = CreateDefaultSprite();
            _spriteRenderer.sortingOrder = 1;
            
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
            textObj.transform.localPosition = new Vector3(0, 1.0f, 0);
            
            // Add text mesh pro component
            _nameText = textObj.AddComponent<TextMeshPro>();
            _nameText.fontSize = 4;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.color = Color.white;
            _nameText.text = "Player";
            
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
        
        // Double-check components were created
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
                    // Restore player state in 2D (x, y)
                    Vector2 position = new Vector2(savedData.Position.x, savedData.Position.y);
                    transform.position = position;
                    NetworkedPosition = position;
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
                // Register on all clients
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
        
        // FIXED: Ensure playerCount is at least 1 to avoid division by zero
        if (playerCount <= 0) playerCount = 1;
        
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
        float startY = -((rows - 1) * spacing) / 2;
        
        // Position in 2D space (x, y)
        Vector2 position = new Vector2(
            startX + col * spacing,
            startY + row * spacing
        );
        
        transform.position = position;
        NetworkedPosition = position;
        
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
            
        // For state authority but not input authority, apply networked position
        if (HasStateAuthority && !HasInputAuthority)
        {
            transform.position = NetworkedPosition;
        }
        
        // For input authority, apply movement and update networked position
        if (GetInput(out NetworkInputData data) && HasInputAuthority)
        {
            // 2D movement (x, y)
            Vector2 move = new Vector2(data.horizontal, data.vertical);
            move = move.normalized * _moveSpeed * Runner.DeltaTime;
            
            if (_rigidbody != null)
            {
                // Apply movement forces
                _rigidbody.MovePosition(_rigidbody.position + move);
                
                // Update networked position if we have state authority
                if (HasStateAuthority)
                {
                    NetworkedPosition = transform.position;
                }
            }
            
            // Save player position for rejoining
            SavePlayerState();
            
            // Check ready status button press
            if (data.buttons.IsSet(1)) 
            {
                // Toggle ready state
                ToggleReady();
            }
        }
    }
    
    public override void Render()
    {
        // If we don't have state authority, interpolate to the networked position
        if (!HasStateAuthority)
        {
            transform.position = Vector2.Lerp(transform.position, NetworkedPosition, Runner.DeltaTime * 10f);
        }
        
        // Update visuals
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