using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Added missing namespace
using TMPro; // Added missing namespace
using Fusion;

public class OpponentsUIController
{
    // UI elements
    private GameObject _opponentsPanel;
    private GameObject _opponentStatsPrefab;
    
    // Dictionary to track opponent displays
    private Dictionary<PlayerRef, OpponentStatsDisplay> _opponentDisplays = new Dictionary<PlayerRef, OpponentStatsDisplay>();
    
    public OpponentsUIController(GameObject opponentsPanel)
    {
        _opponentsPanel = opponentsPanel;
        
        // Create the opponent stats prefab
        _opponentStatsPrefab = CreateOpponentStatsPrefab();
        
        // Initialize
        InitializeOpponentStates();
    }
    
    private GameObject CreateOpponentStatsPrefab()
    {
        GameObject opponentObj = new GameObject("OpponentStatsPrefab");
        opponentObj.SetActive(false);
        
        // Background
        Image bg = opponentObj.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
        
        // Layout
        RectTransform rect = opponentObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 100);
        
        // Add layout element for sizing
        LayoutElement layoutElement = opponentObj.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 100;
        layoutElement.flexibleWidth = 1;
        
        // Add stats display component
        OpponentStatsDisplay display = opponentObj.AddComponent<OpponentStatsDisplay>();
        
        // Name
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(opponentObj.transform, false);
        TMP_Text nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 18;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        nameText.fontStyle = FontStyles.Bold;
        
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.7f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(5, 0);
        nameRect.offsetMax = new Vector2(-5, -5);
        
        // Health
        GameObject healthObj = new GameObject("Health");
        healthObj.transform.SetParent(opponentObj.transform, false);
        TMP_Text healthText = healthObj.AddComponent<TextMeshProUGUI>();
        healthText.fontSize = 16;
        healthText.alignment = TextAlignmentOptions.Left;
        healthText.color = Color.red;
        
        RectTransform healthRect = healthObj.GetComponent<RectTransform>();
        healthRect.anchorMin = new Vector2(0.05f, 0.35f);
        healthRect.anchorMax = new Vector2(0.95f, 0.65f);
        healthRect.offsetMin = Vector2.zero;
        healthRect.offsetMax = Vector2.zero;
        
        // Score
        GameObject scoreObj = new GameObject("Score");
        scoreObj.transform.SetParent(opponentObj.transform, false);
        TMP_Text scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
        scoreText.fontSize = 16;
        scoreText.alignment = TextAlignmentOptions.Left;
        scoreText.color = Color.yellow;
        
        RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0.05f, 0.05f);
        scoreRect.anchorMax = new Vector2(0.95f, 0.35f);
        scoreRect.offsetMin = Vector2.zero;
        scoreRect.offsetMax = Vector2.zero;
        
        // Set references
        display.SetTextElements(nameText, healthText, scoreText);
        
        return opponentObj;
    }
    
    private void InitializeOpponentStates()
    {
        if (GameState.Instance == null) return;
        
        var allPlayerStates = GameState.Instance.GetAllPlayerStates();
        PlayerRef localPlayerRef = GameState.Instance.GetLocalPlayerRef();
        
        foreach (var entry in allPlayerStates)
        {
            if (entry.Key != localPlayerRef)
            {
                AddOpponentDisplay(entry.Key, entry.Value);
            }
        }
    }
    
    public void AddOpponentDisplay(PlayerRef playerRef, PlayerState playerState)
    {
        if (_opponentsPanel == null || _opponentStatsPrefab == null)
        {
            GameManager.Instance.LogManager.LogError("Cannot create opponent display: panel or prefab is null");
            return;
        }
        
        // Skip if we already have this opponent
        if (_opponentDisplays.ContainsKey(playerRef))
        {
            UpdateOpponentStats(playerState);
            return;
        }
        
        string playerName = playerState?.PlayerName.ToString() ?? "Unknown";
        GameManager.Instance.LogManager.LogMessage($"Creating opponent display for {playerName}");
        
        // Create new display
        GameObject opponentObj = GameObject.Instantiate(_opponentStatsPrefab, _opponentsPanel.transform);
        opponentObj.SetActive(true);
        opponentObj.name = $"OpponentDisplay_{playerName}";
        
        OpponentStatsDisplay display = opponentObj.GetComponent<OpponentStatsDisplay>();
        if (display != null)
        {
            // Force create text elements if needed
            display.ForceCreateTextElements();
            
            // Store in the dictionary
            _opponentDisplays[playerRef] = display;
            
            // Update with player state
            display.UpdateDisplay(playerState);
        }
        else
        {
            GameManager.Instance.LogManager.LogError("OpponentStatsDisplay component not found on instantiated prefab");
        }
    }
    
    public void RemoveOpponentDisplay(PlayerRef playerRef)
    {
        if (_opponentDisplays.ContainsKey(playerRef))
        {
            if (_opponentDisplays[playerRef] != null && _opponentDisplays[playerRef].gameObject != null)
            {
                GameObject.Destroy(_opponentDisplays[playerRef].gameObject);
            }
            
            _opponentDisplays.Remove(playerRef);
        }
    }
    
    public void UpdateOpponentStats(PlayerState playerState)
    {
        if (GameState.Instance == null) return;
        
        // Find the player ref for this player state
        PlayerRef playerRef = default;
        var allPlayerStates = GameState.Instance.GetAllPlayerStates();
        foreach (var entry in allPlayerStates)
        {
            if (entry.Value == playerState)
            {
                playerRef = entry.Key;
                break;
            }
        }
        
        if (playerRef != default && _opponentDisplays.TryGetValue(playerRef, out OpponentStatsDisplay display))
        {
            display.UpdateDisplay(playerState);
        }
    }
    
    public void UpdateAllOpponents()
    {
        if (GameState.Instance == null) return;
        
        var allPlayerStates = GameState.Instance.GetAllPlayerStates();
        PlayerRef localPlayerRef = GameState.Instance.GetLocalPlayerRef();
        
        foreach (var entry in allPlayerStates)
        {
            if (entry.Key != localPlayerRef)
            {
                if (_opponentDisplays.TryGetValue(entry.Key, out OpponentStatsDisplay display))
                {
                    display.UpdateDisplay(entry.Value);
                }
                else
                {
                    AddOpponentDisplay(entry.Key, entry.Value);
                }
            }
        }
    }
}