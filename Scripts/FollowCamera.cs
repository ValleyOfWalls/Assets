using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    private Transform _target;
    public Vector3 offset = new Vector3(0, 15, 0); // Higher up for better view
    public float smoothSpeed = 0.2f; // Smoother camera movement
    
    // For zooming out based on player count
    private float _targetOrthographicSize = 10f;
    private float _zoomSpeed = 2f;

    public void SetTarget(Transform target)
    {
        _target = target;
        
        if (_target != null)
        {
            // Initially set the camera position directly to avoid lag
            transform.position = _target.position + offset;
            
            // Set initial rotation
            transform.rotation = Quaternion.Euler(90, 0, 0);
        }
    }

    void LateUpdate()
    {
        if (_target == null)
            return;

        // Calculate the desired position
        Vector3 desiredPosition = _target.position + offset;
        
        // Smoothly move the camera
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
        
        // Ensure camera is pointing down for top-down view
        transform.rotation = Quaternion.Euler(90, 0, 0);
        
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