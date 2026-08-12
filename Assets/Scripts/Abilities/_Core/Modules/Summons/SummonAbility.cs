using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using JoeConticello.VisualEffects;

/// <summary>
/// Manages spawning and lifecycle of summoned pets.
/// Follows the same pattern as ConstructAbility for limit enforcement and network spawning.
/// </summary>
public class SummonAbility : MonoBehaviour, ISubAbility
{
    private SummonConfig config;
    private readonly List<GameObject> _ownedInstances = new List<GameObject>();
    private bool isDestroying;
    private List<GameObject> sharedSummonList;
    private GameObject owner;
    private AbilityDataConfig parentConfig;
    private AbilityDataConfig rawParentConfig;

    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        rawParentConfig = context.rawParentConfig;
        owner = context.owner;
    }

    /// <summary>
    /// Spawn a summoned pet at the specified position.
    /// </summary>
    public bool SpawnSummon(SummonConfig summonConfig, Vector3 spawnPosition, List<GameObject> sharedSummons = null)
    {
        config = summonConfig;
        sharedSummonList = sharedSummons ?? new List<GameObject>();

        if (config.summonPrefab == null)
        {
            Debug.LogError("[SummonAbility] No summon prefab assigned!");
            return false;
        }

        // Clean up null references
        sharedSummonList.RemoveAll(s => s == null);

        // Handle summon limits
        if (config.maxSummons > 0 && sharedSummonList.Count >= config.maxSummons)
        {
            bool shouldProceed = HandleSummonLimit(spawnPosition);
            if (!shouldProceed)
            {
                Debug.Log($"[SummonAbility] Cannot spawn summon — limit reached ({sharedSummonList.Count}/{config.maxSummons})");
                return false;
            }
        }

        // Instantiate the summon prefab
        GameObject summonInstance = Object.Instantiate(config.summonPrefab, spawnPosition, Quaternion.identity);
        summonInstance.name = $"{config.summonPrefab.name}_Summon";

        // Network spawn if in multiplayer
        var networkManager = InstanceFinder.NetworkManager;
        if (networkManager != null && networkManager.IsServerStarted)
        {
            networkManager.ServerManager.Spawn(summonInstance);
        }

        sharedSummonList.Add(summonInstance);
        _ownedInstances.Add(summonInstance);

        // Get or add SummonedPet component
        SummonedPet pet = summonInstance.GetComponent<SummonedPet>();
        if (pet == null)
        {
            pet = summonInstance.AddComponent<SummonedPet>();
        }

        pet.Initialize(config, owner, parentConfig, rawParentConfig);

        // Assign the per-slot offset so each summon follows a distinct position around the owner.
        if (config.slotOffsets != null && config.slotOffsets.Length > 0)
        {
            int slotIndex = (sharedSummonList.Count - 1) % config.slotOffsets.Length;
            pet.SetRelativeOffset(config.slotOffsets[slotIndex]);
        }

        // Spawn visual effect
        if (config.spawnEffectPrefab != null)
        {
            GameObject effect = Object.Instantiate(config.spawnEffectPrefab, spawnPosition, Quaternion.identity);
            if (effect.GetComponent<AutoDestroyEffect>() == null)
                Object.Destroy(effect, 3f);
        }

        // Play spawn animation if configured
        if (!string.IsNullOrEmpty(config.spawnAnimation))
        {
            Animator anim = summonInstance.GetComponent<Animator>();
            if (anim != null)
            {
                anim.Play(config.spawnAnimation, 0);
            }
        }

        Debug.Log($"[SummonAbility] Spawned '{summonInstance.name}' at {spawnPosition}. Total summons: {sharedSummonList.Count}/{config.maxSummons}");
        return true;
    }

    private bool HandleSummonLimit(Vector3 newSpawnPosition)
    {
        sharedSummonList.RemoveAll(s => s == null);

        switch (config.limitBehavior)
        {
            case SummonLimitBehavior.DestroyOldest:
                if (sharedSummonList.Count > 0)
                {
                    GameObject oldest = sharedSummonList[0];
                    sharedSummonList.RemoveAt(0);
                    _ownedInstances.Remove(oldest);
                    if (oldest != null)
                    {
                        SpawnDeathEffect(oldest.transform.position);
                        Destroy(oldest);
                    }
                }
                return true;

            case SummonLimitBehavior.PreventSpawn:
                return false;

            case SummonLimitBehavior.ReplaceClosest:
                GameObject closest = null;
                float closestDist = float.MaxValue;
                foreach (GameObject summon in sharedSummonList)
                {
                    if (summon == null) continue;
                    float dist = Vector3.Distance(summon.transform.position, newSpawnPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = summon;
                    }
                }
                if (closest != null)
                {
                    sharedSummonList.Remove(closest);
                    _ownedInstances.Remove(closest);
                    SpawnDeathEffect(closest.transform.position);
                    Destroy(closest);
                }
                return true;
        }

        return true;
    }

    private void Update()
    {
        if (isDestroying) return;
        _ownedInstances.RemoveAll(s => s == null);
    }

    public void DestroySummon()
    {
        if (isDestroying) return;
        isDestroying = true;

        foreach (GameObject s in _ownedInstances)
        {
            if (s == null) continue;
            sharedSummonList?.Remove(s);
            SpawnDeathEffect(s.transform.position);
            Destroy(s);
        }
        _ownedInstances.Clear();
    }

    private void OnDestroy()
    {
        foreach (GameObject s in _ownedInstances)
        {
            if (s == null) continue;
            sharedSummonList?.Remove(s);
            Destroy(s);
        }
    }

    private void SpawnDeathEffect(Vector3 position)
    {
        if (config != null && config.deathEffectPrefab != null)
        {
            GameObject effect = Object.Instantiate(config.deathEffectPrefab, position, Quaternion.identity);
            AutoDestroyEffect.SetupAutoDestroy(effect, 3f);
        }
    }

    public bool HasActiveSummons => _ownedInstances.Exists(s => s != null);
}
