using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;

/// <summary>
/// Spawns enemies based on an EnemySpawnGroup configuration.
/// Spawns the first wave on scene start and advances when all spawned enemies are defeated.
/// </summary>
public class EnemySpawner : NetworkBehaviour
{
    public EnemySpawnGroup spawnGroup;
    public Collider2D spawnArea;

    [SerializeField] private bool spawnOnAwake = true;
    [SerializeField] private float initialWaveDelay = 0f;
    [SerializeField] private UnityEvent onFloorComplete;
    [SerializeField] private GameObject spawnPrefab;

    [Header("Rewards")]
    [Tooltip("Roll a trait choice for every player when this floor is cleared.")]
    [SerializeField] private bool rollTraitsOnFloorComplete = true;
    [SerializeField] private TraitRollType floorCompleteTraitRollType = TraitRollType.General;

    public event Action FloorComplete;

    private readonly HashSet<int> _aliveSpawnedEnemyIds = new HashSet<int>();
    private int _currentWaveIndex = -1;
    private bool _isSpawningWave;
    private bool _floorCompleted;
    private bool _spawnSequenceStarted;

    private void OnEnable()
    {
        Organism.OnOrganismDeath += HandleOrganismDeath;
    }

    private void OnDisable()
    {
        Organism.OnOrganismDeath -= HandleOrganismDeath;
    }

    private void Awake()
    {
        if (spawnOnAwake)
            TryStartSpawnSequence();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (spawnOnAwake)
            TryStartSpawnSequence();
    }

    [ContextMenu("Start Spawn Sequence")]
    public void TryStartSpawnSequence()
    {
        if (_spawnSequenceStarted || _floorCompleted || !CanSpawnInCurrentMode())
            return;

        if (spawnGroup == null)
        {
            Debug.LogError("[EnemySpawner] No spawn group assigned.", this);
            return;
        }

        if (spawnGroup.waves == null || spawnGroup.waves.Length == 0)
        {
            CompleteFloor();
            return;
        }

        _spawnSequenceStarted = true;
        _aliveSpawnedEnemyIds.Clear();
        _currentWaveIndex = -1;

        if (initialWaveDelay > 0f)
            StartCoroutine(BeginAfterDelay(initialWaveDelay));
        else
            StartNextWave();
    }

    private IEnumerator BeginAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNextWave();
    }

    private bool CanSpawnInCurrentMode()
    {
        // If networking exists, only server should spawn.
        if (InstanceFinder.NetworkManager != null)
            return IsServerInitialized;

        return true;
    }

    private void StartNextWave()
    {
        if (_floorCompleted)
            return;

        _currentWaveIndex++;
        if (_currentWaveIndex >= spawnGroup.waves.Length)
        {
            CompleteFloor();
            return;
        }

        StartCoroutine(SpawnWaveRoutine(spawnGroup.waves[_currentWaveIndex]));
    }

    private IEnumerator SpawnWaveRoutine(Wave wave)
    {
        _isSpawningWave = true;

        if (wave == null || wave.enemies == null || wave.enemies.Count == 0)
        {
            _isSpawningWave = false;
            TryAdvanceAfterWaveClear();
            yield break;
        }

        List<EnemySpawnData> weightedEntries = new List<EnemySpawnData>();
        int totalWeight = 0;

        for (int i = 0; i < wave.enemies.Count; i++)
        {
            EnemySpawnData enemyData = wave.enemies[i];
            if (enemyData == null || enemyData.enemyPrefab == null || enemyData.spawnWeight <= 0)
                continue;

            weightedEntries.Add(enemyData);
            totalWeight += enemyData.spawnWeight;
        }

        if (weightedEntries.Count == 0 || totalWeight <= 0)
        {
            Debug.LogWarning($"[EnemySpawner] Wave {_currentWaveIndex + 1} has no valid weighted enemies.", this);
            _isSpawningWave = false;
            TryAdvanceAfterWaveClear();
            yield break;
        }

        int minCount = Mathf.Min(wave.enemyCountRange.x, wave.enemyCountRange.y);
        int maxCount = Mathf.Max(wave.enemyCountRange.x, wave.enemyCountRange.y);
        int totalToSpawn = UnityEngine.Random.Range(Mathf.Max(0, minCount), Mathf.Max(0, maxCount) + 1);

        for (int spawnIndex = 0; spawnIndex < totalToSpawn; spawnIndex++)
        {
            EnemySpawnData selectedEnemy = SelectWeightedEnemy(weightedEntries, totalWeight);
            if (selectedEnemy == null)
                continue;

            yield return SpawnSingleEnemyRoutine(selectedEnemy.enemyPrefab);

            float delay = Mathf.Max(0f, selectedEnemy.spawnDelay);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
        }

        _isSpawningWave = false;
        TryAdvanceAfterWaveClear();
    }

    private static EnemySpawnData SelectWeightedEnemy(List<EnemySpawnData> entries, int totalWeight)
    {
        if (entries == null || entries.Count == 0 || totalWeight <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            EnemySpawnData entry = entries[i];
            cumulative += entry.spawnWeight;
            if (roll < cumulative)
                return entry;
        }

        return entries[entries.Count - 1];
    }

    private IEnumerator SpawnSingleEnemyRoutine(GameObject enemyPrefab)
    {
        Vector3 spawnPosition = GetSpawnPosition();

        if (spawnPrefab != null)
            yield return SpawnWithAnimation(spawnPosition);

        SpawnEnemyNow(enemyPrefab, spawnPosition);
    }

    private void SpawnEnemyNow(GameObject enemyPrefab, Vector3 spawnPosition)
    {
        GameObject enemyInstance = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

        int instanceId = enemyInstance.GetInstanceID();
        _aliveSpawnedEnemyIds.Add(instanceId);

        SpawnedEnemyTracker tracker = enemyInstance.GetComponent<SpawnedEnemyTracker>();
        if (tracker == null)
            tracker = enemyInstance.AddComponent<SpawnedEnemyTracker>();

        tracker.Initialize(this, instanceId);

        if (InstanceFinder.NetworkManager != null && IsServerInitialized)
            InstanceFinder.ServerManager.Spawn(enemyInstance);
    }

    private IEnumerator SpawnWithAnimation(Vector3 spawnPosition)
    {
        GameObject spawnEffect = Instantiate(spawnPrefab, spawnPosition, Quaternion.identity);
        if (spawnEffect == null)
            yield break;

        Animator animator = spawnEffect.GetComponent<Animator>();
        if (animator != null)
        {
            // Let the animator enter its first state before reading duration.
            yield return null;

            float clipLength = animator.GetCurrentAnimatorStateInfo(0).length;
            if (clipLength > 0f)
                yield return new WaitForSeconds(clipLength);
        }

        Destroy(spawnEffect);
    }

    private Vector3 GetSpawnPosition()
    {
        if (spawnArea != null)
        {
            Bounds bounds = spawnArea.bounds;
            return new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                transform.position.z
            );
        }

        float radius = spawnGroup != null ? Mathf.Max(0f, spawnGroup.spawnRadius) : 0f;
        if (radius <= 0f)
            return transform.position;

        Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
        return transform.position + new Vector3(offset.x, offset.y, 0f);
    }

    private void HandleOrganismDeath(Organism organism)
    {
        if (organism == null)
            return;

        NotifySpawnedEnemyGone(organism.gameObject.GetInstanceID());
    }

    internal void NotifySpawnedEnemyGone(int instanceId)
    {
        if (!_aliveSpawnedEnemyIds.Remove(instanceId))
            return;

        TryAdvanceAfterWaveClear();
    }

    private void TryAdvanceAfterWaveClear()
    {
        if (_isSpawningWave || _floorCompleted)
            return;

        if (_aliveSpawnedEnemyIds.Count > 0)
            return;

        StartNextWave();
    }

    private void CompleteFloor()
    {
        if (_floorCompleted)
            return;

        _floorCompleted = true;
        onFloorComplete?.Invoke();
        FloorComplete?.Invoke();

        Debug.Log($"[EnemySpawner] Floor complete for spawn group '{spawnGroup?.GroupName ?? "Unknown"}'.", this);

        if (rollTraitsOnFloorComplete)
            TriggerFloorCompleteTraitRoll();
    }

    /// <summary>
    /// Rolls a trait choice for every player. CompleteFloor runs on the server (or locally
    /// when offline), but TraitRoller.RollTraits only rolls for the local owning player, so we
    /// fan the request out to every client and let each roll on its own player.
    /// </summary>
    private void TriggerFloorCompleteTraitRoll()
    {
        if (InstanceFinder.NetworkManager != null && IsServerInitialized)
            ObserversRpcRollFloorCompleteTraits(floorCompleteTraitRollType);
        else if (InstanceFinder.NetworkManager == null)
            RollTraitsForLocalPlayer(floorCompleteTraitRollType); // Offline / single-player
    }

    [ObserversRpc(RunLocally = true)]
    private void ObserversRpcRollFloorCompleteTraits(TraitRollType rollType)
    {
        RollTraitsForLocalPlayer(rollType);
    }

    private static void RollTraitsForLocalPlayer(TraitRollType rollType)
    {
        PlayerController player = PlayerController.GetLocalPlayer();
        if (player == null)
            return;

        TraitRoller roller = player.GetComponent<TraitRoller>();
        if (roller == null)
        {
            Debug.LogWarning("[EnemySpawner] Local player has no TraitRoller component; cannot roll floor-complete traits.", player);
            return;
        }

        roller.RollTraits(rollType);
    }

    private sealed class SpawnedEnemyTracker : MonoBehaviour
    {
        private EnemySpawner _owner;
        private int _instanceId;

        public void Initialize(EnemySpawner owner, int instanceId)
        {
            _owner = owner;
            _instanceId = instanceId;
        }

        private void OnDestroy()
        {
            if (_owner != null)
                _owner.NotifySpawnedEnemyGone(_instanceId);
        }
    }
}