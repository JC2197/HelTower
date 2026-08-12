using System.Collections.Generic;
using UnityEngine;

public class Aura : MonoBehaviour, ISubAbility
{
    private AreaConfig config;
    private float nextEffectTick;
    private float startTime;
    private bool isActive;
    private GameObject ownerGameObject;
    private GameObject spellPrefabInstance;
    private Collider2D auraCollider;
    private AbilityDataConfig parentConfig;
    private bool destroyTriggersApplied;

    public AbilityDataConfig ParentConfig => parentConfig;

    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        ownerGameObject = context.owner;
    }

    public void Initialize(AreaConfig areaConfig)
    {
        config = areaConfig;
        if (config == null)
            return;

        if (config.enabled)
        {
            startTime = Time.time + config.auraDelay;
            nextEffectTick = startTime + config.damageInterval;
        }

        // Apply scale directly to the spell prefab instance so particle systems
        // in Local scaling mode see the correct localScale rather than relying
        // on parent hierarchy (which Local-mode particles ignore).
        transform.localScale = Vector3.one;

        if (config.hitbox.prefab != null)
        {
            spellPrefabInstance = Instantiate(config.hitbox.prefab, transform);
            spellPrefabInstance.transform.localPosition = new Vector3(config.offset.x, config.offset.y, 0f);
            spellPrefabInstance.transform.localRotation = Quaternion.identity;
            spellPrefabInstance.transform.localScale = new Vector3(
                config.hitbox.scaleX > 0f ? config.hitbox.scaleX : 1f,
                config.hitbox.scaleY > 0f ? config.hitbox.scaleY : 1f, 1f);
            spellPrefabInstance.name = "AuraEffect";

            auraCollider = spellPrefabInstance.GetComponentInChildren<Collider2D>();
            if (auraCollider != null)
                auraCollider.isTrigger = true;

            ParticleSystem[] particleSystems = spellPrefabInstance.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in particleSystems)
                ps.Play();
        }
    }

    private void Update()
    {
        if (config == null || !config.enabled)
            return;

        if (!isActive && Time.time >= startTime)
            isActive = true;

        if (isActive && Time.time >= nextEffectTick)
        {
            if (config.hasDamageTick) PlayTickPulseEffect();
            ApplyEffects();
            nextEffectTick = Time.time + config.damageInterval;
        }
    }

    private void PlayTickPulseEffect()
    {
        EnsureSpellPrefabInstance();
        if (spellPrefabInstance == null)
            return;

        ParticleSystem[] particleSystems = spellPrefabInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in particleSystems)
            ps.Play();
    }

    private void EnsureSpellPrefabInstance()
    {
        if (spellPrefabInstance != null || config?.hitbox.prefab == null)
            return;

        spellPrefabInstance = Instantiate(config.hitbox.prefab, transform);
        spellPrefabInstance.transform.localPosition = new Vector3(config.offset.x, config.offset.y, 0f);
        spellPrefabInstance.transform.localRotation = Quaternion.identity;
        spellPrefabInstance.transform.localScale = new Vector3(
            config.hitbox.scaleX > 0f ? config.hitbox.scaleX : 1f,
            config.hitbox.scaleY > 0f ? config.hitbox.scaleY : 1f, 1f);
        spellPrefabInstance.name = "AuraEffect";

        auraCollider = spellPrefabInstance.GetComponentInChildren<Collider2D>();
        if (auraCollider != null)
            auraCollider.isTrigger = true;
    }

    private void ApplyEffects()
    {
        Collider2D[] hits = GetHitsInArea();
        if (hits == null)
            return;

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
                continue;

            bool canNegative = config.hitbox != null && config.hitbox.IsNegativeTarget(hit.gameObject);
            bool canPositive = config.hitbox != null && config.hitbox.IsPositiveTarget(hit.gameObject);
            if (!canNegative && !canPositive)
                continue;

            if (canNegative)
            {
                ApplyDamage(hit);
                ApplyOnHitEffects(hit);
            }

            if (canPositive)
            {
                ApplyHealing(hit);
                ApplyBuffEffects(hit);
            }
        }
    }

    private void ApplyOnHitEffects(Collider2D hit)
    {
        GameObject owner = ownerGameObject != null ? ownerGameObject : gameObject;
        config.hitbox?.ApplyOnHitEffects(hit.gameObject, gameObject, owner);
    }

    private void ApplyBuffEffects(Collider2D hit)
    {
        GameObject owner = ownerGameObject != null ? ownerGameObject : gameObject;
        config.hitbox?.ApplyBuffEffects(hit.gameObject, gameObject, owner);
    }

    private void ApplyDamage(Collider2D hit)
    {
        if (config.hitbox == null)
            return;

        GameObject attacker = ownerGameObject != null ? ownerGameObject : gameObject;
        // Reusable hitbox damage (trait scaling, crit, life steal, healing, weapon damage).
        config.hitbox.ApplyDamage(hit, attacker, attacker, ownerGameObject ?? attacker, transform.position,
            parentConfig?.abilityName, parentConfig?.abilityTags?.GetAllTags(), parentConfig);
        HitVisualHelper.SpawnHitVisual(parentConfig, hit.transform.position, hit);
    }

    private void ApplyHealing(Collider2D hit)
    {
        if (config.hitbox == null)
            return;

        GameObject attacker = ownerGameObject != null ? ownerGameObject : gameObject;
        config.hitbox.ApplyHealing(hit, attacker, attacker, ownerGameObject ?? attacker, transform.position,
            parentConfig?.abilityName, parentConfig?.abilityTags?.GetAllTags(), parentConfig);
    }

    private Collider2D[] GetHitsInArea()
    {
        if (auraCollider == null)
            return new Collider2D[0];

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(config.hitbox != null ? config.hitbox.GetCombinedHitLayers() : 0);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        List<Collider2D> results = new List<Collider2D>(20);
        Physics2D.OverlapCollider(auraCollider, filter, results);
        return results.ToArray();
    }

    public bool IsActive => isActive;
    public AreaConfig Config => config;
    public AreaConfig TraitConfig { get; set; }

    private void OnDestroy()
    {
        if (destroyTriggersApplied)
            return;

        if (config?.hitbox != null)
        {
            GameObject triggerOwner = ownerGameObject != null ? ownerGameObject : gameObject;
            config.hitbox.OnDestroy(gameObject, triggerOwner);
        }

        destroyTriggersApplied = true;
    }

}
