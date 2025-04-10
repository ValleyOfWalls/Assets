using UnityEngine;
using TMPro;

public class PlayerUIController
{
    // UI elements
    private GameObject _statsPanel;
    private TMP_Text _nameText;
    private TMP_Text _healthText;
    private TMP_Text _energyText;
    private TMP_Text _scoreText;
    
    // Reference to player state
    private PlayerState _playerState;
    
    public PlayerUIController(GameObject statsPanel, PlayerState playerState)
    {
        _statsPanel = statsPanel;
        _playerState = playerState;
        
        // Create UI elements
        CreateNameText();
        CreateStatsTexts();
        
        // Initial update
        UpdateStats(playerState);
    }
    
    private void CreateNameText()
    {
        GameObject nameObj = new GameObject("PlayerName");
        nameObj.transform.SetParent(_statsPanel.transform, false);
        _nameText = nameObj.AddComponent<TextMeshProUGUI>();
        
        string playerName = "Player";
        try {
            if (_playerState != null) {
                playerName = _playerState.PlayerName.ToString();
            }
        }
        catch (System.Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Could not access PlayerName: {ex.Message}. Using default name.");
        }
        
        _nameText.text = playerName;
        _nameText.fontSize = 20;
        _nameText.color = Color.white;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.fontStyle = FontStyles.Bold;
        
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.85f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(10, 0);
        nameRect.offsetMax = new Vector2(-10, -5);
    }
    
    private void CreateStatsTexts()
    {
        // Create health text
        _healthText = CreateStatText("Health", "Health: 50/50", 0.7f, 0.85f);
        
        // Create energy text
        _energyText = CreateStatText("Energy", "Energy: 3/3", 0.55f, 0.7f);
        
        // Create score text
        _scoreText = CreateStatText("Score", "Score: 0", 0.4f, 0.55f);
    }
    
    private TMP_Text CreateStatText(string name, string defaultText, float minY, float maxY)
    {
        GameObject statObj = new GameObject(name);
        statObj.transform.SetParent(_statsPanel.transform, false);
        TMP_Text statText = statObj.AddComponent<TextMeshProUGUI>();
        statText.text = defaultText;
        statText.fontSize = 18;
        statText.color = Color.white;
        statText.alignment = TextAlignmentOptions.Left;
        
        RectTransform statRect = statObj.GetComponent<RectTransform>();
        statRect.anchorMin = new Vector2(0.05f, minY);
        statRect.anchorMax = new Vector2(0.95f, maxY);
        statRect.offsetMin = Vector2.zero;
        statRect.offsetMax = Vector2.zero;
        
        return statText;
    }
    
    public void UpdateStats(PlayerState playerState)
    {
        if (playerState == null) return;
        
        try {
            // Update name if changed
            string playerName = playerState.PlayerName.ToString();
            if (_nameText != null && !_nameText.text.Equals(playerName))
            {
                _nameText.text = playerName;
            }
            
            // Update health
            if (_healthText != null)
            {
                _healthText.text = $"Health: {playerState.Health}/{playerState.MaxHealth}";
            }
            
            // Update energy
            if (_energyText != null)
            {
                _energyText.text = $"Energy: {playerState.Energy}/{playerState.MaxEnergy}";
            }
            
            // Update score
            if (_scoreText != null)
            {
                _scoreText.text = $"Score: {playerState.GetScore()}";
            }
        } 
        catch (System.Exception ex) {
            GameManager.Instance.LogManager.LogMessage($"Error updating player stats: {ex.Message}");
        }
    }
}