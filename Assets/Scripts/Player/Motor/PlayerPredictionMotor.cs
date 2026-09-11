using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using FishNet.Utility.Template;
/// <summary>
/// Initial FishNet tick-network boundary for the player Rigidbody2D.
/// PlayerController will provide desired velocity when movement is migrated here.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerPredictionMotor : TickNetworkBehaviour
{
    public struct ReplicateData : IReplicateData
    {
        public ReplicateData(Vector2 desiredVelocity, bool hasTeleport, Vector2 teleportPosition)
        {
            DesiredVelocity = desiredVelocity;
            HasTeleport = hasTeleport;
            TeleportPosition = teleportPosition;
            _tick = 0;
        }

        public Vector2 DesiredVelocity;
        public bool HasTeleport;
        public Vector2 TeleportPosition;
        private uint _tick;

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct ReconcileData : IReconcileData
    {
        public ReconcileData(PredictionRigidbody2D body)
        {
            Body = body;
            _tick = 0;
        }

        public PredictionRigidbody2D Body;
        private uint _tick;

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    [SerializeField] private Rigidbody2D body;

    private readonly PlayerPredictionRigidbody2D predictionBody = new PlayerPredictionRigidbody2D();
    private PlayerController playerController;
    private Vector2 desiredVelocity;
    private bool hasPendingTeleport;
    private Vector2 pendingTeleportPosition;

    public Rigidbody2D Rigidbody2D => body;
    public PlayerPredictionRigidbody2D PredictionBody => predictionBody;

    private void Awake()
    {
        body ??= GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();
        predictionBody.Initialize(body);

        if (body != null)
            Debug.Log($"[PlayerPredictionMotor] Initialized on '{name}': mass={body.mass}, bodyType={body.bodyType}, simulated={body.simulated}.");
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
    }

    public void SetDesiredVelocity(Vector2 velocity)
    {
        desiredVelocity = velocity;

        if (IsOwner && body != null)
            predictionBody.SetMovementVelocity(velocity);
    }

    public void SetTeleportRequest(Vector2 position)
    {
        hasPendingTeleport = true;
        pendingTeleportPosition = position;
    }

    protected override void TimeManager_OnTick()
    {
        PerformReplicate(BuildReplicateData());
    }

    protected override void TimeManager_OnPostTick()
    {
        CreateReconcile();
    }

    private ReplicateData BuildReplicateData()
    {
        Vector2 velocity = playerController != null
            ? playerController.GetPredictedMovementVelocity()
            : desiredVelocity;
        return IsOwner ? new ReplicateData(velocity, hasPendingTeleport, pendingTeleportPosition) : default;
    }

    public override void CreateReconcile()
    {
        PerformReconcile(new ReconcileData(predictionBody.Value));
    }

    [Replicate]
    private void PerformReplicate(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        // Preserve queued predicted forces such as knockback; SetVelocity would clear them.
        predictionBody.SetMovementVelocity(data.DesiredVelocity);
        if (data.HasTeleport)
        {
            predictionBody.Value.MovePosition(data.TeleportPosition);
            hasPendingTeleport = false;
        }
        predictionBody.Simulate();
    }

    [Reconcile]
    private void PerformReconcile(ReconcileData data, Channel channel = Channel.Unreliable)
    {
        predictionBody.Value.Reconcile(data.Body);
    }
}
