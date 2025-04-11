using System;
using System.Collections.Generic;
using TMPro; // Still needed if you might add UI back later, otherwise removable
using UnityEngine;
using UnityEngine.UI; // Still needed if you might add UI back later, otherwise removable

public class LogManager : MonoBehaviour
{
    // --- UI References Removed ---
    // private TMP_Text _debugText;
    // private RectTransform _debugRect;
    // private GameObject _debugTextObject; // Keep track for potential destruction
    // private GameObject _debugBackgroundObject; // Keep track for potential destruction

    private List<string> _logMessages = new List<string>();
    private int _maxLogMessages = 20; // Keep max messages for potential future UI use
    private bool _isInitialized = false; // Flag to prevent double init


    public void Initialize()
    {
        if (_isInitialized) return;

        // CreateDebugDisplay(); // REMOVED Call to create UI
        LogMessage("LogManager initialized (Debug UI Disabled)."); // Log to console
        _isInitialized = true;
    }

    // --- UI Creation Method Removed ---
    /*
    private void CreateDebugDisplay()
    {
        // ... Entire method removed ...
    }
    */

    public void LogMessage(string message)
    {
        // Log to Unity console ALWAYS
        Debug.Log($"[GameManager] {message}");

        // Add to internal list (for potential future UI display or buffer)
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _logMessages.Add($"[{timestamp}] {message}");

        // Keep log at reasonable size
        while (_logMessages.Count > _maxLogMessages)
            _logMessages.RemoveAt(0);

        // UpdateDebugText(); // REMOVED Call to update UI
    }

    public void LogError(string message)
    {
        // Log to Unity console ALWAYS
        Debug.LogError($"[GameManager] {message}");

        // Add to internal list
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _logMessages.Add($"[{timestamp}] ERROR: {message}");

        // Keep log at reasonable size
        while (_logMessages.Count > _maxLogMessages)
            _logMessages.RemoveAt(0);

        // UpdateDebugText(); // REMOVED Call to update UI
    }

    // --- Debug Text Update Method Removed ---
    /*
    private void UpdateDebugText()
    {
        // if (_debugText == null) return;
        // ... Logic to update _debugText.text removed ...
    }
    */

    // This method might still be useful if you re-enable the UI later
    public void SetMaxMessages(int maxMessages)
    {
        _maxLogMessages = Mathf.Max(1, maxMessages); // Ensure at least 1 message

        // Trim existing messages if needed (affects internal buffer)
        while (_logMessages.Count > _maxLogMessages)
            _logMessages.RemoveAt(0);
        // UpdateDebugText(); // REMOVED Call to update UI
    }

     // --- ADDED Cleanup ---
     // Optional: If you want to be extra sure no old UI elements remain
     public void DestroyDebugUI()
     {
         // Find potentially existing objects by name if needed and destroy them
         GameObject existingText = GameObject.Find("Debug Text");
         if (existingText != null) Destroy(existingText);

         GameObject existingBg = GameObject.Find("Debug Background");
         if (existingBg != null) Destroy(existingBg);
     }

     // Example usage if needed during cleanup:
     // private void OnDestroy() {
     //     DestroyDebugUI();
     // }
}