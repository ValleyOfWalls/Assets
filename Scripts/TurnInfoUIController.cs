using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnInfoUIController
{
    // UI elements
    private GameObject _turnInfoPanel;
    private TMP_Text _roundText;
    private TMP_Text _turnInfoText;
    private Button _endTurnButton;
    
    // Reference to player state
    private PlayerState _playerState;
    
    // Events
    public event Action OnEndTurnClicked;
    
    public TurnInfoUIController(GameObject turnInfoPanel, PlayerState playerState)
    {
        _turnInfoPanel = turnInfoPanel;
        _playerState = playerState;
        
        // Set up UI elements
        CreateRoundText();
        CreateTurnInfoText();
        CreateEndTurnButton();
        
        // Update initial state
        UpdateRoundInfo(1);
        UpdateTurnState(true);
    }
    
    private void CreateRoundText()
    {
        GameObject roundObj = new GameObject("RoundText");
        roundObj.transform.SetParent(_turnInfoPanel.transform, false);
        _roundText = roundObj.AddComponent<TextMeshProUGUI>();
        _roundText.text = "Round 1";
        _roundText.fontSize = 24;
        _roundText.fontStyle = FontStyles.Bold;
        _roundText.alignment = TextAlignmentOptions.Center;
        _roundText.color = Color.white;
        
        RectTransform roundRect = roundObj.GetComponent<RectTransform>();
        roundRect.anchorMin = new Vector2(0, 0);
        roundRect.anchorMax = new Vector2(0.5f, 1);
        roundRect.offsetMin = new Vector2(20, 10);
        roundRect.offsetMax = new Vector2(-10, -10);
    }
    
    private void CreateTurnInfoText()
    {
        GameObject turnObj = new GameObject("TurnInfoText");
        turnObj.transform.SetParent(_turnInfoPanel.transform, false);
        _turnInfoText = turnObj.AddComponent<TextMeshProUGUI>();
        _turnInfoText.text = "Your Turn";
        _turnInfoText.fontSize = 24;
        _turnInfoText.fontStyle = FontStyles.Bold;
        _turnInfoText.alignment = TextAlignmentOptions.Center;
        _turnInfoText.color = Color.green;
        
        RectTransform turnRect = turnObj.GetComponent<RectTransform>();
        turnRect.anchorMin = new Vector2(0.5f, 0);
        turnRect.anchorMax = new Vector2(1, 1);
        turnRect.offsetMin = new Vector2(10, 10);
        turnRect.offsetMax = new Vector2(-20, -10);
    }
    
    private void CreateEndTurnButton()
    {
        GameObject endTurnObj = new GameObject("EndTurnButton");
        endTurnObj.transform.SetParent(_turnInfoPanel.transform.parent, false); // Add to main layout
        _endTurnButton = endTurnObj.AddComponent<Button>();
        
        // Button image
        Image endTurnImage = endTurnObj.AddComponent<Image>();
        endTurnImage.color = new Color(0.7f, 0.2f, 0.2f);
        
        // Button text
        GameObject endTurnTextObj = new GameObject("Text");
        endTurnTextObj.transform.SetParent(endTurnObj.transform, false);
        TMP_Text endTurnText = endTurnTextObj.AddComponent<TextMeshProUGUI>();
        endTurnText.text = "END TURN";
        endTurnText.fontSize = 18;
        endTurnText.fontStyle = FontStyles.Bold;
        endTurnText.alignment = TextAlignmentOptions.Center;
        endTurnText.color = Color.white;
        
        RectTransform endTurnTextRect = endTurnTextObj.GetComponent<RectTransform>();
        endTurnTextRect.anchorMin = Vector2.zero;
        endTurnTextRect.anchorMax = Vector2.one;
        endTurnTextRect.offsetMin = new Vector2(5, 5);
        endTurnTextRect.offsetMax = new Vector2(-5, -5);
        
        // Position the button
        RectTransform endTurnRect = endTurnObj.GetComponent<RectTransform>();
        endTurnRect.anchorMin = new Vector2(1, 0);
        endTurnRect.anchorMax = new Vector2(1, 0);
        endTurnRect.pivot = new Vector2(1, 0);
        endTurnRect.sizeDelta = new Vector2(180, 60);
        endTurnRect.anchoredPosition = new Vector2(-40, 40);
        
        // Set up click handler
        _endTurnButton.onClick.AddListener(HandleEndTurnClicked);
    }
    
    private void HandleEndTurnClicked()
    {
        OnEndTurnClicked?.Invoke();
    }
    
    public void UpdateRoundInfo(int round)
    {
        if (_roundText != null)
        {
            _roundText.text = $"Round {round}";
        }
    }
    
    public void UpdateTurnState(bool isPlayerTurn)
    {
        if (_turnInfoText == null || _endTurnButton == null) return;
        
        if (isPlayerTurn)
        {
            _turnInfoText.text = "Your Turn";
            _turnInfoText.color = Color.green;
            _endTurnButton.interactable = true;
        }
        else
        {
            _turnInfoText.text = "Monster's Turn";
            _turnInfoText.color = Color.red;
            _endTurnButton.interactable = false;
        }
    }
}