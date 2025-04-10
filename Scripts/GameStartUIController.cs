using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameStartUIController
{
    // UI elements
    private GameObject _gameStartedPanel;
    private TMP_Text _gameStartedText;
    
    public GameStartUIController(Transform canvasTransform)
    {
        CreateGameStartPanel(canvasTransform);
    }
    
    private void CreateGameStartPanel(Transform parentTransform)
    {
        // Create game started panel that overlays the center of the screen
        GameObject gameStartedPanelObj = new GameObject("Game Started Panel");
        gameStartedPanelObj.transform.SetParent(parentTransform, false);
        _gameStartedPanel = gameStartedPanelObj;
        
        Image gameStartedPanelImage = gameStartedPanelObj.AddComponent<Image>();
        gameStartedPanelImage.color = new Color(0, 0, 0, 0.9f);
        RectTransform gameStartedPanelRect = gameStartedPanelObj.GetComponent<RectTransform>();
        gameStartedPanelRect.anchorMin = new Vector2(0.35f, 0.4f);
        gameStartedPanelRect.anchorMax = new Vector2(0.65f, 0.6f);
        gameStartedPanelRect.offsetMin = Vector2.zero;
        gameStartedPanelRect.offsetMax = Vector2.zero;
        
        // Add game started text
        GameObject gameStartedTextObj = new GameObject("Game Started Text");
        gameStartedTextObj.transform.SetParent(gameStartedPanelObj.transform, false);
        _gameStartedText = gameStartedTextObj.AddComponent<TextMeshProUGUI>();
        _gameStartedText.text = "GAME STARTED!";
        _gameStartedText.fontSize = 32;
        _gameStartedText.alignment = TextAlignmentOptions.Center;
        _gameStartedText.color = Color.green;
        RectTransform gameStartedTextRect = gameStartedTextObj.GetComponent<RectTransform>();
        gameStartedTextRect.anchorMin = Vector2.zero;
        gameStartedTextRect.anchorMax = Vector2.one;
        gameStartedTextRect.offsetMin = Vector2.zero;
        gameStartedTextRect.offsetMax = Vector2.zero;
        
        // Initially hide the panel
        _gameStartedPanel.SetActive(false);
    }
    
    public void ShowPanel()
    {
        if (_gameStartedPanel != null)
        {
            _gameStartedPanel.SetActive(true);
        }
    }
    
    public void HidePanel()
    {
        if (_gameStartedPanel != null)
        {
            _gameStartedPanel.SetActive(false);
        }
    }
    
    public void SetText(string text)
    {
        if (_gameStartedText != null)
        {
            _gameStartedText.text = text;
        }
    }
}