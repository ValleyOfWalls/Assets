using System;
using Fusion;
using UnityEngine;

// Network-compatible simple card data
// This struct implements INetworkStruct to be compatible with Fusion networking
[Serializable]
public struct NetworkedCardData : INetworkStruct
{
    public NetworkString<_64> Name;       // Using NetworkString instead of string
    public NetworkString<_128> Description; // Using NetworkString for description
    public int EnergyCost;
    public int CardType;      // Stored as int for networking
    public int CardTarget;    // Stored as int for networking
    public int DamageAmount;
    public int BlockAmount;
    public int HealAmount;
    public int DrawAmount;
    public int EnergyGain;
    public NetworkBool Exhaust;   // Using NetworkBool instead of bool
    public NetworkBool Ethereal;  // Using NetworkBool instead of bool
    
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
            Name = Name.ToString(),
            Description = Description.ToString(),
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