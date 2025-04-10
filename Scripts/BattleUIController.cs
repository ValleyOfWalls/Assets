using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleUIController
{
    // UI panels
    private GameObject _battlePanel;
    private GameObject _playerMonsterPanel;
    
    // Monster displays
    private MonsterDisplay _playerMonsterDisplay;
    private MonsterDisplay _opponentMonsterDisplay;
    
    // Player reference
    private PlayerState _playerState;
    
    // Game objects for reference
    private GameObject _playerAvatarObj;
    private GameObject _vsTextObj;
    
    public BattleUIController(GameObject battlePanel, GameObject playerMonsterPanel, PlayerState playerState)
    {
        _battlePanel = battlePanel;
        _playerMonsterPanel = playerMonsterPanel;
        _playerState = playerState;
        
        // Set up battle UI
        SetupBattlePanel();
        SetupPlayerMonsterPanel();
        
        // Initial update
        UpdatePlayerMonsterDisplay(playerState.GetMonster());
        UpdateOpponentMonsterDisplay(playerState.GetOpponentMonster());
    }
    
    private void SetupBattlePanel()
    {
        // Create player avatar on left (simplified representation of player)
        _playerAvatarObj = new GameObject("PlayerAvatar");
        _playerAvatarObj.transform.SetParent(_battlePanel.transform, false);
        
        RectTransform playerAvatarRect = _playerAvatarObj.AddComponent<RectTransform>();
        playerAvatarRect.anchorMin = new Vector2(0, 0);
        playerAvatarRect.anchorMax = new Vector2(0.45f, 1);
        playerAvatarRect.offsetMin = new Vector2(20, 20);
        playerAvatarRect.offsetMax = new Vector2(-10, -20);
        
        // Add player avatar image
        Image playerAvatar = _playerAvatarObj.AddComponent<Image>();
        playerAvatar.color = new Color(0.3f, 0.5f, 0.8f); // Blue for player
        
        // Add player avatar label
        GameObject playerLabelObj = new GameObject("PlayerLabel");
        playerLabelObj.transform.SetParent(_playerAvatarObj.transform, false);
        TMP_Text playerLabel = playerLabelObj.AddComponent<TextMeshProUGUI>();
        playerLabel.text = "YOU";
        playerLabel.fontSize = 28;
        playerLabel.fontStyle = FontStyles.Bold;
        playerLabel.alignment = TextAlignmentOptions.Center;
        playerLabel.color = Color.white;
        
        RectTransform playerLabelRect = playerLabelObj.GetComponent<RectTransform>();
        playerLabelRect.anchorMin = new Vector2(0, 0.7f);
        playerLabelRect.anchorMax = new Vector2(1, 0.9f);
        playerLabelRect.offsetMin = Vector2.zero;
        playerLabelRect.offsetMax = Vector2.zero;
        
        // Create opponent's monster on right (main battle target)
        GameObject opponentMonsterObj = new GameObject("OpponentMonster");
        opponentMonsterObj.transform.SetParent(_battlePanel.transform, false);
        _opponentMonsterDisplay = opponentMonsterObj.AddComponent<MonsterDisplay>();
        
        RectTransform opponentMonsterRect = opponentMonsterObj.GetComponent<RectTransform>();
        opponentMonsterRect.anchorMin = new Vector2(0.55f, 0);
        opponentMonsterRect.anchorMax = new Vector2(1, 1);
        opponentMonsterRect.offsetMin = new Vector2(10, 20);
        opponentMonsterRect.offsetMax = new Vector2(-20, -20);
        
        // Create VS text in middle
        _vsTextObj = new GameObject("VS");
        _vsTextObj.transform.SetParent(_battlePanel.transform, false);
        TMP_Text vsText = _vsTextObj.AddComponent<TextMeshProUGUI>();
        vsText.text = "VS";
        vsText.fontSize = 36;
        vsText.fontStyle = FontStyles.Bold;
        vsText.alignment = TextAlignmentOptions.Center;
        vsText.color = Color.yellow;
        
        RectTransform vsRect = _vsTextObj.GetComponent<RectTransform>();
        vsRect.anchorMin = new Vector2(0.45f, 0.4f);
        vsRect.anchorMax = new Vector2(0.55f, 0.6f);
        vsRect.offsetMin = Vector2.zero;
        vsRect.offsetMax = Vector2.zero;
        
        // Set opponent monster as non-player monster
        _opponentMonsterDisplay.SetIsPlayerMonster(false);
    }
    
    private void SetupPlayerMonsterPanel()
    {
        // Add panel title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_playerMonsterPanel.transform, false);
        TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "YOUR MONSTER";
        titleText.fontSize = 16;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.85f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        
        // Create monster display for player's monster
        GameObject playerMonsterObj = new GameObject("PlayerMonsterDisplay");
        playerMonsterObj.transform.SetParent(_playerMonsterPanel.transform, false);
        _playerMonsterDisplay = playerMonsterObj.AddComponent<MonsterDisplay>();
        
        RectTransform playerMonsterRect = playerMonsterObj.GetComponent<RectTransform>();
        playerMonsterRect.anchorMin = new Vector2(0, 0);
        playerMonsterRect.anchorMax = new Vector2(1, 0.85f);
        playerMonsterRect.offsetMin = new Vector2(10, 10);
        playerMonsterRect.offsetMax = new Vector2(-10, -5);
        
        // Set player monster as player's own monster
        _playerMonsterDisplay.SetIsPlayerMonster(true);
    }
    
    public void UpdatePlayerMonsterDisplay(Monster monster)
    {
        if (_playerMonsterDisplay != null && monster != null)
        {
            _playerMonsterDisplay.SetMonster(monster);
        }
        else if (_playerMonsterDisplay != null)
        {
            // Create default monster if needed
            Monster defaultMonster = new Monster
            {
                Name = "Your Monster",
                Health = 40,
                MaxHealth = 40,
                Attack = 5,
                Defense = 3,
                TintColor = new Color(0.3f, 0.6f, 0.9f) // Blue for player's monster
            };
            
            _playerMonsterDisplay.SetMonster(defaultMonster);
        }
    }
    
    public void UpdateOpponentMonsterDisplay(Monster monster)
    {
        if (_opponentMonsterDisplay != null && monster != null)
        {
            _opponentMonsterDisplay.SetMonster(monster);
        }
        else if (_opponentMonsterDisplay != null)
        {
            // Create default opponent monster if needed
            Monster defaultOpponent = new Monster
            {
                Name = "Enemy Monster",
                Health = 40,
                MaxHealth = 40,
                Attack = 5,
                Defense = 3,
                TintColor = new Color(0.9f, 0.3f, 0.3f) // Red for enemy monster
            };
            
            _opponentMonsterDisplay.SetMonster(defaultOpponent);
        }
    }
    
    public void PlayCardEffect(CardData card, GameObject targetObj)
    {
        // Create a visual effect based on card type
        GameObject effectObj = new GameObject("CardEffect");
        
        // Find the canvas and make effect a child
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(); // Updated to use newer method
        if (canvas != null)
        {
            effectObj.transform.SetParent(canvas.transform, false);
        }
        else
        {
            effectObj.transform.SetParent(targetObj.transform.parent, false);
        }
        
        // Position at the target
        RectTransform effectRect = effectObj.AddComponent<RectTransform>();
        effectRect.position = targetObj.transform.position;
        effectRect.sizeDelta = new Vector2(100, 100);
        
        // Add image component for the effect
        Image effectImage = effectObj.AddComponent<Image>();
        
        // Set color based on card type
        switch (card.Type)
        {
            case CardType.Attack:
                effectImage.color = new Color(1f, 0.3f, 0.3f, 0.7f);
                break;
            case CardType.Skill:
                effectImage.color = new Color(0.3f, 0.7f, 1f, 0.7f);
                break;
            case CardType.Power:
                effectImage.color = new Color(0.8f, 0.5f, 1f, 0.7f);
                break;
        }
        
        // Add text to show the amount
        GameObject textObj = new GameObject("EffectText");
        textObj.transform.SetParent(effectObj.transform, false);
        
        TMP_Text effectText = textObj.AddComponent<TextMeshProUGUI>();
        effectText.alignment = TextAlignmentOptions.Center;
        effectText.fontSize = 24;
        effectText.fontStyle = FontStyles.Bold;
        
        // Set the text based on card effect
        if (card.DamageAmount > 0)
        {
            effectText.text = $"-{card.DamageAmount}";
            effectText.color = Color.white;
        }
        else if (card.BlockAmount > 0)
        {
            effectText.text = $"+{card.BlockAmount}";
            effectText.color = Color.cyan;
        }
        else if (card.HealAmount > 0)
        {
            effectText.text = $"+{card.HealAmount}";
            effectText.color = Color.green;
        }
        
        // Position the text
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Start animation coroutine
        MonoBehaviour monoBehaviour = UnityEngine.Object.FindAnyObjectByType<MonoBehaviour>(); // Updated to use newer method
        if (monoBehaviour != null)
        {
            monoBehaviour.StartCoroutine(AnimateCardEffect(effectObj));
        }
        else
        {
            // If no MonoBehaviour is found, at least show the effect briefly
            GameObject.Destroy(effectObj, 1.0f);
        }
    }
    
    private static IEnumerator AnimateCardEffect(GameObject effectObj)
    {
        RectTransform rectTransform = effectObj.GetComponent<RectTransform>();
        Image image = effectObj.GetComponent<Image>();
        
        float duration = 1.0f;
        float elapsed = 0f;
        Vector2 startSize = rectTransform.sizeDelta;
        Vector2 endSize = startSize * 1.5f;
        Color startColor = image.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            
            // Scale up
            rectTransform.sizeDelta = Vector2.Lerp(startSize, endSize, t);
            
            // Fade out
            image.color = Color.Lerp(startColor, endColor, t);
            
            // Update text color too
            TMP_Text text = effectObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                Color textColor = text.color;
                text.color = new Color(textColor.r, textColor.g, textColor.b, 1f - t);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Destroy the effect object
        GameObject.Destroy(effectObj);
    }
}