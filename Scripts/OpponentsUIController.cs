using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Added missing namespace
using TMPro;          // Added missing namespace
using Fusion;         // Added for PlayerRef

public class OpponentsUIController
{
    // UI elements
    private GameObject _opponentsPanel; // The parent panel containing opponent displays
    private GameObject _opponentStatsPrefab; // Prefab for a single opponent's display

    // Dictionary to track opponent displays by their PlayerRef
    private Dictionary<PlayerRef, OpponentStatsDisplay> _opponentDisplays = new Dictionary<PlayerRef, OpponentStatsDisplay>();

    public OpponentsUIController(GameObject opponentsPanel)
    {
        _opponentsPanel = opponentsPanel;
        if (_opponentsPanel == null)
        {
            Debug.LogError("OpponentsUIController initialized with a null panel!");
            return;
        }

        // Create the opponent stats prefab
        _opponentStatsPrefab = CreateOpponentStatsPrefab();
        if (_opponentStatsPrefab == null)
        {
             Debug.LogError("Failed to create OpponentStatsPrefab!");
             return;
        }


        // Initialize with any existing opponent states (useful if UI is created after players join)
        InitializeOpponentStates();
        GameManager.Instance?.LogManager?.LogMessage("OpponentsUIController Initialized.");
    }

    // Creates the prefab used for displaying each opponent's stats
    private GameObject CreateOpponentStatsPrefab()
    {
        GameObject opponentObj = new GameObject("OpponentStatsPrefab");
        opponentObj.SetActive(false); // Prefab should be inactive initially

        // Background Image
        Image bg = opponentObj.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.3f, 0.8f); // Example color

        // Layout Element for sizing within a VerticalLayoutGroup
        LayoutElement layoutElement = opponentObj.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 80; // Adjust height as needed
        layoutElement.flexibleWidth = 1; // Allow it to fill width

        // Add the script that controls this specific display
        OpponentStatsDisplay display = opponentObj.AddComponent<OpponentStatsDisplay>();

        // Create Text elements within the prefab
        // Name Text
        TMP_Text nameText = CreateTextElementForPrefab(opponentObj, "Name", "Opponent Name", 16, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, new Vector2(0, 0.7f), new Vector2(1, 1));
        // Health Text
        TMP_Text healthText = CreateTextElementForPrefab(opponentObj, "Health", "HP: --/--", 14, FontStyles.Normal, TextAlignmentOptions.Left, Color.white, new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.65f));
        // Score Text
        TMP_Text scoreText = CreateTextElementForPrefab(opponentObj, "Score", "Score: --", 14, FontStyles.Normal, TextAlignmentOptions.Left, Color.yellow, new Vector2(0.05f, 0.05f), new Vector2(0.5f, 0.35f));
        // *** Placeholder for Fight Status Text ***
        TMP_Text statusText = CreateTextElementForPrefab(opponentObj, "FightStatus", "", 12, FontStyles.Italic, TextAlignmentOptions.Right, Color.cyan, new Vector2(0.5f, 0.05f), new Vector2(0.95f, 0.35f));


        // Pass text references to the display script
        display.SetTextElements(nameText, healthText, scoreText); // Assuming OpponentStatsDisplay has this method

        // Add RectTransform for positioning if needed (though LayoutElement often handles this)
        // RectTransform rect = opponentObj.GetComponent<RectTransform>();
        // if (rect == null) rect = opponentObj.AddComponent<RectTransform>();
        // rect.sizeDelta = new Vector2(0, 100); // Set default size if not using LayoutElement strictly

        return opponentObj;
    }

     // Helper to create TextMeshProUGUI elements on the prefab
     private TMP_Text CreateTextElementForPrefab(GameObject parent, string name, string defaultText, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color, Vector2 anchorMin, Vector2 anchorMax)
     {
         GameObject textObj = new GameObject(name);
         textObj.transform.SetParent(parent.transform, false);
         TMP_Text textComp = textObj.AddComponent<TextMeshProUGUI>();
         textComp.text = defaultText;
         textComp.fontSize = fontSize;
         textComp.fontStyle = style;
         textComp.alignment = alignment;
         textComp.color = color;
         textComp.raycastTarget = false; // Usually UI text shouldn't block raycasts

         RectTransform rect = textObj.GetComponent<RectTransform>();
         rect.anchorMin = anchorMin;
         rect.anchorMax = anchorMax;
         rect.offsetMin = new Vector2(5, 2); // Add some padding
         rect.offsetMax = new Vector2(-5, -2);

         return textComp;
     }


    // Populates the panel with existing opponents when the UI initializes
    private void InitializeOpponentStates()
    {
        if (GameState.Instance == null) return;

        PlayerRef localPlayerRef = GameState.Instance.GetLocalPlayerRef();
        var allPlayerStates = GameState.Instance.GetAllPlayerStates();

        foreach (var entry in allPlayerStates)
        {
            // Add display only for players who are NOT the local player
            if (entry.Key != localPlayerRef)
            {
                AddOpponentDisplay(entry.Key, entry.Value);
            }
        }
    }

    // Adds a new display for an opponent
    public void AddOpponentDisplay(PlayerRef playerRef, PlayerState playerState)
    {
        if (_opponentsPanel == null || _opponentStatsPrefab == null || playerState == null)
        {
            // GameManager.Instance?.LogManager?.LogError("Cannot create opponent display: panel, prefab, or playerState is null");
            return;
        }

        // Avoid adding duplicates
        if (_opponentDisplays.ContainsKey(playerRef))
        {
             // GameManager.Instance?.LogManager?.LogMessage($"Opponent display for {playerRef} already exists. Updating.");
             UpdateOpponentStats(playerState); // Update existing one instead
            return;
        }

        string playerName = playerState.PlayerName.ToString();
        // GameManager.Instance?.LogManager?.LogMessage($"Creating opponent display for {playerName} ({playerRef})");

        // Create new display instance from prefab
        GameObject opponentObj = GameObject.Instantiate(_opponentStatsPrefab, _opponentsPanel.transform);
        opponentObj.name = $"OpponentDisplay_{playerName}"; // Unique name for hierarchy
        opponentObj.SetActive(true); // Activate the instantiated object

        OpponentStatsDisplay display = opponentObj.GetComponent<OpponentStatsDisplay>();
        if (display != null)
        {
            // Store in the dictionary before updating
            _opponentDisplays[playerRef] = display;
            // Update with current player state data
            display.UpdateDisplay(playerState);
             // Also update initial fight status visual
             bool isComplete = GameState.Instance.IsPlayerFightComplete(playerRef);
             UpdateOpponentFightStatusVisual(display, isComplete); // Update visual based on current state
        }
        else
        {
            GameManager.Instance?.LogManager?.LogError("OpponentStatsDisplay component not found on instantiated prefab!");
            GameObject.Destroy(opponentObj); // Clean up failed instance
        }
    }

    // Removes the display for an opponent who left
    public void RemoveOpponentDisplay(PlayerRef playerRef)
    {
        if (_opponentDisplays.TryGetValue(playerRef, out OpponentStatsDisplay display))
        {
            if (display != null && display.gameObject != null)
            {
                GameObject.Destroy(display.gameObject);
            }
            _opponentDisplays.Remove(playerRef);
            // GameManager.Instance?.LogManager?.LogMessage($"Removed opponent display for {playerRef}.");
        }
    }

    // Updates the stats display for a specific opponent based on their PlayerState
    public void UpdateOpponentStats(PlayerState playerState)
    {
        if (playerState == null) return;

        // Find the PlayerRef associated with this PlayerState instance
        // This might be inefficient if called very frequently - consider passing PlayerRef if possible
        PlayerRef playerRef = default;
        if (playerState.Object != null) // Check if the NetworkObject is valid
        {
             playerRef = playerState.Object.InputAuthority; // Or StateAuthority depending on your setup
        } else {
             // Fallback: Search dictionary (less reliable if PlayerState object is reused/invalid)
             foreach(var pair in GameState.Instance?.GetAllPlayerStates() ?? new Dictionary<PlayerRef, PlayerState>()) {
                 if(pair.Value == playerState) {
                     playerRef = pair.Key;
                     break;
                 }
             }
        }


        if (playerRef != default && _opponentDisplays.TryGetValue(playerRef, out OpponentStatsDisplay display))
        {
            if(display != null) display.UpdateDisplay(playerState);
        }
         // else { GameManager.Instance?.LogManager?.LogMessage($"UpdateOpponentStats: Could not find display for PlayerRef {playerRef} associated with PlayerState {playerState.Id}"); }
    }

     // *** ADDED METHOD ***
     // Updates the visual indicator for an opponent's fight completion status
     public void UpdateOpponentFightStatus(PlayerRef playerRef, bool isComplete)
     {
         if (_opponentDisplays.TryGetValue(playerRef, out OpponentStatsDisplay display))
         {
             UpdateOpponentFightStatusVisual(display, isComplete);
         }
          // else { GameManager.Instance?.LogManager?.LogMessage($"UpdateOpponentFightStatus: Could not find display for PlayerRef {playerRef}"); }
     }

     // Helper method to update the visual elements for fight status
     private void UpdateOpponentFightStatusVisual(OpponentStatsDisplay display, bool isComplete)
     {
         if (display == null) return;

         // Find the status text element (assuming it was created in the prefab)
         Transform statusTransform = display.transform.Find("FightStatus"); // Find by name
         TMP_Text statusText = statusTransform?.GetComponent<TMP_Text>();

         if (statusText != null)
         {
             statusText.text = isComplete ? "Fight Done" : ""; // Show text only when done
             statusText.color = isComplete ? Color.green : Color.cyan; // Example color change
              // GameManager.Instance?.LogManager?.LogMessage($"Updated fight status visual for {display.name} to: {statusText.text}");
         }
          // else { GameManager.Instance?.LogManager?.LogMessage($"Could not find 'FightStatus' text element on {display.name}"); }

         // Optionally change background color or add an icon as well
         // Image bgImage = display.GetComponent<Image>();
         // if (bgImage != null) {
         //     bgImage.color = isComplete ? new Color(0.1f, 0.3f, 0.1f, 0.8f) : new Color(0.2f, 0.2f, 0.3f, 0.8f); // Example: Greenish when done
         // }
     }


    // Updates all currently displayed opponents
    public void UpdateAllOpponents()
    {
        if (GameState.Instance == null) return;

        PlayerRef localPlayerRef = GameState.Instance.GetLocalPlayerRef();
        var allPlayerStates = GameState.Instance.GetAllPlayerStates();

         // Check for players who might need a display created (e.g., if UI initialized late)
        foreach (var entry in allPlayerStates)
        {
             if (entry.Key != localPlayerRef) // If it's an opponent
             {
                  if (!_opponentDisplays.ContainsKey(entry.Key))
                  {
                       // Opponent exists in GameState but not in UI, add them
                       AddOpponentDisplay(entry.Key, entry.Value);
                  }
                  else if (_opponentDisplays.TryGetValue(entry.Key, out var display) && display != null)
                  {
                       // Opponent already has a display, update it
                       display.UpdateDisplay(entry.Value);
                       // Also update their fight status visual
                       bool isComplete = GameState.Instance.IsPlayerFightComplete(entry.Key);
                       UpdateOpponentFightStatusVisual(display, isComplete);
                  }
             }
        }

         // Optional: Check for displays whose players no longer exist in GameState
         List<PlayerRef> playersToRemove = new List<PlayerRef>();
         foreach(var existingDisplayRef in _opponentDisplays.Keys)
         {
              if(!allPlayerStates.ContainsKey(existingDisplayRef))
              {
                   playersToRemove.Add(existingDisplayRef);
              }
         }
         foreach(var playerRefToRemove in playersToRemove)
         {
              RemoveOpponentDisplay(playerRefToRemove);
         }
    }
}