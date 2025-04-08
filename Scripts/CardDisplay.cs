using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Display a single card in the hand
public class CardDisplay : MonoBehaviour
{
    // References
    private TMP_Text _titleText;
    private TMP_Text _costText;
    private TMP_Text _descriptionText;
    private Button _button;
    
    // Data
    private CardData _cardData;
    private int _cardIndex;
    
    // Events
    public event Action<CardDisplay> CardClicked;
    
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
        CardClicked?.Invoke(this);
    }
    
    public CardData GetCardData()
    {
        return _cardData;
    }
    
    public int GetCardIndex()
    {
        return _cardIndex;
    }
}

// Display opponent stats in the opponents panel
