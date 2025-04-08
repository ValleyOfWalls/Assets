using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


// Display monster in the monster area
public class MonsterDisplay : MonoBehaviour
{
    // References
    private TMP_Text _nameText;
    private TMP_Text _healthText;
    private Image _monsterImage;
    private Slider _healthBar;
    
    // Data
    private Monster _monster;
    
    private void Awake()
    {
        CreateVisuals();
    }
    
    private void CreateVisuals()
    {
        // Create background
        gameObject.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
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
    
    public void SetMonster(Monster monster)
    {
        if (monster == null) return;
        
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
            _healthBar.value = health;
        }
    }
    
    private void OnDestroy()
    {
        if (_monster != null)
        {
            _monster.OnHealthChanged -= UpdateHealth;
        }
    }
}