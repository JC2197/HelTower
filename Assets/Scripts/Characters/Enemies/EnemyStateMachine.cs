using UnityEngine;

/// <summary>
/// A single behavior state in an enemy's finite state machine.
/// Enter/Exit run exactly once per transition; Tick runs every server frame while active.
/// CheckTransitions is polled every frame and returns the state to switch to (or null to stay).
/// </summary>
public interface IEnemyState
{
    void Enter(Enemy enemy);
    void Tick(Enemy enemy, float deltaTime);
    void Exit(Enemy enemy);

    /// <summary>Return the state to transition to this frame, or null to remain in this state.</summary>
    IEnemyState CheckTransitions(Enemy enemy);
}

/// <summary>
/// Drives a single <see cref="IEnemyState"/> at a time, guaranteeing Enter/Exit fire once per
/// transition. Transitions are evaluated every frame (event-driven), not on a fixed poll timer.
/// </summary>
public class EnemyStateMachine
{
    public IEnemyState Current { get; private set; }
    public string CurrentName => Current != null ? Current.GetType().Name : "None";

    public void ChangeState(Enemy enemy, IEnemyState next)
    {
        if (next == null || next == Current) return;
        Current?.Exit(enemy);
        Current = next;
        Current.Enter(enemy);
    }

    public void Tick(Enemy enemy, float deltaTime)
    {
        if (Current == null) return;

        IEnemyState next = Current.CheckTransitions(enemy);
        if (next != null && next != Current)
            ChangeState(enemy, next);

        Current.Tick(enemy, deltaTime);
    }
}

/// <summary>
/// Shared transition rule for every combat/movement state: the instant an ability begins its
/// precast/cast, yield to <see cref="CastingState"/>; otherwise defer to the enemy's central
/// combat-state selection (retreat/patrol/attack/strafe/chase).
/// </summary>
public abstract class EnemyStateBase : IEnemyState
{
    public virtual void Enter(Enemy enemy) { }
    public virtual void Exit(Enemy enemy) { }
    public abstract void Tick(Enemy enemy, float deltaTime);

    public virtual IEnemyState CheckTransitions(Enemy enemy)
    {
        if (enemy.IsAnyAbilityBusy())
            return enemy.CastingBehavior;

        return enemy.SelectMovementState();
    }
}

/// <summary>Stand still and idle. Fallback when there is no target and no patrol behavior.</summary>
public class IdleState : EnemyStateBase
{
    public override void Enter(Enemy enemy) => enemy.StopMovement();

    public override void Tick(Enemy enemy, float deltaTime)
    {
        enemy.StopMovement();
        enemy.PlayIdle();
    }
}

/// <summary>Move toward the target. Supports continuous movement or move/pause timing from config.</summary>
public class ChaseState : EnemyStateBase
{
    private bool isMoving = true;
    private float phaseTimer;

    public override void Enter(Enemy enemy)
    {
        isMoving = true;
        phaseTimer = 0f;
    }

    public override void Tick(Enemy enemy, float deltaTime)
    {
        if (enemy.Target == null) { enemy.StopMovement(); return; }

        enemy.FaceAndAim(aimAway: false);

        EnemyConfig config = enemy.Config;
        float speedMultiplier = enemy.GetActionSpeedMultiplier(EnemyActionType.Chase, 1f);

        if (config == null || config.continuousMovement)
        {
            enemy.MoveInDirection(enemy.DirectionToTarget(), speedMultiplier);
            return;
        }

        // Timed movement: alternate moving toward the target and pausing in place.
        phaseTimer += deltaTime;
        if (isMoving)
        {
            enemy.MoveInDirection(enemy.DirectionToTarget(), speedMultiplier);
            if (phaseTimer >= config.movementTime)
            {
                isMoving = false;
                phaseTimer = 0f;
                enemy.StopMovement();
                enemy.PlayIdle();
            }
        }
        else
        {
            enemy.StopMovement();
            if (phaseTimer >= config.stopTime)
            {
                isMoving = true;
                phaseTimer = 0f;
            }
        }
    }
}

/// <summary>Hold position in range and fire the highest-priority ready ability.</summary>
public class AttackState : EnemyStateBase
{
    public override void Enter(Enemy enemy) => enemy.StopMovement();

    public override void Tick(Enemy enemy, float deltaTime)
    {
        enemy.FaceAndAim(aimAway: false);
        enemy.StopMovement();
        enemy.PlayIdle();
        enemy.TryUseAbilities();
    }
}

/// <summary>
/// Owns the character exclusively while an ability's precast/cast animation is playing:
/// holds position and does NOT touch the animator, so the ability's own PreAttack/Attack
/// animations are never overwritten. Exits itself the frame the ability finishes.
/// </summary>
public class CastingState : EnemyStateBase
{
    public override void Enter(Enemy enemy) => enemy.StopMovement();

    public override void Tick(Enemy enemy, float deltaTime)
    {
        // Hold position; the active ability owns movement lock and animation.
        enemy.StopMovement();
    }

    public override IEnemyState CheckTransitions(Enemy enemy)
    {
        // Uninterruptible: stay until the ability sequence fully completes.
        if (enemy.IsAnyAbilityBusy())
            return null;

        return enemy.SelectMovementState();
    }
}

/// <summary>Orbit the target at a configured distance (ranged enemies waiting on cooldown).</summary>
public class StrafeState : EnemyStateBase
{
    public override void Tick(Enemy enemy, float deltaTime)
    {
        if (enemy.Target == null) { enemy.StopMovement(); return; }

        EnemyActionConfig strafe = enemy.GetAction(EnemyActionType.Strafe);
        if (strafe == null) { enemy.StopMovement(); enemy.PlayIdle(); return; }

        enemy.FaceAndAim(aimAway: false);

        Vector2 toTarget = enemy.DirectionToTarget();

        // Tangential orbit direction, flipped by strafe rotation preference.
        Vector2 perpendicular = new Vector2(-toTarget.y, toTarget.x);
        if (!strafe.strafeClockwise)
            perpendicular = -perpendicular;

        // Radial correction to hold the desired strafe distance.
        float distanceError = enemy.DistanceToTarget() - strafe.strafeDistance;
        Vector2 radialCorrection = -toTarget * distanceError * 0.5f;

        Vector2 strafeDirection = (perpendicular + radialCorrection).normalized;
        enemy.MoveInDirection(strafeDirection, strafe.movementSpeedMultiplier);
    }
}

/// <summary>
/// Move away from the target while still casting whatever is off cooldown (kite/flee).
/// Triggered by low health or when the target closes inside kite distance.
/// </summary>
public class RetreatState : EnemyStateBase
{
    public override void Tick(Enemy enemy, float deltaTime)
    {
        if (enemy.Target == null) { enemy.StopMovement(); return; }

        enemy.FaceAndAim(aimAway: true);

        float speedMultiplier = enemy.GetActionSpeedMultiplier(EnemyActionType.Retreat, 1.2f);
        enemy.MoveInDirection(-enemy.DirectionToTarget(), speedMultiplier);

        // Keep pressure on while kiting.
        enemy.TryUseAbilities();
    }
}

/// <summary>Wander around the spawn point when no target is present.</summary>
public class PatrolState : EnemyStateBase
{
    private Vector3 patrolTarget;
    private bool hasPatrolTarget;
    private float waitTimer;

    public override void Enter(Enemy enemy)
    {
        hasPatrolTarget = false;
        waitTimer = 0f;
    }

    public override void Tick(Enemy enemy, float deltaTime)
    {
        EnemyActionConfig patrol = enemy.GetAction(EnemyActionType.Patrol);
        if (patrol == null) { enemy.StopMovement(); enemy.PlayIdle(); return; }

        bool reached = hasPatrolTarget &&
                       Vector3.Distance(enemy.transform.position, patrolTarget) < 0.5f;

        if (!hasPatrolTarget || reached)
        {
            if (hasPatrolTarget)
            {
                enemy.StopMovement();
                waitTimer += deltaTime;
                if (waitTimer < patrol.patrolWaitTime)
                {
                    enemy.PlayIdle();
                    return;
                }
            }

            Vector2 randomOffset = Random.insideUnitCircle * patrol.patrolRadius;
            patrolTarget = enemy.SpawnPosition + new Vector3(randomOffset.x, randomOffset.y, 0f);
            hasPatrolTarget = true;
            waitTimer = 0f;
        }

        Vector2 toPatrol = ((Vector2)(patrolTarget - enemy.transform.position)).normalized;
        enemy.MoveInDirection(toPatrol, patrol.movementSpeedMultiplier);
    }
}
