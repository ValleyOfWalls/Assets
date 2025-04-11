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
    private Image _backgroundImage; // Added reference for easier access
    private Button _button;

    // Data
    private CardData _cardData;
    private int _cardIndex; // Index in the LOCAL hand list

    // Drag and drop
    private Canvas _canvas;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Vector2 _originalPosition;
    private Transform _originalParent;
    private bool _isDragging = false;
    private bool _isDragReady = false;

    // Events
    public event Action<CardDisplay> CardClicked;
    public event Action<CardDisplay, GameObject> CardPlayed; // Fired when dropped on valid target

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _backgroundImage = GetComponent<Image>(); // Get background image

        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Attempt to find text elements automatically if not set externally
        if (_titleText == null) _titleText = transform.Find("TitleText")?.GetComponent<TMP_Text>();
        if (_costText == null) _costText = transform.Find("CostText")?.GetComponent<TMP_Text>();
        if (_descriptionText == null) _descriptionText = transform.Find("DescriptionText")?.GetComponent<TMP_Text>();

         // Ensure text elements prevent raycasts so they don't block card clicks/drags
         if (_titleText) _titleText.raycastTarget = false;
         if (_costText) _costText.raycastTarget = false;
         if (_descriptionText) _descriptionText.raycastTarget = false;

          // Find button automatically
         if (_button == null) _button = GetComponent<Button>();
         if (_button != null)
         {
             _button.onClick.RemoveAllListeners(); // Clear existing listeners
             _button.onClick.AddListener(OnButtonClicked);
         }
         else
         {
              Debug.LogWarning($"CardDisplay {gameObject.name} awake without a Button component.");
         }
    }

    // Required method to initialize the card for drag operation
    public void InitializeDragOperation(Canvas canvas)
    {
        _canvas = canvas;
        // Ensure we have the required components (already attempted in Awake, but double-check)
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

        // Only mark as drag-ready if we have all necessary components
        _isDragReady = (_canvas != null && _rectTransform != null && _canvasGroup != null);
        // Debug.Log($"Card {gameObject.name} drag initialized: {_isDragReady}, Canvas: {(_canvas != null ? _canvas.name : "null")}");
    }

    // Allow setting text elements externally if needed (e.g., by HandUIController)
    public void SetTextElements(TMP_Text titleText, TMP_Text costText, TMP_Text descriptionText)
    {
        _titleText = titleText;
        _costText = costText;
        _descriptionText = descriptionText;

         // Ensure text elements prevent raycasts
         if (_titleText) _titleText.raycastTarget = false;
         if (_costText) _costText.raycastTarget = false;
         if (_descriptionText) _descriptionText.raycastTarget = false;

        // Optional: Debug log to verify
        // if (_titleText == null || _costText == null || _descriptionText == null)
        //     Debug.LogError($"Card {gameObject.name} - Not all text elements were set properly externally!");
    }

    // Allow setting button externally
    public void SetButton(Button button)
    {
        _button = button;
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

        // Set title, cost, description
        if (_titleText != null) _titleText.text = _cardData.Name;
        // else Debug.LogError("Title text component is null when updating visuals!");

        if (_costText != null) _costText.text = _cardData.EnergyCost.ToString();
        // else Debug.LogError("Cost text component is null when updating visuals!");

        if (_descriptionText != null)
        {
             // Base description
             string fullDescription = _cardData.Description;

             // Add target info
             string targetInfo = "";
             switch (_cardData.Target)
             {
                 case CardTarget.Self: targetInfo = "[Targets: Your Monster]"; break;
                 case CardTarget.Enemy: targetInfo = "[Targets: Enemy Monster]"; break;
                 // case CardTarget.AllEnemies: targetInfo = "[Targets: All Enemies]"; break; // Less relevant now?
                 case CardTarget.All: targetInfo = "[Targets: All Monsters]"; break;
             }
             if (!string.IsNullOrEmpty(targetInfo))
             {
                 // Add newline and formatting for target info
                 fullDescription += $"\n\n<color=#aaaaaa><size=10>{targetInfo}</size></color>";
             }
            _descriptionText.text = fullDescription;
        }
        // else Debug.LogError("Description text component is null when updating visuals!");


        // Set card background color based on type
        if (_backgroundImage != null)
        {
            switch (_cardData.Type)
            {
                case CardType.Attack: _backgroundImage.color = new Color(0.8f, 0.2f, 0.2f, 1f); break;
                case CardType.Skill: _backgroundImage.color = new Color(0.2f, 0.6f, 0.8f, 1f); break;
                case CardType.Power: _backgroundImage.color = new Color(0.8f, 0.4f, 0.8f, 1f); break;
                default: _backgroundImage.color = new Color(0.3f, 0.3f, 0.3f, 1f); break;
            }
        }

        // Ensure text elements are rendered above background (usually handled by hierarchy)
    }

    private void OnButtonClicked()
    {
        // Handle simple click (if not dragging) - could be used for inspecting card?
        if (!_isDragging)
        {
             // GameManager.Instance?.LogManager?.LogMessage($"Card clicked: {_cardData?.Name}");
            CardClicked?.Invoke(this);
        }
    }

    public CardData GetCardData() => _cardData;
    public int GetCardIndex() => _cardIndex;

    // --- Drag and Drop Implementation ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_isDragReady)
        {
            // Debug.LogWarning($"Attempted to drag card {gameObject.name} but drag operation not initialized.");
            eventData.pointerDrag = null; // Cancel the drag
            return;
        }

        // *** FIXED: Use CanBePlayed() which checks local PlayerState ***
        if (!CanBePlayed())
        {
            // Debug.LogWarning($"Cannot drag card {_cardData?.Name}: Not playable right now.");
            eventData.pointerDrag = null; // Cancel the drag
            return;
        }

        _isDragging = true;
        _originalPosition = _rectTransform.anchoredPosition;
        _originalParent = transform.parent; // Remember original parent (HandPanel)

        // Move to top level of canvas for dragging overlay
        transform.SetParent(_canvas.transform, true);
        transform.SetAsLastSibling(); // Render on top

        _canvasGroup.alpha = 0.6f; // Make semi-transparent
        _canvasGroup.blocksRaycasts = false; // Allow raycasts to pass through to targets

        // Disable button interaction while dragging
        if (_button != null) _button.interactable = false;

        // Debug.Log($"Started dragging card: {_cardData?.Name}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || !_isDragReady) return;

        // Move the card with the pointer
        // Convert screen position to local position within the canvas for correct placement
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform, // The canvas RectTransform
            eventData.position, // Current screen position of the pointer
            _canvas.worldCamera, // The camera rendering the canvas (null for Overlay)
            out Vector2 localPointerPosition))
        {
            _rectTransform.localPosition = localPointerPosition;
        }

        // Highlight potential targets under the pointer
        CheckForTargetsUnderPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return; // Shouldn't happen, but safety check
        _isDragging = false;

        // Reset visual properties
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        if (_button != null) _button.interactable = true; // Re-enable button

        // Find where the card was dropped
        GameObject target = FindDropTarget(eventData.position);

        // Reset all monster highlights regardless of target
        ResetAllMonsterHighlights();

        if (target != null)
        {
            // Valid target found - Fire the CardPlayed event
            // GameManager.Instance?.LogManager?.LogMessage($"Card {(_cardData != null ? _cardData.Name : "null")} played on {target.name}");
            CardPlayed?.Invoke(this, target);
            // The card GameObject will likely be destroyed by the handler (e.g., HandUIController)
            // after the PlayerState confirms the play was successful.
        }
        else
        {
            // No valid target - return card to hand visually
            // GameManager.Instance?.LogManager?.LogMessage($"Card {(_cardData != null ? _cardData.Name : "null")} returned to hand.");
            transform.SetParent(_originalParent); // Return to original parent (HandPanel)
            _rectTransform.anchoredPosition = _originalPosition; // Snap back to original position
        }
    }

    // Checks if the card can currently be played based on turn and energy
    private bool CanBePlayed()
    {
        if (_cardData == null) return false;

        // *** FIXED: Check local player state for turn and energy ***
        PlayerState localPlayerState = GameState.Instance?.GetLocalPlayerState();
        if (localPlayerState == null)
        {
            // Debug.LogWarning("Cannot check CanBePlayed: Local player state is null");
            return false;
        }

        // Check 1: Is it the player's local turn in the fight?
        if (!localPlayerState.GetIsLocalPlayerTurn())
        {
            // Debug.LogWarning($"Cannot play card {_cardData.Name}: Not local player's turn.");
            return false;
        }

        // Check 2: Does the player have enough energy? (Check networked property)
        if (localPlayerState.Energy < _cardData.EnergyCost)
        {
            // Debug.LogWarning($"Cannot play card {_cardData.Name}: Not enough energy ({localPlayerState.Energy} < {_cardData.EnergyCost})");
            return false;
        }

        return true; // All checks passed
    }


    // Finds the first valid drop target under the pointer position
    private GameObject FindDropTarget(Vector2 screenPosition)
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        // Debug Raycast Hits:
        // if (results.Count > 0) {
        //     Debug.Log($"Raycast hit {results.Count} objects at {screenPosition}:");
        //     foreach (var result in results) Debug.Log($"- {result.gameObject.name} ({result.gameObject.layer})");
        // } else Debug.Log("Raycast hit no UI objects.");


        foreach (RaycastResult result in results)
        {
            // Skip the card itself
            if (result.gameObject == gameObject) continue;

            // Check if it's a MonsterDisplay
            MonsterDisplay monsterDisplay = result.gameObject.GetComponent<MonsterDisplay>();
            if (monsterDisplay != null)
            {
                // Check if the card can target this type of monster
                if (IsValidMonsterTarget(monsterDisplay))
                {
                    // Debug.Log($"Found valid monster target: {result.gameObject.name}");
                    return result.gameObject; // Return the valid monster display GameObject
                }
                 //else Debug.Log($"Found invalid monster target: {result.gameObject.name}");
            }
            // Can add checks for other valid drop zones here if needed
        }

        // Debug.Log("No valid drop target found.");
        return null; // No valid target found
    }


    // Checks if the card can target a specific MonsterDisplay based on card rules
    private bool IsValidMonsterTarget(MonsterDisplay monsterDisplay)
    {
        if (_cardData == null || monsterDisplay == null) return false;

        bool isTargetingOwn = monsterDisplay.IsPlayerMonster(); // Check if the target is the player's own

        switch (_cardData.Target)
        {
            case CardTarget.Enemy:
            case CardTarget.AllEnemies: // Treat AllEnemies as single Enemy in 1v1 monster fights
                return !isTargetingOwn;
            case CardTarget.Self:
                return isTargetingOwn;
            case CardTarget.All:
                return true; // Can target either
            default:
                return false;
        }
    }


    // --- Target Highlighting Logic ---

    // Checks potential targets under the pointer during drag
    private void CheckForTargetsUnderPointer(PointerEventData eventData)
    {
        // Reset all highlights first
        ResetAllMonsterHighlights();

        PointerEventData hoverEventData = new PointerEventData(EventSystem.current)
        {
            position = eventData.position
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(hoverEventData, results);


        foreach (RaycastResult result in results)
        {
            if (result.gameObject == gameObject) continue; // Skip self

            MonsterDisplay monsterDisplay = result.gameObject.GetComponent<MonsterDisplay>();
            if (monsterDisplay != null)
            {
                // Highlight if it's a valid target for this card
                if (IsValidMonsterTarget(monsterDisplay))
                {
                    monsterDisplay.ShowHighlight(true);
                    // Only highlight the first valid monster found typically
                     break;
                }
            }
        }
    }

    // Turns off highlight on all monster displays
    private void ResetAllMonsterHighlights()
    {
         // Using FindObjectsByType which is recommended over FindObjectsOfType
        MonsterDisplay[] allDisplays = UnityEngine.Object.FindObjectsByType<MonsterDisplay>(FindObjectsSortMode.None);
        foreach (var display in allDisplays)
        {
            display.ShowHighlight(false);
        }
    }

}