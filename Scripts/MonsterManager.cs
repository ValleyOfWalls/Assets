// valleyofwalls-assets/Scripts/MonsterManager.cs
using Fusion;
using UnityEngine;

/// <summary>
/// Manages the LOCAL representation of the monster owned by the associated PlayerState.
/// Does NOT handle direct networking of monster stats anymore (that's done in PlayerState).
/// </summary>
public class MonsterManager
{
    // Player references (context for the owning PlayerState)
    private PlayerRef _playerRef;
    // Local monster instance owned by the PlayerState this manager belongs to
    private Monster _playerMonster;
    // Reference to the owning PlayerState component (passed in constructor)
    private PlayerState _ownerPlayerState;

    public MonsterManager(NetworkBehaviour ownerNetworkBehaviour, PlayerRef playerRef)
    {
        _ownerPlayerState = ownerNetworkBehaviour as PlayerState;
        if (_ownerPlayerState == null) {
             GameManager.Instance?.LogManager?.LogError("MonsterManager created without a valid owner PlayerState!");
        }
        _playerRef = playerRef;
        // Monster creation is now typically done via CreatePlayerMonsterLocally,
        // called from PlayerState.Spawned() after PlayerName is initialized.
    }

    /// <summary>
    /// Creates the default monster LOCALLY for the player.
    /// Should be called after the owner PlayerState's name is initialized.
    /// </summary>
    public void CreatePlayerMonsterLocally()
    {
         if (_playerMonster != null) {
              // GameManager.Instance?.LogManager?.LogMessage($"Player monster already exists for {_ownerPlayerState?.PlayerName}. Skipping creation.");
              return; // Avoid recreating
         }

        Color monsterColor = new Color(UnityEngine.Random.Range(0.5f, 1f), UnityEngine.Random.Range(0.5f, 1f), UnityEngine.Random.Range(0.5f, 1f));
        string monsterName = "Your Monster"; // Default

        // ** FIXED CS0023 ERROR HERE **
        // Check owner state null first, then access PlayerName and call ToString()
        if (_ownerPlayerState != null) {
            try {
                string playerNameStr = _ownerPlayerState.PlayerName.ToString(); // Access ToString directly
                if (!string.IsNullOrEmpty(playerNameStr)) {
                    monsterName = $"{playerNameStr}'s Monster";
                }
            } catch (System.Exception ex) {
                // Log potential exception if PlayerName isn't ready, but use default
                GameManager.Instance?.LogManager?.LogMessage($"Error accessing PlayerName in MonsterManager.CreatePlayerMonsterLocally: {ex.Message}");
            }
        }

        _playerMonster = new Monster {
            Name = monsterName, Health = 40, MaxHealth = 40, Attack = 5, Defense = 3, TintColor = monsterColor
        };
        // GameManager.Instance?.LogManager?.LogMessage($"Local player monster created: {_playerMonster.Name}");

        // IMPORTANT: Immediately update the owner's networked properties if we have authority
        if (_ownerPlayerState != null && _ownerPlayerState.HasStateAuthority) {
            _ownerPlayerState.UpdateMonsterNetworkedProperties(); // Call the method in PlayerState
        }
    }

    /// <summary>
    /// Updates the local monster instance from networked properties received by the owning PlayerState.
    /// </summary>
    public void UpdateLocalMonsterFromNetworked(
        string monsterName, int monsterHealth, int monsterMaxHealth,
        int monsterAttack, int monsterDefense, Color monsterColor)
    {
        if (_playerMonster == null) {
            // If the local monster doesn't exist yet (e.g., late join), create it.
            // GameManager.Instance?.LogManager?.LogMessage($"Local monster was null for {_ownerPlayerState?.PlayerName}. Creating representation from network data.");
            _playerMonster = new Monster();
        }

         // Update local monster stats from the values passed
         if (_playerMonster.Name != monsterName) _playerMonster.Name = monsterName;
         if (_playerMonster.MaxHealth != monsterMaxHealth) _playerMonster.MaxHealth = monsterMaxHealth;
         _playerMonster.SetHealth(monsterHealth); // Use SetHealth to trigger local events & handle clamping
         if (_playerMonster.Attack != monsterAttack) _playerMonster.Attack = monsterAttack;
         if (_playerMonster.Defense != monsterDefense) _playerMonster.Defense = monsterDefense;
         if (_playerMonster.TintColor != monsterColor) _playerMonster.TintColor = monsterColor;

        // GameManager.Instance?.LogManager?.LogMessage($"Updated local monster representation for {_playerMonster.Name} from network.");
    }


    /// <summary>
    /// Resets the player's local monster stats for a new round.
    /// This affects the local representation; networked stats are reset by PlayerState.
    /// </summary>
    public void ResetPlayerMonsterLocally()
    {
        if (_playerMonster != null) {
             _playerMonster.ResetStatsForNetwork(); // Use the monster's own reset method
             // GameManager.Instance?.LogManager?.LogMessage($"Reset local monster representation for {_playerMonster.Name}.");
        }
    }

    /// <summary>
    /// Gets the local representation of the player's monster.
    /// </summary>
    public Monster GetPlayerMonster()
    {
        return _playerMonster;
    }
}