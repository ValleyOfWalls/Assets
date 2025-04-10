using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Display a single card in the hand with drag and drop functionality
public class CardDisplay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // References
    private TMP_Text _titleText;
    private TMP_Text _costText;
    private TMP_Text _descriptionText;
    private Button _button;
    
    // Data
    private CardData _cardData;
    private int _cardIndex;
    
    // Drag and drop - these need to be properly initialized
    private Canvas _canvas;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Vector2 _originalPosition;
    private Transform _originalParent;
    private bool _isDragging = false;
    private bool _isDragReady = false;
    
    // Events
    public event Action<CardDisplay> CardClicked;
    public event Action<CardDisplay, GameObject> CardPlayed;
    
    private void Awake()
    {
        // These components are essential for dragging - get them right away
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        // Make sure all text elements are visible on start
        ValidateTextElements();
    }
    
    // Required method to initialize the card for drag operation
    public void InitializeDragOperation(Canvas canvas)
    {
        _canvas = canvas;
        
        // Ensure we have the required components
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null)
        {
            Debug.LogError($"Card {gameObject.name} missing RectTransform component");
            _rectTransform = gameObject.AddComponent<RectTransform>();
        }
        
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            Debug.LogError($"Card {gameObject.name} missing CanvasGroup component");
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Only mark as drag-ready if we have all the necessary components
        _isDragReady = (_canvas != null && _rectTransform != null && _canvasGroup != null);
        
        // Debug log to verify initialization
        Debug.Log($"Card {gameObject.name} drag initialized: {_isDragReady}, Canvas: {(_canvas != null ? _canvas.name : "null")}");
    }
    
    public void SetTextElements(TMP_Text titleText, TMP_Text costText, TMP_Text descriptionText)
    {
        _titleText = titleText;
        _costText = costText;
        _descriptionText = descriptionText;
        
        // Debug log to verify text elements are set
        if (_titleText == null || _costText == null || _descriptionText == null)
        {
            Debug.LogError($"Card {gameObject.name} - Not all text elements were set properly!");
        }
        else
        {
            Debug.Log($"Card {gameObject.name} - Text elements set successfully.");
            
            // Ensure text elements are set up properly
            if (_titleText != null)
            {
                _titleText.fontSize = 16;
                _titleText.color = Color.white;
                _titleText.text = "Title"; // Default text to verify visibility
                _titleText.raycastTarget = false; // Prevent text from blocking input
            }
            
            if (_costText != null)
            {
                _costText.fontSize = 20;
                _costText.color = Color.yellow;
                _costText.text = "0"; // Default text to verify visibility
                _costText.raycastTarget = false; // Prevent text from blocking input
            }
            
            if (_descriptionText != null)
            {
                _descriptionText.fontSize = 14;
                _descriptionText.color = Color.white;
                _descriptionText.text = "Description"; // Default text to verify visibility
                _descriptionText.raycastTarget = false; // Prevent text from blocking input
            }
        }
    }
    
    private void ValidateTextElements()
    {
        // Check if the text elements are null and try to recover them from child objects
        if (_titleText == null)
        {
            _titleText = transform.Find("TitleText")?.GetComponent<TMP_Text>();
            if (_titleText == null)
                Debug.LogError($"Card {gameObject.name} - Title text component is null!");
        }
        
        if (_costText == null)
        {
            _costText = transform.Find("CostText")?.GetComponent<TMP_Text>();
            if (_costText == null)
                Debug.LogError($"Card {gameObject.name} - Cost text component is null!");
        }
        
        if (_descriptionText == null)
        {
            _descriptionText = transform.Find("DescriptionText")?.GetComponent<TMP_Text>();
            if (_descriptionText == null)
                Debug.LogError($"Card {gameObject.name} - Description text component is null!");
        }
    }
    
    public void SetButton(Button button)
    {
        _button = button;
        
        // Set up click handler
        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnButtonClicked);
        }
    }
    
    public void SetCardData(CardData cardData, int index)
    {
        _cardData = cardData;
        _cardIndex = index;
        
        UpdateVisuals();
    }
    
    private void UpdateVisuals()
    {
        if (_cardData == null) return;
        
        // Make sure text components are valid
        ValidateTextElements();
        
        // Set title
        if (_titleText != null)
        {
            _titleText.text = _cardData.Name;
            Debug.Log($"Set title text to: {_cardData.Name}");
        }
        else
        {
            Debug.LogError("Title text component is null when updating visuals!");
        }
        
        // Set cost
        if (_costText != null)
        {
            _costText.text = _cardData.EnergyCost.ToString();
            Debug.Log($"Set cost text to: {_cardData.EnergyCost}");
        }
        else
        {
            Debug.LogError("Cost text component is null when updating visuals!");
        }
        
        // Set description
        if (_descriptionText != null)
        {
            _descriptionText.text = _cardData.Description;
            Debug.Log($"Set description text to: {_cardData.Description}");
        }
        else
        {
            Debug.LogError("Description text component is null when updating visuals!");
        }
        
        // Set card background color based on type
        Image background = GetComponent<Image>();
        if (background != null)
        {
            switch (_cardData.Type)
            {
                case CardType.Attack:
                    background.color = new Color(0.8f, 0.2f, 0.2f, 1f);
                    break;
                case CardType.Skill:
                    background.color = new Color(0.2f, 0.6f, 0.8f, 1f);
                    break;
                case CardType.Power:
                    background.color = new Color(0.8f, 0.4f, 0.8f, 1f);
                    break;
                default:
                    background.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                    break;
            }
        }
        
        // Ensure all text elements are in the foreground
        if (_titleText != null)
            _titleText.transform.SetAsLastSibling();
        if (_costText != null)
            _costText.transform.SetAsLastSibling();
        if (_descriptionText != null)
            _descriptionText.transform.SetAsLastSibling();
        
        // UPDATED: Add visual cue for targeting type
        if (_descriptionText != null)
        {
            // Add target info to description
            string targetInfo = "";
            switch (_cardData.Target)
            {
                case CardTarget.Self:
                    targetInfo = "[Targets: Your Monster]";
                    break;
                case CardTarget.Enemy:
                    targetInfo = "[Targets: Enemy Monster]";
                    break;
                case CardTarget.AllEnemies:
                    targetInfo = "[Targets: All Enemies]";
                    break;
                case CardTarget.All:
                    targetInfo = "[Targets: All]";
                    break;
            }
            
            // Add target info below the regular description
            if (!string.IsNullOrEmpty(targetInfo))
            {
                _descriptionText.text = $"{_cardData.Description}\n\n<color=#aaaaaa><size=10>{targetInfo}</size></color>";
            }
        }
    }
    
    private void OnButtonClicked()
    {
        if (!_isDragging)
        {
            CardClicked?.Invoke(this);
        }
    }
    
    public CardData GetCardData()
    {
        return _cardData;
    }
    
    public int GetCardIndex()
    {
        return _cardIndex;
    }
    
    // Drag and drop implementation
    public void OnBeginDrag(PointerEventData eventData)
    {
        // First, verify drag operation is properly initialized
        if (!_isDragReady)
        {
            Debug.LogWarning($"Attempted to drag card {gameObject.name} but drag operation not initialized. Canvas: {(_canvas != null ? _canvas.name : "null")}");
            eventData.pointerDrag = null;  // Cancel the drag
            return;
        }
        
        if (!CanBePlayed())
        {
            // Log why the card can't be played
            if (GameState.Instance == null)
                Debug.LogWarning("Cannot play card: GameState is null");
            else if (!GameState.Instance.IsLocalPlayerTurn())
                Debug.LogWarning("Cannot play card: Not your turn");
            else if (_cardData == null)
                Debug.LogWarning("Cannot play card: Card data is null");
            else {
                PlayerState localPlayerState = GameState.Instance.GetLocalPlayerState();
                if (localPlayerState == null)
                    Debug.LogWarning("Cannot play card: Local player state is null");
                else
                    Debug.LogWarning($"Cannot play card: Not enough energy ({localPlayerState.Energy} < {_cardData.EnergyCost})");
            }
            
            // Can't play the card - abort drag
            eventData.pointerDrag = null;
            return;
        }
        
        _isDragging = true;
        _originalPosition = _rectTransform.anchoredPosition;
        _originalParent = transform.parent;
        
        // Set as last sibling to render on top
        transform.SetAsLastSibling();
        
        // Make semi-transparent while dragging
        _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false;
        
        // Make sure the card is interactable
        if (_button != null)
        {
            _button.interactable = false;
        }
        
        Debug.Log($"Started dragging card: {(_cardData != null ? _cardData.Name : "null")}");
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || !_isDragReady) return;
        
        // Convert screen position to local position within the canvas
        Vector2 localPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position,
            _canvas.worldCamera,
            out localPosition))
        {
            // Move the card
            transform.position = _canvas.transform.TransformPoint(localPosition);
        }
        
        // UPDATED: Check for valid targets as we drag
        CheckForTargetsUnderPointer(eventData);
    }
    
    // NEW: Check for valid targets under the pointer
    private void CheckForTargetsUnderPointer(PointerEventData eventData)
    {
        // Raycast to find potential targets
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        // Reset all monster highlights first
        ResetAllMonsterHighlights();
        
        // Check each result for valid targets
        foreach (RaycastResult result in results)
        {
            // Skip the card itself
            if (result.gameObject == gameObject)
                continue;
                
            // Check for monster targets
            MonsterDisplay monsterDisplay = result.gameObject.GetComponent<MonsterDisplay>();
            if (monsterDisplay != null)
            {
                // Highlight if it's a valid target
                if (IsValidMonsterTarget(monsterDisplay))
                {
                    monsterDisplay.ShowHighlight(true);
                }
            }
        }
    }
    
    // NEW: Reset all highlights
    private void ResetAllMonsterHighlights()
    {
        // Using FindObjectsByType with FindObjectsSortMode.None which is faster
        MonsterDisplay[] allDisplays = UnityEngine.Object.FindObjectsByType<MonsterDisplay>(FindObjectsSortMode.None);
        foreach (var display in allDisplays)
        {
            display.ShowHighlight(false);
        }
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        
        _isDragging = false;
        
        // Reset visual properties
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        
        if (_button != null)
        {
            _button.interactable = true;
        }
        
        // Check if the card was dropped on a valid target
        GameObject target = FindDropTarget(eventData.position);
        
        // Reset all highlights
        ResetAllMonsterHighlights();
        
        if (target != null)
        {
            // Card was dropped on a valid target
            CardPlayed?.Invoke(this, target);
            
            Debug.Log($"Card {(_cardData != null ? _cardData.Name : "null")} played on {target.name}");
        }
        else
        {
            // Return to original position
            transform.SetParent(_originalParent);
            _rectTransform.anchoredPosition = _originalPosition;
            
            Debug.Log($"Card {(_cardData != null ? _cardData.Name : "null")} returned to hand");
        }
    }
    
    private GameObject FindDropTarget(Vector2 screenPosition)
    {
        // Raycast to find potential targets
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;
        
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        // Debug the raycast results
        if (results.Count > 0) {
            Debug.Log($"Raycast hit {results.Count} objects:");
            foreach (var result in results) {
                Debug.Log($"- {result.gameObject.name} ({result.gameObject.GetType()})");
            }
        } else {
            Debug.Log("Raycast hit no objects");
        }
        
        // Check each result for valid targets
        foreach (RaycastResult result in results)
        {
            // Skip the card itself
            if (result.gameObject == gameObject)
                continue;
                
            // Check for monster targets
            MonsterDisplay monsterDisplay = result.gameObject.GetComponent<MonsterDisplay>();
            if (monsterDisplay != null)
            {
                // Check if the card can target this monster based on its target type
                if (IsValidMonsterTarget(monsterDisplay))
                {
                    Debug.Log($"Found valid monster target: {result.gameObject.name}");
                    return result.gameObject;
                }
                else
                {
                    Debug.Log($"Found invalid monster target: {result.gameObject.name}");
                }
            }
        }
        
        Debug.Log("No valid target found");
        return null;
    }
    
    // UPDATED: Check if monster is a valid target based on player/opponent status
    private bool IsValidMonsterTarget(MonsterDisplay monsterDisplay)
    {
        if (_cardData == null) return false;
        
        // Check if it's the player's monster or opponent's monster
        bool isPlayerMonster = monsterDisplay.IsPlayerMonster();
        
        switch (_cardData.Target)
        {
            case CardTarget.Enemy:
            case CardTarget.AllEnemies:
                return !isPlayerMonster; // Target opponent's monster
                
            case CardTarget.Self:
                return isPlayerMonster; // Target player's monster
                
            case CardTarget.All:
                return true; // Can target either
                
            default:
                return false;
        }
    }
    
    private bool CanBePlayed()
    {
        if (GameState.Instance == null) {
            Debug.LogWarning("GameState.Instance is null");
            return false;
        }
        
        if (!GameState.Instance.IsLocalPlayerTurn()) {
            Debug.LogWarning("Not local player's turn");
            return false;
        }
        
        if (_cardData == null) {
            Debug.LogWarning("Card data is null");
            return false;
        }
        
        PlayerState localPlayerState = GameState.Instance.GetLocalPlayerState();
        if (localPlayerState == null) {
            Debug.LogWarning("Local player state is null");
            return false;
        }
        
        // Check if player has enough energy
        bool hasEnoughEnergy = localPlayerState.Energy >= _cardData.EnergyCost;
        if (!hasEnoughEnergy) {
            Debug.LogWarning($"Not enough energy: {localPlayerState.Energy} < {_cardData.EnergyCost}");
        }
        
        return hasEnoughEnergy;
    }
}