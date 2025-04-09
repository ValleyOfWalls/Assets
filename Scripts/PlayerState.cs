using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    // Stats (networked)
    [Networked] public int Health { get; private set; }
    [Networked] public int MaxHealth { get; private set; }
    [Networked] public int Energy { get; private set; }
    [Networked] public int MaxEnergy { get; private set; }
    [Networked] public int Score { get; private set; }
    [Networked] public NetworkString<_32> PlayerName { get; private set; }

    // Hand cards - modified to use networked properties instead of RPCs
    [Networked, Capacity(10)]
    private NetworkArray<NetworkedCardData> _networkedHand { get; }

    // References
    private Monster _playerMonster;
    private Monster _opponentMonster;
    
    // Local card collections
    private List<CardData> _deck = new List<CardData>();
    private List<CardData> _hand = new List<CardData>();
    private List<CardData> _discardPile = new List<CardData>();
    
    // Events
    public static event Action<PlayerState> OnStatsChanged;
    public static event Action<PlayerState, List<CardData>> OnHandChanged;

    // Constants
    private const int STARTING_HEALTH = 50;
    private const int STARTING_ENERGY = 3;
    private const int HAND_SIZE = 5;
    private const int MAX_HAND_SIZE = 10;

    // Change detector
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        base.Spawned();
        
        if (HasStateAuthority)
        {
            // Initialize default stats
            MaxHealth = STARTING_HEALTH;
            Health = MaxHealth;
            MaxEnergy = STARTING_ENERGY;
            Energy = MaxEnergy;
            Score = 0;
            
            // Get name from the Player component
            var networkRunner = GameManager.Instance.NetworkManager.GetRunner();
            if (networkRunner != null)
            {
                Player playerComponent = null;
                NetworkObject playerObject = GameManager.Instance.PlayerManager.GetPlayerObject(Object.InputAuthority);
                
                if (playerObject != null)
                {
                    playerComponent = playerObject.GetComponent<Player>();
                }
                
                if (playerComponent != null)
                {
                    PlayerName = playerComponent.GetPlayerName();
                    GameManager.Instance.LogManager.LogMessage($"PlayerState set name to: {PlayerName}");
                }
                else
                {
                    // Fallback to default name
                    PlayerName = $"Player_{Object.InputAuthority.PlayerId}";
                    GameManager.Instance.LogManager.LogMessage($"PlayerState using default name: {PlayerName}");
                }
            }
            
            // Initialize the player's monster
            InitializeMonster();
            
            // Create starting deck
            CreateStartingDeck();
        }
        
        // Initialize change detector for networked properties
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        
        GameManager.Instance.LogManager.LogMessage($"PlayerState spawned for {PlayerName}");
        
        // Attempt to register with GameState if available, otherwise schedule for later
        AttemptRegisterWithGameState();
        
        // Start a coroutine to keep trying to register with GameState if it's not available yet
        StartCoroutine(RegisterWithGameStateWhenAvailable());
    }

    public override void Render()
    {
        base.Render();
        
        // Check for changes in the networked hand
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(_networkedHand))
            {
                UpdateLocalHandFromNetworked();
            }
        }
    }

    // Update local hand from networked data
    private void UpdateLocalHandFromNetworked()
    {
        _hand.Clear();
        
        // Convert networked data to local card data
        for (int i = 0; i < _networkedHand.Length; i++)
        {
            // Skip default/empty cards
            if (string.IsNullOrEmpty(_networkedHand[i].Name.ToString()))
                continue;
                
            _hand.Add(_networkedHand[i].ToCardData());
        }
        
        // Notify UI
        OnHandChanged?.Invoke(this, _hand);
        GameManager.Instance.LogManager.LogMessage($"Hand updated for {PlayerName} with {_hand.Count} cards");
    }

    // New method - Try to register with GameState
    private void AttemptRegisterWithGameState()
    {
        if (Runner != null && Object != null)
        {
            if (GameState.Instance != null)
            {
                GameState.Instance.RegisterPlayer(Object.InputAuthority, this);
                GameManager.Instance.LogManager.LogMessage($"PlayerState registered with GameState for player {Object.InputAuthority}");
            }
            else
            {
                GameManager.Instance.LogManager.LogMessage("GameState.Instance is currently null, will try to register later");
            }
        }
    }

    // New coroutine - Keep trying to register until successful
    private IEnumerator RegisterWithGameStateWhenAvailable()
    {
        float timeoutSeconds = 30.0f; // Set a reasonable timeout
        float startTime = Time.time;
        
        // Keep trying until we succeed or timeout
        while (GameState.Instance == null)
        {
            yield return new WaitForSeconds(0.5f); // Check every half second
            
            // Check for timeout
            if (Time.time - startTime > timeoutSeconds)
            {
                GameManager.Instance.LogManager.LogError("Timed out waiting for GameState.Instance to become available");
                yield break;
            }
        }
        
        // GameState is now available, register with it
        AttemptRegisterWithGameState();
    }

    private void InitializeMonster()
    {
        // Create monster instance
        _playerMonster = new Monster
        {
            Name = $"{PlayerName}'s Monster",
            Health = 40,
            MaxHealth = 40,
            Attack = 5,
            Defense = 3
        };
        
        GameManager.Instance.LogManager.LogMessage($"Monster initialized for {PlayerName}");
    }

    private void CreateStartingDeck()
    {
        // Create some basic starter cards
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
        
        // Shuffle the deck
        ShuffleDeck();
        
        GameManager.Instance.LogManager.LogMessage($"Starting deck created for {PlayerName} with {_deck.Count} cards");
    }

    private void ShuffleDeck()
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

    public void DrawInitialHand()
    {
        if (HasStateAuthority)
        {
            _hand.Clear();
            
            // Draw starting hand
            for (int i = 0; i < HAND_SIZE; i++)
            {
                DrawCard();
            }
            
            // Update networked hand directly
            UpdateNetworkedHand();
        }
    }

    // Update networked hand from local hand
    private void UpdateNetworkedHand()
    {
        if (!HasStateAuthority) return;
        
        // Clear networked hand by filling with default values
        for (int i = 0; i < _networkedHand.Length; i++)
        {
            _networkedHand.Set(i, default);
        }
        
        // Update networked hand with current local hand
        for (int i = 0; i < _hand.Count && i < MAX_HAND_SIZE; i++)
        {
            _networkedHand.Set(i, NetworkedCardData.FromCardData(_hand[i]));
        }
    }

    private void DrawCard()
    {
        if (_deck.Count == 0)
        {
            // Shuffle discard into deck if empty
            _deck.AddRange(_discardPile);
            _discardPile.Clear();
            ShuffleDeck();
            
            if (_deck.Count == 0)
            {
                // If still empty, no cards to draw
                return;
            }
        }
        
        // Draw from the top
        CardData drawnCard = _deck[0];
        _deck.RemoveAt(0);
        _hand.Add(drawnCard);
    }

    public void PlayCard(int cardIndex, Monster target)
    {
        if (!HasStateAuthority || cardIndex < 0 || cardIndex >= _hand.Count) return;
        
        CardData card = _hand[cardIndex];
        
        // Check if enough energy
        if (Energy < card.EnergyCost)
        {
            GameManager.Instance.LogManager.LogMessage($"Not enough energy to play {card.Name}");
            return;
        }
        
        // Handle card effects based on type and target
        bool cardPlayed = false;
        
        switch (card.Target)
        {
            case CardTarget.Enemy:
                if (target != null)
                {
                    // Apply damage
                    if (card.DamageAmount > 0)
                    {
                        target.TakeDamage(card.DamageAmount);
                    }
                    cardPlayed = true;
                }
                break;
                
            case CardTarget.Self:
                // Apply self effects
                if (card.BlockAmount > 0)
                {
                    // Add block (implement this if we add a block system)
                }
                
                if (card.HealAmount > 0)
                {
                    ModifyHealth(card.HealAmount);
                }
                
                if (card.EnergyGain > 0)
                {
                    ModifyEnergy(card.EnergyGain);
                }
                
                cardPlayed = true;
                break;
                
            case CardTarget.AllEnemies:
                // Apply to all enemies (currently just one)
                if (target != null && card.DamageAmount > 0)
                {
                    target.TakeDamage(card.DamageAmount);
                }
                cardPlayed = true;
                break;
        }
        
        if (cardPlayed)
        {
            // Remove the card from hand and add to discard
            _hand.RemoveAt(cardIndex);
            _discardPile.Add(card);
            
            // Use energy
            ModifyEnergy(-card.EnergyCost);
            
            // Update networked hand
            UpdateNetworkedHand();
            
            GameManager.Instance.LogManager.LogMessage($"{PlayerName} played {card.Name}");
        }
    }

    public void SetOpponentMonster(Monster monster)
{
    _opponentMonster = monster;
    if (monster != null)
    {
        GameManager.Instance.LogManager.LogMessage($"{PlayerName} now fighting against {monster.Name}");
    }
    else
    {
        GameManager.Instance.LogManager.LogMessage($"{PlayerName} has no opponent monster assigned yet");
    }
}

    public Monster GetMonster()
    {
        return _playerMonster;
    }

    public Monster GetOpponentMonster()
    {
        return _opponentMonster;
    }

    public void ModifyHealth(int amount)
    {
        if (!HasStateAuthority) return;
        
        Health = Mathf.Clamp(Health + amount, 0, MaxHealth);
        OnStatsChanged?.Invoke(this);
        
        if (Health <= 0)
        {
            GameManager.Instance.LogManager.LogMessage($"{PlayerName} has been defeated!");
            // Handle player defeat
        }
    }

    public void ModifyEnergy(int amount)
    {
        if (!HasStateAuthority) return;
        
        Energy = Mathf.Clamp(Energy + amount, 0, MaxEnergy);
        OnStatsChanged?.Invoke(this);
    }

    public void IncreaseScore()
    {
        if (!HasStateAuthority) return;
        
        Score++;
        OnStatsChanged?.Invoke(this);
        
        GameManager.Instance.LogManager.LogMessage($"{PlayerName} score increased to {Score}");
    }

    public int GetScore()
    {
        return Score;
    }

    public void PrepareForNewRound()
    {
        if (!HasStateAuthority) return;
        
        // Reset energy
        Energy = MaxEnergy;
        
        // Discard hand and draw new cards
        _discardPile.AddRange(_hand);
        _hand.Clear();
        
        // Draw new hand
        DrawInitialHand();
        
        // Reset any round-specific stats or effects
        
        OnStatsChanged?.Invoke(this);
        
        GameManager.Instance.LogManager.LogMessage($"{PlayerName} ready for new round");
    }

    public void EndTurn()
    {
        if (!HasStateAuthority) return;
        
        // Process end of turn effects
        
        // Request next turn
        if (GameState.Instance != null)
            GameState.Instance.NextTurn();
        
        GameManager.Instance.LogManager.LogMessage($"{PlayerName} ended their turn");
    }

    public List<CardData> GetHand()
    {
        return new List<CardData>(_hand);
    }
}