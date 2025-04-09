using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OpponentStatsDisplay : MonoBehaviour
{
    // References
    private TMP_Text _nameText;
    private TMP_Text _healthText;
    private TMP_Text _scoreText;
    
    // Data
    private PlayerState _playerState;
    
    // Add these to help with debugging
    private bool _textElementsSet = false;
    
    private void Awake()
    {
        // Check if we already have child text elements we can use
        TMP_Text[] existingTexts = GetComponentsInChildren<TMP_Text>();
        if (existingTexts.Length >= 3)
        {
            // Try to find the text elements by name
            foreach (TMP_Text text in existingTexts)
            {
                if (text.gameObject.name.Contains("Name"))
                    _nameText = text;
                else if (text.gameObject.name.Contains("Health"))
                    _healthText = text;
                else if (text.gameObject.name.Contains("Score"))
                    _scoreText = text;
            }
            
            // If we found all three, mark as set
            if (_nameText != null && _healthText != null && _scoreText != null)
            {
                _textElementsSet = true;
                if (GameManager.Instance != null)
                    GameManager.Instance.LogManager.LogMessage("OpponentStatsDisplay text elements found automatically");
            }
        }
    }
    
    public void SetTextElements(TMP_Text nameText, TMP_Text healthText, TMP_Text scoreText)
    {
        _nameText = nameText;
        _healthText = healthText;
        _scoreText = scoreText;
        
        // Set default text to verify they work
        if (_nameText != null)
            _nameText.text = "Opponent";
        if (_healthText != null)
            _healthText.text = "HP: --/--";
        if (_scoreText != null)
            _scoreText.text = "Score: --";
        
        _textElementsSet = (_nameText != null && _healthText != null && _scoreText != null);
        
        // Debug to verify text elements are set
        if (GameManager.Instance != null)
        {
            if (_textElementsSet)
                GameManager.Instance.LogManager.LogMessage("OpponentStatsDisplay text elements set successfully");
            else
                GameManager.Instance.LogManager.LogError($"OpponentStatsDisplay text elements not all set: Name={_nameText != null}, Health={_healthText != null}, Score={_scoreText != null}");
        }
    }
    
    public void UpdateDisplay(PlayerState playerState)
    {
        _playerState = playerState;
        
        if (_playerState == null)
        {
            GameManager.Instance.LogManager.LogError("Cannot update display with null PlayerState");
            return;
        }
        
        // Check if we have valid text elements
        if (!_textElementsSet)
        {
            // Try to find text elements if they weren't set
            TMP_Text[] existingTexts = GetComponentsInChildren<TMP_Text>(true);
            if (existingTexts.Length >= 3)
            {
                // Assign them based on sibling index or try to identify by name
                if (existingTexts[0] != null && _nameText == null)
                    _nameText = existingTexts[0];
                if (existingTexts.Length > 1 && existingTexts[1] != null && _healthText == null)
                    _healthText = existingTexts[1];
                if (existingTexts.Length > 2 && existingTexts[2] != null && _scoreText == null)
                    _scoreText = existingTexts[2];
                
                _textElementsSet = (_nameText != null && _healthText != null && _scoreText != null);
                
                if (_textElementsSet)
                    GameManager.Instance.LogManager.LogMessage("OpponentStatsDisplay text elements recovered from children");
                else
                    GameManager.Instance.LogManager.LogError($"Failed to recover text elements: found {existingTexts.Length} text components");
            }
            else
            {
                GameManager.Instance.LogManager.LogError($"Cannot find text elements: found only {existingTexts.Length} text components");
            }
        }
        
        // Update name with explicit ToString to avoid NetworkString issues
        if (_nameText != null)
        {
            string playerName = _playerState.PlayerName.ToString();
            _nameText.text = string.IsNullOrEmpty(playerName) ? "Unknown Player" : playerName;
            GameManager.Instance.LogManager.LogMessage($"Updated opponent name text: {_nameText.text}");
        }
        else
        {
            GameManager.Instance.LogManager.LogError("Name text element is null in OpponentStatsDisplay");
        }
        
        // Update health with explicit value access
        if (_healthText != null)
        {
            _healthText.text = $"HP: {_playerState.Health}/{_playerState.MaxHealth}";
            GameManager.Instance.LogManager.LogMessage($"Updated opponent health text: {_healthText.text}");
        }
        else
        {
            GameManager.Instance.LogManager.LogError("Health text element is null in OpponentStatsDisplay");
        }
        
        // Update score with the method call
        if (_scoreText != null)
        {
            _scoreText.text = $"Score: {_playerState.GetScore()}";
            GameManager.Instance.LogManager.LogMessage($"Updated opponent score text: {_scoreText.text}");
        }
        else
        {
            GameManager.Instance.LogManager.LogError("Score text element is null in OpponentStatsDisplay");
        }
    }
    
    private void OnEnable()
    {
        // When enabled, check if we need to update the display
        if (_playerState != null && _textElementsSet)
        {
            UpdateDisplay(_playerState);
        }
    }
    
    // This method can be used to debug the display in the editor
    public void ForceCreateTextElements()
    {
        if (_textElementsSet)
            return;
            
        // Create name text if needed
        if (_nameText == null)
        {
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(transform, false);
            _nameText = nameObj.AddComponent<TextMeshProUGUI>();
            _nameText.fontSize = 18;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.color = Color.white;
            _nameText.fontStyle = FontStyles.Bold;
            _nameText.text = "Opponent";
            
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.7f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(5, 0);
            nameRect.offsetMax = new Vector2(-5, -5);
        }
        
        // Create health text if needed
        if (_healthText == null)
        {
            GameObject healthObj = new GameObject("Health");
            healthObj.transform.SetParent(transform, false);
            _healthText = healthObj.AddComponent<TextMeshProUGUI>();
            _healthText.fontSize = 16;
            _healthText.alignment = TextAlignmentOptions.Left;
            _healthText.color = Color.red;
            _healthText.text = "HP: --/--";
            
            RectTransform healthRect = healthObj.GetComponent<RectTransform>();
            healthRect.anchorMin = new Vector2(0.05f, 0.35f);
            healthRect.anchorMax = new Vector2(0.95f, 0.65f);
            healthRect.offsetMin = Vector2.zero;
            healthRect.offsetMax = Vector2.zero;
        }
        
        // Create score text if needed
        if (_scoreText == null)
        {
            GameObject scoreObj = new GameObject("Score");
            scoreObj.transform.SetParent(transform, false);
            _scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
            _scoreText.fontSize = 16;
            _scoreText.alignment = TextAlignmentOptions.Left;
            _scoreText.color = Color.yellow;
            _scoreText.text = "Score: --";
            
            RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.05f, 0.05f);
            scoreRect.anchorMax = new Vector2(0.95f, 0.35f);
            scoreRect.offsetMin = Vector2.zero;
            scoreRect.offsetMax = Vector2.zero;
        }
        
        _textElementsSet = true;
        GameManager.Instance.LogManager.LogMessage("Force-created text elements for OpponentStatsDisplay");
    }
}