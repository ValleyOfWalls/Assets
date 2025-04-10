using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandUIController
{
    // UI elements
    private GameObject _handPanel;
    private Canvas _gameCanvas;
    
    // Card prefab and instances
    private GameObject _cardPrefab;
    private List<CardDisplay> _cardDisplays = new List<CardDisplay>();
    
    // Reference to player state
    private PlayerState _playerState;
    
    // Events
    public event Action<CardDisplay, GameObject> OnCardPlayed;
    
    public HandUIController(GameObject handPanel, Canvas gameCanvas, PlayerState playerState)
    {
        _handPanel = handPanel;
        _gameCanvas = gameCanvas;
        _playerState = playerState;
        
        // Create card prefab
        CreateCardPrefab();
    }
    
    private void CreateCardPrefab()
    {
        _cardPrefab = new GameObject("CardPrefab");
        _cardPrefab.SetActive(false);
        
        // Add RectTransform - essential for UI elements
        RectTransform cardRect = _cardPrefab.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(180, 250);
        
        // Card background
        Image cardBg = _cardPrefab.AddComponent<Image>();
        cardBg.color = new Color(0.2f, 0.2f, 0.2f);
        
        // Add CardDisplay component
        CardDisplay display = _cardPrefab.AddComponent<CardDisplay>();
        
        // Add CanvasGroup - required for drag operations
        CanvasGroup canvasGroup = _cardPrefab.AddComponent<CanvasGroup>();
        
        // Create title text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(_cardPrefab.transform, false);
        TMPro.TextMeshProUGUI titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.fontSize = 16;
        titleText.alignment = TMPro.TextAlignmentOptions.Center;
        titleText.color = Color.white;
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.85f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        
        // Create cost text
        GameObject costObj = new GameObject("CostText");
        costObj.transform.SetParent(_cardPrefab.transform, false);
        TMPro.TextMeshProUGUI costText = costObj.AddComponent<TMPro.TextMeshProUGUI>();
        costText.fontSize = 20;
        costText.alignment = TMPro.TextAlignmentOptions.Center;
        costText.color = Color.yellow;
        
        // Add background circle for cost
        GameObject costBgObj = new GameObject("CostBg");
        costBgObj.transform.SetParent(costObj.transform, false);
        costBgObj.transform.SetSiblingIndex(0);
        Image costBg = costBgObj.AddComponent<Image>();
        costBg.color = new Color(0.1f, 0.1f, 0.3f);
        
        RectTransform costBgRect = costBgObj.GetComponent<RectTransform>();
        costBgRect.anchorMin = Vector2.zero;
        costBgRect.anchorMax = Vector2.one;
        costBgRect.offsetMin = new Vector2(-5, -5);
        costBgRect.offsetMax = new Vector2(5, 5);
        
        RectTransform costRect = costObj.GetComponent<RectTransform>();
        costRect.anchorMin = new Vector2(0, 0.9f);
        costRect.anchorMax = new Vector2(0.2f, 1);
        costRect.offsetMin = Vector2.zero;
        costRect.offsetMax = Vector2.zero;
        
        // Create description text
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(_cardPrefab.transform, false);
        TMPro.TextMeshProUGUI descText = descObj.AddComponent<TMPro.TextMeshProUGUI>();
        descText.fontSize = 14;
        descText.alignment = TMPro.TextAlignmentOptions.Center;
        descText.color = Color.white;
        
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.1f, 0.2f);
        descRect.anchorMax = new Vector2(0.9f, 0.6f);
        descRect.offsetMin = Vector2.zero;
        descRect.offsetMax = Vector2.zero;
        
        // Set references in CardDisplay
        display.SetTextElements(titleText, costText, descText);
        
        // Add button for interactivity
        Button cardButton = _cardPrefab.AddComponent<Button>();
        ColorBlock colors = cardButton.colors;
        colors.highlightedColor = new Color(0.8f, 0.8f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.9f);
        cardButton.colors = colors;
        
        // Set up the button click handler
        display.SetButton(cardButton);
        
        GameManager.Instance.LogManager.LogMessage("Card prefab created in HandUIController");
    }
    
    public void UpdateHand(List<CardData> hand)
    {
        // Clear existing cards
        foreach (var display in _cardDisplays)
        {
            if (display != null && display.gameObject != null)
            {
                // Unsubscribe from events
                display.CardPlayed -= OnCardPlayedHandler;
                GameObject.Destroy(display.gameObject);
            }
        }
        _cardDisplays.Clear();
        
        // No cards in hand
        if (hand == null || hand.Count == 0) return;
        
        // Create card displays
        for (int i = 0; i < hand.Count; i++)
        {
            // Create card instance
            GameObject cardObj = GameObject.Instantiate(_cardPrefab, _handPanel.transform);
            cardObj.SetActive(true);
            cardObj.name = $"Card_{hand[i].Name}_{i}"; // Naming for debugging
            
            // Get the card display component
            CardDisplay display = cardObj.GetComponent<CardDisplay>();
            
            // Initialize drag operation
            display.InitializeDragOperation(_gameCanvas);
            
            // Set the card data
            display.SetCardData(hand[i], i);
            
            // Subscribe to card played event
            display.CardPlayed += OnCardPlayedHandler;
            
            // Add to list
            _cardDisplays.Add(display);
            
            // Add layout element to control card size
            LayoutElement layoutElement = cardObj.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = cardObj.AddComponent<LayoutElement>();
            }
            layoutElement.preferredWidth = 180;
            layoutElement.preferredHeight = 250;
            layoutElement.flexibleWidth = 0;
        }
        
        GameManager.Instance.LogManager.LogMessage($"Updated hand with {hand.Count} cards");
    }
    
    private void OnCardPlayedHandler(CardDisplay display, GameObject target)
    {
        // Forward the event
        OnCardPlayed?.Invoke(display, target);
    }
    
    public void SetCardsInteractable(bool interactable)
    {
        foreach (var display in _cardDisplays)
        {
            if (display != null && display.gameObject != null)
            {
                Button button = display.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = interactable;
                }
                
                CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.interactable = interactable;
                    canvasGroup.alpha = interactable ? 1.0f : 0.6f;
                }
            }
        }
    }
    
    // Helper method to get a card display by index
    public CardDisplay GetCardDisplayByIndex(int index)
    {
        if (index >= 0 && index < _cardDisplays.Count)
        {
            return _cardDisplays[index];
        }
        return null;
    }
    
    // Method to highlight playable cards based on energy
    public void HighlightPlayableCards(int currentEnergy)
    {
        foreach (var display in _cardDisplays)
        {
            if (display != null && display.gameObject != null)
            {
                CardData card = display.GetCardData();
                bool canPlay = (card != null && card.EnergyCost <= currentEnergy);
                
                // Set alpha based on playability
                CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = canPlay ? 1.0f : 0.6f;
                }
                
                // Optionally add a visual highlight for playable cards
                Image cardImage = display.GetComponent<Image>();
                if (cardImage != null)
                {
                    // Subtle outline for playable cards
                    cardImage.material = canPlay ? 
                        new Material(Shader.Find("UI/Default")) : 
                        null;
                }
            }
        }
    }
    
    // Method to handle card click
    public void HandleCardClick(CardDisplay display)
    {
        if (display != null && _playerState != null)
        {
            CardData card = display.GetCardData();
            int cardIndex = display.GetCardIndex();
            
            // Check if card can be played
            if (card.EnergyCost <= _playerState.Energy && 
                GameState.Instance != null && 
                GameState.Instance.IsLocalPlayerTurn())
            {
                // Determine appropriate target based on card type
                GameObject target = null;
                
                switch (card.Target)
                {
                    case CardTarget.Enemy:
                    case CardTarget.AllEnemies:
                        // Get opponent monster display
                        // This would need a reference to the BattleUIController
                        // or the target selection would be handled elsewhere
                        break;
                        
                    case CardTarget.Self:
                        // Get player's own monster display
                        // Same as above, needs reference
                        break;
                }
                
                // If target is found or not needed, play the card
                if (target != null || card.Target == CardTarget.All)
                {
                    OnCardPlayed?.Invoke(display, target);
                }
            }
        }
    }
}