using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles LOCAL card creation, management (deck, hand, discard), and effects application logic.
/// This class NO LONGER handles networking directly.
/// </summary>
public class CardManager
{
    // Card collections (LOCAL ONLY)
    private List<CardData> _deck = new List<CardData>();
    private List<CardData> _hand = new List<CardData>();
    private List<CardData> _discardPile = new List<CardData>();

    // Constants
    private const int HAND_SIZE = 5; // Default hand size

    public CardManager()
    {
        // Don't create deck immediately, wait for explicit call if needed
        // CreateStartingDeck();
        // ShuffleDeck();
    }

    /// <summary>
    /// Creates the starting deck for the player locally.
    /// </summary>
    public void CreateStartingDeck()
    {
        _deck.Clear();
        _hand.Clear(); // Ensure hand is clear before dealing
        _discardPile.Clear(); // Ensure discard is clear

        // Add basic attack cards
        for (int i = 0; i < 5; i++)
        {
            _deck.Add(new CardData
            {
                Name = "Strike",
                Description = "Deal 6 damage",
                EnergyCost = 1,
                Type = CardType.Attack,
                Target = CardTarget.Enemy,
                DamageAmount = 6
            });
        }

        // Add basic defense cards
        for (int i = 0; i < 5; i++)
        {
            _deck.Add(new CardData
            {
                Name = "Defend",
                Description = "Gain 5 block",
                EnergyCost = 1,
                Type = CardType.Skill,
                Target = CardTarget.Self,
                BlockAmount = 5
            });
        }

        // Add a couple special cards
        _deck.Add(new CardData
        {
            Name = "Cleave",
            Description = "Deal 8 damage to all enemies", // Description needs update if only 1 enemy
            EnergyCost = 2,
            Type = CardType.Attack,
            Target = CardTarget.Enemy, // Changed from AllEnemies as only 1v1 monster fights currently
            DamageAmount = 8
        });
        _deck.Add(new CardData
        {
            Name = "Second Wind",
            Description = "Gain 2 energy",
            EnergyCost = 0,
            Type = CardType.Skill,
            Target = CardTarget.Self,
            EnergyGain = 2
        });

        GameManager.Instance?.LogManager?.LogMessage($"Local starting deck created with {_deck.Count} cards.");
    }

    /// <summary>
    /// Shuffles the local deck to randomize card order.
    /// </summary>
    public void ShuffleDeck()
    {
        int n = _deck.Count;
        System.Random rng = new System.Random(); // Use System.Random for potentially better shuffling
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData temp = _deck[k];
            _deck[k] = _deck[n];
            _deck[n] = temp;
        }
         GameManager.Instance?.LogManager?.LogMessage($"Local deck shuffled.");
    }

    /// <summary>
    /// Draws cards locally up to the defined hand size.
    /// Handles reshuffling the discard pile if the deck runs out.
    /// </summary>
    public void DrawToHandSize()
    {
         GameManager.Instance?.LogManager?.LogMessage($"Attempting to draw to hand size ({HAND_SIZE}). Current hand: {_hand.Count}");
        while (_hand.Count < HAND_SIZE)
        {
            if (!DrawCard())
            {
                 GameManager.Instance?.LogManager?.LogMessage($"Cannot draw more cards (Deck: {_deck.Count}, Discard: {_discardPile.Count}). Hand size: {_hand.Count}");
                break; // No more cards to draw from deck or discard
            }
        }
         GameManager.Instance?.LogManager?.LogMessage($"Finished drawing. Final hand size: {_hand.Count}");
    }

    /// <summary>
    /// Draws a single card locally from the deck to the hand.
    /// Handles reshuffling the discard pile if necessary.
    /// </summary>
    /// <returns>True if a card was successfully drawn, false otherwise.</returns>
    public bool DrawCard()
    {
        // Check if deck is empty
        if (_deck.Count == 0)
        {
            // If discard pile has cards, reshuffle it into the deck
            if (_discardPile.Count > 0)
            {
                 GameManager.Instance?.LogManager?.LogMessage($"Deck empty. Reshuffling {_discardPile.Count} cards from discard pile.");
                _deck.AddRange(_discardPile);
                _discardPile.Clear();
                ShuffleDeck();
            }
            else
            {
                // Both deck and discard are empty - cannot draw
                 GameManager.Instance?.LogManager?.LogMessage($"Deck and discard pile empty. Cannot draw card.");
                return false;
            }
        }

        // Draw the top card from the deck
        CardData drawnCard = _deck[0];
        _deck.RemoveAt(0);
        _hand.Add(drawnCard);
        // GameManager.Instance?.LogManager?.LogMessage($"Drew card: {drawnCard.Name}. Hand size: {_hand.Count}, Deck size: {_deck.Count}");


        return true;
    }

    /// <summary>
    /// Plays a card locally from the hand, moving it to the discard pile.
    /// </summary>
    /// <param name="cardIndex">The index of the card to play in the hand.</param>
    public void PlayCard(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= _hand.Count)
        {
             GameManager.Instance?.LogManager?.LogMessage($"Invalid card index to play: {cardIndex}. Hand size: {_hand.Count}");
            return;
        }

        // Move the card from hand to discard pile
        CardData card = _hand[cardIndex];
        _hand.RemoveAt(cardIndex);
        _discardPile.Add(card); // Add to discard pile
        // GameManager.Instance?.LogManager?.LogMessage($"Played card: {card.Name}. Moved to discard. Hand size: {_hand.Count}, Discard size: {_discardPile.Count}");
    }

    /// <summary>
    /// Discards the entire hand locally and draws a new hand up to the hand size.
    /// Typically called at the start of a new round or turn.
    /// </summary>
    public void PrepareForNewRound()
    {
        // Discard current hand
         GameManager.Instance?.LogManager?.LogMessage($"Preparing for new round. Discarding {_hand.Count} cards.");
        _discardPile.AddRange(_hand);
        _hand.Clear();

        // Draw new hand
        DrawToHandSize();
    }

    /// <summary>
    /// Gets a copy of the current local hand.
    /// </summary>
    /// <returns>A new list containing the cards currently in the hand.</returns>
    public List<CardData> GetHand()
    {
        // Return a copy to prevent external modification of the internal list
        return new List<CardData>(_hand);
    }

     /// <summary>
     /// Gets the current number of cards in the local hand.
     /// </summary>
    public int GetHandCount() => _hand.Count;

    /// <summary>
    /// Gets the current number of cards in the local deck.
    /// </summary>
    public int GetDeckCount() => _deck.Count;

    /// <summary>
    /// Gets the current number of cards in the local discard pile.
    /// </summary>
    public int GetDiscardCount() => _discardPile.Count;

    // --- Methods removed as networking is handled elsewhere ---
    // public NetworkedCardData[] GetNetworkedHand(int maxSize) { ... }
    // public void UpdateFromNetworkedHand(NetworkedCardData[] networkedHand) { ... }
    // public void ApplyCardEffects(...) { ... } // Effect application logic moved to PlayerState or Battle Controllers
}