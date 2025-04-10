using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUIController
{
    // UI elements
    private GameObject _lobbyPanel;
    private Button _readyButton;
    private TMP_Text _countdownText;
    private TMP_Text _roomNameText;
    private Transform _playerListContent;
    private GameObject _playerListItemPrefab;
    
    // Player list tracking
    private Dictionary<string, GameObject> _playerListItems = new Dictionary<string, GameObject>();
    
    // Events
    public event Action OnReadyButtonClicked;
    
    public LobbyUIController(Transform canvasTransform)
    {
        CreateLobbyPanel(canvasTransform);
    }
    
    private void CreateLobbyPanel(Transform parentTransform)
    {
        // Create lobby panel
        GameObject lobbyPanelObj = new GameObject("Lobby Panel");
        lobbyPanelObj.transform.SetParent(parentTransform, false);
        _lobbyPanel = lobbyPanelObj;
        
        Image lobbyPanelImage = lobbyPanelObj.AddComponent<Image>();
        lobbyPanelImage.color = new Color(0, 0, 0, 0.8f);
        RectTransform lobbyPanelRect = lobbyPanelObj.GetComponent<RectTransform>();
        lobbyPanelRect.anchorMin = new Vector2(0, 0);
        lobbyPanelRect.anchorMax = new Vector2(0.3f, 1);
        lobbyPanelRect.offsetMin = Vector2.zero;
        lobbyPanelRect.offsetMax = Vector2.zero;
        
        // Add title text
        CreateLobbyTitle(lobbyPanelObj.transform);
        
        // Add room name display
        CreateRoomNameDisplay(lobbyPanelObj.transform);
        
        // Add player list title
        CreatePlayerListTitle(lobbyPanelObj.transform);
        
        // Add player list
        CreatePlayerList(lobbyPanelObj.transform);
        
        // Add ready button
        CreateReadyButton(lobbyPanelObj.transform);
        
        // Add countdown text
        CreateCountdownText(lobbyPanelObj.transform);
        
        // Set up button listeners
        SetupButtonListeners();
        
        // Initially hide the panel
        _lobbyPanel.SetActive(false);
    }
    
    private void CreateLobbyTitle(Transform parentTransform)
    {
        GameObject lobbyTitleObj = new GameObject("Lobby Title");
        lobbyTitleObj.transform.SetParent(parentTransform, false);
        TMP_Text lobbyTitleText = lobbyTitleObj.AddComponent<TextMeshProUGUI>();
        lobbyTitleText.text = "Game Lobby";
        lobbyTitleText.fontSize = 24;
        lobbyTitleText.alignment = TextAlignmentOptions.Center;
        lobbyTitleText.color = Color.white;
        RectTransform lobbyTitleRect = lobbyTitleObj.GetComponent<RectTransform>();
        lobbyTitleRect.anchorMin = new Vector2(0.1f, 0.9f);
        lobbyTitleRect.anchorMax = new Vector2(0.9f, 1f);
        lobbyTitleRect.offsetMin = Vector2.zero;
        lobbyTitleRect.offsetMax = Vector2.zero;
    }
    
    private void CreateRoomNameDisplay(Transform parentTransform)
    {
        GameObject roomNameObj = new GameObject("Room Name Text");
        roomNameObj.transform.SetParent(parentTransform, false);
        _roomNameText = roomNameObj.AddComponent<TextMeshProUGUI>();
        _roomNameText.text = "Room: asd";
        _roomNameText.fontSize = 18;
        _roomNameText.alignment = TextAlignmentOptions.Center;
        _roomNameText.color = Color.white;
        RectTransform roomNameRect = roomNameObj.GetComponent<RectTransform>();
        roomNameRect.anchorMin = new Vector2(0.1f, 0.85f);
        roomNameRect.anchorMax = new Vector2(0.9f, 0.9f);
        roomNameRect.offsetMin = Vector2.zero;
        roomNameRect.offsetMax = Vector2.zero;
    }
    
    private void CreatePlayerListTitle(Transform parentTransform)
    {
        GameObject playerListTitleObj = new GameObject("Player List Title");
        playerListTitleObj.transform.SetParent(parentTransform, false);
        TMP_Text playerListTitleText = playerListTitleObj.AddComponent<TextMeshProUGUI>();
        playerListTitleText.text = "Players";
        playerListTitleText.fontSize = 18;
        playerListTitleText.alignment = TextAlignmentOptions.Center;
        playerListTitleText.color = Color.white;
        RectTransform playerListTitleRect = playerListTitleObj.GetComponent<RectTransform>();
        playerListTitleRect.anchorMin = new Vector2(0.1f, 0.8f);
        playerListTitleRect.anchorMax = new Vector2(0.9f, 0.85f);
        playerListTitleRect.offsetMin = Vector2.zero;
        playerListTitleRect.offsetMax = Vector2.zero;
    }
    
    private void CreatePlayerList(Transform parentTransform)
    {
        GameObject playerListObj = new GameObject("Player List");
        playerListObj.transform.SetParent(parentTransform, false);
        
        RectTransform playerListRect = playerListObj.AddComponent<RectTransform>();
        playerListRect.anchorMin = new Vector2(0.05f, 0.3f);
        playerListRect.anchorMax = new Vector2(0.95f, 0.8f);
        playerListRect.offsetMin = Vector2.zero;
        playerListRect.offsetMax = Vector2.zero;
        
        // Create content parent - this will hold our player items
        GameObject playerListContentObj = new GameObject("Player List Content");
        playerListContentObj.transform.SetParent(playerListObj.transform, false);
        
        // Add vertical layout group
        VerticalLayoutGroup verticalLayout = playerListContentObj.AddComponent<VerticalLayoutGroup>();
        verticalLayout.spacing = 5f;
        verticalLayout.padding = new RectOffset(5, 5, 5, 5);
        
        // Get reference to player list content
        _playerListContent = playerListContentObj.transform;
        
        RectTransform contentRect = playerListContentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        
        // Create player list item prefab
        _playerListItemPrefab = CreatePlayerListItemPrefab();
    }
    
    private GameObject CreatePlayerListItemPrefab()
    {
        GameObject itemObj = new GameObject("Player List Item");
        itemObj.SetActive(false); // This is a prefab
        
        // Add background image
        Image itemImage = itemObj.AddComponent<Image>();
        itemImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Set layout properties
        RectTransform itemRect = itemObj.GetComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0, 30);
        
        // Add horizontal layout group
        HorizontalLayoutGroup itemLayout = itemObj.AddComponent<HorizontalLayoutGroup>();
        itemLayout.padding = new RectOffset(5, 5, 5, 5);
        itemLayout.spacing = 5f;
        itemLayout.childAlignment = TextAnchor.MiddleLeft;
        
        // Add player name text
        GameObject nameTextObj = new GameObject("Player Name");
        nameTextObj.transform.SetParent(itemObj.transform, false);
        TMP_Text nameText = nameTextObj.AddComponent<TextMeshProUGUI>();
        nameText.text = "Player Name";
        nameText.fontSize = 14;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Left;
        
        // Add layout element to name
        LayoutElement nameLayout = nameTextObj.AddComponent<LayoutElement>();
        nameLayout.flexibleWidth = 1;
        
        // Add ready status text
        GameObject statusTextObj = new GameObject("Ready Status");
        statusTextObj.transform.SetParent(itemObj.transform, false);
        TMP_Text statusText = statusTextObj.AddComponent<TextMeshProUGUI>();
        statusText.text = "Not Ready";
        statusText.fontSize = 14;
        statusText.color = Color.red;
        statusText.alignment = TextAlignmentOptions.Right;
        
        // Add layout element to status
        LayoutElement statusLayout = statusTextObj.AddComponent<LayoutElement>();
        statusLayout.preferredWidth = 80;
        
        return itemObj;
    }
    
    private void CreateReadyButton(Transform parentTransform)
    {
        GameObject readyButtonObj = new GameObject("Ready Button");
        readyButtonObj.transform.SetParent(parentTransform, false);
        _readyButton = readyButtonObj.AddComponent<Button>();
        Image readyButtonImage = readyButtonObj.AddComponent<Image>();
        readyButtonImage.color = new Color(0.2f, 0.7f, 0.2f, 1f);
        RectTransform readyButtonRect = readyButtonObj.GetComponent<RectTransform>();
        readyButtonRect.anchorMin = new Vector2(0.1f, 0.2f);
        readyButtonRect.anchorMax = new Vector2(0.9f, 0.25f);
        readyButtonRect.offsetMin = Vector2.zero;
        readyButtonRect.offsetMax = Vector2.zero;
        
        GameObject readyButtonText = new GameObject("Text");
        readyButtonText.transform.SetParent(readyButtonObj.transform, false);
        TMP_Text readyText = readyButtonText.AddComponent<TextMeshProUGUI>();
        readyText.text = "Ready";
        readyText.fontSize = 18;
        readyText.alignment = TextAlignmentOptions.Center;
        readyText.color = Color.white;
        RectTransform readyTextRect = readyButtonText.GetComponent<RectTransform>();
        readyTextRect.anchorMin = Vector2.zero;
        readyTextRect.anchorMax = Vector2.one;
        readyTextRect.offsetMin = Vector2.zero;
        readyTextRect.offsetMax = Vector2.zero;
    }
    
    private void CreateCountdownText(Transform parentTransform)
    {
        GameObject countdownObj = new GameObject("Countdown Text");
        countdownObj.transform.SetParent(parentTransform, false);
        _countdownText = countdownObj.AddComponent<TextMeshProUGUI>();
        _countdownText.text = "";
        _countdownText.fontSize = 24;
        _countdownText.alignment = TextAlignmentOptions.Center;
        _countdownText.color = Color.yellow;
        RectTransform countdownRect = countdownObj.GetComponent<RectTransform>();
        countdownRect.anchorMin = new Vector2(0.1f, 0.1f);
        countdownRect.anchorMax = new Vector2(0.9f, 0.2f);
        countdownRect.offsetMin = Vector2.zero;
        countdownRect.offsetMax = Vector2.zero;
    }
    
    private void SetupButtonListeners()
    {
        if (_readyButton != null)
        {
            _readyButton.onClick.RemoveAllListeners();
            _readyButton.onClick.AddListener(() => {
                OnReadyButtonClicked?.Invoke();
            });
        }
    }
    
    public void ShowPanel()
    {
        if (_lobbyPanel != null)
        {
            _lobbyPanel.SetActive(true);
        }
    }
    
    public void HidePanel()
    {
        if (_lobbyPanel != null)
        {
            _lobbyPanel.SetActive(false);
        }
    }
    
    public void UpdateRoomName(string roomName)
    {
        if (_roomNameText != null)
        {
            _roomNameText.text = $"Room: {roomName}";
        }
    }
    
    public void UpdateReadyButtonState(bool isReady)
    {
        if (_readyButton == null) return;
        
        if (isReady)
        {
            ColorBlock colors = _readyButton.colors;
            colors.normalColor = new Color(0.7f, 0.2f, 0.2f, 1f);
            _readyButton.colors = colors;
            
            TMP_Text buttonText = _readyButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = "Cancel Ready";
            }
        }
        else
        {
            ColorBlock colors = _readyButton.colors;
            colors.normalColor = new Color(0.2f, 0.7f, 0.2f, 1f);
            _readyButton.colors = colors;
            
            TMP_Text buttonText = _readyButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = "Ready";
            }
        }
    }
    
    public void UpdatePlayersList(List<string> playerNames, Dictionary<string, bool> readyStatuses)
    {
        if (_playerListContent == null || _playerListItemPrefab == null)
        {
            Debug.LogError("Player list components are not initialized");
            return;
        }
        
        // Remove any players that are no longer in the list
        List<string> playersToRemove = new List<string>();
        foreach (var entry in _playerListItems)
        {
            if (!playerNames.Contains(entry.Key))
            {
                playersToRemove.Add(entry.Key);
            }
        }
        
        foreach (var playerName in playersToRemove)
        {
            if (_playerListItems[playerName] != null)
            {
                GameObject.Destroy(_playerListItems[playerName]);
            }
            _playerListItems.Remove(playerName);
        }
        
        // Add or update player items
        foreach (var playerName in playerNames)
        {
            GameObject playerItem;
            if (_playerListItems.ContainsKey(playerName))
            {
                playerItem = _playerListItems[playerName];
                if (playerItem == null)
                {
                    // Item was destroyed, recreate it
                    playerItem = GameObject.Instantiate(_playerListItemPrefab, _playerListContent);
                    playerItem.SetActive(true);
                    _playerListItems[playerName] = playerItem;
                }
            }
            else
            {
                // Create a new player item
                playerItem = GameObject.Instantiate(_playerListItemPrefab, _playerListContent);
                playerItem.SetActive(true);
                _playerListItems.Add(playerName, playerItem);
            }
            
            // Update player item UI
            TMP_Text nameText = playerItem.transform.Find("Player Name")?.GetComponent<TMP_Text>();
            TMP_Text statusText = playerItem.transform.Find("Ready Status")?.GetComponent<TMP_Text>();
            
            if (nameText != null)
            {
                nameText.text = playerName;
            }
            
            if (statusText != null && readyStatuses.TryGetValue(playerName, out bool isReady))
            {
                statusText.text = isReady ? "Ready" : "Not Ready";
                statusText.color = isReady ? Color.green : Color.red;
            }
        }
    }
    
    public void SetCountdownText(string text)
    {
        if (_countdownText != null)
        {
            _countdownText.text = text;
        }
    }
    
    public void UpdateCountdown(float countdown)
    {
        if (_countdownText != null)
        {
            _countdownText.text = $"Game starting in: {countdown:F1}";
        }
    }
}