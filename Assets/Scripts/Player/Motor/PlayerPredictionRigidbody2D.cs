using FishNet.Object.Prediction;
using UnityEngine;

/// <summary>
/// Player-owned boundary around FishNet's PredictionRigidbody2D.
/// Movement and knockback will use this same wrapper once player prediction is enabled.
/// </summary>
public sealed class PlayerPredictionRigidbody2D
{
    private readonly PredictionRigidbody2D predictionRigidbody = new PredictionRigidbody2D();

    public Rigidbody2D Rigidbody2D => predictionRigidbody.Rigidbody2D;
    public bool IsInitialized => Rigidbody2D != null;

    public void Initialize(Rigidbody2D rigidbody)
    {
        if (rigidbody == null)
        {
            Debug.LogError("[PlayerPredictionRigidbody2D] Cannot initialize without a Rigidbody2D.");
            return;
        }

        predictionRigidbody.Initialize(rigidbody);
    }

    public void AddForce(Vector2 force, ForceMode2D mode = ForceMode2D.Force)
    {
        if (!IsInitialized)
            return;

        predictionRigidbody.AddForce(force, mode);
    }

    public void SetVelocity(Vector2 velocity)
    {
        if (!IsInitialized)
            return;

        predictionRigidbody.Velocity(velocity);
    }

    public void Simulate()
    {
        if (!IsInitialized)
            return;

        predictionRigidbody.Simulate();
    }

    public void Reconcile(PlayerPredictionRigidbody2D other)
    {
        if (other == null || !IsInitialized || !other.IsInitialized)
            return;

        predictionRigidbody.Reconcile(other.predictionRigidbody);
    }

    public PredictionRigidbody2D Value => predictionRigidbody;
}