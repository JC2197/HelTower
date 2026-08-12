using UnityEngine;


[DisallowMultipleComponent]
public sealed class PlayerMotor : MonoBehaviour, ICharacterMotor
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private CharacterFacingMode facingMode = CharacterFacingMode.Aim;
    [SerializeField] private Transform visualTransform;
    [SerializeField, Min(0f)] private float facingDirectionThreshold = 0.01f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string movingParameter = "IsMoving";

    private CharacterMotorState state;
    private bool hasFacing;
    public CharacterMotorState State => state;

    private void Awake()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void Simulate(in CharacterInputFrame input, float deltaTime)
    {
        Vector2 velocity = input.Move * moveSpeed;
        Vector2 position = body != null ? body.position + velocity * deltaTime : (Vector2)transform.position + velocity * deltaTime;

        float angle = Mathf.Atan2(input.AimWorld.y - position.y, input.AimWorld.x - position.x) * Mathf.Rad2Deg;
        bool facingLeft = state.FacingLeft;
        if (TryGetFacingLeft(input, position, out bool requestedFacingLeft))
        {
            facingLeft = requestedFacingLeft;
            ApplyFacing(facingLeft);
        }

        if (body != null)
        {
            body.linearVelocity = velocity;
        }
        else
        {
            transform.position = position;
        }

        ApplyAnimation(velocity);
        state = new CharacterMotorState(position, velocity, angle, facingLeft);
    }

    public void Teleport(Vector2 worldPosition)
    {
        if (body != null)
        {
            body.position = worldPosition;
            body.linearVelocity = Vector2.zero;
        }
        else
        {
            transform.position = worldPosition;
        }

        state = new CharacterMotorState(worldPosition, Vector2.zero, state.AimAngleDeg, state.FacingLeft);
    }

    private bool TryGetFacingLeft(in CharacterInputFrame input, Vector2 position, out bool facingLeft)
    {
        float directionX = facingMode == CharacterFacingMode.Aim
            ? input.AimWorld.x - position.x
            : input.Move.x;

        if (Mathf.Abs(directionX) < facingDirectionThreshold)
        {
            facingLeft = state.FacingLeft;
            return false;
        }

        facingLeft = directionX < 0f;
        return true;
    }

    private void ApplyFacing(bool facingLeft)
    {
        if (hasFacing && facingLeft == state.FacingLeft)
            return;

        Transform visual = visualTransform != null ? visualTransform : transform;
        Vector3 scale = visual.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingLeft ? -1f : 1f);
        visual.localScale = scale;
        hasFacing = true;
    }

    private void ApplyAnimation(Vector2 velocity)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(movingParameter))
            animator.SetBool(movingParameter, velocity.sqrMagnitude > 0f);
    }
}

