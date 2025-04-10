using System;
using UnityEngine;

// Monster class - represents both player pets and opponents
[Serializable]
public class Monster
{
    // Basic stats
    public string Name;
    public int Health;
    public int MaxHealth;
    public int Attack;
    public int Defense;
    
    // Visual data
    public string SpritePath = "";
    public Color TintColor = Color.white;
    
    // AI behavior and cards
    private CardData[] _monsterDeck;
    
    // State
    private int _block = 0;
    
    // Events
    public event Action<int, int> OnHealthChanged;
    public event Action<int> OnBlockChanged;
    
    // NEW METHOD: Set health with event triggering
    public void SetHealth(int newHealth)
    {
        if (Health != newHealth)
        {
            Health = Mathf.Clamp(newHealth, 0, MaxHealth);
            OnHealthChanged?.Invoke(Health, MaxHealth);
            
            // Log health change
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"{Name} health set to {Health}/{MaxHealth}");
        }
    }
    
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        
        int healthBefore = Health;
        int damageAfterBlock = amount;
        
        // Apply block if available
        if (_block > 0)
        {
            if (_block >= amount)
            {
                _block -= amount;
                damageAfterBlock = 0;
            }
            else
            {
                damageAfterBlock -= _block;
                _block = 0;
            }
            
            // Notify about block change
            OnBlockChanged?.Invoke(_block);
        }
        
        // Apply remaining damage (reduced by defense)
        if (damageAfterBlock > 0)
        {
            // Defense reduces damage by a percentage
            float damageReduction = Defense / 100f;
            int reducedDamage = Mathf.Max(1, Mathf.FloorToInt(damageAfterBlock * (1f - damageReduction)));
            
            Health = Mathf.Max(0, Health - reducedDamage);
            
            // Log damage with detailed information
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"{Name} took {reducedDamage} damage (reduced from {damageAfterBlock}), health: {healthBefore} -> {Health}");
            
            // Notify with health change
            OnHealthChanged?.Invoke(Health, MaxHealth);
        }
        else {
            // Log blocked damage
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"{Name} blocked all {amount} damage with {_block} block remaining");
        }
    }
    
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        
        int healthBefore = Health;
        int newHealth = Mathf.Min(MaxHealth, Health + amount);
        int actualHeal = newHealth - Health;
        
        if (actualHeal > 0)
        {
            Health = newHealth;
            
            // Notify with health change
            OnHealthChanged?.Invoke(Health, MaxHealth);
            
            // Log healing
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"{Name} healed for {actualHeal} HP (health: {healthBefore} -> {Health})");
        }
        else
        {
            // Log when no healing occurs (already at max)
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"{Name} could not be healed (already at max health: {Health}/{MaxHealth})");
        }
    }
    
    public void AddBlock(int amount)
    {
        if (amount <= 0) return;
        
        int previousBlock = _block;
        _block += amount;
        
        // Only invoke the event if block actually changed
        if (_block != previousBlock)
        {
            OnBlockChanged?.Invoke(_block);
            
            // Log block addition
            if (GameManager.Instance != null)
                GameManager.Instance.LogManager.LogMessage($"{Name} gained {amount} block, now has {_block} block");
        }
    }
    
    public int GetBlock()
    {
        return _block;
    }
    
    public bool IsDefeated()
    {
        return Health <= 0;
    }
    
    public void Reset()
    {
        Health = MaxHealth;
        _block = 0;
        
        // Notify about resets
        OnHealthChanged?.Invoke(Health, MaxHealth);
        OnBlockChanged?.Invoke(_block);
        
        if (GameManager.Instance != null)
            GameManager.Instance.LogManager.LogMessage($"{Name} stats reset to full");
    }
    
    // Create a visual effect at the monster's position
    public void CreateDamageEffect(Vector3 position, int amount, DamageEffectType effectType)
    {
        // This method would be implemented if you want to create visual effects
        // directly from the monster class, but typically this would be handled by the UI
    }
    
    // AI can be implemented later
    public CardData ChooseAction()
    {
        // For now, return a simple attack action
        return new CardData
        {
            Name = "Monster Attack",
            Description = $"Deal {Attack} damage",
            Type = CardType.Attack,
            Target = CardTarget.Enemy,
            DamageAmount = Attack
        };
    }
    
    // Update monster stats when leveling up
    public void LevelUp(int healthBonus, int attackBonus, int defenseBonus)
    {
        MaxHealth += healthBonus;
        Health += healthBonus;
        Attack += attackBonus;
        Defense += defenseBonus;
        
        OnHealthChanged?.Invoke(Health, MaxHealth);
        
        if (GameManager.Instance != null)
            GameManager.Instance.LogManager.LogMessage($"{Name} leveled up! HP: +{healthBonus}, ATK: +{attackBonus}, DEF: +{defenseBonus}");
    }
}

// Enum for different damage effect types
public enum DamageEffectType
{
    Damage,
    Heal,
    Block
}