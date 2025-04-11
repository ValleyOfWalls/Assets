using Fusion;
using UnityEngine;

/// <summary>
/// Helper class to validate inputs and operations for PlayerState.
/// Contains static methods for common checks.
/// </summary>
public static class PlayerStateValidator // Made class static as it only contains static methods
{
    /// <summary>
    /// Verifies if the player can play a card based on their LOCAL turn state and energy level.
    /// </summary>
    public static bool CanPlayCard(PlayerState playerState, CardData card)
    {
        if (playerState == null || card == null)
        {
            // GameManager.Instance?.LogManager?.LogMessage("Validation: Cannot play card - null player state or card data.");
            return false;
        }

        // *** FIXED: Check PlayerState's local turn status ***
        // Check if it's the player's local turn within their current fight simulation.
        if (!playerState.GetIsLocalPlayerTurn())
        {
            // GameManager.Instance?.LogManager?.LogMessage($"Validation: Cannot play card - Not player's local turn ({playerState.PlayerName}).");
            return false;
        }

        // Check if player has enough energy (using the networked Energy property)
        if (playerState.Energy < card.EnergyCost)
        {
            // GameManager.Instance?.LogManager?.LogMessage($"Validation: Cannot play card {card.Name} - Not enough energy ({playerState.Energy}/{card.EnergyCost}).");
            return false;
        }

        // All checks passed
        return true;
    }

    /// <summary>
    /// Validates if a monster can be targeted by a specific card based on card rules.
    /// </summary>
    /// <param name="card">The card being played.</param>
    /// <param name="monster">The potential target monster.</param>
    /// <param name="isTargetingOwnMonster">Is the target the player's own monster?</param>
    /// <returns></returns>
    public static bool IsValidMonsterTarget(CardData card, Monster monster, bool isTargetingOwnMonster)
    {
        if (card == null || monster == null)
        {
            // GameManager.Instance?.LogManager?.LogMessage("Validation: Cannot validate target - null card or monster.");
            return false;
        }

        switch (card.Target)
        {
            case CardTarget.Enemy:
            case CardTarget.AllEnemies: // Treat AllEnemies as single Enemy in 1v1 monster fights
                return !isTargetingOwnMonster; // Target must be opponent's monster

            case CardTarget.Self:
                return isTargetingOwnMonster; // Target must be player's own monster

            case CardTarget.All:
                return true; // Can target any monster

            default:
                // GameManager.Instance?.LogManager?.LogMessage($"Validation: Unknown card target type: {card.Target}");
                return false;
        }
    }

    // This method might be less relevant now with authority checks within PlayerState,
    // but kept for potential future use or different contexts.
    /// <summary>
    /// Determines if a player has authority to modify monster health directly.
    /// (Note: Usually modifications should go through PlayerState methods/RPCs).
    /// </summary>
    public static bool CanModifyMonsterHealth(NetworkObject networkObject, Monster monster)
    {
        if (networkObject == null || monster == null)
        {
            return false;
        }

        // Can modify if has state authority over the PlayerState object
        if (networkObject.HasStateAuthority)
        {
            return true;
        }

        return false;
    }
}