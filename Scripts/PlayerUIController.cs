using UnityEngine;
using TMPro; // Required for TextMeshProUGUI
using Fusion; // Required for NetworkString etc.
using System; // *** ADDED: For Exception types ***

public class PlayerUIController
{
    // UI elements
    private GameObject _statsPanel; // Parent panel for stats UI
    private TMP_Text _nameText;
    private TMP_Text _healthText;
    private TMP_Text _energyText;
    private TMP_Text _scoreText;

    // Reference to player state (should be the LOCAL player's state)
    private PlayerState _playerState;

    // Default texts
    private const string DefaultName = "Player";
    private const string DefaultHealth = "HP: --/--";
    private const string DefaultEnergy = "Energy: -/-";
    private const string DefaultScore = "Score: -";


    public PlayerUIController(GameObject statsPanel, PlayerState playerState)
    {
        _statsPanel = statsPanel;
        _playerState = playerState;

        if (_statsPanel == null) {
             Debug.LogError("PlayerUIController: Stats Panel is null!");
             return;
        }
         if (_playerState == null) {
             Debug.LogError("PlayerUIController: PlayerState is null! Cannot initialize.");
             // Don't proceed if state is null
             // Set default texts in Create methods as fallback
             CreateNameText();
             CreateStatsTexts();
             return;
         }

        // Create UI elements within the stats panel
        CreateNameText();
        CreateStatsTexts();

        // Attempt initial update, guarding against null/unspawned state
        UpdateStats(_playerState);

        // Event subscriptions are likely handled by GameUI now

        GameManager.Instance?.LogManager?.LogMessage($"PlayerUIController initialized for PlayerState ID: {_playerState.Id}.");
    }

    private void CreateNameText()
    {
         if (_statsPanel == null) return; // Guard against null panel

        GameObject nameObj = new GameObject("PlayerName_UI");
        nameObj.transform.SetParent(_statsPanel.transform, false);
        _nameText = nameObj.AddComponent<TextMeshProUGUI>();

        _nameText.text = DefaultName; // Default text first
        _nameText.fontSize = 20;
        _nameText.color = Color.white;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.fontStyle = FontStyles.Bold;

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.85f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(10, 0);
        nameRect.offsetMax = new Vector2(-10, -5);

        TryUpdateNameText(); // Attempt to set actual name
    }

    private void CreateStatsTexts()
    {
        if (_statsPanel == null) return; // Guard

        _healthText = CreateStatTextElement("Health_UI", DefaultHealth, 0.7f, 0.85f);
        _energyText = CreateStatTextElement("Energy_UI", DefaultEnergy, 0.55f, 0.7f);
        _scoreText = CreateStatTextElement("Score_UI", DefaultScore, 0.4f, 0.55f);
    }

    private TMP_Text CreateStatTextElement(string name, string defaultText, float minY, float maxY)
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
        statRect.offsetMin = new Vector2(5, 2);
        statRect.offsetMax = new Vector2(-5, -2);

        return statText;
    }

     private void TryUpdateNameText()
     {
         if (_nameText == null) return;

         // *** FIXED: Check the NetworkObject associated with the PlayerState ***
         // A NetworkBehaviour is ready when its 'Object' property is valid and spawned.
         if (_playerState != null && _playerState.Object != null && _playerState.Object.IsValid)
         {
             try
             {
                 string playerName = _playerState.PlayerName.ToString(); // Access NetworkString
                 _nameText.text = string.IsNullOrEmpty(playerName) ? DefaultName : playerName;
             }
             catch (InvalidOperationException ex)
             {
                  GameManager.Instance?.LogManager?.LogMessage($"Caught exception accessing PlayerName (likely timing): {ex.Message}. Setting default.");
                  _nameText.text = DefaultName;
             }
             catch (Exception ex)
             {
                  GameManager.Instance?.LogManager?.LogError($"Unexpected error updating name text: {ex.Message}");
                  _nameText.text = DefaultName;
             }
         }
         else
         {
             _nameText.text = DefaultName; // State not ready
         }
     }


    // Updates all stats UI elements based on the provided PlayerState
    public void UpdateStats(PlayerState playerState)
    {
        if (playerState == null || playerState != _playerState) return; // Ignore if not the expected state

        // *** FIXED: Check the NetworkObject associated with the PlayerState ***
        // Use 'Object.IsValid' as the primary check for whether the state is ready for access.
        if (_playerState.Object == null || !_playerState.Object.IsValid)
        {
             // GameManager.Instance?.LogManager?.LogMessage($"PlayerUIController: UpdateStats called but PlayerState Object is not valid ({_playerState?.Id}). Setting defaults.");
             if (_nameText != null) _nameText.text = DefaultName;
             if (_healthText != null) _healthText.text = DefaultHealth;
             if (_energyText != null) _energyText.text = DefaultEnergy;
             if (_scoreText != null) _scoreText.text = DefaultScore;
            return;
        }

        // GameManager.Instance?.LogManager?.LogMessage($"PlayerUIController: Updating stats for {playerState.PlayerName}");

        try {
            // Safely update UI elements now that we know the state Object is valid
            TryUpdateNameText(); // Update name separately

            if (_healthText != null)
            {
                _healthText.text = $"HP: {playerState.Health}/{playerState.MaxHealth}";
            }
            if (_energyText != null)
            {
                _energyText.text = $"Energy: {playerState.Energy}/{playerState.MaxEnergy}";
            }
            if (_scoreText != null)
            {
                _scoreText.text = $"Score: {playerState.GetScore()}";
            }
        }
        catch (InvalidOperationException ex) { // Catch exceptions during property access
            GameManager.Instance?.LogManager?.LogMessage($"Error updating player stats UI (InvalidOp): {ex.Message}");
        }
        catch (Exception ex) { // General catch
            GameManager.Instance?.LogManager?.LogError($"Unexpected error updating player stats UI: {ex.Message}\n{ex.StackTrace}");
        }
    }
}