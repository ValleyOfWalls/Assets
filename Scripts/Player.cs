// valleyofwalls-assets/Scripts/Player.cs
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
    // Movement speed (Currently unused due to movement code being commented out)
    private float _moveSpeed = 5f; // CS0414 warning is expected here

    // Screen bounds
    private Vector2 _screenBounds;
    // Track if components were created
    private bool _componentsCreated = false;

    // --- NEW: Store previous tick's button state ---
    private NetworkButtons _previousButtons;

    private void Awake()
    {
        CreateAllComponents();
        Camera mainCamera = Camera.main;
        if (mainCamera != null) { float height = mainCamera.orthographicSize * 2; float width = height * mainCamera.aspect; _screenBounds = new Vector2(width / 2, height / 2); }
    }

    // --- Methods for component creation remain unchanged ---
    private void CreateAllComponents() { if (_componentsCreated) return; Create2DPhysicsComponents(); CreateVisualComponents(); _componentsCreated = true; }
    private void Create2DPhysicsComponents() { _collider = GetComponent<CircleCollider2D>(); if (_collider == null) { _collider = gameObject.AddComponent<CircleCollider2D>(); _collider.radius = 0.5f; } _rigidbody = GetComponent<Rigidbody2D>(); if (_rigidbody == null) { _rigidbody = gameObject.AddComponent<Rigidbody2D>(); _rigidbody.gravityScale = 0f; _rigidbody.linearDamping = 5f; _rigidbody.angularDamping = 5f; _rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation; _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous; _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate; } }
    private void CreateVisualComponents() { CreateSpriteRenderer(); CreateNameText(); }
    private void CreateSpriteRenderer() { _spriteRenderer = GetComponentInChildren<SpriteRenderer>(); if (_spriteRenderer == null) { GameObject spriteObj = new GameObject("PlayerSprite"); spriteObj.transform.SetParent(transform, false); spriteObj.transform.localPosition = Vector3.zero; _spriteRenderer = spriteObj.AddComponent<SpriteRenderer>(); _spriteRenderer.sprite = CreateDefaultSprite(); _spriteRenderer.sortingOrder = 1; } }
    private void CreateNameText() { _nameText = GetComponentInChildren<TextMeshPro>(); if (_nameText == null) { GameObject textObj = new GameObject("PlayerNameText"); textObj.transform.SetParent(transform, false); textObj.transform.localPosition = new Vector3(0, 1.0f, 0); _nameText = textObj.AddComponent<TextMeshPro>(); _nameText.fontSize = 4; _nameText.alignment = TextAlignmentOptions.Center; _nameText.color = Color.white; _nameText.text = "Player"; } }
    private Sprite CreateDefaultSprite() { Texture2D texture = new Texture2D(128, 128); Color[] colors = new Color[128 * 128]; Vector2 center = new Vector2(63.5f, 63.5f); float radiusSq = 60f * 60f; for (int y = 0; y < 128; y++) { for (int x = 0; x < 128; x++) { float dx = x - center.x; float dy = y - center.y; colors[y * 128 + x] = (dx * dx + dy * dy < radiusSq) ? Color.white : Color.clear; } } texture.SetPixels(colors); texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f)); }
    // ---

    public override void Spawned()
    {
        base.Spawned();
        if (!_componentsCreated) CreateAllComponents();
        if (HasStateAuthority) { if (PlayerColor.r == 0 && PlayerColor.g == 0 && PlayerColor.b == 0) PlayerColor = new Color( UnityEngine.Random.Range(0.5f, 1f), UnityEngine.Random.Range(0.5f, 1f), UnityEngine.Random.Range(0.5f, 1f) ); PositionPlayerInUniqueSpace(); NetworkedPosition = transform.position; string playerName = PlayerName.ToString(); if (!string.IsNullOrEmpty(playerName)) { PlayerData savedData = GameManager.Instance.LobbyManager.GetPlayerData(playerName); if (savedData != null) { Vector2 position = new Vector2(savedData.Position.x, savedData.Position.y); transform.position = position; NetworkedPosition = position; PlayerColor = savedData.PlayerColor; } } }
        UpdateVisuals();
        if (!HasInputAuthority && Runner != null) GameManager.Instance.PlayerManager.OnPlayerObjectSpawned(Runner, Object, Object.InputAuthority);
        if (HasInputAuthority) { string playerName = PlayerName.ToString(); if (string.IsNullOrEmpty(playerName)) { playerName = GameManager.Instance.UIManager.GetLocalPlayerName(); PlayerName = playerName; } if (!string.IsNullOrEmpty(playerName)) { RPC_RegisterPlayer(playerName, Object.InputAuthority); } }
    }

    private void PositionPlayerInUniqueSpace() { int playerCount = GameManager.Instance.PlayerManager.GetPlayerCount(); if (playerCount <= 0) playerCount = 1; int index = playerCount - 1; int cols = Mathf.CeilToInt(Mathf.Sqrt(playerCount)); int rows = Mathf.CeilToInt((float)playerCount / cols); int row = index / cols; int col = index % cols; float spacing = 4f; float startX = -((cols - 1) * spacing) / 2; float startY = -((rows - 1) * spacing) / 2; Vector2 position = new Vector2( startX + col * spacing, startY + row * spacing ); transform.position = position; NetworkedPosition = position; }
    private void UpdateVisuals() { if (_spriteRenderer != null) { _spriteRenderer.color = PlayerColor; } if (_nameText != null) { _nameText.text = PlayerName.ToString(); } }

    public override void FixedUpdateNetwork()
    {
        if (GameManager.Instance.LobbyManager.IsGameStarted() || !GameManager.Instance.NetworkManager.IsConnected) return;

        // Apply authoritative position if we don't have input/state authority
        if (!HasInputAuthority && HasStateAuthority) { transform.position = NetworkedPosition; }

        // Process Input if we have Input Authority
        if (GetInput(out NetworkInputData currentData)) // Get CURRENT input
        {
            if (HasInputAuthority) // Check if this client controls the player
            {
                // *** Player Movement Code Commented Out ***
                // ...

                SavePlayerState(); // Still save state if needed

                // *** CORRECTED: Check Ready button press (button index 1) using GetPressed ***
                // GetPressed compares currentData.buttons with _previousButtons
                NetworkButtons pressedThisTick = currentData.buttons.GetPressed(_previousButtons);

                // Check if button 1 was pressed down this tick
                if (pressedThisTick.IsSet(1))
                {
                    ToggleReady(); // Process ready button press
                }
            }

            // *** IMPORTANT: Update previous buttons state AFTER processing input for this tick ***
            _previousButtons = currentData.buttons;
        }
        // Handle case where input might not be available yet (e.g., very first tick)
        else
        {
             // Reset previous buttons if no input is available for the current tick
            _previousButtons = default;
        }
    }

    public override void Render() { if (!HasStateAuthority) transform.position = Vector2.Lerp(transform.position, NetworkedPosition, Runner.DeltaTime * 10f); UpdateVisuals(); }
    private void ToggleReady() { if (HasStateAuthority) { IsReady = !IsReady; RPC_SetReadyStatus(PlayerName.ToString(), IsReady); } }
    private void SavePlayerState() { string playerName = PlayerName.ToString(); if (string.IsNullOrEmpty(playerName)) return; PlayerData data = new PlayerData { Position = transform.position, PlayerColor = PlayerColor }; GameManager.Instance.LobbyManager.UpdatePlayerData(playerName, data); }
    public void SetPlayerName(string name) { if (HasStateAuthority) { PlayerName = name; UpdateVisuals(); } }
    public string GetPlayerName() { return PlayerName.ToString(); }
    public void SetReadyStatus(bool isReady) { if (HasStateAuthority) { IsReady = isReady; RPC_SetReadyStatus(PlayerName.ToString(), isReady); } }
    public bool GetReadyStatus() { return IsReady; }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)] private void RPC_RegisterPlayer(string playerName, PlayerRef playerRef) { GameManager.Instance.LobbyManager.RegisterPlayer(playerName, playerRef); }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)] private void RPC_SetReadyStatus(string playerName, bool isReady) { GameManager.Instance.LobbyManager.SetPlayerReadyStatus(playerName, isReady); GameManager.Instance.UIManager.UpdatePlayersList(); if (GameManager.Instance.PlayerManager.GetPlayerCount() == 1) GameManager.Instance.LobbyManager.DebugForceReadyCheck(); }

    public override void Despawned(NetworkRunner runner, bool hasState) { base.Despawned(runner, hasState); }
}