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
    private float beamDamageTimer;

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

    private ParticleSystem muzzleFlash;
    private GameObject muzzleFlashLight;

    private GameObject impactEffect;
    private Animator impactAnimator;
    private GameObject impactParticles;
    private System.Func<bool> holdChecker;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        statOwner = context.statOwner;
    }

    public void SetHoldChecker(System.Func<bool> checker)
    {
        holdChecker = checker;
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

        bool isChanneled = beamConfig.canHoldToFire && !IsAutocast;

        if (beamConfig.channelCostPerSecond > 0f && isChanneled)
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

        if (isChanneled)
        {
            // If it's a channeled/hold ability, shut down when the player lets go
            if (!IsAbilityButtonHeld())
                StopBeam("Hold-to-fire button released.");
        }
        else
        {
            // FIXED: If it's a single-shot (autocast OR manual single-click), 
            // force it to shut down once its singleShotDuration is reached!
            float maxDuration = Mathf.Max(0.05f, beamConfig.singleShotDuration);
            if (beamLifetime >= maxDuration)
            {
                StopBeam("Single-shot duration reached.");
            }
            else if (beamConfig.hitbox?.prefab != null && activeBeamGO == null && !HasActiveChainRenderers() && beamLifetime > 0.05f)
            {
                StopBeam("BeamRenderer visual completed execution.");
            }
        }
    }

    private void StartBeam()
    {
        if (launchZone == null)
            launchZone = WeaponLaunchPoint.FindLaunchZone(transform);

        isBeamActive = true;
        energyConsumptionTimer = 0f;
        beamLifetime = 0f;
        beamDamageTimer = 0f;

        if (!_isExtraBeam) lockedTarget = null;
        hasLockedEndpoint = _isExtraBeam && lockedTarget != null;
        singleShotDamageDealt = false;

        Vector3 start = GetBeamStartPosition();
        Enemy initialEnemy = null;
        Vector3 initialTarget;

        // 1. Resolve Target exactly once
        if (IsAutocast)
        {
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

        // 2. Lock target references safely for single-shot tracking snapshots
        if (initialEnemy != null)
        {
            lockedTarget = initialEnemy;
            lockedEndpoint = initialTarget;
            hasLockedEndpoint = true;
            LogDebug($"StartBeam locked to target={lockedTarget.name}");
        }

        // 3. FIXED: Run physics calculations BEFORE spawning visuals to prevent wall-piercing layout flashes
        ComputeBeamEndpoint(start, initialTarget, initialEnemy, out Vector3 finalVisualEnd, out Vector3 direction);
        beamEndPosition = finalVisualEnd;

        LogDebug($"StartBeam start={start} initialTarget={initialTarget} finalVisualEnd={beamEndPosition} direction={direction}");

        // 4. Initialize visual layers correctly
        SpawnActiveRenderer(start, beamEndPosition, IsAutocast);
        InitializeMuzzleFlash(start, direction);
        EnableMuzzleFlash();

        // 5. Fire extra multi-beam paths if autocast condition metrics require it
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

        Collider2D[] hits = Physics2D.OverlapCircleAll(start, beamConfig.maxBeamDistance, GetBeamTargetMask());
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
        Collider2D[] nearCursor = Physics2D.OverlapCircleAll(cursorWorld, radius, GetBeamTargetMask());

        Enemy best = PickClosestLivingEnemy(nearCursor, cursorWorld);
        if (best != null)
            return best;

        Collider2D[] nearStart = Physics2D.OverlapCircleAll(start, beamConfig.maxBeamDistance, GetBeamTargetMask());
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
        float beamReach = beamConfig.fixedDistance
            ? beamConfig.maxBeamDistance
            : Mathf.Min(desiredDistance, beamConfig.maxBeamDistance);

        // Exclude the caster's own layer so the raycast does not immediately
        // hit the player's own collider from the LaunchZone origin.
        LayerMask castMask = GetBeamTargetMask() & ~(1 << gameObject.layer);
        RaycastHit2D[] hits = Physics2D.RaycastAll(start, direction, beamReach, castMask);
        RaycastHit2D hit = default;
        bool hasBlockingHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Enemy hitEnemy = hits[i].collider != null
                ? hits[i].collider.GetComponentInParent<Enemy>()
                : null;

            if (beamConfig.fixedDistance && hitEnemy != null)
                continue;

            hit = hits[i];
            hasBlockingHit = true;
            break;
        }

        if (hasBlockingHit)
        {
            Enemy hitEnemy = hit.collider.GetComponentInParent<Enemy>();
            if (!beamConfig.fixedDistance && autoTargetEnemy != null && hitEnemy == autoTargetEnemy)
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
        if (beamConfig.hitbox?.prefab == null)
            return;

        bool shouldLoop = beamConfig.canHoldToFire && !isAutocast;

        activeBeamGO = Instantiate(beamConfig.hitbox.prefab, end, Quaternion.identity);
        AutoDestroyEffect autoDestroy = activeBeamGO.GetComponent<AutoDestroyEffect>();
        if (autoDestroy != null)
            Destroy(autoDestroy);

        activeBeamRenderer = activeBeamGO.GetComponent<BeamRenderer>()
                          ?? activeBeamGO.GetComponentInChildren<BeamRenderer>(true);

        if (activeBeamRenderer == null)
        {
            Debug.LogWarning("[beamrenderer] beamConfig.hitbox.prefab has no BeamRenderer component.", this);
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
                LogDebug($"Single-shot chain hit -> {target.name}, value={beamConfig.hitbox.damage:F2}");
                DealBeamHit(target, beamConfig.hitbox.damage);
            }
            return;
        }

        LogDebug($"Single-shot hit -> {lockedTarget.name}, value={beamConfig.hitbox.damage:F2}");
        DealBeamHit(lockedTarget, beamConfig.hitbox.damage);
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
        float valuePerTick = beamConfig.hitbox.damage;
        beamDamageTimer += deltaTime;

        float beamLength = Vector3.Distance(start, end);
        Vector3 center = (start + end) * 0.5f;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        HashSet<Organism> directHits = new HashSet<Organism>();
        Organism primaryTarget = null;
        float primaryDist = float.MaxValue;

        LayerMask boxCastMask = GetBeamTargetMask() & ~(1 << gameObject.layer);
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            center,
            new Vector2(Mathf.Max(0.01f, beamLength), Mathf.Max(0.01f, beamConfig.beamWidth)),
            angle,
            boxCastMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Organism organism = hits[i] != null ? hits[i].GetComponentInParent<Organism>() : null;
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

        if (beamDamageTimer < timeBetweenHits)
            return;

        beamDamageTimer -= timeBetweenHits;

        foreach (Organism organism in currentHits)
        {
            if (organism == null || !organism.IsAlive)
                continue;

            DealBeamHit(organism, valuePerTick);
        }
    }

    private void DealBeamHit(Organism organism, float value)
    {
        if (organism == null || !organism.IsAlive)
            return;

        HitboxConfig hitbox = beamConfig.hitbox;
        Collider2D targetCollider = organism.GetComponent<Collider2D>();
        if (targetCollider == null)
            targetCollider = organism.GetComponentInChildren<Collider2D>();
        if (hitbox == null || targetCollider == null)
            return;

        GameObject statAttacker = statOwner ?? gameObject;
        GameObject damageAttacker = gameObject;
        Vector3 hitPosition = organism.transform.position;
        string abilityName = parentConfig?.abilityName;
        List<string> abilityTags = parentConfig?.abilityTags?.GetAllTags();

        if (hitbox.IsPositiveTarget(organism.gameObject))
        {
            hitbox.ApplyHealing(targetCollider, statAttacker, damageAttacker, statAttacker,
                hitPosition, abilityName, abilityTags, parentConfig);
        }

        if (hitbox.IsNegativeTarget(organism.gameObject))
        {
            hitbox.ApplyDamage(targetCollider, statAttacker, damageAttacker, statAttacker,
                hitPosition, abilityName, abilityTags, parentConfig);
            hitbox.ApplyOnHitEffects(organism.gameObject, gameObject, damageAttacker);
        }

        hitbox.SpawnHitFeedback(hitPosition, parentConfig, targetCollider);
    }

    private LayerMask GetBeamTargetMask()
    {
        return beamConfig.hitbox != null ? beamConfig.hitbox.GetCombinedHitLayers() : 0;
    }

    private void RenderSingleShotChainVisuals(List<ChainLink> chainLinks)
    {
        if (beamConfig == null || beamConfig.hitbox?.prefab == null)
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
        if (beamConfig == null || beamConfig.hitbox?.prefab == null)
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
        GameObject go = Instantiate(beamConfig.hitbox.prefab, end, Quaternion.identity);
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
        Debug.Log($"[BeamAbility] GetBeamStartPosition called. launchZone={(launchZone != null ? launchZone.name : "<null>")}, transform.position={transform.position}");
        return launchZone != null ? launchZone.position : transform.position;
    }

    private bool IsAbilityButtonHeld()
    {
        return holdChecker != null && holdChecker();
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

            ProjectileSpawner.InstantiateMuzzleFlashRoot(beamConfig.muzzleFlashPrefab, position, rotation, weaponTransform, shouldFlipY, false);
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
