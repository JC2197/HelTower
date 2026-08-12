using UnityEngine;

public abstract class Pet: Organism
{
    [Header("Pet Settings")]
    [SerializeField] protected Transform ownerTransform;
    [SerializeField] protected float followDistance = 2f;
    [SerializeField] protected float stopDistance = 0.5f;
    [SerializeField] protected float followSpeed = 3f;
    [SerializeField] protected float acceleration = 5f;
    [SerializeField] protected float deceleration = 8f;

    [Header("Pet Behavior")]
    [SerializeField] protected bool shouldFollow = true;
    [SerializeField] protected Vector2 relativeOffset = Vector2.zero;

    [Header("Pet Animations")]
    [SerializeField] protected string idleAnimation = "Idle";
    [SerializeField] protected string moveAnimation = "Move";

    protected Vector2 currentVelocity = Vector2.zero;
    protected Vector2 targetPosition;
    protected Animator animator;
    protected string currentAnimationPlaying = "";
    private bool isMoving = false;
    private bool isFacingLeft = false; // Add this
    protected SpriteRenderer petSpriteRenderer; // Add this

    protected override void Awake()
    {
        base.Awake();
        
        animator = GetComponent<Animator>();
        petSpriteRenderer = GetComponent<SpriteRenderer>(); // Add this
        
        if(ownerTransform == null)
        {
            PlayerController player = PlayerController.GetLocalPlayer();
            if(player != null)
            {
                ownerTransform = player.transform;
            }
        }
        PlayAnimation(idleAnimation);
    }
    
    protected virtual void OnEnable()
    {
        PlayerController.OnPlayerSpawned += HandlePlayerSpawned;
    }
    
    protected virtual void OnDisable()
    {
        PlayerController.OnPlayerSpawned -= HandlePlayerSpawned;
    }
    
    private void HandlePlayerSpawned(PlayerController newPlayer)
    {
        ownerTransform = newPlayer.transform;
    }
    
    protected override void HandleUpdate() // Fixed: removed "void override" -> "override void"
    {
        if (!shouldFollow || ownerTransform == null) return;
        FollowOwner();
        UpdateAnimation();
    }

    protected virtual void FollowOwner()
    {
        targetPosition = (Vector2)ownerTransform.position + relativeOffset;
        float distanceToTarget = Vector2.Distance(transform.position, targetPosition);

        if (distanceToTarget > followDistance)
        {
            Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
            currentVelocity = Vector2.MoveTowards(currentVelocity, direction * followSpeed, acceleration * Time.deltaTime);
            if(rb != null)
            {
                rb.linearVelocity = currentVelocity;
            }
            isMoving = true;
            
            // Flip sprite based on movement direction
            if (petSpriteRenderer != null && Mathf.Abs(direction.x) > 0.1f)
            {
                bool shouldFaceLeft = direction.x < 0;
                if (shouldFaceLeft != isFacingLeft)
                {
                    isFacingLeft = shouldFaceLeft;
                    petSpriteRenderer.flipX = isFacingLeft;
                }
            }
        }
        else
        {
            currentVelocity = Vector2.MoveTowards(currentVelocity, Vector2.zero, deceleration * Time.deltaTime);
            if(rb != null)
            {
                rb.linearVelocity = currentVelocity;
            }
            isMoving = currentVelocity.magnitude > 0.1f;
        }
    }

    protected virtual void UpdateAnimation()
    {
        string targetAnimation = isMoving ? moveAnimation : idleAnimation;
        if (targetAnimation != currentAnimationPlaying)
        {
            PlayAnimation(targetAnimation);
        }
    }
    
    protected virtual void PlayAnimation(string animationName)
    {
        if (animator == null || string.IsNullOrEmpty(animationName)) return; // Fixed: != to ==
        animator.Play(animationName, 0);
        currentAnimationPlaying = animationName;
    }
    
    public virtual void SetOwner(Transform newOwner)
    {
        ownerTransform = newOwner;
    }
    
    public virtual void SetFollowBehavior(bool follow)
    {
        shouldFollow = follow;
    }
    
    public virtual void SetRelativeOffset(Vector2 offset)
    {
        relativeOffset = offset;
    }

    public virtual void SetFollowDistance(float distance)
    {
        followDistance = distance;
    }
    
    protected override void HandleDeath()
    {
        shouldFollow = false;
        Destroy(gameObject);
    }
    
    protected virtual void OnDrawGizmosSelected()
    {
        if (ownerTransform == null) return;
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ownerTransform.position, followDistance);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ownerTransform.position, stopDistance);
        
        Vector2 target = (Vector2)ownerTransform.position + relativeOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target, 0.2f);
        Gizmos.DrawLine(transform.position, target);
    }
}
