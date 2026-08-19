using UnityEngine;
using JoeConticello.VisualEffects;
/// <summary>
/// Centralized hit visual spawner. Any ability type calls this when it damages a target.
/// Reads hitVisualPrefab, hitVisualSound, and hitFlashColor from AbilityDataConfig.
/// </summary>
public static class HitVisualHelper
{



    /// <summary>
    /// Spawn hit visual and play hit sound at the target position using the ability's config.
    /// Call this from any ability type after dealing damage to a target.
    /// </summary>
    /// <param name="config">The ability's AbilityDataConfig (provides prefab, sound, flash color).</param>
    /// <param name="position">World position of the hit (typically the target's transform.position).</param>
    /// <param name="target">The hit target's Collider2D (used for sorting order). Can be null.</param>
    public static void SpawnHitVisual(AbilityDataConfig config, Vector3 position, Collider2D target = null, Quaternion? rotation = null)
    {
        if (config == null) return;

        if (config.hitVisualPrefab != null)
        {
            GameObject effect = Object.Instantiate(config.hitVisualPrefab, position, rotation ?? Quaternion.identity);

            // Match sorting to target so the effect renders in front
            if (target != null)
            {
                SpriteRenderer targetRenderer = target.GetComponent<SpriteRenderer>();
                if (targetRenderer == null)
                    targetRenderer = target.GetComponentInParent<SpriteRenderer>();
                if (targetRenderer == null)
                    targetRenderer = target.GetComponentInChildren<SpriteRenderer>(true);

                if (targetRenderer != null)
                {
                    string sortingLayer = targetRenderer.sortingLayerName;
                    int sortingOrder = targetRenderer.sortingOrder + 1;

                    SpriteRenderer effectRenderer = effect.GetComponent<SpriteRenderer>();
                    if (effectRenderer != null)
                    {
                        effectRenderer.sortingLayerName = sortingLayer;
                        effectRenderer.sortingOrder = sortingOrder;
                    }

                    ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (ParticleSystem ps in particles)
                    {
                        ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                        if (psr != null)
                        {
                            psr.sortingLayerName = sortingLayer;
                            psr.sortingOrder = sortingOrder + 10000;
                        }
                    }
                }
            }

            // Auto-destroy based on particle/animation duration
            float maxDuration = 0f;
            ParticleSystem[] effectParticles = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in effectParticles)
            {
                var main = ps.main;
                float duration = main.duration + main.startLifetime.constantMax;
                if (duration > maxDuration) maxDuration = duration;
            }
            Animator anim = effect.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
                {
                    if (clip.length > maxDuration) maxDuration = clip.length;
                }
            }
            float destroyDelay = Mathf.Clamp(maxDuration, 0.5f, 5f);
            Object.Destroy(effect, destroyDelay);
        }

        if (config.hitVisualSound != null)
        {
            AudioManager.Instance?.PlaySpatialSound(config.hitVisualSound, position, 1f, Random.Range(0.9f, 1.1f));
        }
    }

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
            null,
            Vector3.zero,
            false,
            target,
            config.hitVisualSound
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

        Bounds targetBounds = targetRenderer.sprite != null ? targetRenderer.bounds : target.bounds;

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
