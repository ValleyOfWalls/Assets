using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Required for Button, Image, LayoutElement etc.
using TMPro; // Required for TextMeshProUGUI

public class HandUIController
{
    // UI elements
    private GameObject _handPanel;
    private Canvas _gameCanvas; // Needed for drag operations calculations

    // Card prefab and instances
    private GameObject _cardPrefab; // Prefab used to create card visuals
    private List<CardDisplay> _cardDisplays = new List<CardDisplay>(); // List of currently displayed cards

    // Reference to player state (for energy checks, turn status)
    private PlayerState _localPlayerState; // Should be the *local* player's state

    // --- Event ---
    // Fired when a card is successfully dropped onto a valid target.
    // The subscriber (e.g., GameUI) is responsible for telling PlayerState to PlayCardLocally.
    public event Action<CardDisplay, GameObject> OnCardPlayedRequested;

    // Cached energy for highlighting
    private int _lastKnownEnergy = -1;
    private bool _lastKnownTurnState = false;

    public HandUIController(GameObject handPanel, Canvas gameCanvas, PlayerState localPlayerState)
    {
        _handPanel = handPanel;
        _gameCanvas = gameCanvas;
        _localPlayerState = localPlayerState;

        if (_handPanel == null || _gameCanvas == null || _localPlayerState == null)
        {
            Debug.LogError("HandUIController initialized with null references!");
            return;
        }

        // Ensure localPlayerState is indeed the one for the local player
        if (!_localPlayerState.Object.HasInputAuthority)
        {
             Debug.LogWarning("HandUIController initialized with a PlayerState that doesn't have Input Authority. UI might not function correctly.");
        }


        // Create card prefab
        CreateCardPrefab();

        // Subscribe to local player state changes for energy/stat updates
        // Assumes OnStatsChanged is static or accessible appropriately
        PlayerState.OnStatsChanged += HandleStatsChanged;
         // Subscribe to local turn state changes
         PlayerState.OnLocalTurnStateChanged += HandleLocalTurnStateChanged; // Assuming PlayerState fires this event


        // Initial UI state update based on current player state
        HandleStatsChanged(_localPlayerState); // Update energy highlighting
        HandleLocalTurnStateChanged(_localPlayerState.GetIsLocalPlayerTurn()); // Update card interactability

         GameManager.Instance?.LogManager?.LogMessage("HandUIController Initialized.");
    }

    // Call this when the HandUIController is no longer needed to prevent memory leaks
    public void Cleanup()
    {
        PlayerState.OnStatsChanged -= HandleStatsChanged;
        PlayerState.OnLocalTurnStateChanged -= HandleLocalTurnStateChanged;
        // Clear card displays and unsubscribe their events
        ClearCardDisplays();
        GameManager.Instance?.LogManager?.LogMessage("HandUIController Cleaned up.");
    }


    private void CreateCardPrefab()
    {
        // (Prefab creation code remains largely the same as provided previously)
        _cardPrefab = new GameObject("CardPrefab");
        _cardPrefab.SetActive(false); // Keep prefab inactive

        RectTransform cardRect = _cardPrefab.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(180, 250); // Example size

        Image cardBg = _cardPrefab.AddComponent<Image>();
        cardBg.color = new Color(0.2f, 0.2f, 0.2f); // Default background

        // Add essential components for display and interaction
        CardDisplay display = _cardPrefab.AddComponent<CardDisplay>();
        CanvasGroup canvasGroup = _cardPrefab.AddComponent<CanvasGroup>();
        Button cardButton = _cardPrefab.AddComponent<Button>();
        cardButton.targetGraphic = cardBg; // Make background clickable

        // Configure button colors (optional)
        ColorBlock colors = cardButton.colors;
        colors.highlightedColor = new Color(0.8f, 0.8f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.9f);
        cardButton.colors = colors;

        // Create Text Elements (Title, Cost, Description)
        // Ensure these child objects exist or create them dynamically
        TMP_Text titleText = CreateTextElementOnPrefab("TitleText", 16, TextAlignmentOptions.Center, Color.white, new Vector2(0, 0.85f), Vector2.one);
        TMP_Text costText = CreateTextElementOnPrefab("CostText", 20, TextAlignmentOptions.Center, Color.yellow, new Vector2(0, 0.9f), new Vector2(0.2f, 1f));
        TMP_Text descText = CreateTextElementOnPrefab("DescriptionText", 14, TextAlignmentOptions.Center, Color.white, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.6f));

        // Pass references to CardDisplay script
        display.SetTextElements(titleText, costText, descText);
        display.SetButton(cardButton); // Ensure CardDisplay can handle button clicks if needed

        // GameManager.Instance?.LogManager?.LogMessage("Card prefab created in HandUIController");
    }

    // Helper to create text elements *on the prefab*
    private TMP_Text CreateTextElementOnPrefab(string name, float fontSize, TextAlignmentOptions alignment, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(_cardPrefab.transform, false); // Parent to prefab
        TMP_Text textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.raycastTarget = false; // IMPORTANT: Prevent text blocking card interactions

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return textComponent;
    }


     // Clears existing card displays and unsubscribes events
    private void ClearCardDisplays()
    {
        foreach (var display in _cardDisplays)
        {
            if (display != null)
            {
                // Unsubscribe from the specific event handler for THIS instance
                display.CardPlayed -= CardDisplay_OnCardPlayed;
                if (display.gameObject != null)
                {
                    GameObject.Destroy(display.gameObject);
                }
            }
        }
        _cardDisplays.Clear();
    }


    // Updates the UI based on the LOCAL hand data provided
    public void UpdateHand(List<CardData> hand)
    {
        if (_handPanel == null || _cardPrefab == null || _gameCanvas == null)
        {
             Debug.LogError("HandUIController cannot update hand - essential components missing.");
            return;
        }

        // GameManager.Instance?.LogManager?.LogMessage($"HandUI: Updating hand UI with {hand?.Count ?? 0} cards.");

        // Clear existing card displays and unsubscribe events
        ClearCardDisplays();

        // No cards in hand
        if (hand == null || hand.Count == 0)
        {
             // Optional: Show an empty hand message or visual
             // GameManager.Instance?.LogManager?.LogMessage("HandUI: Hand is empty.");
            return;
        }

        // Create card displays for the new hand
        for (int i = 0; i < hand.Count; i++)
        {
            CardData cardData = hand[i];
            if (cardData == null) continue; // Skip null card data

            // Create card instance
            GameObject cardObj = GameObject.Instantiate(_cardPrefab, _handPanel.transform);
            cardObj.SetActive(true);
            cardObj.name = $"Card_{cardData.Name}_{i}"; // Naming for debugging

            CardDisplay display = cardObj.GetComponent<CardDisplay>();
            if (display == null)
            {
                Debug.LogError($"Card instance {cardObj.name} is missing CardDisplay component!");
                GameObject.Destroy(cardObj); // Cleanup invalid object
                continue;
            }

            // Initialize drag operation (needs the Canvas)
            display.InitializeDragOperation(_gameCanvas);
            display.SetCardData(cardData, i);

            // Subscribe to the card played event from THIS specific card display
            display.CardPlayed += CardDisplay_OnCardPlayed; // Subscribe internal handler

            _cardDisplays.Add(display);

            // Add layout element to help with horizontal layout spacing
            LayoutElement layoutElement = cardObj.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = cardObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 180; // Match prefab size or adjust as needed
            layoutElement.preferredHeight = 250;
            layoutElement.flexibleWidth = 0; // Prevent stretching if panel is wide
        }

        // After updating the hand, highlight playable cards based on current energy
        // And ensure cards are interactable based on current turn state
        UpdateCardVisualState();
    }

    // Internal handler for when a CardDisplay signals it was played
     private void CardDisplay_OnCardPlayed(CardDisplay display, GameObject target)
     {
         // GameManager.Instance?.LogManager?.LogMessage($"HandUI: Received play request for card: {display?.GetCardData()?.Name} on target: {target?.name}");
         // Forward the event request outwards (e.g., to GameUI which calls PlayerState)
         OnCardPlayedRequested?.Invoke(display, target);

          // Optional: Immediately hide or destroy the card visually for responsiveness,
          // although the hand update after PlayerState confirms might handle this.
          // if(display != null) display.gameObject.SetActive(false);
     }


    // Sets interactability based on whether it's the player's turn
    public void SetCardsInteractable(bool interactable)
    {
        // GameManager.Instance?.LogManager?.LogMessage($"HandUI: Setting card interactability to: {interactable}");
        _lastKnownTurnState = interactable; // Cache the turn state
        UpdateCardVisualState(); // Update visuals based on combined state
    }

    // Handles player stat changes, specifically for energy updates
    private void HandleStatsChanged(PlayerState playerState)
    {
        // Ensure this update is for the local player this UI controls
        if (playerState == _localPlayerState)
        {
            if (_lastKnownEnergy != playerState.Energy)
            {
                _lastKnownEnergy = playerState.Energy;
                // GameManager.Instance?.LogManager?.LogMessage($"HandUI: Detected energy change: {_lastKnownEnergy}");
                UpdateCardVisualState(); // Update visuals based on combined state
            }
        }
    }

     // Handle changes in local turn state
     private void HandleLocalTurnStateChanged(bool isPlayerTurnActive)
     {
          // This assumes the event is fired for the local player
           // GameManager.Instance?.LogManager?.LogMessage($"HandUI: Detected local turn state change: {isPlayerTurnActive}");
          SetCardsInteractable(isPlayerTurnActive);
     }


    // Updates card visuals based on both playability (energy) and interactability (turn state)
    private void UpdateCardVisualState()
    {
        bool isPlayerTurn = _lastKnownTurnState;
        int currentEnergy = _lastKnownEnergy;

        foreach (var display in _cardDisplays)
        {
            if (display != null)
            {
                CardData card = display.GetCardData();
                bool canAfford = (card != null && card.EnergyCost <= currentEnergy);
                bool canPlay = isPlayerTurn && canAfford; // Can only play if it's turn AND can afford

                // --- Update Interactability ---
                Button button = display.GetComponent<Button>();
                if (button != null) button.interactable = isPlayerTurn; // Button interactable only on player turn

                CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    // Make draggable only if it can be played
                    canvasGroup.interactable = canPlay;
                     // Dim the card if it cannot be played (either wrong turn or too expensive)
                    canvasGroup.alpha = canPlay ? 1.0f : 0.6f;
                    // Ensure raycasts are blocked only when interactable (for dragging)
                     // canvasGroup.blocksRaycasts = canPlay; // Might interfere with dropping ONTO cards? Usually keep false for dragging.
                }

                 // Optional: Add other visual cues like borders for affordability vs playability
            }
        }
    }
}