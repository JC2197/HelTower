using UnityEngine;

/// <summary>
/// Utility class for quickly accessing player, character data, and weapon information.
/// Reduces boilerplate code for common player-related queries.
/// </summary>
public static class PlayerUtil
{
    /// <summary>
    /// Find the PlayerController in the scene
    /// </summary>
    /// <returns>PlayerController instance, or null if not found</returns>
    public static PlayerController GetPlayer()
    {
        return PlayerController.GetLocalPlayer();
    }

    /// <summary>
    /// Get the PlayerController from a GameObject (if it has one)
    /// </summary>
    /// <param name="owner">GameObject to check (typically projectile owner)</param>
    /// <returns>PlayerController component, or null if not found</returns>
    public static PlayerController GetPlayer(GameObject owner)
    {
        if (owner == null) return null;
        return owner.GetComponent<PlayerController>();
    }

    /// <summary>
    /// Get the CharacterData from the player in the scene
    /// </summary>
    /// <returns>CharacterData instance, or null if player or data not found</returns>
    public static CharacterData GetCharacterData()
    {
        PlayerController player = GetPlayer();
        return player?.GetCurrentCharacterData();
    }

    /// <summary>
    /// Get the CharacterData from a GameObject's PlayerController
    /// </summary>
    /// <param name="owner">GameObject to check (typically projectile owner)</param>
    /// <returns>CharacterData instance, or null if not found</returns>
    public static CharacterData GetCharacterData(GameObject owner)
    {
        PlayerController player = GetPlayer(owner);
        return player?.GetCurrentCharacterData();
    }

    /// <summary>
    /// Get the main hand WeaponConfig from the player in the scene
    /// </summary>
    /// <returns>WeaponConfig instance, or null if player or weapon not found</returns>
    public static WeaponConfig GetWeapon()
    {
        CharacterData characterData = GetCharacterData();
        return characterData?.mainHandWeaponConfig;
    }

    /// <summary>
    /// Get the main hand WeaponConfig from a GameObject's player
    /// </summary>
    /// <param name="owner">GameObject to check (typically projectile owner)</param>
    /// <returns>WeaponConfig instance, or null if not found</returns>
    public static WeaponConfig GetWeapon(GameObject owner)
    {
        CharacterData characterData = GetCharacterData(owner);
        return characterData?.mainHandWeaponConfig;
    }

    // /// <summary>
    // /// Get the equipped weapon ItemInstance from the player in the scene
    // /// </summary>
    // /// <returns>ItemInstance for equipped weapon, or null if not found</returns>
    // public static ItemInstance GetEquippedWeapon()
    // {
    //     CharacterData characterData = GetCharacterData();
    //     if (characterData?.equippedGear != null && characterData.equippedGear.TryGetValue(GearSlot.Weapon, out ItemInstance weaponItem))
    //     {
    //         return weaponItem;
    //     }   
    //     return null;
    // }

    // /// <summary>
    // /// Get the equipped weapon ItemInstance from a GameObject's player
    // /// </summary>
    // /// <param name="owner">GameObject to check (typically projectile owner)</param>
    // /// <returns>ItemInstance for equipped weapon, or null if not found</returns>
    // public static ItemInstance GetEquippedWeapon(GameObject owner)
    // {
    //     CharacterData characterData = GetCharacterData(owner);
    //     if (characterData?.equippedGear != null && characterData.equippedGear.TryGetValue(GearSlot.Weapon, out ItemInstance weaponItem))
    //     {
    //         return weaponItem;
    //     }
    //     return null;
    // }
}
