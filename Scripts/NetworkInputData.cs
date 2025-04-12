// valleyofwalls-assets/Scripts/NetworkInputData.cs
using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    // Keep existing fields if needed (like movement, other buttons)
    public NetworkButtons buttons; // Example: Keep buttons

    // --- New fields for card play ---
    // A flag set for one tick when a card is played
    public NetworkBool PlayedCard;

    // Details of the card play action for this tick
    public int CardIndex;        // Index in hand of the card played
    public PlayerRef TargetPlayer;   // Target player (PlayerRef.None if self or no player target)
    public int DamageAmount;     // Damage dealt by the card (if any)
    // Add other simple effect flags/values if needed (e.g., BlockAmount for self-block)
    // Keep this minimal; complex effects might need other systems or RPCs for non-state things.
}