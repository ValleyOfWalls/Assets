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
        if (Health != clampedHealth)
        {
            int oldHealth = Health;
            Health = clampedHealth;
            OnHealthChanged?.Invoke(Health, MaxHealth); // Trigger event for UI updates

            // Log health change
            // GameManager.Instance?.LogManager?.LogMessage($"{Name} health set to {Health}/{MaxHealth}");
        }
    }

    // Applies damage, considering block and defense
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        int healthBefore = Health;
        int damageAfterBlock = amount;

        // Apply block if available
        if (_block > 0)
        {
            int blockedAmount = Mathf.Min(_block, amount);
            _block -= blockedAmount;
            damageAfterBlock -= blockedAmount;
            OnBlockChanged?.Invoke(_block); // Notify block change
            // GameManager.Instance?.LogManager?.LogMessage($"{Name} blocked {blockedAmount} damage. {_block} block remaining.");
        }

        // Apply remaining damage (reduced by defense)
        if (damageAfterBlock > 0)
        {
            // Simple defense: reduce damage by a flat amount or percentage (example: flat reduction)
            // int reducedDamage = Mathf.Max(1, damageAfterBlock - Defense); // Example: Flat reduction
            // Example: Percentage reduction
             float damageReduction = Mathf.Clamp01(Defense / 100f); // Assuming Defense is like a percentage
             int reducedDamage = Mathf.Max(1, Mathf.FloorToInt(damageAfterBlock * (1f - damageReduction)));


            int finalDamage = Mathf.Max(1, reducedDamage); // Ensure at least 1 damage goes through if not fully blocked

            int newHealth = Mathf.Max(0, Health - finalDamage);
            if (Health != newHealth)
            {
                 SetHealth(newHealth); // Use SetHealth to trigger event
                // GameManager.Instance?.LogManager?.LogMessage($"{Name} took {finalDamage} damage (reduced from {damageAfterBlock}), health: {healthBefore} -> {Health}");
            }
        }
        else if (amount > 0) // Log only if initial damage was > 0 and was fully blocked
        {
            // GameManager.Instance?.LogManager?.LogMessage($"{Name} blocked all {amount} damage.");
        }
    }

    // Heals the monster, clamping at MaxHealth
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        int healthBefore = Health;
        int newHealth = Mathf.Min(MaxHealth, Health + amount);
        int actualHeal = newHealth - Health;

        if (actualHeal > 0)
        {
             SetHealth(newHealth); // Use SetHealth to trigger event
            // GameManager.Instance?.LogManager?.LogMessage($"{Name} healed for {actualHeal} HP (health: {healthBefore} -> {Health})");
        }
        // else { GameManager.Instance?.LogManager?.LogMessage($"{Name} could not be healed (already at max health: {Health}/{MaxHealth})"); }

    }

    // Adds block to the monster
    public void AddBlock(int amount)
    {
        if (amount <= 0) return;
        int previousBlock = _block;
        _block += amount;

        if (_block != previousBlock)
        {
            OnBlockChanged?.Invoke(_block); // Trigger event for UI
            // GameManager.Instance?.LogManager?.LogMessage($"{Name} gained {amount} block, now has {_block} block");
        }
    }

    // Gets the current block amount
    public int GetBlock()
    {
        return _block;
    }

     // *** ADDED METHOD ***
     // Resets block locally without triggering events (useful for turn/round end)
     public void ResetBlockLocally()
     {
         if (_block != 0)
         {
            _block = 0;
             // Optionally trigger event if UI needs to know block *reset*
             OnBlockChanged?.Invoke(_block);
             // GameManager.Instance?.LogManager?.LogMessage($"{Name} block reset locally.");
         }
     }


    // Checks if the monster's health is at or below zero
    public bool IsDefeated()
    {
        return Health <= 0;
    }

    // Resets health to max and block to zero (Typically for Networked Reset via PlayerState)
    // Use ResetPlayerMonsterLocally in MonsterManager for purely local resets.
    public void ResetStatsForNetwork()
    {
         bool changed = false;
         if(Health != MaxHealth)
         {
              Health = MaxHealth;
              OnHealthChanged?.Invoke(Health, MaxHealth);
              changed = true;
         }
         if(_block != 0)
         {
              _block = 0;
              OnBlockChanged?.Invoke(_block);
              changed = true;
         }

        //if (changed && GameManager.Instance != null)
            //GameManager.Instance.LogManager.LogMessage($"{Name} network stats reset requested.");
    }

    // Basic AI action selection (Placeholder)
    public CardData ChooseAction()
    {
        // For now, return a simple attack action based on current Attack stat
        return new CardData
        {
            Name = "Monster Attack",
            Description = $"Deal {Attack} damage",
            Type = CardType.Attack,
            Target = CardTarget.Enemy, // Assume target is always the opponent monster
            DamageAmount = Attack
        };
    }

    // Updates monster stats (e.g., from upgrades)
    public void ApplyStatChanges(int healthBonus, int maxHealthBonus, int attackBonus, int defenseBonus)
    {
         bool statsChanged = false;
         if (maxHealthBonus != 0) { MaxHealth += maxHealthBonus; statsChanged = true; }
         if (healthBonus != 0) { Health += healthBonus; statsChanged = true; } // Heal/Damage by bonus amount
         if (attackBonus != 0) { Attack += attackBonus; statsChanged = true; }
         if (defenseBonus != 0) { Defense += defenseBonus; statsChanged = true; }

         // Clamp health after applying bonuses
         Health = Mathf.Clamp(Health, 0, MaxHealth);

         if (statsChanged)
         {
             OnHealthChanged?.Invoke(Health, MaxHealth); // Trigger health update event
             // Log stat changes
             // GameManager.Instance?.LogManager?.LogMessage($"{Name} stats updated! HP: +{healthBonus}/{maxHealthBonus}, ATK: +{attackBonus}, DEF: +{defenseBonus}. Current: {Health}/{MaxHealth} HP, {Attack} ATK, {Defense} DEF");
         }
    }
}