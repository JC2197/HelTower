using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using JoeConticello.VisualEffects;

/// <summary>
/// Beam ability runtime using BeamRenderer pulses for visuals.
/// Start point comes from LaunchZone (or player fallback).
/// Endpoint is either cursor-driven or enemy auto-targeted.
/// </summary>
public class BeamAbility : MonoBehaviour, ISubAbility
{
    private BeamAbilityConfig beamConfig;
    private AbilityDataConfig parentConfig;
    private GameObject statOwner; // The player owner (differs from gameObject when fired by a summon)
    private PlayerController playerController;
    private Transform launchZone;

    private bool isBeamActive;
    private float energyConsumptionTimer;
    private float beamLifetime;

    private GameObject activeBeamGO;
    private BeamRenderer activeBeamRenderer;
    private readonly List<GameObject> activeChainBeamGOs = new List<GameObject>();
    private readonly List<BeamRenderer> activeChainBeamRenderers = new List<BeamRenderer>();
    private Enemy lockedTarget;
    private Vector3 lockedEndpoint;
    private bool hasLockedEndpoint;
    private bool singleShotDamageDealt;
    private bool _isExtraBeam;
    private readonly List<BeamAbility> _extraBeams = new List<BeamAbility>();

    private Vector3 beamEndPosition;

    private readonly Dictionary<Organism, float> organismHitTimers = new Dictionary<Organism, float>();

    private ParticleSystem muzzleFlash;
    private GameObject muzzleFlashLight;

    private GameObject impactEffect;
    private Animator impactAnimator;
    private GameObject impactParticles;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        launchZone = transform.Find("Weapon/LaunchZone");
    }

    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        statOwner = context.statOwner;
    }

    public void Initialize(AbilityDataConfig config)
    {
        parentConfig = config;
        beamConfig = config != null ? config.beamConfig : null;

        LogDebug($"Initialize called. beamConfig={(beamConfig != null ? "set" : "null")}, autocast={(parentConfig != null && parentConfig.autocast)}");

        if (beamConfig == null)
        {
            Debug.LogError("[beamability] No BeamAbilityConfig found in AbilityDataConfig!", this);
        }
    }

    public bool Activate()
    {
        if (beamConfig == null)
        {
            Debug.LogError("[beamability] Activate failed. beamConfig is null.", this);
            return false;
        }

        if (isBeamActive)
        {
            LogDebug("Activate ignored because beam is already active.");
            return true;
        }

        LogDebug($"Activate accepted. targetingMode={beamConfig.targetingMode}, holdToFire={beamConfig.canHoldToFire}");

        StartBeam();
        return true;
    }

    public bool IsHoldingButton()
    {
        return beamConfig != null && beamConfig.canHoldToFire && IsAbilityButtonHeld();
    }

    private bool IsAutocast => parentConfig != null && parentConfig.autocast;

    private void Update()
    {
        if (!isBeamActive || beamConfig == null)
            return;

        beamLifetime += Time.deltaTime;

        Vector3 start = GetBeamStartPosition();
        Vector3 desiredTarget = ResolveActiveTarget(start, out Enemy autoTargetEnemy);
        ComputeBeamEndpoint(start, desiredTarget, autoTargetEnemy, out Vector3 end, out Vector3 direction);

        if (IsAutocast && autoTargetEnemy != null)
        {
            lockedEndpoint = end;
            hasLockedEndpoint = true;
        }

        beamEndPosition = end;

        UpdateActiveRenderer(start, beamEndPosition);
        UpdateImpactEffect(beamEndPosition, direction);
        UpdateImpactParticles(beamEndPosition, direction);
        ApplyBeamDamage(start, beamEndPosition, direction);

        if (beamConfig.channelCostPerSecond > 0f && beamConfig.canHoldToFire)
        {
            energyConsumptionTimer += Time.deltaTime;
            float energyToConsume = beamConfig.channelCostPerSecond * energyConsumptionTimer;

            if (energyToConsume >= 1f)
            {
                if (playerController != null && playerController.CurrentEnergy >= energyToConsume)
                {
                    playerController.ModifyEnergy(-energyToConsume);
                    energyConsumptionTimer = 0f;
                }
                else
                {
                    StopBeam("Insufficient energy during channel consumption.");
                    return;
                }
            }
        }

        bool isAutocast = IsAutocast;
        if (beamConfig.canHoldToFire && !isAutocast)
        {
            if (!IsAbilityButtonHeld())
                StopBeam("Hold-to-fire released.");
        }
        else if (isAutocast && beamConfig.beamRendererPrefab != null && activeBeamGO == null && !HasActiveChainRenderers())
        {
            StopBeam("BeamRenderer completed.");
        }
        else if (isAutocast && beamConfig.beamRendererPrefab == null && beamLifetime >= Mathf.Max(0.05f, beamConfig.singleShotDuration))
        {
            StopBeam("Single-shot duration reached (no renderer).");
        }
    }

    private void StartBeam()
    {
        if (launchZone == null)
            launchZone = transform.Find("Weapon/LaunchZone");

        isBeamActive = true;
        energyConsumptionTimer = 0f;
        beamLifetime = 0f;
        organismHitTimers.Clear();
        // Preserve a pre-locked target (set via SetLockedTarget for multi-beam extras).
        if (!_isExtraBeam) lockedTarget = null;
        hasLockedEndpoint = _isExtraBeam && lockedTarget != null;
        singleShotDamageDealt = false;

        Vector3 start = GetBeamStartPosition();
        Enemy initialEnemy = null;
        Vector3 initialTarget;

        if (IsAutocast)
        {
            // Extra beams have their target pre-locked via SetLockedTarget — skip the search.
            if (lockedTarget == null)
                initialEnemy = FindAutoTargetEnemy(start, start);
            else
                initialEnemy = lockedTarget;
            initialTarget = initialEnemy != null ? initialEnemy.transform.position : start;
        }
        else
        {
            initialTarget = ResolveDesiredTarget(start, out initialEnemy);
        }

        // For autocast, lock onto whichever enemy was found at cast time.
        if (IsAutocast)
        {
            lockedTarget = initialEnemy;
            lockedEndpoint = initialTarget;
            hasLockedEndpoint = true;
            LogDebug($"StartBeam autocast locked to target={(lockedTarget != null ? lockedTarget.name : "none")}");
        }

        Vector3 direction = (initialTarget - start).sqrMagnitude > 0.0001f ? (initialTarget - start).normalized : Vector3.right;

        LogDebug($"StartBeam start={start} initialTarget={initialTarget} direction={direction}");

        SpawnActiveRenderer(start, initialTarget, IsAutocast);
        InitializeMuzzleFlash(start, direction);
        EnableMuzzleFlash();

        // Spawn extra beams for multi-beam autocast (primary beam only, not recursive).
        if (IsAutocast && !_isExtraBeam && beamConfig.beamAmount > 1)
            SpawnExtraBeams(start);
    }

    private void StopBeam(string reason = "")
    {
        if (!isBeamActive)
            return;

        if (!string.IsNullOrEmpty(reason))
            LogDebug($"StopBeam reason: {reason}");
        else
            LogDebug("StopBeam called.");

        isBeamActive = false;
        organismHitTimers.Clear();
        lockedTarget = null;
        hasLockedEndpoint = false;

        if (activeBeamRenderer != null)
            activeBeamRenderer.TriggerEnd();
        activeBeamGO = null;
        activeBeamRenderer = null;
        ClearChainRenderers();

        DisableMuzzleFlash();
        HideImpactEffects();

        // Destroy extra beam instances spawned for multi-beam.
        for (int i = 0; i < _extraBeams.Count; i++)
        {
            if (_extraBeams[i] != null)
                Destroy(_extraBeams[i]);
        }
        _extraBeams.Clear();
    }

    /// <summary>
    /// Pre-lock this beam to a specific enemy before Activate() is called.
    /// Used by SpawnExtraBeams so each extra beam targets a unique enemy.
    /// </summary>
    public void SetLockedTarget(Enemy target)
    {
        lockedTarget = target;
        lockedEndpoint = target != null ? target.transform.position : transform.position;
        hasLockedEndpoint = target != null;
    }

    /// <summary>
    /// Spawns (beamAmount - 1) extra BeamAbility components, each locked to a unique enemy
    /// within the multiBeamAngle cone centred on the primary beam direction.
    /// Each extra beam is a full clone: same chain, bounce, damage, renderer config.
    /// </summary>
    private void SpawnExtraBeams(Vector3 start)
    {
        if (lockedTarget == null) return;

        Vector3 primaryDir = (lockedEndpoint - start).sqrMagnitude > 0.0001f
            ? (lockedEndpoint - start).normalized
            : Vector3.right;

        bool fullCircle = beamConfig.multiBeamAngle >= 360f;
        float halfAngle = beamConfig.multiBeamAngle * 0.5f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(start, beamConfig.maxBeamDistance, beamConfig.hitLayers);
        List<Enemy> candidates = new List<Enemy>();
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            Enemy e = hits[i].GetComponentInParent<Enemy>();
            if (e == null || !e.IsAlive || e == lockedTarget) continue;

            if (!fullCircle)
            {
                float angle = Vector3.Angle(primaryDir, (e.transform.position - start).normalized);
                if (angle > halfAngle) continue;
            }
            candidates.Add(e);
        }

        candidates.Sort((a, b) =>
            Vector3.Distance(start, a.transform.position)
                .CompareTo(Vector3.Distance(start, b.transform.position)));

        int extraCount = Mathf.Min(beamConfig.beamAmount - 1, candidates.Count);
        for (int i = 0; i < extraCount; i++)
        {
            BeamAbility extra = gameObject.AddComponent<BeamAbility>();
            extra._isExtraBeam = true;
            extra.SetContext(new SubAbilityContext
            {
                parentConfig = parentConfig,
                owner = gameObject,
                statOwner = statOwner
            });
            extra.Initialize(parentConfig);
            extra.SetLockedTarget(candidates[i]);
            extra.Activate();
            _extraBeams.Add(extra);
        }
    }

    /// <summary>
    /// For autocast beams returns the locked target position every frame.
    /// For manual/hold beams resolves freely each frame.
    /// </summary>
    private Vector3 ResolveActiveTarget(Vector3 start, out Enemy autoTargetEnemy)
    {
        if (IsAutocast && lockedTarget != null)
        {
            autoTargetEnemy = lockedTarget.IsAlive ? lockedTarget : null;
            if (autoTargetEnemy != null)
                return lockedTarget.transform.position;

            // Locked target died mid-beam; keep the last endpoint so visuals play out.
            lockedTarget = null;
            autoTargetEnemy = null;
            return hasLockedEndpoint ? lockedEndpoint : start;
        }

        if (IsAutocast)
        {
            // Autocast never falls back to cursor.
            autoTargetEnemy = null;
            return hasLockedEndpoint ? lockedEndpoint : start;
        }

        return ResolveDesiredTarget(start, out autoTargetEnemy);
    }

    private Vector3 ResolveDesiredTarget(Vector3 start, out Enemy autoTargetEnemy)
    {
        Vector3 cursorWorld = InputUtility.GetMouseWorldPositionClamped(start, beamConfig.maxBeamDistance);
        autoTargetEnemy = null;

        if (beamConfig.targetingMode == BeamTargetingMode.AutoTargetEnemy)
        {
            autoTargetEnemy = FindAutoTargetEnemy(cursorWorld, start);
            if (autoTargetEnemy != null)
            {
                LogVerbose($"Auto-target acquired: {autoTargetEnemy.name} at {autoTargetEnemy.transform.position}");
                return autoTargetEnemy.transform.position;
            }

            if (beamConfig.fallbackToCursorWhenNoEnemy)
            {
                LogVerbose($"Auto-target miss. Falling back to cursor at {cursorWorld}");
                return cursorWorld;
            }

            LogVerbose("Auto-target miss with no fallback. Returning start position.");
            return start;
        }

        return cursorWorld;
    }

    private Enemy FindAutoTargetEnemy(Vector3 cursorWorld, Vector3 start)
    {
        float radius = Mathf.Max(0.1f, beamConfig.trackingRadius);
        Collider2D[] nearCursor = Physics2D.OverlapCircleAll(cursorWorld, radius, beamConfig.hitLayers);

        Enemy best = PickClosestLivingEnemy(nearCursor, cursorWorld);
        if (best != null)
            return best;

        Collider2D[] nearStart = Physics2D.OverlapCircleAll(start, beamConfig.maxBeamDistance, beamConfig.hitLayers);
        return PickClosestLivingEnemy(nearStart, start);
    }

    private static Enemy PickClosestLivingEnemy(Collider2D[] colliders, Vector3 from)
    {
        Enemy best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Enemy enemy = colliders[i] != null ? colliders[i].GetComponentInParent<Enemy>() : null;
            if (enemy == null || !enemy.IsAlive)
                continue;

            float dist = Vector3.Distance(from, enemy.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        return best;
    }

    private void ComputeBeamEndpoint(
        Vector3 start,
        Vector3 desiredTarget,
        Enemy autoTargetEnemy,
        out Vector3 end,
        out Vector3 direction)
    {
        Vector3 toTarget = desiredTarget - start;
        direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.right;

        float desiredDistance = toTarget.magnitude;
        float beamReach = Mathf.Min(desiredDistance, beamConfig.maxBeamDistance);

        // Exclude the caster's own layer so the raycast does not immediately
        // hit the player's own collider from the LaunchZone origin.
        LayerMask castMask = beamConfig.hitLayers & ~(1 << gameObject.layer);
        RaycastHit2D hit = Physics2D.Raycast(start, direction, beamReach, castMask);

        if (hit.collider != null)
        {
            Enemy hitEnemy = hit.collider.GetComponentInParent<Enemy>();
            if (autoTargetEnemy != null && hitEnemy == autoTargetEnemy)
                end = autoTargetEnemy.transform.position;
            else
                end = hit.point;

            LogVerbose($"Beam endpoint ray hit {hit.collider.name}. end={end}");
        }
        else
        {
            end = start + direction * beamReach;
            LogVerbose($"Beam endpoint no hit. end={end} reach={beamReach}");
        }
    }

    private void SpawnActiveRenderer(Vector3 start, Vector3 end, bool isAutocast)
    {
        if (beamConfig.beamRendererPrefab == null)
            return;

        bool shouldLoop = beamConfig.canHoldToFire && !isAutocast;

        activeBeamGO = Instantiate(beamConfig.beamRendererPrefab, end, Quaternion.identity);
        activeBeamRenderer = activeBeamGO.GetComponent<BeamRenderer>()
                          ?? activeBeamGO.GetComponentInChildren<BeamRenderer>(true);

        if (activeBeamRenderer == null)
        {
            Debug.LogWarning("[beamrenderer] beamRendererPrefab has no BeamRenderer component.", this);
            Destroy(activeBeamGO);
            activeBeamGO = null;
            return;
        }

        activeBeamRenderer.SetLooping(shouldLoop);
        activeBeamRenderer.SetStartPoint(start);
        LogDebug($"[beamrenderer] Spawned BeamRenderer at end={end}, looping={shouldLoop}");
    }

    private void UpdateActiveRenderer(Vector3 start, Vector3 end)
    {
        if (activeBeamGO == null || activeBeamRenderer == null)
            return;

        activeBeamGO.transform.position = end;
        activeBeamRenderer.UpdateGeometry(start);
    }

    private void ApplyBeamDamage(Vector3 start, Vector3 end, Vector3 direction)
    {
        bool isChanneled = beamConfig.canHoldToFire && !IsAutocast;

        if (isChanneled)
            ApplyChanneledDamage(start, end, direction);
        else
            ApplySingleShotDamage(start, end, direction);
    }

    // Single-shot: deal configured beam damage once per activation.
    private void ApplySingleShotDamage(Vector3 start, Vector3 end, Vector3 direction)
    {
        if (singleShotDamageDealt)
            return;

        singleShotDamageDealt = true;

        if (lockedTarget == null || !lockedTarget.IsAlive)
        {
            LogDebug("Single-shot: no valid locked target, skipping damage.");
            return;
        }

        if (ShouldChain())
        {
            ChainBuildResult chainResult = BuildChainTargets(lockedTarget);
            RenderSingleShotChainVisuals(chainResult.links);
            for (int i = 0; i < chainResult.targets.Count; i++)
            {
                Organism target = chainResult.targets[i];
                LogDebug($"Single-shot chain hit -> {target.name}, value={beamConfig.value:F2}");
                DealBeamHit(target, beamConfig.value);
            }
            return;
        }

        LogDebug($"Single-shot hit -> {lockedTarget.name}, value={beamConfig.value:F2}");
        DealBeamHit(lockedTarget, beamConfig.value);
    }

    private bool ShouldChain()
    {
        return beamConfig != null && beamConfig.chainAmount > 0;
    }

    private struct ChainLink
    {
        public Organism from;
        public Organism to;
    }

    private class ChainBuildResult
    {
        public readonly List<Organism> targets = new List<Organism>();
        public readonly List<ChainLink> links = new List<ChainLink>();
    }

    private struct ChainWorkItem
    {
        public Organism source;
        public int remainingBudget;
    }

    private ChainBuildResult BuildChainTargets(Organism initialTarget)
    {
        ChainBuildResult result = new ChainBuildResult();
        if (initialTarget == null || !initialTarget.IsAlive)
            return result;

        HashSet<Organism> usedTargets = new HashSet<Organism> { initialTarget };
        Queue<ChainWorkItem> frontier = new Queue<ChainWorkItem>();

        result.targets.Add(initialTarget);
        frontier.Enqueue(new ChainWorkItem
        {
            source = initialTarget,
            remainingBudget = Mathf.Max(0, beamConfig.chainAmount)
        });

        while (frontier.Count > 0)
        {
            ChainWorkItem item = frontier.Dequeue();
            if (item.source == null || !item.source.IsAlive || item.remainingBudget <= 0)
                continue;

            List<Organism> nextTargets = FindChainTargetsInRange(item.source, usedTargets, item.remainingBudget);
            int spawnCount = nextTargets.Count;
            if (spawnCount <= 0)
                continue;

            int remainingAfterSpawn = Mathf.Max(0, item.remainingBudget - spawnCount);
            int carryBase = remainingAfterSpawn / spawnCount;
            int carryExtra = remainingAfterSpawn % spawnCount;

            for (int i = 0; i < spawnCount; i++)
            {
                Organism child = nextTargets[i];
                if (child == null || !child.IsAlive || !usedTargets.Add(child))
                    continue;

                result.targets.Add(child);
                result.links.Add(new ChainLink { from = item.source, to = child });

                int childBudget = carryBase + (i < carryExtra ? 1 : 0);
                if (childBudget > 0)
                {
                    frontier.Enqueue(new ChainWorkItem
                    {
                        source = child,
                        remainingBudget = childBudget
                    });
                }
            }
        }

        return result;
    }

    private List<Organism> FindChainTargetsInRange(Organism fromOrganism, HashSet<Organism> excluded, int maxTargets)
    {
        List<Organism> candidates = new List<Organism>();
        if (fromOrganism == null || maxTargets <= 0)
            return candidates;

        float radius = Mathf.Max(0.1f, beamConfig.maxBeamDistance);
        Collider2D[] nearby = Physics2D.OverlapCircleAll(fromOrganism.transform.position, radius, GetBeamTargetMask());

        for (int i = 0; i < nearby.Length; i++)
        {
            Organism candidate = nearby[i] != null ? nearby[i].GetComponentInParent<Organism>() : null;
            if (candidate == null || !candidate.IsAlive || excluded.Contains(candidate))
                continue;

            candidates.Add(candidate);
        }

        candidates.Sort((a, b) =>
        {
            float distA = Vector3.Distance(fromOrganism.transform.position, a.transform.position);
            float distB = Vector3.Distance(fromOrganism.transform.position, b.transform.position);
            return distA.CompareTo(distB);
        });

        if (candidates.Count > maxTargets)
            candidates.RemoveRange(maxTargets, candidates.Count - maxTargets);

        return candidates;
    }

    // Channeled: apply configured beam damage each tick at hitsPerSecond frequency.
    private void ApplyChanneledDamage(Vector3 start, Vector3 end, Vector3 direction)
    {
        float deltaTime = Time.deltaTime;
        float timeBetweenHits = 1f / Mathf.Max(0.1f, beamConfig.hitsPerSecond);
        float valuePerTick = beamConfig.value;

        float beamLength = Vector3.Distance(start, end);
        Vector3 center = (start + end) * 0.5f;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        HashSet<Organism> directHits = new HashSet<Organism>();
        Organism primaryTarget = null;
        float primaryDist = float.MaxValue;

        LayerMask boxCastMask = GetBeamTargetMask() & ~(1 << gameObject.layer);
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            center,
            new Vector2(Mathf.Max(0.01f, beamLength), Mathf.Max(0.01f, beamConfig.beamWidth)),
            angle,
            Vector2.zero,
            0f,
            boxCastMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Organism organism = hits[i].collider != null ? hits[i].collider.GetComponentInParent<Organism>() : null;
            if (organism == null || !organism.IsAlive)
                continue;

            directHits.Add(organism);

            float dist = Vector3.Distance(start, organism.transform.position);
            if (dist < primaryDist)
            {
                primaryDist = dist;
                primaryTarget = organism;
            }
        }

        HashSet<Organism> currentHits;
        ChainBuildResult chainResult = null;
        if (ShouldChain() && primaryTarget != null)
        {
            chainResult = BuildChainTargets(primaryTarget);
            currentHits = new HashSet<Organism>(chainResult.targets);
        }
        else
        {
            currentHits = directHits;
        }

        if (ShouldChain() && chainResult != null)
            SyncChainedRenderers(chainResult.links, beamConfig.canHoldToFire && !IsAutocast);
        else
            ClearChainRenderers();

        foreach (Organism organism in currentHits)
        {
            if (organism == null || !organism.IsAlive)
                continue;

            // Start new targets at 0 so first hit occurs after one normal tick interval.
            // This keeps damage timing visually aligned with beam startup animation.
            float timer = organismHitTimers.TryGetValue(organism, out float existing) ? existing : 0f;
            timer += deltaTime;

            if (timer >= timeBetweenHits)
            {
                DealBeamHit(organism, valuePerTick);
                timer = 0f;
            }

            organismHitTimers[organism] = timer;
        }

        List<Organism> toRemove = new List<Organism>();
        foreach (Organism tracked in organismHitTimers.Keys)
        {
            if (!currentHits.Contains(tracked))
                toRemove.Add(tracked);
        }

        for (int i = 0; i < toRemove.Count; i++)
            organismHitTimers.Remove(toRemove[i]);
    }

    private void DealBeamHit(Organism organism, float value)
    {
        if (organism == null || !organism.IsAlive)
            return;

        if (ShouldHealTarget(organism))
        {
            organism.Heal(value);
            LogVerbose($"Beam tick -> heal target={organism.name}, value={value:F2}");
            ApplyBeamOnHitEffects(organism);
            SpawnBeamHitVisual(organism);
            return;
        }

        IDamageable damageable = organism;
        if (damageable == null)
            return;

        DamageContext damageContext = DamageCalculator.CalculateDamageWithTraitEffects(
            value,
            beamConfig.damageTypeName,
            parentConfig?.abilityName,
            parentConfig?.abilityTags?.GetAllTags(),
            gameObject,
            organism.gameObject,
            organism.transform.position,
            parentConfig
        );

        damageable.TakeDamage(
            damageContext.FinalDamage,
            beamConfig.damageTypeName,
            organism.transform.position,
            beamConfig.hitFlashColor,
            gameObject,
            damageContext.CritMultiplier);

        PlayerController attackerPlayer = (statOwner ?? gameObject).GetComponent<PlayerController>();
        attackerPlayer?.NotifyAttackDamage(parentConfig, organism.gameObject, damageContext.FinalDamage, beamConfig.damageTypeName);

        // Life steal — use statOwner (player) when fired by a summon, otherwise this gameObject
        LifeStealProcessor.Apply(beamConfig.lifeSteal, damageContext.FinalDamage, statOwner ?? gameObject);

        LogVerbose($"Beam tick -> damage target={organism.name}, value={value:F2}, final={damageContext.FinalDamage:F2}, critMult={damageContext.CritMultiplier:F2}");

        ApplyBeamOnHitEffects(organism);
        SpawnBeamHitVisual(organism);
    }

    private void ApplyBeamOnHitEffects(Organism organism)
    {
        if (beamConfig?.onHitEffects == null || organism == null)
            return;

        beamConfig.onHitEffects.ApplyEffects(organism.gameObject, gameObject, gameObject);
    }

    private void SpawnBeamHitVisual(Organism organism)
    {
        if (organism == null)
            return;

        Collider2D targetCollider = organism.GetComponent<Collider2D>();
        if (targetCollider == null)
            targetCollider = organism.GetComponentInChildren<Collider2D>();

        HitVisualHelper.SpawnHitVisual(parentConfig, organism.transform.position, targetCollider);
    }

    private LayerMask GetBeamTargetMask()
    {
        LayerMask combined = beamConfig.hitLayers;
        if (beamConfig.canHeal)
            combined |= beamConfig.healTargets;

        return combined;
    }

    private bool ShouldHealTarget(Organism organism)
    {
        if (organism == null || !beamConfig.canHeal)
            return false;

        return (beamConfig.healTargets.value & (1 << organism.gameObject.layer)) != 0;
    }

    private void RenderSingleShotChainVisuals(List<ChainLink> chainLinks)
    {
        if (beamConfig == null || beamConfig.beamRendererPrefab == null)
            return;

        if (chainLinks == null || chainLinks.Count == 0)
            return;

        ClearChainRenderers();

        for (int i = 0; i < chainLinks.Count; i++)
        {
            Organism from = chainLinks[i].from;
            Organism to = chainLinks[i].to;
            if (from == null || to == null)
                continue;

            SpawnChainRenderer(from.transform.position, to.transform.position, false);
        }
    }

    private void SyncChainedRenderers(List<ChainLink> chainLinks, bool shouldLoop)
    {
        if (beamConfig == null || beamConfig.beamRendererPrefab == null)
            return;

        CompactChainRendererLists();

        int desiredLinks = chainLinks != null ? chainLinks.Count : 0;

        while (activeChainBeamRenderers.Count < desiredLinks)
            SpawnChainRenderer(Vector3.zero, Vector3.zero, shouldLoop);

        while (activeChainBeamRenderers.Count > desiredLinks)
            RemoveLastChainRenderer();

        for (int i = 0; i < desiredLinks; i++)
        {
            Organism from = chainLinks[i].from;
            Organism to = chainLinks[i].to;
            GameObject go = activeChainBeamGOs[i];
            BeamRenderer renderer = activeChainBeamRenderers[i];

            if (from == null || to == null || go == null || renderer == null)
                continue;

            go.transform.position = to.transform.position;
            renderer.UpdateGeometry(from.transform.position);
        }
    }

    private void SpawnChainRenderer(Vector3 start, Vector3 end, bool shouldLoop)
    {
        GameObject go = Instantiate(beamConfig.beamRendererPrefab, end, Quaternion.identity);
        BeamRenderer renderer = go.GetComponent<BeamRenderer>()
                              ?? go.GetComponentInChildren<BeamRenderer>(true);

        if (renderer == null)
        {
            Destroy(go);
            return;
        }

        renderer.SetLooping(shouldLoop);
        renderer.SetStartPoint(start);
        activeChainBeamGOs.Add(go);
        activeChainBeamRenderers.Add(renderer);
    }

    private void RemoveLastChainRenderer()
    {
        int lastIndex = activeChainBeamRenderers.Count - 1;
        if (lastIndex < 0)
            return;

        BeamRenderer renderer = activeChainBeamRenderers[lastIndex];
        GameObject go = activeChainBeamGOs[lastIndex];

        if (renderer != null)
            renderer.TriggerEnd();

        activeChainBeamRenderers.RemoveAt(lastIndex);
        activeChainBeamGOs.RemoveAt(lastIndex);
    }

    private void CompactChainRendererLists()
    {
        for (int i = activeChainBeamRenderers.Count - 1; i >= 0; i--)
        {
            if (activeChainBeamRenderers[i] != null && activeChainBeamGOs[i] != null)
                continue;

            activeChainBeamRenderers.RemoveAt(i);
            activeChainBeamGOs.RemoveAt(i);
        }
    }

    private void ClearChainRenderers()
    {
        for (int i = activeChainBeamRenderers.Count - 1; i >= 0; i--)
        {
            BeamRenderer renderer = activeChainBeamRenderers[i];
            if (renderer != null)
                renderer.TriggerEnd();
        }

        activeChainBeamRenderers.Clear();
        activeChainBeamGOs.Clear();
    }

    private bool HasActiveChainRenderers()
    {
        CompactChainRendererLists();
        return activeChainBeamGOs.Count > 0;
    }

    private Vector3 GetBeamStartPosition()
    {
        return launchZone != null ? launchZone.position : transform.position;
    }

    private bool IsAbilityButtonHeld()
    {
        return InputHelper.GetMouseButton(0);
    }

    public void HandleBeamTriggerStay(Collider2D other)
    {
        // Legacy hook retained for BeamColliderHandler compatibility.
    }

    public void HandleBeamTriggerExit(Collider2D other)
    {
        // Legacy hook retained for BeamColliderHandler compatibility.
    }

    private void InitializeMuzzleFlash(Vector3 position, Vector3 direction)
    {
        if (beamConfig == null)
            return;

        Transform weaponTransform = transform.Find("WeaponHolder/Weapon");

        if (beamConfig.muzzleFlashPrefab != null && muzzleFlash == null)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0, 0, angle);
            bool shouldFlipY = Mathf.Abs(angle) > 90f;

            muzzleFlash = ProjectileSpawner.InstantiateMuzzleFlashRoot(beamConfig.muzzleFlashPrefab, position, rotation, weaponTransform, shouldFlipY, false);

            var main = muzzleFlash.main;
            main.loop = true;
            muzzleFlash.Stop();
        }

        if (beamConfig.enableMuzzleLight && muzzleFlashLight == null)
        {
            muzzleFlashLight = new GameObject("BeamMuzzleFlashLight");
            muzzleFlashLight.transform.position = position;

            if (weaponTransform != null)
                muzzleFlashLight.transform.SetParent(weaponTransform, true);

            Light2D light2D = muzzleFlashLight.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Point;
            light2D.color = beamConfig.muzzleLightColor;
            light2D.intensity = beamConfig.muzzleLightIntensity;
            light2D.pointLightOuterRadius = beamConfig.muzzleLightRange;

            muzzleFlashLight.SetActive(false);
        }
    }

    private void EnableMuzzleFlash()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (muzzleFlashLight != null)
            muzzleFlashLight.SetActive(true);
    }

    private void DisableMuzzleFlash()
    {
        if (muzzleFlash != null)
            muzzleFlash.Stop();

        if (muzzleFlashLight != null)
            muzzleFlashLight.SetActive(false);
    }

    private void UpdateImpactEffect(Vector3 position, Vector3 direction)
    {
        if (beamConfig.impactEffectPrefab == null)
            return;

        if (impactEffect == null)
        {
            impactEffect = Instantiate(beamConfig.impactEffectPrefab, transform);
            impactAnimator = impactEffect.GetComponent<Animator>();
            if (impactAnimator == null)
                impactAnimator = impactEffect.GetComponentInChildren<Animator>();
        }

        impactEffect.SetActive(true);
        impactEffect.transform.position = position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        impactEffect.transform.rotation = Quaternion.Euler(0, 0, angle);

        if (impactAnimator != null && !string.IsNullOrEmpty(beamConfig.impactAnimationName))
        {
            int stateHash = Animator.StringToHash(beamConfig.impactAnimationName);
            if (impactAnimator.HasState(0, stateHash))
                impactAnimator.Play(stateHash, 0, 0f);
        }
    }

    private void UpdateImpactParticles(Vector3 position, Vector3 direction)
    {
        if (beamConfig.impactParticlePrefab == null)
            return;

        if (impactParticles == null)
        {
            impactParticles = Instantiate(beamConfig.impactParticlePrefab, transform);
            ParticleSystem ps = impactParticles.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Play();
        }

        impactParticles.SetActive(true);
        impactParticles.transform.position = position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        impactParticles.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void HideImpactEffects()
    {
        if (impactEffect != null)
            impactEffect.SetActive(false);

        if (impactParticles != null)
        {
            ParticleSystem ps = impactParticles.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Stop();

            impactParticles.SetActive(false);
        }
    }

    private void OnDisable()
    {
        StopBeam("Component disabled.");
    }

    private void OnDestroy()
    {
        if (muzzleFlash != null)
            Destroy(muzzleFlash.gameObject);

        if (muzzleFlashLight != null)
            Destroy(muzzleFlashLight);

        if (impactEffect != null)
            Destroy(impactEffect);

        if (impactParticles != null)
            Destroy(impactParticles);
    }

    private void LogDebug(string message)
    {
        Debug.Log($"[beamability:{GetAbilityDebugName()}] {message}", this);
    }

    private void LogVerbose(string message)
    {
        Debug.Log($"[beamrenderer:{GetAbilityDebugName()}] {message}", this);
    }

    private string GetAbilityDebugName()
    {
        if (parentConfig != null && !string.IsNullOrEmpty(parentConfig.abilityName))
            return parentConfig.abilityName;

        return name;
    }
}
