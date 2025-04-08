using UnityEngine;

public class CameraManager : MonoBehaviour
{
    private GameObject _cameraPrefab;
    private GameObject _activeCamera;
    
    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing CameraManager...");
        CreateCameraPrefab();
    }
    
    private void CreateCameraPrefab()
    {
        _cameraPrefab = new GameObject("Camera_Prefab");
        _cameraPrefab.SetActive(false);
        DontDestroyOnLoad(_cameraPrefab);
        
        // Add camera component
        Camera cam = _cameraPrefab.AddComponent<Camera>();
        
        // Configure for 2D top-down view
        cam.orthographic = true;
        cam.orthographicSize = 10f; // Larger field of view
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.2f, 0.3f); // Dark blue-ish background
        
        // We'll only add an AudioListener if one doesn't already exist in the scene
        // This fixes the "2 audio listeners" warning
        if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
        {
            _cameraPrefab.AddComponent<AudioListener>();
            GameManager.Instance.LogManager.LogMessage("Added AudioListener to camera");
        }
        else
        {
            GameManager.Instance.LogManager.LogMessage("AudioListener already exists in scene, not adding to camera");
        }
        
        // Add follow script
        FollowCamera follow = _cameraPrefab.AddComponent<FollowCamera>();
        follow.offset = new Vector3(0, 15, 0); // Higher up for better view
        follow.smoothSpeed = 0.2f; // Smoother following
        
        GameManager.Instance.LogManager.LogMessage("Camera prefab created");
    }
    
    public void CreatePlayerCamera(Transform playerTransform)
    {
        if (_cameraPrefab == null)
        {
            GameManager.Instance.LogManager.LogError("Camera prefab is null");
            return;
        }
        
        // If an active camera already exists, destroy it first
        if (_activeCamera != null)
        {
            Destroy(_activeCamera);
        }
        
        _activeCamera = Instantiate(_cameraPrefab);
        _activeCamera.SetActive(true);
        
        // Get camera component
        Camera camera = _activeCamera.GetComponent<Camera>();
        if (camera != null)
        {
            // Make sure it's set to orthographic for 2D view
            camera.orthographic = true;
            camera.orthographicSize = 10f; // Set to appropriate size to see multiple players
        }
        
        FollowCamera followCam = _activeCamera.GetComponent<FollowCamera>();
        if (followCam != null)
        {
            followCam.SetTarget(playerTransform);
            followCam.offset = new Vector3(0, 15, -2); // Position behind and above for better perspective
            GameManager.Instance.LogManager.LogMessage("Camera created and following player");
        }
        else
        {
            GameManager.Instance.LogManager.LogError("FollowCamera component not found on camera prefab");
        }
    }
}