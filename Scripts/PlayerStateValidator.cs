using Fusion;
using UnityEngine;

/// <summary>
/// Helper class to validate inputs and operations for PlayerState
/// </summary>
public class PlayerStateValidator
{
    /// <summary>
    /// Verifies if the player can play a card based on current game state and energy
    /// </summary>
    public static bool CanPlayCard(PlayerState playerState, CardData card)
    {
        if (playerState == null || card == null)
        {
            return false;
        }
        
        // Check if it's the player's turn
        if (GameState.Instance == null || !GameState.Instance.IsLocalPlayerTurn())
        {
            return false;
        }
        
        // Check if player has enough energy
        if (playerState.Energy < card.EnergyCost)
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Validates if a monster can be targeted by a specific card
    /// </summary>
    public static bool IsValidMonsterTarget(CardData card, Monster monster, bool isPlayerMonster)
    {
        if (card == null || monster == null)
        {
            return false;
        }
        
        switch (card.Target)
        {
            case CardTarget.Enemy:
            case CardTarget.AllEnemies:
                return !isPlayerMonster; // Target opponent monster
                
            case CardTarget.Self:
                return isPlayerMonster; // Target player's own monster
                
            case CardTarget.All:
                return true; // Can target any monster
                
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Determines if a player has authority to modify monster health
    /// </summary>
    public static bool CanModifyMonsterHealth(NetworkObject networkObject, Monster monster)
    {
        // Check if network object exists
        if (networkObject == null)
        {
            return false;
        }
        
        // Can modify if has state authority
        if (networkObject.HasStateAuthority)
        {
            return true;
        }
        
        // Can't modify without state authority
        return false;
    }
}