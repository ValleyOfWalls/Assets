using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConnectUIController
{
    // UI elements
    private GameObject _connectPanel;
    private TMP_InputField _roomNameInput;
    private TMP_InputField _playerNameInput;
    private Button _createRoomButton;
    private Button _joinRoomButton;
    private TMP_Text _statusText;
    
    // Events
    public event Action<string, string> OnCreateRoomClicked;
    public event Action<string, string> OnJoinRoomClicked;
    
    public ConnectUIController(Transform canvasTransform)
    {
        CreateConnectPanel(canvasTransform);
    }
    
    private void CreateConnectPanel(Transform parentTransform)
    {
        GameObject panelObj = new GameObject("Connect Panel");
        panelObj.transform.SetParent(parentTransform, false);
        _connectPanel = panelObj;
        
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.3f, 0.3f);
        panelRect.anchorMax = new Vector2(0.7f, 0.7f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Add title text
        CreateTitleText(panelObj.transform);
        
        // Add player name input
        CreatePlayerNameInput(panelObj.transform);
        
        // Add room name input
        CreateRoomNameInput(panelObj.transform);
        
        // Add create room button
        CreateRoomButton(panelObj.transform);
        
        // Add join room button
        CreateJoinButton(panelObj.transform);
        
        // Add status text
        CreateStatusText(panelObj.transform);
        
        // Set up button listeners
        SetupButtonListeners();
    }
    
    private void CreateTitleText(Transform parentTransform)
    {
        GameObject titleObj = new GameObject("Title Text");
        titleObj.transform.SetParent(parentTransform, false);
        TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Photon Fusion Lobby";
        titleText.fontSize = 24;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.8f);
        titleRect.anchorMax = new Vector2(0.9f, 0.95f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
    }
    
    private void CreatePlayerNameInput(Transform parentTransform)
    {
        GameObject nameInputObj = new GameObject("Player Name Input");
        nameInputObj.transform.SetParent(parentTransform, false);
        _playerNameInput = nameInputObj.AddComponent<TMP_InputField>();
        Image nameInputImage = nameInputObj.AddComponent<Image>();
        nameInputImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform nameInputRect = nameInputObj.GetComponent<RectTransform>();
        nameInputRect.anchorMin = new Vector2(0.1f, 0.7f);
        nameInputRect.anchorMax = new Vector2(0.9f, 0.85f);
        nameInputRect.offsetMin = Vector2.zero;
        nameInputRect.offsetMax = Vector2.zero;
        
        // Create text area for player name input field
        GameObject nameTextArea = new GameObject("Text Area");
        nameTextArea.transform.SetParent(nameInputObj.transform, false);
        TMP_Text nameInputText = nameTextArea.AddComponent<TextMeshProUGUI>();
        nameInputText.text = "";
        nameInputText.color = Color.white;
        nameInputText.fontSize = 18;
        nameInputText.alignment = TextAlignmentOptions.Left;
        RectTransform nameTextRect = nameTextArea.GetComponent<RectTransform>();
        nameTextRect.anchorMin = new Vector2(0.05f, 0.1f);
        nameTextRect.anchorMax = new Vector2(0.95f, 0.9f);
        nameTextRect.offsetMin = Vector2.zero;
        nameTextRect.offsetMax = Vector2.zero;
        
        // Create placeholder for player name input field
        GameObject namePlaceholder = new GameObject("Placeholder");
        namePlaceholder.transform.SetParent(nameInputObj.transform, false);
        TMP_Text namePlaceholderText = namePlaceholder.AddComponent<TextMeshProUGUI>();
        namePlaceholderText.text = "Enter Your Name";
        namePlaceholderText.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        namePlaceholderText.fontSize = 18;
        namePlaceholderText.alignment = TextAlignmentOptions.Left;
        RectTransform namePlaceholderRect = namePlaceholder.GetComponent<RectTransform>();
        namePlaceholderRect.anchorMin = new Vector2(0.05f, 0.1f);
        namePlaceholderRect.anchorMax = new Vector2(0.95f, 0.9f);
        namePlaceholderRect.offsetMin = Vector2.zero;
        namePlaceholderRect.offsetMax = Vector2.zero;
        
        // Connect the player name input field components
        _playerNameInput.textComponent = nameInputText;
        _playerNameInput.placeholder = namePlaceholderText;
        _playerNameInput.text = "Player" + UnityEngine.Random.Range(1000, 10000);
    }
    
    private void CreateRoomNameInput(Transform parentTransform)
    {
        GameObject inputObj = new GameObject("Room Name Input");
        inputObj.transform.SetParent(parentTransform, false);
        _roomNameInput = inputObj.AddComponent<TMP_InputField>();
        Image inputImage = inputObj.AddComponent<Image>();
        inputImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.1f, 0.55f);
        inputRect.anchorMax = new Vector2(0.9f, 0.7f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;
        
        // Create text area for room input field
        GameObject textArea = new GameObject("Text Area");
        textArea.transform.SetParent(inputObj.transform, false);
        TMP_Text inputText = textArea.AddComponent<TextMeshProUGUI>();
        inputText.text = "";
        inputText.color = Color.white;
        inputText.fontSize = 18;
        inputText.alignment = TextAlignmentOptions.Left;
        RectTransform textRect = textArea.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0.1f);
        textRect.anchorMax = new Vector2(0.95f, 0.9f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Create placeholder for room input field
        GameObject placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(inputObj.transform, false);
        TMP_Text placeholderText = placeholder.AddComponent<TextMeshProUGUI>();
        placeholderText.text = "Enter Room Name";
        placeholderText.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        placeholderText.fontSize = 18;
        placeholderText.alignment = TextAlignmentOptions.Left;
        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = new Vector2(0.05f, 0.1f);
        placeholderRect.anchorMax = new Vector2(0.95f, 0.9f);
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        
        // Connect the room input field components
        _roomNameInput.textComponent = inputText;
        _roomNameInput.placeholder = placeholderText;
        _roomNameInput.text = "asd"; // Default to "asd"
    }
    
    private void CreateRoomButton(Transform parentTransform)
    {
        GameObject createButtonObj = new GameObject("Create Room Button");
        createButtonObj.transform.SetParent(parentTransform, false);
        _createRoomButton = createButtonObj.AddComponent<Button>();
        Image createButtonImage = createButtonObj.AddComponent<Image>();
        createButtonImage.color = new Color(0.2f, 0.7f, 0.2f, 1f);
        RectTransform createButtonRect = createButtonObj.GetComponent<RectTransform>();
        createButtonRect.anchorMin = new Vector2(0.1f, 0.4f);
        createButtonRect.anchorMax = new Vector2(0.45f, 0.55f);
        createButtonRect.offsetMin = Vector2.zero;
        createButtonRect.offsetMax = Vector2.zero;
        
        GameObject createButtonText = new GameObject("Text");
        createButtonText.transform.SetParent(createButtonObj.transform, false);
        TMP_Text createText = createButtonText.AddComponent<TextMeshProUGUI>();
        createText.text = "Create Room";
        createText.fontSize = 18;
        createText.alignment = TextAlignmentOptions.Center;
        createText.color = Color.white;
        RectTransform createTextRect = createButtonText.GetComponent<RectTransform>();
        createTextRect.anchorMin = Vector2.zero;
        createTextRect.anchorMax = Vector2.one;
        createTextRect.offsetMin = Vector2.zero;
        createTextRect.offsetMax = Vector2.zero;
    }
    
    private void CreateJoinButton(Transform parentTransform)
    {
        GameObject joinButtonObj = new GameObject("Join Room Button");
        joinButtonObj.transform.SetParent(parentTransform, false);
        _joinRoomButton = joinButtonObj.AddComponent<Button>();
        Image joinButtonImage = joinButtonObj.AddComponent<Image>();
        joinButtonImage.color = new Color(0.2f, 0.2f, 0.7f, 1f);
        RectTransform joinButtonRect = joinButtonObj.GetComponent<RectTransform>();
        joinButtonRect.anchorMin = new Vector2(0.55f, 0.4f);
        joinButtonRect.anchorMax = new Vector2(0.9f, 0.55f);
        joinButtonRect.offsetMin = Vector2.zero;
        joinButtonRect.offsetMax = Vector2.zero;
        
        GameObject joinButtonText = new GameObject("Text");
        joinButtonText.transform.SetParent(joinButtonObj.transform, false);
        TMP_Text joinText = joinButtonText.AddComponent<TextMeshProUGUI>();
        joinText.text = "Join Room";
        joinText.fontSize = 18;
        joinText.alignment = TextAlignmentOptions.Center;
        joinText.color = Color.white;
        RectTransform joinTextRect = joinButtonText.GetComponent<RectTransform>();
        joinTextRect.anchorMin = Vector2.zero;
        joinTextRect.anchorMax = Vector2.one;
        joinTextRect.offsetMin = Vector2.zero;
        joinTextRect.offsetMax = Vector2.zero;
    }
    
    private void CreateStatusText(Transform parentTransform)
    {
        GameObject statusObj = new GameObject("Status Text");
        statusObj.transform.SetParent(parentTransform, false);
        _statusText = statusObj.AddComponent<TextMeshProUGUI>();
        _statusText.text = "Enter your name and a room name to create or join";
        _statusText.fontSize = 16;
        _statusText.alignment = TextAlignmentOptions.Center;
        _statusText.color = Color.white;
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.1f, 0.2f);
        statusRect.anchorMax = new Vector2(0.9f, 0.35f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;
    }
    
    private void SetupButtonListeners()
    {
        if (_createRoomButton != null)
        {
            _createRoomButton.onClick.RemoveAllListeners();
            _createRoomButton.onClick.AddListener(() => {
                string roomName = _roomNameInput.text;
                string playerName = _playerNameInput.text;
                OnCreateRoomClicked?.Invoke(roomName, playerName);
            });
        }

        if (_joinRoomButton != null)
        {
            _joinRoomButton.onClick.RemoveAllListeners();
            _joinRoomButton.onClick.AddListener(() => {
                string roomName = _roomNameInput.text;
                string playerName = _playerNameInput.text;
                OnJoinRoomClicked?.Invoke(roomName, playerName);
            });
        }
    }
    
    public void ShowPanel()
    {
        if (_connectPanel != null)
        {
            _connectPanel.SetActive(true);
        }
    }
    
    public void HidePanel()
    {
        if (_connectPanel != null)
        {
            _connectPanel.SetActive(false);
        }
    }
    
    public void UpdateStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
        }
    }
}