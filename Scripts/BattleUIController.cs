// valleyofwalls-assets/Scripts/BattleUIController.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

public class BattleUIController
{
    // UI panels
    private GameObject _battlePanel;
    private GameObject _playerMonsterPanel;
    // Monster displays
    private MonsterDisplay _playerMonsterDisplay;
    private MonsterDisplay _opponentMonsterDisplay;
    // Player reference
    private PlayerState _playerState;

    // Game objects for reference
    private GameObject _playerAvatarObj;
    private GameObject _vsTextObj;

    // Placeholder monster for initialization - MaxHealth 0 signals placeholder display
    private readonly Monster _placeholderMonster = new Monster { Name = "Waiting...", Health = 0, MaxHealth = 0, TintColor = Color.gray };


    public BattleUIController(GameObject battlePanel, GameObject playerMonsterPanel, PlayerState playerState)
    {
        _battlePanel = battlePanel;
        _playerMonsterPanel = playerMonsterPanel;
        _playerState = playerState;

        if (_battlePanel == null || _playerMonsterPanel == null || _playerState == null) {
             GameManager.Instance?.LogManager?.LogError("BattleUIController initialized with null parameters!");
             return;
        }

        SetupBattlePanel();
        SetupPlayerMonsterPanel();
        UpdatePlayerMonsterDisplay(_playerState.GetMonster());

        PlayerState.OnOpponentMonsterChanged -= HandleOpponentMonsterChanged; // Prevent duplicates
        PlayerState.OnOpponentMonsterChanged += HandleOpponentMonsterChanged;

        // Attempt initial opponent update only if data is valid *right now*
        Monster initialOpponent = _playerState.GetOpponentMonster();
        if (initialOpponent != null && _playerState.IsOpponentMonsterReady()) {
             // GameManager.Instance?.LogManager?.LogMessage("BattleUIController: Initial opponent monster valid. Updating display.");
             UpdateOpponentMonsterDisplay(initialOpponent);
        } else {
             // GameManager.Instance?.LogManager?.LogMessage("BattleUIController: Initial opponent monster not valid/ready. Setting placeholder directly.");
             if (_opponentMonsterDisplay != null) {
                 _opponentMonsterDisplay.SetMonster(_placeholderMonster); // Set placeholder explicitly
             }
        }
    }

    private void HandleOpponentMonsterChanged(Monster opponentMonster)
    {
         if (_playerState == null || !_playerState.Object.HasInputAuthority) return; // Ignore if not for local player

         // GameManager.Instance?.LogManager?.LogMessage($"BattleUIController: HandleOpponentMonsterChanged called with monster: {opponentMonster?.Name ?? "null"}.");
         UpdateOpponentMonsterDisplay(opponentMonster); // Update with null or actual monster
    }


    private void SetupBattlePanel()
    {
        _playerAvatarObj = new GameObject("PlayerAvatar"); _playerAvatarObj.transform.SetParent(_battlePanel.transform, false);
        RectTransform playerAvatarRect = _playerAvatarObj.AddComponent<RectTransform>(); playerAvatarRect.anchorMin = new Vector2(0, 0); playerAvatarRect.anchorMax = new Vector2(0.45f, 1); playerAvatarRect.offsetMin = new Vector2(20, 20); playerAvatarRect.offsetMax = new Vector2(-10, -20);
        Image playerAvatar = _playerAvatarObj.AddComponent<Image>(); playerAvatar.color = new Color(0.3f, 0.5f, 0.8f);
        GameObject playerLabelObj = new GameObject("PlayerLabel"); playerLabelObj.transform.SetParent(_playerAvatarObj.transform, false);
        TMP_Text playerLabel = playerLabelObj.AddComponent<TextMeshProUGUI>(); playerLabel.text = "YOU"; playerLabel.fontSize = 28; playerLabel.fontStyle = FontStyles.Bold; playerLabel.alignment = TextAlignmentOptions.Center; playerLabel.color = Color.white;
        RectTransform playerLabelRect = playerLabelObj.GetComponent<RectTransform>(); playerLabelRect.anchorMin = new Vector2(0, 0.7f); playerLabelRect.anchorMax = new Vector2(1, 0.9f); playerLabelRect.offsetMin = Vector2.zero; playerLabelRect.offsetMax = Vector2.zero;

        GameObject opponentMonsterObj = new GameObject("OpponentMonster"); opponentMonsterObj.transform.SetParent(_battlePanel.transform, false);
        _opponentMonsterDisplay = opponentMonsterObj.AddComponent<MonsterDisplay>();
        RectTransform opponentMonsterRect = opponentMonsterObj.GetComponent<RectTransform>(); opponentMonsterRect.anchorMin = new Vector2(0.55f, 0); opponentMonsterRect.anchorMax = new Vector2(1, 1); opponentMonsterRect.offsetMin = new Vector2(10, 20); opponentMonsterRect.offsetMax = new Vector2(-20, -20);
        if (_opponentMonsterDisplay != null) _opponentMonsterDisplay.SetIsPlayerMonster(false);

        _vsTextObj = new GameObject("VS"); _vsTextObj.transform.SetParent(_battlePanel.transform, false);
        TMP_Text vsText = _vsTextObj.AddComponent<TextMeshProUGUI>(); vsText.text = "VS"; vsText.fontSize = 36; vsText.fontStyle = FontStyles.Bold; vsText.alignment = TextAlignmentOptions.Center; vsText.color = Color.yellow;
        RectTransform vsRect = _vsTextObj.GetComponent<RectTransform>(); vsRect.anchorMin = new Vector2(0.45f, 0.4f); vsRect.anchorMax = new Vector2(0.55f, 0.6f); vsRect.offsetMin = Vector2.zero; vsRect.offsetMax = Vector2.zero;
    }

    private void SetupPlayerMonsterPanel()
    {
        GameObject titleObj = new GameObject("Title"); titleObj.transform.SetParent(_playerMonsterPanel.transform, false);
        TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>(); titleText.text = "YOUR MONSTER"; titleText.fontSize = 16; titleText.fontStyle = FontStyles.Bold; titleText.alignment = TextAlignmentOptions.Center; titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>(); titleRect.anchorMin = new Vector2(0, 0.85f); titleRect.anchorMax = new Vector2(1, 1); titleRect.offsetMin = Vector2.zero; titleRect.offsetMax = Vector2.zero;

        GameObject playerMonsterObj = new GameObject("PlayerMonsterDisplay"); playerMonsterObj.transform.SetParent(_playerMonsterPanel.transform, false);
        _playerMonsterDisplay = playerMonsterObj.AddComponent<MonsterDisplay>();
        RectTransform playerMonsterRect = playerMonsterObj.GetComponent<RectTransform>(); playerMonsterRect.anchorMin = new Vector2(0, 0); playerMonsterRect.anchorMax = new Vector2(1, 0.85f); playerMonsterRect.offsetMin = new Vector2(10, 10); playerMonsterRect.offsetMax = new Vector2(-10, -5);
        if (_playerMonsterDisplay != null) _playerMonsterDisplay.SetIsPlayerMonster(true);
    }

    public void UpdatePlayerMonsterDisplay(Monster monster) {
        if (_playerMonsterDisplay == null) return;
        if (monster != null) {
            if (_playerState != null && (string.IsNullOrEmpty(monster.Name) || monster.Name == "Your Monster" || monster.Name.StartsWith("Monster_"))) {
                 try { string pName = _playerState.PlayerName.ToString(); if (!string.IsNullOrEmpty(pName)) monster.Name = $"{pName}'s Monster"; } catch {}
            }
            _playerMonsterDisplay.SetMonster(monster);
        } else {
            string dName = "Player Monster"; try { if(_playerState != null) dName = $"{_playerState.PlayerName}'s Monster"; } catch {}
            Monster defaultM = new Monster { Name = dName, Health = 40, MaxHealth = 40, Attack = 5, Defense = 3, TintColor = new Color(0.3f, 0.6f, 0.9f) };
            _playerMonsterDisplay.SetMonster(defaultM);
        }
    }

    public void UpdateOpponentMonsterDisplay(Monster monster) {
        if (_opponentMonsterDisplay == null) return;
        if (monster != null) {
             if (string.IsNullOrEmpty(monster.Name) || monster.Name == "Your Monster" || monster.Name.StartsWith("Monster_") || monster.Name == "Waiting...") {
                 string oppName = "Opponent";
                 if (_playerState != null) {
                     PlayerRef opponentRef = _playerState.GetOpponentPlayerRef();
                     if (opponentRef != default && GameState.Instance != null) {
                         PlayerState opponentState = GameState.Instance.GetPlayerState(opponentRef);
                         if (opponentState != null) { try { string nameFromState = opponentState.PlayerName.ToString(); if (!string.IsNullOrEmpty(nameFromState)) oppName = nameFromState; } catch {} }
                     }
                 }
                 monster.Name = $"{oppName}'s Monster";
             }
            _opponentMonsterDisplay.SetMonster(monster);
        } else {
            _opponentMonsterDisplay.SetMonster(_placeholderMonster); // Use the defined placeholder
        }
    }

    public void UpdateOpponentMonsterHealth(int health) {
        Monster opponentMonster = _opponentMonsterDisplay?.GetMonster();
        if (opponentMonster != null) { _opponentMonsterDisplay.SetHealthDisplay(health, opponentMonster.MaxHealth); }
    }

    public void PlayCardEffect(CardData card, GameObject targetObj) {
        if (card == null || targetObj == null) return;
        GameObject effectObj = new GameObject("CardEffect"); Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas != null) effectObj.transform.SetParent(canvas.transform, false); else effectObj.transform.SetParent(targetObj.transform.parent, false);
        RectTransform effectRect = effectObj.AddComponent<RectTransform>(); effectRect.position = targetObj.transform.position; effectRect.sizeDelta = new Vector2(100, 100);
        Image effectImage = effectObj.AddComponent<Image>();
        switch (card.Type) { case CardType.Attack: effectImage.color = new Color(1f, 0.3f, 0.3f, 0.7f); break; case CardType.Skill: effectImage.color = new Color(0.3f, 0.7f, 1f, 0.7f); break; case CardType.Power: effectImage.color = new Color(0.8f, 0.5f, 1f, 0.7f); break; default: effectImage.color = Color.clear; break; }
        GameObject textObj = new GameObject("EffectText"); textObj.transform.SetParent(effectObj.transform, false);
        TMP_Text effectText = textObj.AddComponent<TextMeshProUGUI>(); effectText.alignment = TextAlignmentOptions.Center; effectText.fontSize = 24; effectText.fontStyle = FontStyles.Bold;
        if (card.DamageAmount > 0) { effectText.text = $"-{card.DamageAmount}"; effectText.color = Color.white; } else if (card.BlockAmount > 0) { effectText.text = $"+{card.BlockAmount}"; effectText.color = Color.cyan; } else if (card.HealAmount > 0) { effectText.text = $"+{card.HealAmount}"; effectText.color = Color.green; } else { effectText.text = ""; }
        RectTransform textRect = textObj.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = Vector2.zero; textRect.offsetMax = Vector2.zero;
        MonoBehaviour sceneMonoBehaviour = UnityEngine.Object.FindAnyObjectByType<MonoBehaviour>();
        if (sceneMonoBehaviour != null) { sceneMonoBehaviour.StartCoroutine(AnimateCardEffect(effectObj)); } else { GameObject.Destroy(effectObj, 1.0f); }
    }

    private static IEnumerator AnimateCardEffect(GameObject effectObj) {
         if (effectObj == null) yield break;
        RectTransform rectTransform = effectObj.GetComponent<RectTransform>(); Image image = effectObj.GetComponent<Image>(); TMP_Text text = effectObj.GetComponentInChildren<TMP_Text>();
        if (rectTransform == null || image == null) { GameObject.Destroy(effectObj); yield break; }
        float duration = 1.0f; float elapsed = 0f; Vector2 startSize = rectTransform.sizeDelta; Vector2 endSize = startSize * 1.5f;
        Color startColor = image.color; Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f); Color startTextColor = text != null ? text.color : Color.clear; Color endTextColor = new Color(startTextColor.r, startTextColor.g, startTextColor.b, 0f);
        while (elapsed < duration) {
            float t = elapsed / duration; rectTransform.sizeDelta = Vector2.Lerp(startSize, endSize, t); image.color = Color.Lerp(startColor, endColor, t); if (text != null) text.color = Color.Lerp(startTextColor, endTextColor, t);
            elapsed += Time.deltaTime; yield return null;
        }
        GameObject.Destroy(effectObj);
    }

    public void ResetCachedHealth() { if (_opponentMonsterDisplay != null) { UpdateOpponentMonsterDisplay(null); } }
    public void Cleanup() { PlayerState.OnOpponentMonsterChanged -= HandleOpponentMonsterChanged; }
}