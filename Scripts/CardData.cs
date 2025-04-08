using System;
using UnityEngine;

// Card-related enums
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