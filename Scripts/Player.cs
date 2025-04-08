using Fusion;
using UnityEngine;
using TMPro;

public class Player : NetworkBehaviour
{
    [Networked] public Color PlayerColor { get; set; }
    [Networked] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    
    // Movement speed
    private float _moveSpeed = 5f;
    
    // Visual components
    public SpriteRenderer _spriteRenderer;
    public TextMeshPro _nameText;
    
    // Movement handling
    private Vector2 _moveDirection;
    private CharacterController _characterController;
    
    // Screen position management
    private Vector2 _screenBounds;
    
    private void Awake()
    {
        // Find components if not already set
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
        if (_nameText == null)
            _nameText = GetComponentInChildren<TextMeshPro>();
            
        // Use existing CharacterController instead of trying to add Rigidbody2D
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
        {
            Debug.LogError("CharacterController component missing from player prefab!");
        }
        
        // Calculate screen bounds
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float height = mainCamera.orthographicSize * 2;
            float width = height * mainCamera.aspect;
            _screenBounds = new Vector2(width / 2, height / 2);
        }
    }

    public override void Spawned()
    {
        base.Spawned();
        
        // Initialize sprite renderer if not assigned
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        
        // Get the local player name from UI if this is the local player
        if (HasInputAuthority && string.IsNullOrEmpty(PlayerName.ToString()))
        {
            string localName = GameManager.Instance.UIManager.GetLocalPlayerName();
            if (!string.IsNullOrEmpty(localName))
            {
                PlayerName = localName;
                GameManager.Instance.LogManager.LogMessage($"Setting local player name to: {localName}");
            }
        }
        
        // Set player name text
        if (_nameText != null)
        {
            _nameText.text = PlayerName.ToString();
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
            
            // Check if this player is rejoining
            string playerName = PlayerName.ToString();
            if (!string.IsNullOrEmpty(playerName))
            {
                PlayerData savedData = GameManager.Instance.LobbyManager.GetPlayerData(playerName);
                if (savedData != null)
                {
                    // Restore player state
                    transform.position = savedData.Position;
                    PlayerColor = savedData.PlayerColor;
                    GameManager.Instance.LogManager.LogMessage($"Restored state for player {playerName}");
                }
            }
        }
        
        // Position the player in a unique space if it's a new spawn
        if (HasStateAuthority && transform.position.x == 0 && transform.position.z == 0)
        {
            PositionPlayerInUniqueSpace();
        }
        
        // Apply the color to the visual
        UpdateVisuals();
        
        // Register with LobbyManager
        if (HasStateAuthority)
        {
            string playerName = PlayerName.ToString();
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = $"Player{UnityEngine.Random.Range(1000, 10000)}";
                PlayerName = playerName;
            }
            
            GameManager.Instance.LobbyManager.RegisterPlayer(playerName, Object.InputAuthority);
            GameManager.Instance.UIManager.UpdatePlayersList();
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
        float startY = -((rows - 1) * spacing) / 2;
        
        // Since we're working in 3D with CharacterController, use X and Z
        Vector3 position = new Vector3(
            startX + col * spacing,
            1, // Keep y at 1 for CharacterController
            startY + row * spacing
        );
        
        transform.position = position;
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
                    
                    // Apply gravity to keep the player grounded
                    _characterController.Move(Vector3.down * 9.81f * Runner.DeltaTime);
                }
                
                // Save player position for rejoining
                SavePlayerState();
            }
        }
        
        // Check ready status button press
        if (HasInputAuthority && GetInput(out NetworkInputData readyInput))
        {
            // Check if R key is pressed (button 1 in our input mapping)
            if (readyInput.buttons.IsSet(1)) 
            {
                IsReady = !IsReady;
                GameManager.Instance.LogManager.LogMessage($"Player {PlayerName} ready status changed to: {IsReady}");
                GameManager.Instance.LobbyManager.SetPlayerReadyStatus(PlayerName.ToString(), IsReady);
                
                // Update UI
                GameManager.Instance.UIManager.UpdatePlayersList();
            }
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
    
    public override void Render()
    {
        // Update visuals if color or name has changed over the network
        UpdateVisuals();
    }
    
    public void SetPlayerName(string name)
    {
        if (HasStateAuthority)
        {
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
        }
    }
    
    public bool GetReadyStatus()
    {
        return IsReady;
    }
}