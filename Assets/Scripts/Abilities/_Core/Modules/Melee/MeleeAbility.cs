using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;

/// <summary>
/// Handles melee attacks by instantiating a meleeFX prefab at a spawn position
/// relative to the attacker, oriented toward the attack direction.
/// The meleeFX prefab's own Animator controls when its collider is active.
/// MeleeAbility handles damage/effects when collisions occur.
/// The meleeFX instance is destroyed when its animation completes.
/// </summary>
public class MeleeAbility : MonoBehaviour, ISubAbility
{
    private MeleeConfig config;
    private GameObject hitboxInstance;
    private HashSet<Collider2D> hitTargets = new HashSet<Collider2D>();
    private GameObject owner;
    private GameObject statOwner;
    private Vector2 attackDirection;
    private float spawnTime;
    private Animator hitboxAnimator;
    private string abilityName;
    private List<string> abilityTags;
    private AbilityDataConfig parentConfig;
    private bool firedFromOffhand;
    private bool destroyTriggersApplied;

    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        owner = context.owner;
        statOwner = context.statOwner != null ? context.statOwner : context.owner;
        abilityName = context.AbilityName;
        abilityTags = context.AbilityTags;
    }

    /// <summary>
    /// Instantiates the meleeFX prefab from config at (weapon root + direction * radius),
    /// rotated toward the attack direction (0 = right).
    /// Falls back to owner center when no weapon root exists.
    /// <paramref name="firedFromOffhand"/> selects the offhand weapon as the spawn origin
    /// (for alternating dual-wield fire); defaults to mainhand.
    /// </summary>
    public void PerformAttack(MeleeConfig meleeConfig, Vector2 direction, bool firedFromOffhand = false, bool visualOnly = false)
    {
        config = meleeConfig;
        attackDirection = direction.normalized;
        this.firedFromOffhand = firedFromOffhand;

        if (config.hitbox.prefab == null)
        {
            Debug.LogError("[MeleeAbility] hitbox.prefab is null in MeleeConfig!");
            Destroy(this);
            return;
        }

        // Spawn position: weapon root offset by radius along attack direction.
        Transform ownerTransform = owner != null ? owner.transform : transform;
        Vector3 spawnPos = ownerTransform.position + (Vector3)(attackDirection * config.meleeFXRadiusDistance);


        // Rotation: 0° = facing right, rotated toward attack direction
        float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
        Quaternion spawnRotation = Quaternion.Euler(0f, 0f, angle);

        hitboxInstance = Object.Instantiate(config.hitbox.prefab, spawnPos, spawnRotation);

        // Local-only: never network-spawned. Damage/effects stay authoritative wherever this runs
        // (server for enemies, owner for players); observers get the visual via the RPC below,
        // avoiding FishNet's parenting/spawn-ordering rules entirely for this cosmetic object.
        if (config.stickToCharacter)
        {
            hitboxInstance.transform.SetParent(ownerTransform, true);
        }
        ApplyFlipAndScale(hitboxInstance, config, angle);

        if (visualOnly)
        {
            foreach (Collider2D collider in hitboxInstance.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;

            hitboxAnimator = hitboxInstance.GetComponentInChildren<Animator>();
            spawnTime = Time.time;
            return;
        }

        if (config.meleeSound != null)
            AudioManager.Instance.PlaySpatialSound(config.meleeSound, spawnPos, 1f, 1f);

        // Tell every other observer to show the same swing visual locally.
        // ObserversRpc can only be invoked by the server; player-owner-triggered attacks on a
        // true remote client skip this (mirrors PlayerController.ObserversRpcSpawnMuzzleFlash).
        if (InstanceFinder.IsServerStarted)
            owner?.GetComponent<Organism>()?.ObserversRpcSpawnMeleeSwingVisual(abilityName, spawnPos, angle, firedFromOffhand);

        // Find ALL colliders on the hitbox prefab (root + children) so every collider
        // shape contributes to hit detection, not just the first one found. This matters
        // for meleeFX prefabs built from multiple collider segments (e.g. a multi-part
        // swing arc or a blade split into several hitboxes along its length).
        Collider2D[] allColliders = hitboxInstance.GetComponentsInChildren<Collider2D>(true);

        if (allColliders == null || allColliders.Length == 0)
        {
            Debug.LogError("[MeleeAbility] meleeFX prefab has no Collider2D!");
            Object.Destroy(hitboxInstance);
            hitboxInstance = null;
            Destroy(this);
            return;
        }

        foreach (Collider2D col in allColliders)
        {
            col.isTrigger = true;

            // Attach TriggerHandler to each collider's GameObject so every collider forwards
            // its own trigger events into the shared hit-processing callback.
            GameObject colliderObject = col.gameObject;
            TriggerHandler triggerHandler = colliderObject.GetComponent<TriggerHandler>();
            if (triggerHandler == null)
                triggerHandler = colliderObject.AddComponent<TriggerHandler>();
            triggerHandler.onTriggerEnter = OnHitboxTriggerEnter;
        }
        // Get animator from root or first child
        hitboxAnimator = hitboxInstance.GetComponent<Animator>();
        if (hitboxAnimator == null)
            hitboxAnimator = hitboxInstance.GetComponentInChildren<Animator>();

        spawnTime = Time.time;
    }

    /// <summary>Shared flip/scale setup used by both the authoritative instance and remote-visual copies.</summary>
    private static void ApplyFlipAndScale(GameObject instance, MeleeConfig config, float angle)
    {
        if (config.flipMeleeFX || config.flipMeleeFXY)
        {
            if (Mathf.Abs(angle) > 90f)
            {
                foreach (SpriteRenderer spriteRenderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    spriteRenderer.flipX = config.flipMeleeFX;
                    spriteRenderer.flipY = config.flipMeleeFXY;
                }
            }
        }
        else if (config.hitbox.scaleX > 0f && config.hitbox.scaleX != 1f || config.hitbox.scaleY > 0f && config.hitbox.scaleY != 1f)
        {
            instance.transform.localScale = new Vector3(config.hitbox.scaleX, config.hitbox.scaleY, 1f);
        }
    }

    /// <summary>
    /// Spawns a purely cosmetic, non-colliding copy of the swing prefab. Called on observer
    /// machines via Organism.ObserversRpcSpawnMeleeSwingVisual — no damage, no networking.
    /// </summary>
    public static void SpawnVisualOnly(MeleeConfig config, Vector3 spawnPos, Quaternion spawnRotation)
    {
        HitVisualHelper.SpawnEffect(config.hitbox.prefab, spawnPos, spawnRotation);
    }

    private Transform ResolveMeleeSpawnOrigin()
    {
        if (owner == null)
            return transform;

        Transform ownerTransform = owner.transform;
        if (config.stickToCharacter)
            return ownerTransform;
        if (firedFromOffhand)
        {
            Transform offHandWeaponPreferred = ownerTransform.Find("OffHandWeaponHolder/OffHandWeapon");
            if (offHandWeaponPreferred != null)
                return offHandWeaponPreferred;
        }

        Transform mainHandWeapon = ownerTransform.Find("WeaponHolder/Weapon");
        if (mainHandWeapon != null)
            return mainHandWeapon;

        Transform offHandWeapon = ownerTransform.Find("OffHandWeaponHolder/OffHandWeapon");
        if (offHandWeapon != null)
            return offHandWeapon;

        return ownerTransform;
    }

    private void Update()
    {
        if (hitboxInstance == null)
        {
            Destroy(this);
            return;
        }

        // Translate meleeFX along attack direction if speed > 0
        if (config.meleeFXSpeed > 0f)
            hitboxInstance.transform.position += (Vector3)attackDirection * config.meleeFXSpeed * Time.deltaTime;

        // Auto-destroy when animation completes
        if (hitboxAnimator != null && hitboxAnimator.runtimeAnimatorController != null)
        {
            AnimatorStateInfo stateInfo = hitboxAnimator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.normalizedTime >= 1f && !hitboxAnimator.IsInTransition(0))
                DestroyHitbox();
        }
        else if (Time.time > spawnTime + 1f)
        {
            // Fallback: destroy after 1 second if no animator
            DestroyHitbox();
        }
    }

    private void OnHitboxTriggerEnter(Collider2D other)
    {
        // Exclude self-hits by hierarchy, not just root-reference equality — "other" is often a
        // child collider (e.g. "Visuals"), so a bare "other.gameObject == owner" check never
        // matches and stickToCharacter swings can hit their own attacker.
        if (owner != null && other.transform.IsChildOf(owner.transform))
            return;

        // Check if already hit this target in this activation window
        if (!config.allowMultiHit && hitTargets.Contains(other))
            return;

        // Check layer mask
        if (((1 << other.gameObject.layer) & config.hitbox.hitLayers) == 0)
            return;

        // Mark as hit
        hitTargets.Add(other);

        bool isServerStarted = InstanceFinder.IsServerStarted;
        bool isNetworkActive = InstanceFinder.NetworkManager != null;
        Vector3 hitPos = hitboxInstance.transform.position;
        Vector2 radialDir = ((Vector2)hitPos - (Vector2)transform.position).normalized;
        // Reusable hitbox processing. statOwner drives trait/stat scaling, weapon-damage
        // resolution, and life-steal healing (so summon attacks heal the player), while owner
        // is credited as the attacker.
        float damageDealt = config.hitbox.ApplyDamage(other, statOwner, owner, statOwner, hitPos, abilityName, abilityTags, parentConfig);
        config.hitbox.ApplyKnockback(other, owner, radialDir);
        config.hitbox.ApplyPull(other, hitPos);
        config.hitbox.ApplyOnHitEffects(other.gameObject, owner, statOwner);
        config.hitbox.SpawnHitFeedback(other.transform.position, parentConfig, other);

        // Tell every other observer to show the same hit feedback locally.
        if (isServerStarted)
            owner?.GetComponent<Organism>()?.ObserversRpcSpawnMeleeHitFeedback(abilityName, other.transform.position);
    }

    /// <summary>
    /// Destroys the spawned meleeFX instance and cleans up this component.
    /// </summary>
    public void DestroyHitbox()
    {
        if (!destroyTriggersApplied)
        {
            config?.hitbox?.OnDestroy(hitboxInstance != null ? hitboxInstance : gameObject, statOwner ?? owner ?? gameObject);
            destroyTriggersApplied = true;
        }

        if (hitboxInstance != null)
        {
            Object.Destroy(hitboxInstance);
            hitboxInstance = null;
        }

        hitTargets.Clear();
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (!destroyTriggersApplied)
        {
            config?.hitbox?.OnDestroy(hitboxInstance != null ? hitboxInstance : gameObject, statOwner ?? owner ?? gameObject);
            destroyTriggersApplied = true;
        }

        // Safety: ensure the meleeFX instance is cleaned up if DestroyHitbox was not called
        if (hitboxInstance != null)
        {
            Object.Destroy(hitboxInstance);
            hitboxInstance = null;
        }
    }
}

/// <summary>
/// Helper component to forward trigger events to MeleeAbility
/// </summary>
public class TriggerHandler : MonoBehaviour
{
    public System.Action<Collider2D> onTriggerEnter;

    private void OnTriggerEnter2D(Collider2D other)
    {
        onTriggerEnter?.Invoke(other);
    }
}
