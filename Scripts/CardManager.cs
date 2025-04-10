using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles card creation, management and effects
/// </summary>
public class CardManager
{
    // Card collections
    private List<CardData> _deck = new List<CardData>();
    private List<CardData> _hand = new List<CardData>();
    private List<CardData> _discardPile = new List<CardData>();
    
    // Constants
    private const int HAND_SIZE = 5;
    
    public CardManager()
    {
        CreateStartingDeck();
        ShuffleDeck();
    }
    
    /// <summary>
    /// Creates the starting deck for the player
    /// </summary>
    public void CreateStartingDeck()
    {
        _deck.Clear();
        
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
            Description = "Deal 8 damage to all enemies",
            EnergyCost = 2,
            Type = CardType.Attack,
            Target = CardTarget.AllEnemies,
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
        
        GameManager.Instance.LogManager.LogMessage($"Starting deck created with {_deck.Count} cards");
    }
    
    /// <summary>
    /// Shuffles the deck to randomize card order
    /// </summary>
    public void ShuffleDeck()
    {
        int n = _deck.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            CardData temp = _deck[k];
            _deck[k] = _deck[n];
            _deck[n] = temp;
        }
    }
    
    /// <summary>
    /// Draws cards up to a full hand
    /// </summary>
    public void DrawToHandSize()
    {
        while (_hand.Count < HAND_SIZE)
        {
            if (!DrawCard())
            {
                break; // No more cards to draw
            }
        }
    }
    
    /// <summary>
    /// Draws a single card from the deck to the hand
    /// </summary>
    public bool DrawCard()
    {
        // Check if deck is empty
        if (_deck.Count == 0)
        {
            // Shuffle discard into deck
            if (_discardPile.Count > 0)
            {
                _deck.AddRange(_discardPile);
                _discardPile.Clear();
                ShuffleDeck();
            }
            else
            {
                // Both deck and discard are empty
                return false;
            }
        }
        
        // Draw from the top
        CardData drawnCard = _deck[0];
        _deck.RemoveAt(0);
        _hand.Add(drawnCard);
        
        return true;
    }
    
    /// <summary>
    /// Plays a card from the hand
    /// </summary>
    public void PlayCard(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= _hand.Count)
        {
            return;
        }
        
        // Move the card to discard pile
        CardData card = _hand[cardIndex];
        _hand.RemoveAt(cardIndex);
        _discardPile.Add(card);
    }
    
    /// <summary>
    /// Applies card effects to the target monster
    /// </summary>
    public void ApplyCardEffects(CardData card, Monster target, PlayerState playerState)
    {
        if (card == null || playerState == null)
        {
            return;
        }
        
        // Apply different effects based on card target
        bool isPlayerMonster = (target == playerState.GetMonster());
        
        if (PlayerStateValidator.IsValidMonsterTarget(card, target, isPlayerMonster))
        {
            // For damage effects
            if (card.DamageAmount > 0 && !isPlayerMonster)
            {
                target.TakeDamage(card.DamageAmount);
            }
            
            // For block effects
            if (card.BlockAmount > 0 && isPlayerMonster)
            {
                target.AddBlock(card.BlockAmount);
            }
            
            // For heal effects
            if (card.HealAmount > 0)
            {
                if (isPlayerMonster)
                {
                    target.Heal(card.HealAmount);
                }
                else
                {
                    playerState.ModifyHealth(card.HealAmount);
                }
            }
            
            // For energy gain effects
            if (card.EnergyGain > 0)
            {
                playerState.ModifyEnergy(card.EnergyGain);
            }
        }
    }
    
    /// <summary>
    /// Resets the hand and draws new cards for a new round
    /// </summary>
    public void PrepareForNewRound()
    {
        // Discard current hand
        _discardPile.AddRange(_hand);
        _hand.Clear();
        
        // Draw new hand
        DrawToHandSize();
    }
    
    /// <summary>
    /// Gets the current hand for display
    /// </summary>
    public List<CardData> GetHand()
    {
        return new List<CardData>(_hand);
    }
    
    /// <summary>
    /// Converts the hand to networked representation
    /// </summary>
    public NetworkedCardData[] GetNetworkedHand(int maxSize)
    {
        NetworkedCardData[] networkedHand = new NetworkedCardData[maxSize];
        
        // Initialize with default values
        for (int i = 0; i < maxSize; i++)
        {
            networkedHand[i] = default;
        }
        
        // Fill with actual hand data
        for (int i = 0; i < _hand.Count && i < maxSize; i++)
        {
            networkedHand[i] = NetworkedCardData.FromCardData(_hand[i]);
        }
        
        return networkedHand;
    }
    
    /// <summary>
    /// Updates the hand from networked data
    /// </summary>
    public void UpdateFromNetworkedHand(NetworkedCardData[] networkedHand)
    {
        _hand.Clear();
        
        for (int i = 0; i < networkedHand.Length; i++)
        {
            // Skip default/empty cards
            if (string.IsNullOrEmpty(networkedHand[i].Name.ToString()))
                continue;
                
            _hand.Add(networkedHand[i].ToCardData());
        }
    }
}