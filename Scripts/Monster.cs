// valleyofwalls-assets/Scripts/Monster.cs
using System;
using UnityEngine;

// Enum for different damage effect types
public enum DamageEffectType
{
    Damage,
    Heal,
    Block
}

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
    // AI behavior and cards (Placeholder)
    // private CardData[] _monsterDeck;
    // State (Local representation)
    private int _block = 0;
    // Events (Local representation)
    public event Action<int, int> OnHealthChanged;
    public event Action<int> OnBlockChanged;

    // Set health with event triggering (Clamps value)
    public void SetHealth(int newHealth)
    {
        int clampedHealth = Mathf.Clamp(newHealth, 0, MaxHealth);
        if (Health != clampedHealth) {
            Health = clampedHealth;
            OnHealthChanged?.Invoke(Health, MaxHealth); // Trigger event for UI updates
            // GameManager.Instance?.LogManager?.LogMessage($"{Name} health set to {Health}/{MaxHealth}");
        }
    }

    // Applies damage, considering block and defense
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        int damageAfterBlock = amount;

        // Apply block if available
        if (_block > 0) {
            int blockedAmount = Mathf.Min(_block, amount);
            _block -= blockedAmount;
            damageAfterBlock -= blockedAmount;
            OnBlockChanged?.Invoke(_block); // Notify block change
            // GameManager.Instance?.LogManager?.LogMessage($"{Name} blocked {blockedAmount} damage. {_block} block remaining.");
        }

        // Apply remaining damage (reduced by defense)
        if (damageAfterBlock > 0) {
             float damageReduction = Mathf.Clamp01(Defense / 100f); // Assuming Defense is like a percentage
             int reducedDamage = Mathf.Max(1, Mathf.FloorToInt(damageAfterBlock * (1f - damageReduction)));
            int finalDamage = Mathf.Max(1, reducedDamage); // Ensure at least 1 damage goes through if not fully blocked
            int newHealth = Mathf.Max(0, Health - finalDamage);
            if (Health != newHealth) {
                 SetHealth(newHealth); // Use SetHealth to trigger event
                 // GameManager.Instance?.LogManager?.LogMessage($"{Name} took {finalDamage} damage (reduced from {damageAfterBlock}), health now {Health}");
            }
        }
        // else if (amount > 0) { GameManager.Instance?.LogManager?.LogMessage($"{Name} blocked all {amount} damage."); }
    }

    // Heals the monster, clamping at MaxHealth
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        int healthBefore = Health;
        int newHealth = Mathf.Min(MaxHealth, Health + amount);
        int actualHeal = newHealth - Health;
        if (actualHeal > 0) {
             SetHealth(newHealth); // Use SetHealth to trigger event
             // GameManager.Instance?.LogManager?.LogMessage($"{Name} healed for {actualHeal} HP, health now {Health})");
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"{Name} could not be healed (already at max health: {Health}/{MaxHealth})"); }
    }

    // Adds block to the monster
    public void AddBlock(int amount)
    {
        if (amount <= 0) return;
        int previousBlock = _block;
        _block += amount;
        if (_block != previousBlock) {
            OnBlockChanged?.Invoke(_block); // Trigger event for UI
            // GameManager.Instance?.LogManager?.LogMessage($"{Name} gained {amount} block, now has {_block} block");
        }
    }

    // Gets the current block amount
    public int GetBlock()
    {
        return _block;
    }

     // Resets block locally without triggering events (useful for turn/round end)
     public void ResetBlockLocally()
     {
         if (_block != 0) {
            _block = 0;
             OnBlockChanged?.Invoke(_block); // Optionally trigger event if UI needs to know block *reset*
             // GameManager.Instance?.LogManager?.LogMessage($"{Name} block reset locally.");
         }
     }

    // Checks if the monster's health is at or below zero
    public bool IsDefeated()
    {
        return Health <= 0;
    }

    // Resets health to max and block to zero (Used by MonsterManager/PlayerState)
    public void ResetStatsForNetwork()
    {
         bool healthChanged = (Health != MaxHealth);
         bool blockChanged = (_block != 0);

         if(healthChanged) {
              Health = MaxHealth;
              OnHealthChanged?.Invoke(Health, MaxHealth);
         }
         if(blockChanged) {
              _block = 0;
              OnBlockChanged?.Invoke(_block);
         }
        //if (healthChanged || blockChanged) GameManager.Instance?.LogManager?.LogMessage($"{Name} stats reset requested.");
    }

    // Basic AI action selection (Placeholder)
    public CardData ChooseAction()
    {
        return new CardData {
            Name = "Monster Attack", Description = $"Deal {Attack} damage",
            Type = CardType.Attack, Target = CardTarget.Enemy, DamageAmount = Attack
        };
    }

    // Updates monster stats (e.g., from upgrades)
    public void ApplyStatChanges(int healthBonus, int maxHealthBonus, int attackBonus, int defenseBonus)
    {
         // ** REMOVED 'changed' variable **
         if (maxHealthBonus != 0) MaxHealth += maxHealthBonus;
         if (healthBonus != 0) Health += healthBonus; // Heal/Damage by bonus amount
         if (attackBonus != 0) Attack += attackBonus;
         if (defenseBonus != 0) Defense += defenseBonus;

         // Clamp health after applying bonuses and trigger health update event if needed
         int previousHealth = Health;
         Health = Mathf.Clamp(Health, 0, MaxHealth);

         // Trigger health update if health or max health changed
         if (previousHealth != Health || maxHealthBonus != 0) {
             OnHealthChanged?.Invoke(Health, MaxHealth);
             // GameManager.Instance?.LogManager?.LogMessage($"{Name} stats updated! HP: {Health}/{MaxHealth}, ATK: {Attack}, DEF: {Defense}");
         }
    }
}