using Fusion;
using UnityEngine;

/// <summary>
/// Manages the LOCAL representation of the monster owned by the associated PlayerState.
/// Also holds references related to the opponent for the PlayerState's context.
/// Does NOT handle direct networking of monster stats anymore (that's done in PlayerState).
/// </summary>
public class MonsterManager
{
    // Player references (context for the owning PlayerState)
    private PlayerRef _playerRef;
    // Opponent references are now handled within PlayerState directly

    // Local monster instance owned by the PlayerState this manager belongs to
    private Monster _playerMonster;

    // Reference to the owning PlayerState component (passed in constructor)
    private PlayerState _ownerPlayerState;

    public MonsterManager(NetworkBehaviour ownerNetworkBehaviour, PlayerRef playerRef)
    {
        _ownerPlayerState = ownerNetworkBehaviour as PlayerState;
        if (_ownerPlayerState == null)
        {
             GameManager.Instance?.LogManager?.LogError("MonsterManager created without a valid owner PlayerState!");
        }
        _playerRef = playerRef;

        // Note: Monster creation is now typically done via CreatePlayerMonsterLocally,
        // called from PlayerState.Spawned() after PlayerName is initialized.
        // CreatePlayerMonsterLocally(); // Don't call here, PlayerName might not be set yet.
    }

    /// <summary>
    /// Creates the default monster LOCALLY for the player.
    /// Should be called after the owner PlayerState's name is initialized.
    /// </summary>
    public void CreatePlayerMonsterLocally()
    {
         if (_playerMonster != null)
         {
              // GameManager.Instance?.LogManager?.LogMessage($"Player monster already exists for {_ownerPlayerState?.PlayerName}. Skipping creation.");
              return; // Avoid recreating
         }

        // Create random color for monster
        Color monsterColor = new Color(
            UnityEngine.Random.Range(0.5f, 1f),
            UnityEngine.Random.Range(0.5f, 1f),
            UnityEngine.Random.Range(0.5f, 1f)
        );

        string monsterName = "Your Monster";
        if (_ownerPlayerState != null && !string.IsNullOrEmpty(_ownerPlayerState.PlayerName.ToString()))
        {
             monsterName = $"{_ownerPlayerState.PlayerName}'s Monster";
        }


        _playerMonster = new Monster
        {
            Name = monsterName, // Use initialized name if available
            Health = 40, // Consider getting defaults from config
            MaxHealth = 40,
            Attack = 5,
            Defense = 3,
            TintColor = monsterColor
        };

        GameManager.Instance?.LogManager?.LogMessage($"Local player monster created: {_playerMonster.Name}");

        // IMPORTANT: Immediately update the owner's networked properties if we have authority
        if (_ownerPlayerState != null && _ownerPlayerState.HasStateAuthority)
        {
            _ownerPlayerState.UpdateMonsterNetworkedProperties(); // Call the method in PlayerState
        }
    }

    /// <summary>
    /// Updates the local monster instance from networked properties received by the owning PlayerState.
    /// </summary>
    public void UpdateLocalMonsterFromNetworked(
        string monsterName, // Now using string
        int monsterHealth,
        int monsterMaxHealth,
        int monsterAttack,
        int monsterDefense,
        Color monsterColor)
    {
        if (_playerMonster == null)
        {
             // If the local monster doesn't exist yet (e.g., late join), create it.
             GameManager.Instance?.LogManager?.LogMessage($"Local monster was null for {_ownerPlayerState?.PlayerName}. Creating representation from network data.");
            _playerMonster = new Monster();
        }

         // Update local monster stats from the values passed (which came from PlayerState's networked props)
         bool changed = false;
         if (_playerMonster.Name != monsterName) { _playerMonster.Name = monsterName; changed = true;}
         if (_playerMonster.MaxHealth != monsterMaxHealth) { _playerMonster.MaxHealth = monsterMaxHealth; changed = true; } // Update MaxHealth first
         if (_playerMonster.Health != monsterHealth) { _playerMonster.SetHealth(monsterHealth); changed = true; } // Use SetHealth to trigger local events
         if (_playerMonster.Attack != monsterAttack) { _playerMonster.Attack = monsterAttack; changed = true; }
         if (_playerMonster.Defense != monsterDefense) { _playerMonster.Defense = monsterDefense; changed = true; }
         if (_playerMonster.TintColor != monsterColor) { _playerMonster.TintColor = monsterColor; changed = true; }


        //if (changed)
            //GameManager.Instance?.LogManager?.LogMessage($"Updated local monster representation for {_playerMonster.Name} from network.");

    }

    // Method removed: Setting opponent is now handled directly in PlayerState
    // public void SetOpponentMonster(PlayerRef opponentRef, PlayerState opponentState) { ... }

    // Method removed: Updating opponent monster is now handled by PlayerState updating its local representation
    // public void UpdateOpponentMonster() { ... }

    // Method removed: Applying damage and score granting is handled in PlayerState
    // public void ApplyDamageToPlayerMonster(int amount, ref int monsterHealth) { ... }

    /// <summary>
    /// Resets the player's local monster stats for a new round.
    /// This affects the local representation; networked stats are reset by PlayerState.
    /// </summary>
    public void ResetPlayerMonsterLocally()
    {
        if (_playerMonster != null)
        {
             _playerMonster.ResetStatsForNetwork(); // Use the monster's own reset method
             // Optionally trigger OnPlayerMonsterChanged event if needed immediately by UI
             // PlayerState.OnPlayerMonsterChanged?.Invoke(_playerMonster);
             GameManager.Instance?.LogManager?.LogMessage($"Reset local monster representation for {_playerMonster.Name}.");
        }
    }

    /// <summary>
    /// Gets the local representation of the player's monster.
    /// </summary>
    public Monster GetPlayerMonster()
    {
        return _playerMonster;
    }

    // Method removed: Getting opponent monster is handled by PlayerState
    // public Monster GetOpponentMonster() { ... }
}