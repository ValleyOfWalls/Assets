using Fusion;
using UnityEngine;

public class Player : NetworkBehaviour
{
    [Networked] public Color PlayerColor { get; set; }
    
    public MeshRenderer _meshRenderer;
    private CharacterController _characterController;
    
    private void Awake()
    {
        // Find character controller if not already set
        _characterController = GetComponent<CharacterController>();
    }

    public override void Spawned()
    {
        base.Spawned();
        
        // Find mesh renderer if not assigned in prefab
        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
        }
        
        // Set a random color for the player when it's first spawned
        if (HasStateAuthority)
        {
            PlayerColor = new Color(
                UnityEngine.Random.Range(0f, 1f),
                UnityEngine.Random.Range(0f, 1f),
                UnityEngine.Random.Range(0f, 1f)
            );
        }
        
        // Apply the color to the visual
        UpdateVisuals();
        
        Debug.Log($"Player spawned with ID: {Object.Id}");
    }
    
    private void UpdateVisuals()
    {
        if (_meshRenderer != null)
        {
            _meshRenderer.material.color = PlayerColor;
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            // Apply movement only when we have input authority
            if (HasInputAuthority)
            {
                // Simple movement implementation
                Vector3 move = new Vector3(data.horizontal, 0, data.vertical);
                move = move.normalized * 5f * Runner.DeltaTime;
                
                if (_characterController != null)
                {
                    _characterController.Move(move);
                    
                    // Apply gravity
                    _characterController.Move(Vector3.down * 9.81f * Runner.DeltaTime);
                }
                
                // Handle jump
                if (data.buttons.IsSet(0) && _characterController.isGrounded)
                {
                    // Jump logic
                    Vector3 jumpVector = Vector3.up * 5f;
                    _characterController.Move(jumpVector * Runner.DeltaTime);
                }
            }
        }
    }
    
    public override void Render()
    {
        // Update visuals if color has changed over the network
        UpdateVisuals();
    }
}