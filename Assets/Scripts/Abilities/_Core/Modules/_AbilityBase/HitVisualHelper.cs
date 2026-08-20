using UnityEngine;
using JoeConticello.VisualEffects;
using FishNet.Object;
/// <summary>
/// Centralized hit visual spawner. Any ability type calls this when it damages a target.
/// Reads hitVisualPrefab, hitVisualSound, and hitFlashColor from AbilityDataConfig.
/// </summary>
public static class HitVisualHelper
{

    /// <summary>
    /// Overload that accepts a GameObject target instead of Collider2D.
    /// </summary>
    public static void SpawnHitVisual(AbilityDataConfig config, Vector3 position, GameObject target, Quaternion? rotation = null)
    {
        Collider2D col = null;
        if (target != null)
        {
            col = target.GetComponent<Collider2D>();
            if (col == null)
                col = target.GetComponentInChildren<Collider2D>(true);
        }

        SpawnHitVisual(config, position, col, rotation);
    }

    public static void SpawnHitVisual(AbilityDataConfig config, Vector3 position, Collider2D target = null, Quaternion? rotation = null)
    {
        if (config == null) return;
        SpawnEffect(
            config.hitVisualPrefab,
            position,
            rotation ?? Quaternion.identity,
            sortAndSizeTarget: target,
            sound: config.hitVisualSound
        );
    }

    public static GameObject SpawnEffect(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null,
        Vector3 localOffset = default,
        bool disableColliders = false,
        Collider2D sortAndSizeTarget = null,
        AudioClip sound = null,
        bool autoDestroy = true)
    {
        if (prefab == null) return null;
        GameObject effect = parent != null
            ? Object.Instantiate(prefab, parent)
            : Object.Instantiate(prefab, position, rotation);
        if (parent != null)
            effect.transform.localPosition = localOffset;

        if (disableColliders)
        {
            foreach (Collider2D col in effect.GetComponentsInChildren<Collider2D>(true))
            {
                col.enabled = false;
            }
        }

        if (sortAndSizeTarget != null)
        {
            SortAndSizeEffect(effect, sortAndSizeTarget);
        }
        if (autoDestroy)
        {
            float destroyDelay = AutoDestroyEffect.CalculateLifetime(effect);
            AutoDestroyEffect.SetupAutoDestroy(effect);
        }

        if (sound != null)
        {
            AudioManager.Instance?.PlaySpatialSound(sound, position, 1f, Random.Range(0.9f, 1.1f));
        }
        return effect;
    }



    //This is for effect manager to put status effects onto characters/enemies and size them to the enemy.

    private static void SortAndSizeEffect(GameObject effect, Collider2D sortAndSizeTarget)
    {
        SpriteRenderer targetRenderer = sortAndSizeTarget.GetComponent<SpriteRenderer>();
        if (targetRenderer == null) targetRenderer = sortAndSizeTarget.GetComponentInParent<SpriteRenderer>();
        if (targetRenderer == null) targetRenderer = sortAndSizeTarget.GetComponentInChildren<SpriteRenderer>();
        if (targetRenderer == null) return;
        string sortingLayer = targetRenderer.sortingLayerName;
        int sortingOrder = targetRenderer.sortingOrder;
        SpriteRenderer effectRenderer = effect.GetComponent<SpriteRenderer>();
        if (effectRenderer != null)
        {
            effectRenderer.sortingLayerName = sortingLayer;
            effectRenderer.sortingOrder = sortingOrder;
        }

        Bounds targetBounds = targetRenderer.sprite != null ? targetRenderer.bounds : sortAndSizeTarget.bounds;

        foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sortingLayerName = sortingLayer;
                psr.sortingOrder = sortingOrder + 10000;
            }

            var shape = ps.shape;
            if (!shape.enabled) continue;

            if (shape.shapeType == ParticleSystemShapeType.SingleSidedEdge)
                shape.scale = new Vector3(targetBounds.size.x, targetBounds.size.y, 1f);
            else if (targetRenderer.sprite != null)
            {
                shape.shapeType = ParticleSystemShapeType.Sprite;
                shape.sprite = targetRenderer.sprite;
            }
        }

    }

}

/// <summary>
/// Shared resolver for the "network path" every hitbox-ish ObserversRpc uses: look up the
/// attacking Organism's ability by name and get its (identical-on-every-client) AbilityDataConfig,
/// instead of relying on instance fields that are only populated on the machine that spawned the
/// object locally. Call this from an ObserversRpc body, then hand the result to HitVisualHelper.SpawnEffect.
/// </summary>
public static class NetworkVisualEffects
{
    public static AbilityDataConfig ResolveAbilityConfig(NetworkObject ownerNob, string abilityName)
    {
        return ownerNob != null ? ResolveAbilityConfig(ownerNob.GetComponent<Organism>(), abilityName) : null;
    }

    public static AbilityDataConfig ResolveAbilityConfig(Organism owner, string abilityName)
    {
        return owner != null ? owner.FindDataDrivenAbilityByName(abilityName)?.EffectiveAbilityConfig : null;
    }
}
