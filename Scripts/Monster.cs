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
    
    public void TakeDamage(int amount)
    {
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
            
            OnBlockChanged?.Invoke(_block);
        }
        
        // Apply remaining damage
        if (damageAfterBlock > 0)
        {
            Health = Mathf.Max(0, Health - damageAfterBlock);
            OnHealthChanged?.Invoke(Health, MaxHealth);
        }
    }
    
    public void Heal(int amount)
    {
        int newHealth = Mathf.Min(MaxHealth, Health + amount);
        Health = newHealth;
        OnHealthChanged?.Invoke(Health, MaxHealth);
    }
    
    public void AddBlock(int amount)
    {
        _block += amount;
        OnBlockChanged?.Invoke(_block);
    }
    
    public bool IsDefeated()
    {
        return Health <= 0;
    }
    
    public void Reset()
    {
        Health = MaxHealth;
        _block = 0;
        OnHealthChanged?.Invoke(Health, MaxHealth);
        OnBlockChanged?.Invoke(_block);
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
}