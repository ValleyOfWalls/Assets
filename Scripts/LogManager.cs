using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LogManager : MonoBehaviour
{
    private TMP_Text _debugText;
    private List<string> _logMessages = new List<string>();
    private int _maxLogMessages = 20;
    private RectTransform _debugRect;
    
    public void Initialize()
    {
        CreateDebugDisplay();
    }
    
    private void CreateDebugDisplay()
    {
        // Add debug text to the UI canvas
        // We'll create this early since other managers will log messages during initialization
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            // Create a canvas for debug text if none exists yet
            GameObject canvasObj = new GameObject("Debug Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
        }
        
        GameObject debugObj = new GameObject("Debug Text");
        debugObj.transform.SetParent(canvas.transform, false);
        _debugText = debugObj.AddComponent<TextMeshProUGUI>();
        _debugText.text = "";
        _debugText.fontSize = 12;
        _debugText.alignment = TextAlignmentOptions.Left;
        _debugText.color = Color.white;
        _debugText.textWrappingMode = TextWrappingModes.Normal; // Updated from enableWordWrapping = true
        _debugText.overflowMode = TextOverflowModes.Truncate; // Ensure text is truncated, not scrollable
        
        _debugRect = debugObj.GetComponent<RectTransform>();
        _debugRect.anchorMin = new Vector2(0.01f, 0.01f);
        _debugRect.anchorMax = new Vector2(0.4f, 0.25f);
        _debugRect.offsetMin = Vector2.zero;
        _debugRect.offsetMax = Vector2.zero;
        
        // Add a background panel to make text more readable
        GameObject backgroundObj = new GameObject("Debug Background");
        backgroundObj.transform.SetParent(canvas.transform, false);
        backgroundObj.transform.SetSiblingIndex(debugObj.transform.GetSiblingIndex());
        
        Image background = backgroundObj.AddComponent<Image>();
        background.color = new Color(0, 0, 0, 0.5f); // Semi-transparent black
        
        RectTransform bgRect = backgroundObj.GetComponent<RectTransform>();
        bgRect.anchorMin = _debugRect.anchorMin;
        bgRect.anchorMax = _debugRect.anchorMax;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // Move the debug text on top of the background
        debugObj.transform.SetAsLastSibling();
        
        LogMessage("LogManager initialized");
    }
    
    public void LogMessage(string message)
    {
        Debug.Log($"[GameManager] {message}");
        
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _logMessages.Add($"[{timestamp}] {message}");
        
        // Keep log at reasonable size
        while (_logMessages.Count > _maxLogMessages)
            _logMessages.RemoveAt(0);
            
        UpdateDebugText();
    }

    public void LogError(string message)
    {
        Debug.LogError($"[GameManager] {message}");
        
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _logMessages.Add($"[{timestamp}] ERROR: {message}");
        
        // Keep log at reasonable size
        while (_logMessages.Count > _maxLogMessages)
            _logMessages.RemoveAt(0);
            
        UpdateDebugText();
    }

    private void UpdateDebugText()
    {
        if (_debugText == null)
            return;
            
        // Join all messages with newlines
        string fullText = string.Join("\n", _logMessages);
        _debugText.text = fullText;
        
        // Force a canvas update to get accurate text measurements
        Canvas.ForceUpdateCanvases();
        
        // Check if text exceeds the available height
        // We need to check the preferred height against the actual rect height
        float availableHeight = _debugRect.rect.height;
        float textHeight = _debugText.preferredHeight;
        
        // If text is too tall, remove oldest messages until it fits
        while (textHeight > availableHeight && _logMessages.Count > 0)
        {
            // Remove the oldest message
            _logMessages.RemoveAt(0);
            
            // Update text and recalculate height
            fullText = string.Join("\n", _logMessages);
            _debugText.text = fullText;
            
            // Force update to get new measurements
            Canvas.ForceUpdateCanvases();
            textHeight = _debugText.preferredHeight;
        }
    }
    
    // Add this method to adjust the max visible messages
    public void SetMaxMessages(int maxMessages)
    {
        _maxLogMessages = Mathf.Max(1, maxMessages); // Ensure at least 1 message
        
        // Trim existing messages if needed
        while (_logMessages.Count > _maxLogMessages)
            _logMessages.RemoveAt(0);
            
        UpdateDebugText();
    }
}