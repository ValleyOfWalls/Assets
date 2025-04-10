using Fusion;
using UnityEngine;

/// <summary>
/// Manages monster creation and interaction
/// </summary>
public class MonsterManager
{
    // Player references
    private PlayerRef _playerRef;
    private PlayerRef _opponentPlayerRef;
    private PlayerState _opponentPlayerState;
    
    // Monsters
    private Monster _playerMonster;
    private Monster _opponentMonster;
    
    // Networked sync
    private PlayerState _ownerPlayerState; // Reference to owner PlayerState
    
    public MonsterManager(NetworkBehaviour networkBehaviour, PlayerRef playerRef)
    {
        _ownerPlayerState = networkBehaviour as PlayerState;
        _playerRef = playerRef;
        
        // Create player's monster
        CreatePlayerMonster();
    }
    
    /// <summary>
    /// Creates the default monster for the player
    /// </summary>
    private void CreatePlayerMonster()
    {
        // Create random color for monster
        Color monsterColor = new Color(
            UnityEngine.Random.Range(0.5f, 1f),
            UnityEngine.Random.Range(0.5f, 1f),
            UnityEngine.Random.Range(0.5f, 1f)
        );
        
        _playerMonster = new Monster
        {
            Name = "Your Monster",
            Health = 40,
            MaxHealth = 40,
            Attack = 5,
            Defense = 3,
            TintColor = monsterColor
        };
        
        GameManager.Instance.LogManager.LogMessage($"Player monster created: {_playerMonster.Name}");
    }
    
    /// <summary>
    /// Updates networked monster properties from local monster
    /// </summary>
    public void UpdateNetworkedMonsterProperties(
        ref NetworkString<_32> monsterName,
        ref int monsterHealth,
        ref int monsterMaxHealth,
        ref int monsterAttack,
        ref int monsterDefense,
        ref Color monsterColor)
    {
        if (_playerMonster == null) return;
        
        // We need to use the string value to set the NetworkString
        monsterName = _playerMonster.Name;
        monsterHealth = _playerMonster.Health;
        monsterMaxHealth = _playerMonster.MaxHealth;
        monsterAttack = _playerMonster.Attack;
        monsterDefense = _playerMonster.Defense;
        monsterColor = _playerMonster.TintColor;
    }
    
    /// <summary>
    /// Updates local monster from networked properties
    /// </summary>
    public void UpdateLocalMonsterFromNetworked(
        NetworkString<_32> monsterName,
        int monsterHealth,
        int monsterMaxHealth,
        int monsterAttack,
        int monsterDefense,
        Color monsterColor)
    {
        if (_playerMonster == null)
        {
            _playerMonster = new Monster();
        }
        
        _playerMonster.Name = monsterName.ToString();
        _playerMonster.Health = monsterHealth;
        _playerMonster.MaxHealth = monsterMaxHealth;
        _playerMonster.Attack = monsterAttack;
        _playerMonster.Defense = monsterDefense;
        _playerMonster.TintColor = monsterColor;
    }
    
    /// <summary>
    /// Sets the opponent monster reference
    /// </summary>
    public void SetOpponentMonster(PlayerRef opponentRef, PlayerState opponentState)
    {
        _opponentPlayerRef = opponentRef;
        _opponentPlayerState = opponentState;
        
        // Create a local monster instance based on opponent's monster data
        if (_opponentPlayerState != null)
        {
            _opponentMonster = new Monster
            {
                Name = opponentState.MonsterName.ToString(),
                Health = opponentState.MonsterHealth,
                MaxHealth = opponentState.MonsterMaxHealth,
                Attack = opponentState.MonsterAttack,
                Defense = opponentState.MonsterDefense,
                TintColor = opponentState.MonsterColor
            };
            
            GameManager.Instance.LogManager.LogMessage($"Opponent monster set: {_opponentMonster.Name}");
        }
    }
    
    /// <summary>
    /// Updates opponent monster if opponent state changed
    /// </summary>
    public void UpdateOpponentMonster()
    {
        if (_opponentPlayerState == null) return;
        
        bool hasChanged = false;
        
        if (_opponentMonster == null)
        {
            _opponentMonster = new Monster();
            hasChanged = true;
        }
        
        // Update monster properties if changed
        if (_opponentMonster.Name != _opponentPlayerState.MonsterName.ToString())
        {
            _opponentMonster.Name = _opponentPlayerState.MonsterName.ToString();
            hasChanged = true;
        }
        
        if (_opponentMonster.Health != _opponentPlayerState.MonsterHealth)
        {
            _opponentMonster.Health = _opponentPlayerState.MonsterHealth;
            hasChanged = true;
        }
        
        if (_opponentMonster.MaxHealth != _opponentPlayerState.MonsterMaxHealth)
        {
            _opponentMonster.MaxHealth = _opponentPlayerState.MonsterMaxHealth;
            hasChanged = true;
        }
        
        if (_opponentMonster.Attack != _opponentPlayerState.MonsterAttack)
        {
            _opponentMonster.Attack = _opponentPlayerState.MonsterAttack;
            hasChanged = true;
        }
        
        if (_opponentMonster.Defense != _opponentPlayerState.MonsterDefense)
        {
            _opponentMonster.Defense = _opponentPlayerState.MonsterDefense;
            hasChanged = true;
        }
        
        if (!_opponentMonster.TintColor.Equals(_opponentPlayerState.MonsterColor))
        {
            _opponentMonster.TintColor = _opponentPlayerState.MonsterColor;
            hasChanged = true;
        }
        
        if (hasChanged)
        {
            GameManager.Instance.LogManager.LogMessage($"Updated opponent monster: {_opponentMonster.Name}");
        }
    }
    
    /// <summary>
    /// Applies damage to the player's monster and updates networked state
    /// </summary>
    public void ApplyDamageToPlayerMonster(int amount, ref int monsterHealth)
    {
        if (_playerMonster == null) return;
        
        int healthBefore = _playerMonster.Health;
        _playerMonster.TakeDamage(amount);
        
        // Update networked value
        monsterHealth = _playerMonster.Health;
        
        int actualDamage = healthBefore - _playerMonster.Health;
        GameManager.Instance.LogManager.LogMessage($"Applied {actualDamage} damage to player monster (Health: {_playerMonster.Health}/{_playerMonster.MaxHealth})");
        
        // Check for monster defeat
        if (_playerMonster.IsDefeated())
        {
            GameManager.Instance.LogManager.LogMessage("Player monster defeated!");
            
            // Grant score to opponent - using owner's RPC
            if (_ownerPlayerState != null && _ownerPlayerState.HasStateAuthority && _opponentPlayerRef != default)
            {
                _ownerPlayerState.RPC_GrantScoreToAttacker(_opponentPlayerRef);
            }
        }
    }
    
    /// <summary>
    /// Resets the player's monster for a new round
    /// </summary>
    public void ResetPlayerMonster()
    {
        if (_playerMonster != null)
        {
            _playerMonster.Reset();
        }
    }
    
    /// <summary>
    /// Gets the player's monster
    /// </summary>
    public Monster GetPlayerMonster()
    {
        return _playerMonster;
    }
    
    /// <summary>
    /// Gets the opponent's monster
    /// </summary>
    public Monster GetOpponentMonster()
    {
        return _opponentMonster;
    }
}