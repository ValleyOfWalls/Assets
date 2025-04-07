using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // UI references
    private Canvas _uiCanvas;
    private TMP_InputField _roomNameInput;
    private Button _createRoomButton;
    private Button _joinRoomButton;
    private TMP_Text _statusText;
    private GameObject _connectPanel;
    
    public void Initialize()
    {
        GameManager.Instance.LogManager.LogMessage("Initializing UIManager...");
        
        // Create UI
        CreateUI();
        SetupUIListeners();
    }
    
    private void CreateUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("UI Canvas");
        _uiCanvas = canvasObj.AddComponent<Canvas>();
        _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        // Create a panel for connection UI
        GameObject panelObj = new GameObject("Connect Panel");
        panelObj.transform.SetParent(_uiCanvas.transform, false);
        _connectPanel = panelObj;
        
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.3f, 0.3f);
        panelRect.anchorMax = new Vector2(0.7f, 0.7f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Add title text
        GameObject titleObj = new GameObject("Title Text");
        titleObj.transform.SetParent(panelObj.transform, false);
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

        // Add room name input
        GameObject inputObj = new GameObject("Room Name Input");
        inputObj.transform.SetParent(panelObj.transform, false);
        _roomNameInput = inputObj.AddComponent<TMP_InputField>();
        Image inputImage = inputObj.AddComponent<Image>();
        inputImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.1f, 0.6f);
        inputRect.anchorMax = new Vector2(0.9f, 0.75f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;
        
        // Create text area for input field
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
        
        // Create placeholder for input field
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
        
        // Connect the input field components
        _roomNameInput.textComponent = inputText;
        _roomNameInput.placeholder = placeholderText;
        _roomNameInput.text = "Room" + UnityEngine.Random.Range(1000, 10000);

        // Add create room button
        GameObject createButtonObj = new GameObject("Create Room Button");
        createButtonObj.transform.SetParent(panelObj.transform, false);
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

        // Add join room button
        GameObject joinButtonObj = new GameObject("Join Room Button");
        joinButtonObj.transform.SetParent(panelObj.transform, false);
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

        // Add status text
        GameObject statusObj = new GameObject("Status Text");
        statusObj.transform.SetParent(panelObj.transform, false);
        _statusText = statusObj.AddComponent<TextMeshProUGUI>();
        _statusText.text = "Enter a room name and create or join a room";
        _statusText.fontSize = 16;
        _statusText.alignment = TextAlignmentOptions.Center;
        _statusText.color = Color.white;
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.1f, 0.2f);
        statusRect.anchorMax = new Vector2(0.9f, 0.35f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;
        
        GameManager.Instance.LogManager.LogMessage("UI created successfully");
    }

    private void SetupUIListeners()
    {
        if (_createRoomButton != null)
        {
            _createRoomButton.onClick.RemoveAllListeners();
            _createRoomButton.onClick.AddListener(() => {
                GameManager.Instance.NetworkManager.CreateRoom(_roomNameInput.text);
            });
        }

        if (_joinRoomButton != null)
        {
            _joinRoomButton.onClick.RemoveAllListeners();
            _joinRoomButton.onClick.AddListener(() => {
                GameManager.Instance.NetworkManager.JoinRoom(_roomNameInput.text);
            });
        }
        
        GameManager.Instance.LogManager.LogMessage("UI listeners set up");
    }

    // Implement common UI actions
    public void ShowConnectUI()
    {
        if (_connectPanel != null)
            _connectPanel.SetActive(true);
    }

    public void HideConnectUI()
    {
        if (_connectPanel != null)
            _connectPanel.SetActive(false);
    }
    
    public void UpdateStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
        }
    }
}