using System;
using UnityEngine;

// Class to store player data for rejoining
[Serializable]
public class PlayerData
{
    public Vector3 Position = Vector3.zero;
    public Color PlayerColor = Color.white;
    
    // Add game-specific player data fields
    public int Health = 50;
    public int MaxHealth = 50;
    public int Energy = 3;
    public int MaxEnergy = 3;
    public int Score = 0;
    
    // Monster data
    public string MonsterName = "";
    public int MonsterHealth = 40;
    public int MonsterMaxHealth = 40;
    public int MonsterAttack = 5;
    public int MonsterDefense = 3;
    public Color MonsterColor = Color.white;
}