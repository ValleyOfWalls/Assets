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
        RectTransform debugRect = debugObj.GetComponent<RectTransform>();
        debugRect.anchorMin = new Vector2(0.01f, 0.01f);
        debugRect.anchorMax = new Vector2(0.4f, 0.25f);
        debugRect.offsetMin = Vector2.zero;
        debugRect.offsetMax = Vector2.zero;
        
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
        if (_debugText != null)
        {
            _debugText.text = string.Join("\n", _logMessages);
        }
    }
}