using UnityEngine;
using UnityEngine.UI;

public class GameUILayout
{
    // Main layouts
    private GameObject _mainLayout;
    private GameObject _handPanel;
    private GameObject _statsPanel;
    private GameObject _opponentsPanel;
    private GameObject _battlePanel;
    private GameObject _playerMonsterPanel;
    private GameObject _turnInfoPanel;
    
    public GameUILayout(Transform canvasTransform)
    {
        CreateMainLayout(canvasTransform);
        CreateStatsPanel();
        CreateOpponentsPanel();
        CreateBattlePanel();
        CreatePlayerMonsterPanel();
        CreateTurnInfoPanel();
        CreateHandPanel();
        
        GameManager.Instance.LogManager.LogMessage("GameUI layout created");
    }
    
    private void CreateMainLayout(Transform canvasTransform)
    {
        // Create a main layout container that will hold all UI elements
        _mainLayout = new GameObject("MainLayout");
        _mainLayout.transform.SetParent(canvasTransform, false);
        
        // Add a background image
        Image background = _mainLayout.AddComponent<Image>();
        background.color = new Color(0.05f, 0.05f, 0.1f, 0.8f); // Dark blue semi-transparent background
        
        // Set to full screen
        RectTransform mainRect = _mainLayout.GetComponent<RectTransform>();
        mainRect.anchorMin = Vector2.zero;
        mainRect.anchorMax = Vector2.one;
        mainRect.offsetMin = Vector2.zero;
        mainRect.offsetMax = Vector2.zero;
    }
    
    private void CreateStatsPanel()
    {
        _statsPanel = new GameObject("PlayerStats");
        _statsPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image statsBg = _statsPanel.AddComponent<Image>();
        statsBg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Position in top left
        RectTransform statsRect = _statsPanel.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0, 1);
        statsRect.anchorMax = new Vector2(0.2f, 1);
        statsRect.pivot = new Vector2(0, 1);
        statsRect.offsetMin = new Vector2(20, -200);
        statsRect.offsetMax = new Vector2(-20, -20);
    }
    
    private void CreateOpponentsPanel()
    {
        _opponentsPanel = new GameObject("OpponentsPanel");
        _opponentsPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image opponentsBg = _opponentsPanel.AddComponent<Image>();
        opponentsBg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Position on the right
        RectTransform opponentsRect = _opponentsPanel.GetComponent<RectTransform>();
        opponentsRect.anchorMin = new Vector2(0.8f, 0.3f);
        opponentsRect.anchorMax = new Vector2(1, 0.9f);
        opponentsRect.pivot = new Vector2(1, 0.5f);
        opponentsRect.offsetMin = new Vector2(20, 0);
        opponentsRect.offsetMax = new Vector2(-20, 0);
        
        // Add vertical layout
        VerticalLayoutGroup layout = _opponentsPanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(10, 10, 10, 10);
    }
    
    private void CreateBattlePanel()
    {
        _battlePanel = new GameObject("BattlePanel");
        _battlePanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image battleBg = _battlePanel.AddComponent<Image>();
        battleBg.color = new Color(0.1f, 0.1f, 0.2f, 0.4f); // More transparent
        
        // Position in center
        RectTransform battleRect = _battlePanel.GetComponent<RectTransform>();
        battleRect.anchorMin = new Vector2(0.2f, 0.3f);
        battleRect.anchorMax = new Vector2(0.8f, 0.8f);
        battleRect.offsetMin = Vector2.zero;
        battleRect.offsetMax = Vector2.zero;
    }
    
    private void CreatePlayerMonsterPanel()
    {
        _playerMonsterPanel = new GameObject("PlayerMonsterPanel");
        _playerMonsterPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image monsterBg = _playerMonsterPanel.AddComponent<Image>();
        monsterBg.color = new Color(0.1f, 0.2f, 0.3f, 0.8f); // Different color to distinguish
        
        // Position in bottom left corner
        RectTransform monsterRect = _playerMonsterPanel.GetComponent<RectTransform>();
        monsterRect.anchorMin = new Vector2(0, 0);
        monsterRect.anchorMax = new Vector2(0.2f, 0.3f);
        monsterRect.offsetMin = new Vector2(20, 230); // Above hand panel
        monsterRect.offsetMax = new Vector2(-20, -10);
    }
    
    private void CreateTurnInfoPanel()
    {
        _turnInfoPanel = new GameObject("TurnInfoPanel");
        _turnInfoPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image turnInfoBg = _turnInfoPanel.AddComponent<Image>();
        turnInfoBg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Position at top
        RectTransform turnInfoRect = _turnInfoPanel.GetComponent<RectTransform>();
        turnInfoRect.anchorMin = new Vector2(0.25f, 1);
        turnInfoRect.anchorMax = new Vector2(0.75f, 1);
        turnInfoRect.pivot = new Vector2(0.5f, 1);
        turnInfoRect.offsetMin = new Vector2(0, -80);
        turnInfoRect.offsetMax = new Vector2(0, -20);
    }
    
    private void CreateHandPanel()
    {
        _handPanel = new GameObject("HandPanel");
        _handPanel.transform.SetParent(_mainLayout.transform, false);
        
        // Background
        Image handBg = _handPanel.AddComponent<Image>();
        handBg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        
        // Position at bottom
        RectTransform handRect = _handPanel.GetComponent<RectTransform>();
        handRect.anchorMin = new Vector2(0, 0);
        handRect.anchorMax = new Vector2(1, 0);
        handRect.pivot = new Vector2(0.5f, 0);
        handRect.offsetMin = new Vector2(100, 20);
        handRect.offsetMax = new Vector2(-100, 220);
        
        // Horizontal layout for cards
        HorizontalLayoutGroup layout = _handPanel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(20, 20, 10, 10);
        layout.childAlignment = TextAnchor.MiddleCenter;
        
        // Content size fitter to adjust based on card count
        ContentSizeFitter fitter = _handPanel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
    
    // Getters for panels
    public GameObject GetMainLayout() => _mainLayout;
    public GameObject GetHandPanel() => _handPanel;
    public GameObject GetStatsPanel() => _statsPanel;
    public GameObject GetOpponentsPanel() => _opponentsPanel;
    public GameObject GetBattlePanel() => _battlePanel;
    public GameObject GetPlayerMonsterPanel() => _playerMonsterPanel;
    public GameObject GetTurnInfoPanel() => _turnInfoPanel;
}