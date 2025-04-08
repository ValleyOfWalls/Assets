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
    
    public void SetTextElements(TMP_Text nameText, TMP_Text healthText, TMP_Text scoreText)
    {
        _nameText = nameText;
        _healthText = healthText;
        _scoreText = scoreText;
    }
    
    public void UpdateDisplay(PlayerState playerState)
    {
        _playerState = playerState;
        
        if (_playerState == null) return;
        
        // Update name
        if (_nameText != null)
        {
            _nameText.text = _playerState.PlayerName.ToString();
        }
        
        // Update health
        if (_healthText != null)
        {
            _healthText.text = $"HP: {_playerState.Health}/{_playerState.MaxHealth}";
        }
        
        // Update score
        if (_scoreText != null)
        {
            _scoreText.text = $"Score: {_playerState.GetScore()}";
        }
    }
}

