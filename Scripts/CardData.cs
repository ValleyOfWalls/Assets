using System;
using UnityEngine;

// Card-related enums (keep if you don't already have these defined)
public enum CardType
{
    Attack,
    Skill,
    Power
}

public enum CardTarget
{
    Self,
    Enemy,
    AllEnemies,
    All
}

// Network-compatible simple card data
// This is a struct (value type) which is easier to network
[Serializable]
public struct NetworkedCardData
{
    public string Name;
    public string Description;
    public int EnergyCost;
    public int CardType; // Stored as int for networking
    public int CardTarget; // Stored as int for networking
    public int DamageAmount;
    public int BlockAmount;
    public int HealAmount;
    public int DrawAmount;
    public int EnergyGain;
    public bool Exhaust;
    public bool Ethereal;
    
    // Conversion helper
    public static NetworkedCardData FromCardData(CardData card)
    {
        return new NetworkedCardData
        {
            Name = card.Name,
            Description = card.Description,
            EnergyCost = card.EnergyCost,
            CardType = (int)card.Type,
            CardTarget = (int)card.Target,
            DamageAmount = card.DamageAmount,
            BlockAmount = card.BlockAmount,
            HealAmount = card.HealAmount,
            DrawAmount = card.DrawAmount,
            EnergyGain = card.EnergyGain,
            Exhaust = card.Exhaust,
            Ethereal = card.Ethereal
        };
    }
    
    // Convert back to CardData
    public CardData ToCardData()
    {
        return new CardData
        {
            Name = Name,
            Description = Description,
            EnergyCost = EnergyCost,
            Type = (CardType)CardType,
            Target = (CardTarget)CardTarget,
            DamageAmount = DamageAmount,
            BlockAmount = BlockAmount,
            HealAmount = HealAmount,
            DrawAmount = DrawAmount,
            EnergyGain = EnergyGain,
            Exhaust = Exhaust,
            Ethereal = Ethereal
        };
    }
}

// Full card data for gameplay - not sent directly over network
[Serializable]
public class CardData
{
    public string Name;
    public string Description;
    public int EnergyCost;
    public CardType Type;
    public CardTarget Target;
    
    // Effect values
    public int DamageAmount = 0;
    public int BlockAmount = 0;
    public int HealAmount = 0;
    public int DrawAmount = 0;
    public int EnergyGain = 0;
    
    // Special effects can be added later
    public bool Exhaust = false;
    public bool Ethereal = false;
    
    // Visual data
    public Color CardColor = Color.white;
    public string ArtworkPath = "";
}