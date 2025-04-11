// valleyofwalls-assets/Scripts/MonsterDisplay.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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
    // Visual feedback
    private Image _highlightImage;
    private Image _backgroundImage;
    private Color _originalBackgroundColor;
    private bool _isPlayerMonster = false;
    private Coroutine _damageFlashCoroutine = null;

    // Awake: Create visuals immediately
    private void Awake()
    {
        CreateVisuals();
    }

    // OnEnable/OnDisable/OnDestroy: Handle event subscriptions and cleanup
    private void OnEnable() { if (_monster != null) { SubscribeToMonsterEvents(); UpdateVisualsFromMonster(); } }
    private void OnDisable() { if (_monster != null) { UnsubscribeFromMonsterEvents(); } StopDamageFlash(); }
    private void OnDestroy() { if (_monster != null) { UnsubscribeFromMonsterEvents(); } StopDamageFlash(); } // Ensure cleanup

    private void SubscribeToMonsterEvents() {
        if (_monster == null) return;
        _monster.OnHealthChanged -= UpdateHealth; _monster.OnBlockChanged -= UpdateBlock; // Unsub first
        _monster.OnHealthChanged += UpdateHealth; _monster.OnBlockChanged += UpdateBlock;
    }
    private void UnsubscribeFromMonsterEvents() {
        if (_monster == null) return;
        _monster.OnHealthChanged -= UpdateHealth; _monster.OnBlockChanged -= UpdateBlock;
    }

     private void StopDamageFlash() {
         if (_damageFlashCoroutine != null) {
             StopCoroutine(_damageFlashCoroutine); _damageFlashCoroutine = null;
             if (_backgroundImage != null) _backgroundImage.color = _originalBackgroundColor;
         }
     }

    // Creates the visual elements if they don't exist
    private void CreateVisuals()
    {
        // Background Image
        _backgroundImage = GetComponent<Image>();
        if (_backgroundImage == null) _backgroundImage = gameObject.AddComponent<Image>();
        _backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        _originalBackgroundColor = _backgroundImage.color;

        // Highlight Image (for targeting)
        GameObject highlightObj = new GameObject("Highlight");
        highlightObj.transform.SetParent(transform, false); // Ensure correct parenting
        _highlightImage = highlightObj.AddComponent<Image>();
        _highlightImage.color = new Color(1f, 1f, 0.5f, 0.0f); // Start invisible
        _highlightImage.raycastTarget = false; // Don't block drops
        RectTransform highlightRect = highlightObj.GetComponent<RectTransform>();
        highlightRect.anchorMin = Vector2.zero; highlightRect.anchorMax = Vector2.one;
        highlightRect.offsetMin = new Vector2(-10, -10); highlightRect.offsetMax = new Vector2(10, 10); // Slightly larger

        // Monster Image (Placeholder Sprite)
        GameObject imageObj = new GameObject("MonsterImage");
        imageObj.transform.SetParent(transform, false);
        _monsterImage = imageObj.AddComponent<Image>();
        _monsterImage.sprite = CreateDefaultSprite();
        _monsterImage.color = Color.white;
        _monsterImage.raycastTarget = false; // Image shouldn't block drops
        RectTransform imageRect = imageObj.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.2f, 0.3f); imageRect.anchorMax = new Vector2(0.8f, 0.8f); // Centered
        imageRect.offsetMin = Vector2.zero; imageRect.offsetMax = Vector2.zero;

        // Name Text
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(transform, false);
        _nameText = nameObj.AddComponent<TextMeshProUGUI>();
        _nameText.text = "Monster"; _nameText.fontSize = 16; _nameText.alignment = TextAlignmentOptions.Center; _nameText.color = Color.white;
        _nameText.raycastTarget = false;
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.8f); nameRect.anchorMax = new Vector2(1, 1); // Top
        nameRect.offsetMin = Vector2.zero; nameRect.offsetMax = Vector2.zero;

        // Health Text
        GameObject healthObj = new GameObject("HealthText");
        healthObj.transform.SetParent(transform, false);
        _healthText = healthObj.AddComponent<TextMeshProUGUI>();
        _healthText.text = "HP: -- / --"; _healthText.fontSize = 14; _healthText.alignment = TextAlignmentOptions.Center; _healthText.color = Color.white;
        _healthText.raycastTarget = false;
        RectTransform healthRect = healthObj.GetComponent<RectTransform>();
        healthRect.anchorMin = new Vector2(0, 0.15f); healthRect.anchorMax = new Vector2(1, 0.25f); // Above health bar
        healthRect.offsetMin = Vector2.zero; healthRect.offsetMax = Vector2.zero;

        // Health Bar Slider
        GameObject barObj = new GameObject("HealthBar");
        barObj.transform.SetParent(transform, false);
        _healthBar = barObj.AddComponent<Slider>();
        _healthBar.minValue = 0; _healthBar.maxValue = 1; _healthBar.value = 1; _healthBar.interactable = false;
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.1f, 0.05f); barRect.anchorMax = new Vector2(0.9f, 0.15f); // Bottom center
        barRect.offsetMin = Vector2.zero; barRect.offsetMax = Vector2.zero;

        // Health Bar Background
        GameObject barBg = new GameObject("Background");
        barBg.transform.SetParent(barObj.transform, false); // Parent to slider
        Image barBgImage = barBg.AddComponent<Image>();
        barBgImage.color = new Color(0.2f, 0.2f, 0.2f); barBgImage.raycastTarget = false;
        RectTransform barBgRect = barBg.GetComponent<RectTransform>();
        barBgRect.anchorMin = Vector2.zero; barBgRect.anchorMax = Vector2.one; // Fill slider area
        barBgRect.offsetMin = Vector2.zero; barBgRect.offsetMax = Vector2.zero;
        _healthBar.targetGraphic = barBgImage; // Assign background for interaction visuals (though disabled)

        // Health Bar Fill Area
        GameObject barFillArea = new GameObject("Fill Area"); // Slider needs Fill Area parent
        barFillArea.transform.SetParent(barObj.transform, false);
        RectTransform fillAreaRect = barFillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero; fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero; fillAreaRect.offsetMax = Vector2.zero;

        // Health Bar Fill
        GameObject barFill = new GameObject("Fill");
        barFill.transform.SetParent(fillAreaRect.transform, false); // Parent fill to Fill Area
        Image barFillImage = barFill.AddComponent<Image>();
        barFillImage.color = new Color(0.8f, 0.2f, 0.2f); // Red fill
        barFillImage.raycastTarget = false;
        RectTransform barFillRect = barFill.GetComponent<RectTransform>();
         barFillRect.anchorMin = Vector2.zero; barFillRect.anchorMax = Vector2.one; // Fill the fill area
        barFillRect.offsetMin = Vector2.zero; barFillRect.offsetMax = Vector2.zero;
        _healthBar.fillRect = barFillRect; // Assign fill rect

         // Block Display (Shield Icon + Text)
        GameObject blockObj = new GameObject("BlockDisplay");
        blockObj.transform.SetParent(transform, false); // Parent to main display
        // Position block display (e.g., top right)
        RectTransform blockRect = blockObj.AddComponent<RectTransform>();
        blockRect.anchorMin = new Vector2(1, 1); blockRect.anchorMax = new Vector2(1, 1);
        blockRect.pivot = new Vector2(1, 1); blockRect.anchoredPosition = new Vector2(-5, -5); // Offset from top-right
        blockRect.sizeDelta = new Vector2(40, 40); // Adjust size as needed

        _blockImage = blockObj.AddComponent<Image>();
        // TODO: Assign a shield sprite to _blockImage.sprite here if desired
        _blockImage.color = new Color(0.2f, 0.6f, 0.8f, 0.8f); // Blueish tint
        _blockImage.enabled = false; _blockImage.raycastTarget = false; // Start hidden

        GameObject blockTextObj = new GameObject("BlockText");
        blockTextObj.transform.SetParent(blockObj.transform, false); // Parent to block display obj
        _blockText = blockTextObj.AddComponent<TextMeshProUGUI>();
        _blockText.text = "0"; _blockText.fontSize = 18; _blockText.fontStyle = FontStyles.Bold;
        _blockText.alignment = TextAlignmentOptions.Center; _blockText.color = Color.white; _blockText.raycastTarget = false;
        // Center text within the block display object
        RectTransform blockTextRect = blockTextObj.GetComponent<RectTransform>();
        blockTextRect.anchorMin = Vector2.zero; blockTextRect.anchorMax = Vector2.one;
        blockTextRect.offsetMin = Vector2.zero; blockTextRect.offsetMax = Vector2.zero;
        _blockText.enabled = false; // Start hidden
    }

    // Creates a simple white circle sprite
    private Sprite CreateDefaultSprite() {
        Texture2D texture = new Texture2D(128, 128); Color[] colors = new Color[128 * 128];
        Vector2 center = new Vector2(63.5f, 63.5f); float radiusSq = 60f * 60f;
        for (int y = 0; y < 128; y++) { for (int x = 0; x < 128; x++) {
                float dx = x - center.x; float dy = y - center.y;
                colors[y * 128 + x] = (dx * dx + dy * dy < radiusSq) ? Color.white : Color.clear; } }
        texture.SetPixels(colors); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
    }


    // Set if this is the player's own monster (for targeting and visuals)
    public void SetIsPlayerMonster(bool isPlayerMonster) {
        _isPlayerMonster = isPlayerMonster;
        if (_backgroundImage != null) {
             _backgroundImage.color = _isPlayerMonster ? new Color(0.2f, 0.3f, 0.4f, 0.5f) : new Color(0.4f, 0.2f, 0.2f, 0.5f);
             _originalBackgroundColor = _backgroundImage.color;
        }
    }
    // Check if this is the player's monster
    public bool IsPlayerMonster() { return _isPlayerMonster; }

    // Assigns a monster and updates visuals
    public void SetMonster(Monster monster)
    {
        StopDamageFlash();
        if (_monster != null) UnsubscribeFromMonsterEvents();
        _monster = monster;
        UpdateVisualsFromMonster(); // Update visuals regardless of null/not null
        if (_monster != null) SubscribeToMonsterEvents(); // Subscribe only if not null
    }

    // Updates all visual elements based on the current _monster
    private void UpdateVisualsFromMonster() {
         if (_monster == null || _monster.MaxHealth <= 0) { // Handle null or placeholder monster (MaxHealth=0)
             if (_nameText != null) _nameText.text = _monster?.Name ?? "---"; // Show name if available (e.g., "Waiting...")
             UpdateHealth(0, 0); // Use 0/0 to trigger placeholder text in UpdateHealth
             if (_monsterImage != null) _monsterImage.color = Color.gray;
             UpdateBlock(0);
             return;
         };
         // Update with actual monster data
         if (_nameText != null) _nameText.text = _monster.Name;
         UpdateHealth(_monster.Health, _monster.MaxHealth);
         if (_monsterImage != null) _monsterImage.color = _monster.TintColor;
         UpdateBlock(_monster.GetBlock());
    }

    // Update Health Display - Handles Placeholder Text
    private void UpdateHealth(int health, int maxHealth)
    {
        if (maxHealth <= 0) { // Treat 0 or less max health as placeholder signal
            if (_healthText != null) _healthText.text = "HP: -- / --";
            if (_healthBar != null) { _healthBar.gameObject.SetActive(false); }
        } else {
            if (_healthText != null) _healthText.text = $"HP: {health}/{maxHealth}";
            if (_healthBar != null) {
                 _healthBar.gameObject.SetActive(true);
                 _healthBar.minValue = 0; _healthBar.maxValue = maxHealth; _healthBar.value = health;
            }
        }
    }

    // Update Block Display
    private void UpdateBlock(int block)
    {
        bool showBlock = block > 0;
        if (_blockImage != null) _blockImage.enabled = showBlock;
        if (_blockText != null) { _blockText.text = block.ToString(); _blockText.enabled = showBlock; }
    }

    // --- Damage Flash ---
    private void FlashDamage() { StopDamageFlash(); _damageFlashCoroutine = StartCoroutine(DamageFlashEffect()); }
    private IEnumerator DamageFlashEffect() {
        if (_backgroundImage == null) { _damageFlashCoroutine = null; yield break; }
        Color flashColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        _backgroundImage.color = flashColor;
        yield return new WaitForSeconds(0.1f);
        float duration = 0.2f; float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime; float t = elapsed / duration;
            _backgroundImage.color = Color.Lerp(flashColor, _originalBackgroundColor, t);
            yield return null;
        }
        _backgroundImage.color = _originalBackgroundColor;
        _damageFlashCoroutine = null;
    }

    // --- Drag and Drop ---
    public void OnDrop(PointerEventData eventData) {
        if (eventData?.pointerDrag == null) return;
        CardDisplay cardDisplay = eventData.pointerDrag.GetComponent<CardDisplay>();
        if (cardDisplay == null) return;
        CardData card = cardDisplay.GetCardData();
        if (card == null || _monster == null) return; // Need card and monster data

        bool canTarget = false;
        if (_isPlayerMonster) canTarget = (card.Target == CardTarget.Self || card.Target == CardTarget.All);
        else canTarget = (card.Target == CardTarget.Enemy || card.Target == CardTarget.AllEnemies || card.Target == CardTarget.All);

        if (!canTarget) { StartCoroutine(FlashInvalidTarget()); }
        ShowHighlight(false);
    }

    // --- Targeting Highlights ---
    private IEnumerator FlashInvalidTarget() {
        if (_highlightImage == null) yield break;
        _highlightImage.color = new Color(1f, 0f, 0f, 0.5f);
        yield return new WaitForSeconds(0.2f);
        _highlightImage.color = new Color(1f, 1f, 0.5f, 0f);
    }
    public void ShowHighlight(bool show) {
        if (_highlightImage != null) {
            _highlightImage.color = new Color(1f, 1f, 0.5f, show ? 0.5f : 0f);
        }
    }

    // --- Getters ---
    public Monster GetMonster() { return _monster; }

    // Direct health display update
    public void SetHealthDisplay(int health, int maxHealth) {
         UpdateHealth(health, maxHealth);
    }
}