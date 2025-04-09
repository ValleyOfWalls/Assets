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
    
    // Required method to initialize the card for drag operation
    public void InitializeDragOperation(Canvas canvas)
    {
        _canvas = canvas;
        
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
        
        // Set title
        if (_titleText != null)
        {
            _titleText.text = _cardData.Name;
        }
        
        // Set cost
        if (_costText != null)
        {
            _costText.text = _cardData.EnergyCost.ToString();
        }
        
        // Set description
        if (_descriptionText != null)
        {
            _descriptionText.text = _cardData.Description;
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
            Debug.LogWarning($"Attempted to drag card {gameObject.name} but drag operation not initialized");
            eventData.pointerDrag = null;  // Cancel the drag
            return;
        }
        
        if (!CanBePlayed())
        {
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
                    return result.gameObject;
                }
            }
            
            // Add other target types as needed (player, etc.)
        }
        
        return null;
    }
    
    private bool IsValidMonsterTarget(MonsterDisplay monsterDisplay)
    {
        if (_cardData == null) return false;
        
        // Get the target's parent object to determine if it's player or opponent monster
        bool isOpponentMonster = monsterDisplay.gameObject.name.Contains("Opponent");
        
        switch (_cardData.Target)
        {
            case CardTarget.Enemy:
            case CardTarget.AllEnemies:
                return isOpponentMonster;
                
            case CardTarget.Self:
                return !isOpponentMonster;
                
            case CardTarget.All:
                return true;
                
            default:
                return false;
        }
    }
    
    private bool CanBePlayed()
    {
        if (GameState.Instance == null || !GameState.Instance.IsLocalPlayerTurn())
            return false;
            
        if (_cardData == null)
            return false;
            
        PlayerState localPlayerState = GameState.Instance.GetLocalPlayerState();
        if (localPlayerState == null)
            return false;
            
        // Check if player has enough energy
        return localPlayerState.Energy >= _cardData.EnergyCost;
    }
}