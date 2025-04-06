using UnityEngine;
using Fusion;
using TMPro;

public class Player : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    
    // Networked properties
    [Networked] public Color PlayerColor { get; set; }
    [Networked] public NetworkString<_16> Nickname { get; set; }
    
    // Components
    private CharacterController _characterController;
    private Transform _cameraTransform;
    private TextMesh _nicknameText;
    private MeshRenderer _renderer;
    
    // Track previous values for manual change detection
    private string _previousNickname;
    private Color _previousColor;
    
    private void Awake()
    {
        // Get or add components
        _characterController = GetComponent<CharacterController>() ?? gameObject.AddComponent<CharacterController>();
        _characterController.center = new Vector3(0, 1, 0);
        _characterController.height = 2f;
        _characterController.radius = 0.5f;
        
        // Create player model if it doesn't exist
        if (transform.childCount == 0)
        {
            CreatePlayerModel();
        }
        
        // Get existing components from model
        _renderer = GetComponentInChildren<MeshRenderer>();
        _nicknameText = GetComponentInChildren<TextMesh>();
        
        // If components don't exist, create them
        if (_renderer == null || _nicknameText == null)
        {
            CreatePlayerModel();
            _renderer = GetComponentInChildren<MeshRenderer>();
            _nicknameText = GetComponentInChildren<TextMesh>();
        }
    }
    
    public override void Spawned()
    {
        // Setup player for the local player
        if (Object.HasInputAuthority)
        {
            // Create camera for local player
            SetupCamera();
            
            // Set default properties
            if (string.IsNullOrEmpty(Nickname.ToString()))
            {
                RPC_SetNickname($"Player {Runner.LocalPlayer.PlayerId}");
            }
            
            // Set random color (only if we have StateAuthority)
            if (Object.HasStateAuthority)
            {
                PlayerColor = new Color(Random.value, Random.value, Random.value);
            }
        }
        
        // Apply current properties
        UpdatePlayerColor();
        if (_nicknameText != null)
        {
            _nicknameText.text = Nickname.ToString();
        }
        
        // Initialize previous values
        _previousNickname = Nickname.ToString();
        _previousColor = PlayerColor;
    }
    
    // Check for property changes manually
    public override void Render()
    {
        // Check for nickname changes
        string currentNickname = Nickname.ToString();
        if (_previousNickname != currentNickname)
        {
            _previousNickname = currentNickname;
            UpdateNicknameText();
        }
        
        // Check for color changes
        if (_previousColor != PlayerColor)
        {
            _previousColor = PlayerColor;
            UpdatePlayerColor();
        }
    }
    
    private void CreatePlayerModel()
    {
        // Create capsule for player model
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        model.transform.SetParent(transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        
        // Remove collider since we're using CharacterController
        Destroy(model.GetComponent<Collider>());
        
        // Create nickname text
        GameObject textObj = new GameObject("NicknameText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0, 2.3f, 0);
        textObj.transform.localRotation = Quaternion.identity;
        
        // Add TextMesh component
        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.fontSize = 42;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = Color.white;
        textMesh.text = "Player";
        
        // Scale text to be readable
        textObj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        
        // Add Billboard script
        textObj.AddComponent<Billboard>();
        
        // Save references
        _renderer = model.GetComponent<MeshRenderer>();
        _nicknameText = textMesh;
    }
    
    private void SetupCamera()
    {
        // Create camera for local player
        GameObject cameraObj = new GameObject("PlayerCamera");
        _cameraTransform = cameraObj.transform;
        _cameraTransform.SetParent(transform);
        _cameraTransform.localPosition = new Vector3(0, 3, -6);
        _cameraTransform.localRotation = Quaternion.Euler(20, 0, 0);
        
        // Add camera component
        Camera camera = cameraObj.AddComponent<Camera>();
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1000f;
        
        // Destroy existing main camera if it exists
        if (Camera.main != null && Camera.main.gameObject != cameraObj)
        {
            Destroy(Camera.main.gameObject);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Only move if we have input authority
        if (GetInput(out NetworkInputData data))
        {
            // Normalize input direction and apply speed
            Vector3 move = new Vector3(data.direction.x, 0, data.direction.z);
            if (move.sqrMagnitude > 0)
            {
                move.Normalize();
                move *= moveSpeed * Runner.DeltaTime;
                
                // Apply movement
                _characterController.Move(move);
                
                // Rotate player to face movement direction
                if (move != Vector3.zero)
                {
                    transform.forward = move;
                }
            }
            
            // Apply gravity
            _characterController.Move(new Vector3(0, Physics.gravity.y * Runner.DeltaTime, 0));
        }
    }
    
    // Update player color
    private void UpdatePlayerColor()
    {
        if (_renderer != null)
        {
            _renderer.material.color = PlayerColor;
        }
    }
    
    // Update nickname text
    private void UpdateNicknameText()
    {
        if (_nicknameText != null)
        {
            _nicknameText.text = Nickname.ToString();
        }
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetNickname(string newNickname)
    {
        Nickname = newNickname;
    }
}

// Simple billboard script to make text face camera
public class Billboard : MonoBehaviour
{
    private void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.forward = Camera.main.transform.forward;
        }
    }
}