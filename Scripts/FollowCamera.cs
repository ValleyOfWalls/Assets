using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    private Transform _target;
    public float smoothSpeed = 0.2f;
    
    // For zooming out based on player count
    private float _targetOrthographicSize = 10f;
    private float _zoomSpeed = 2f;

    public void SetTarget(Transform target)
    {
        _target = target;
        
        if (_target != null)
        {
            // Initially set the camera position directly to avoid lag
            transform.position = new Vector3(_target.position.x, _target.position.y, -10); // Keep z at -10 for 2D
        }
    }

    void LateUpdate()
    {
        if (_target == null)
            return;

        // Calculate the desired position (keep z at -10 for 2D camera)
        Vector3 desiredPosition = new Vector3(_target.position.x, _target.position.y, -10);
        
        // Smoothly move the camera
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
        
        // Adjust orthographic size based on player count
        AdjustZoomForPlayerCount();
    }
    
    private void AdjustZoomForPlayerCount()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic)
            return;
            
        // Determine how many players are in the game
        int playerCount = 0;
        
        if (GameManager.Instance != null && GameManager.Instance.PlayerManager != null)
        {
            playerCount = GameManager.Instance.PlayerManager.GetPlayerCount();
        }
        
        // Set target orthographic size based on player count
        // More players = zoomed out more
        if (playerCount <= 1)
            _targetOrthographicSize = 10f;
        else if (playerCount <= 2)
            _targetOrthographicSize = 15f;
        else if (playerCount <= 3)
            _targetOrthographicSize = 20f;
        else
            _targetOrthographicSize = 25f;
            
        // Smoothly adjust the camera zoom
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, _targetOrthographicSize, Time.deltaTime * _zoomSpeed);
    }
}