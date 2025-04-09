using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Display monster in the battle area with targeting support
public class MonsterDisplay : MonoBehaviour, IDropHandler
{
    // References
    private TMP_Text _nameText;
    private TMP_Text _healthText;
    private Image _monsterImage;
    private Slider _healthBar;
    private Image _blockImage;
    private TMP_Text _blockText;
    
    // Data
    private Monster _monster;
    private int _currentBlock = 0;
    
    // Visual feedback
    private Image _highlightImage;
    
    // Determines if this is the player's own monster (for targeting)
    private bool _isPlayerMonster = false;
    
    private void Awake()
    {
        CreateVisuals();
    }
    
    private void CreateVisuals()
    {
        // Make sure we have an Image component for the background
        Image backgroundImage = GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
        }
        backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
        // Create highlight for targeting feedback
        GameObject highlightObj = new GameObject("Highlight");
        highlightObj.transform.SetParent(transform, false);
        _highlightImage = highlightObj.AddComponent<Image>();
        _highlightImage.color = new Color(1f, 1f, 0.5f, 0.0f); // Start invisible
        
        RectTransform highlightRect = highlightObj.GetComponent<RectTransform>();
        highlightRect.anchorMin = Vector2.zero;
        highlightRect.anchorMax = Vector2.one;
        highlightRect.offsetMin = new Vector2(-10, -10);
        highlightRect.offsetMax = new Vector2(10, 10);
        
        // Create monster image
        GameObject imageObj = new GameObject("MonsterImage");
        imageObj.transform.SetParent(transform, false);
        _monsterImage = imageObj.AddComponent<Image>();
        _monsterImage.color = Color.white; // Default color
        
        RectTransform imageRect = imageObj.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.2f, 0.3f);
        imageRect.anchorMax = new Vector2(0.8f, 0.8f);
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        
        // Create name text
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(transform, false);
        _nameText = nameObj.AddComponent<TextMeshProUGUI>();
        _nameText.text = "Monster";
        _nameText.fontSize = 16;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.color = Color.white;
        
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.8f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        
        // Create health text
        GameObject healthObj = new GameObject("HealthText");
        healthObj.transform.SetParent(transform, false);
        _healthText = healthObj.AddComponent<TextMeshProUGUI>();
        _healthText.text = "HP: 40/40";
        _healthText.fontSize = 14;
        _healthText.alignment = TextAlignmentOptions.Center;
        _healthText.color = Color.white;
        
        RectTransform healthRect = healthObj.GetComponent<RectTransform>();
        healthRect.anchorMin = new Vector2(0, 0.15f);
        healthRect.anchorMax = new Vector2(1, 0.25f);
        healthRect.offsetMin = Vector2.zero;
        healthRect.offsetMax = Vector2.zero;
        
        // Create health bar
        GameObject barObj = new GameObject("HealthBar");
        barObj.transform.SetParent(transform, false);
        _healthBar = barObj.AddComponent<Slider>();
        _healthBar.minValue = 0;
        _healthBar.maxValue = 1;
        _healthBar.value = 1;
        
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.1f, 0.05f);
        barRect.anchorMax = new Vector2(0.9f, 0.15f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;
        
        // Add background and fill for the health bar
        GameObject barBg = new GameObject("Background");
        barBg.transform.SetParent(barObj.transform, false);
        Image barBgImage = barBg.AddComponent<Image>();
        barBgImage.color = new Color(0.2f, 0.2f, 0.2f);
        
        RectTransform barBgRect = barBg.GetComponent<RectTransform>();
        barBgRect.anchorMin = Vector2.zero;
        barBgRect.anchorMax = Vector2.one;
        barBgRect.offsetMin = Vector2.zero;
        barBgRect.offsetMax = Vector2.zero;
        
        GameObject barFill = new GameObject("Fill");
        barFill.transform.SetParent(barObj.transform, false);
        Image barFillImage = barFill.AddComponent<Image>();
        barFillImage.color = new Color(0.8f, 0.2f, 0.2f);
        
        RectTransform barFillRect = barFill.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = new Vector2(1, 1);
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;
        
        _healthBar.targetGraphic = barBgImage;
        _healthBar.fillRect = barFillRect;
        
        // Create block display
        GameObject blockObj = new GameObject("BlockDisplay");
        blockObj.transform.SetParent(transform, false);
        
        // Block background
        _blockImage = blockObj.AddComponent<Image>();
        _blockImage.color = new Color(0.2f, 0.6f, 0.8f, 0.8f);
        _blockImage.enabled = false; // Start hidden
        
        RectTransform blockRect = blockObj.GetComponent<RectTransform>();
        blockRect.anchorMin = new Vector2(0.8f, 0.8f);
        blockRect.anchorMax = new Vector2(1, 1);
        blockRect.offsetMin = new Vector2(0, 0);
        blockRect.offsetMax = new Vector2(0, 10);
        
        // Block text
        GameObject blockTextObj = new GameObject("BlockText");
        blockTextObj.transform.SetParent(blockObj.transform, false);
        _blockText = blockTextObj.AddComponent<TextMeshProUGUI>();
        _blockText.text = "0";
        _blockText.fontSize = 18;
        _blockText.fontStyle = FontStyles.Bold;
        _blockText.alignment = TextAlignmentOptions.Center;
        _blockText.color = Color.white;
        
        RectTransform blockTextRect = blockTextObj.GetComponent<RectTransform>();
        blockTextRect.anchorMin = Vector2.zero;
        blockTextRect.anchorMax = Vector2.one;
        blockTextRect.offsetMin = Vector2.zero;
        blockTextRect.offsetMax = Vector2.zero;
        
        // Create default circle sprite for monster
        _monsterImage.sprite = CreateDefaultSprite();
    }
    
    private Sprite CreateDefaultSprite()
    {
        // Create a default circle sprite
        Texture2D texture = new Texture2D(128, 128);
        Color[] colors = new Color[128 * 128];
        
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                float dx = x - 64;
                float dy = y - 64;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                if (dist < 60)
                    colors[y * 128 + x] = Color.white;
                else
                    colors[y * 128 + x] = Color.clear;
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
    }
    
    // NEW: Set whether this monster belongs to the player
    public void SetIsPlayerMonster(bool isPlayerMonster)
    {
        _isPlayerMonster = isPlayerMonster;
        
        // Update visuals to differentiate
        if (_isPlayerMonster)
        {
            // Player's monster has a blue tint to the background
            Image bgImage = GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = new Color(0.2f, 0.3f, 0.4f, 0.5f);
            }
        }
        else
        {
            // Enemy monster has a red tint to the background
            Image bgImage = GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = new Color(0.4f, 0.2f, 0.2f, 0.5f);
            }
        }
    }
    
    // NEW: Check if this is the player's monster
    public bool IsPlayerMonster()
    {
        return _isPlayerMonster;
    }
    
    public void SetMonster(Monster monster)
    {
        if (monster == null)
        {
            Debug.LogWarning("Tried to set null monster in MonsterDisplay");
            return;
        }
        
        // Unsubscribe from previous monster events if any
        if (_monster != null)
        {
            _monster.OnHealthChanged -= UpdateHealth;
            _monster.OnBlockChanged -= UpdateBlock;
        }
        
        _monster = monster;
        
        // Update name
        if (_nameText != null)
        {
            _nameText.text = monster.Name;
        }
        
        // Update health
        if (_healthText != null)
        {
            _healthText.text = $"HP: {monster.Health}/{monster.MaxHealth}";
        }
        
        // Update health bar
        if (_healthBar != null)
        {
            _healthBar.maxValue = monster.MaxHealth;
            _healthBar.value = monster.Health;
        }
        
        // Update image tint
        if (_monsterImage != null)
        {
            _monsterImage.color = monster.TintColor;
        }
        
        // Subscribe to monster events
        monster.OnHealthChanged += UpdateHealth;
        monster.OnBlockChanged += UpdateBlock;
        
        // Initial block update
        UpdateBlock(monster.GetBlock());
    }
    
    private void UpdateHealth(int health, int maxHealth)
    {
        // Update health text
        if (_healthText != null)
        {
            _healthText.text = $"HP: {health}/{maxHealth}";
        }
        
        // Update health bar
        if (_healthBar != null)
        {
            _healthBar.maxValue = maxHealth;
            _healthBar.value = health;
        }
        
        // Visual feedback for damage
        StartCoroutine(FlashDamage());
    }
    
    private void UpdateBlock(int block)
    {
        _currentBlock = block;
        
        // Show/hide block display
        if (_blockImage != null)
        {
            _blockImage.enabled = block > 0;
        }
        
        // Update block text
        if (_blockText != null)
        {
            _blockText.text = block.ToString();
        }
    }
    
    private IEnumerator FlashDamage()
    {
        // Flash red when taking damage
        Image background = GetComponent<Image>();
        if (background == null)
        {
            yield break;
        }
        
        Color originalColor = background.color;
        background.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        
        yield return new WaitForSeconds(0.1f);
        
        background.color = originalColor;
    }
    
    // Handle card dropping on monster
    public void OnDrop(PointerEventData eventData)
    {
        // Safety check for null eventData
        if (eventData == null || eventData.pointerDrag == null) 
        {
            return;
        }
        
        // Check if a card was dropped on this monster
        GameObject droppedObject = eventData.pointerDrag;
        CardDisplay cardDisplay = droppedObject.GetComponent<CardDisplay>();
        if (cardDisplay == null) return;
        
        // Get the card data
        CardData card = cardDisplay.GetCardData();
        
        // Check if this card can target this monster based on player/enemy status
        bool canTarget = false;
        
        if (_isPlayerMonster)
        {
            // Player's own monster can be targeted by Self or All cards
            canTarget = (card.Target == CardTarget.Self || card.Target == CardTarget.All);
        }
        else
        {
            // Opponent's monster can be targeted by Enemy, AllEnemies, or All cards
            canTarget = (card.Target == CardTarget.Enemy || 
                        card.Target == CardTarget.AllEnemies || 
                        card.Target == CardTarget.All);
        }
        
        if (!canTarget)
        {
            // Show invalid target feedback
            StartCoroutine(FlashInvalidTarget());
            GameManager.Instance.LogManager.LogMessage($"Cannot target this monster with card {card.Name}");
            return;
        }
        
        // Card dropped event will be handled by the CardDisplay component
        ShowHighlight(false);
    }
    
    // NEW: Visual feedback for invalid targeting
    private IEnumerator FlashInvalidTarget()
    {
        if (_highlightImage == null)
            yield break;
            
        // Flash red to indicate invalid target
        _highlightImage.color = new Color(1f, 0f, 0f, 0.5f);
        
        yield return new WaitForSeconds(0.2f);
        
        // Hide highlight
        _highlightImage.color = new Color(1f, 1f, 0.5f, 0f);
    }
    
    // Show highlight when valid card is dragged over
    public void ShowHighlight(bool show)
    {
        if (_highlightImage != null)
        {
            _highlightImage.color = new Color(1f, 1f, 0.5f, show ? 0.5f : 0f);
        }
    }
    
    private void OnDestroy()
    {
        if (_monster != null)
        {
            _monster.OnHealthChanged -= UpdateHealth;
            _monster.OnBlockChanged -= UpdateBlock;
        }
    }
    
    // Get the monster data
    public Monster GetMonster()
    {
        return _monster;
    }
}