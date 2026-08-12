using UnityEngine;
using TMPro;

public class DamageFloater : MonoBehaviour
{
    [SerializeField] private TMP_Text damageText;
    
    private DamageFloaterConfig config;
    private float timer = 0f;
    private Vector3 velocity;
    private CanvasGroup canvasGroup;
    private Vector2 moveDirection = Vector2.up;
    private Vector3 startScale;
    private Vector3 endScale;
    private float scaleDuration;
    
    // Target tracking
    private Transform targetTransform;
    private Vector3 localOffset;
    private Camera mainCamera;
    private bool isDetached = false; // Track if we've stopped following the target
    private Vector3 fixedWorldPosition; // World position to track after detachment
    
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        if (damageText == null)
        {
            damageText = GetComponentInChildren<TMP_Text>();
        }
        
        mainCamera = Camera.main;
    }
    
    private void Update()
    {
        if (config == null) return;
        
        timer += Time.deltaTime;
        
        // Check if target was destroyed and detach if needed
        if (!isDetached && targetTransform != null)
        {
            // Use Unity's implicit bool check - returns false for destroyed objects
            if (!targetTransform || targetTransform.gameObject == null)
            {
                // Capture the world position at the moment of detachment
                fixedWorldPosition = targetTransform ? targetTransform.position + localOffset : transform.position;
                isDetached = true;
                targetTransform = null;
            }
        }
        
        // Update world position (follow target or stay at fixed position)
        if (!isDetached && targetTransform != null)
        {
            // Apply velocity to the local offset so it floats away from the target
            if (velocity != Vector3.zero)
            {
                float curveValue = config.movementCurve.Evaluate(timer / config.lifetime);
                localOffset += velocity * curveValue * Time.deltaTime;
            }
            
            // Follow the living target with updated offset
            transform.position = targetTransform.position + localOffset;
        }
        else if (isDetached)
        {
            // Stay at the fixed world position
            transform.position = fixedWorldPosition;
            
            // Apply velocity movement when detached
            if (velocity != Vector3.zero)
            {
                float curveValue = config.movementCurve.Evaluate(timer / config.lifetime);
                transform.position += velocity * curveValue * Time.deltaTime;
            }
        }
        else
        {
            // Not tied to target, just apply velocity
            if (velocity != Vector3.zero)
            {
                float curveValue = config.movementCurve.Evaluate(timer / config.lifetime);
                transform.position += velocity * curveValue * Time.deltaTime;
            }
        }
        
        // Scale animation (first quarter of lifetime)
        if (timer <= scaleDuration)
        {
            float scaleProgress = timer / scaleDuration;
            transform.localScale = Vector3.Lerp(startScale, endScale, scaleProgress);
        }
        
        // Fade out
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / config.lifetime);
        }
        
        // Destroy when lifetime expires
        if (timer >= config.lifetime)
        {
            Destroy(gameObject);
        }
    }
    
    public void Initialize(DamageFloaterConfig configuration, float damage, Color color, Vector2 direction, bool isCritical = false, Transform target = null)
    {
        config = configuration;
        targetTransform = target;
        
        if (damageText != null)
        {
            string text = Mathf.CeilToInt(damage).ToString();
            if (isCritical)
            {
                text += "!";
            }
            
            damageText.text = text;
            damageText.color = color;
            
            if (config.fontMaterial != null)
            {
                damageText.fontMaterial = config.fontMaterial;
            }
        }
        
        SetupAnimation(direction);
    }
    
    public void Initialize(DamageFloaterConfig configuration, string text, Color color, Vector2 direction, bool isCritical = false, Transform target = null)
    {
        config = configuration;
        targetTransform = target;
        
        if (damageText != null)
        {
            if (isCritical)
            {
                text += "!";
            }
            damageText.text = text;
            damageText.color = color;
            
            if (config.fontMaterial != null)
            {
                damageText.fontMaterial = config.fontMaterial;
            }
        }
        
        SetupAnimation(direction);
    }
    
    private void SetupAnimation(Vector2 direction)
    {
        if (config == null) return;
        
        // If float speed is zero, don't apply any movement
        if (Mathf.Approximately(config.floatSpeed, 0f))
        {
            velocity = Vector3.zero;
        }
        else
        {
            moveDirection = direction.normalized;
            
            // Reduce random offset and make it perpendicular to direction
            Vector2 perpendicular = new Vector2(-moveDirection.y, moveDirection.x);
            float randomPerpendicular = Random.Range(-config.randomOffset.x * 0.3f, config.randomOffset.x * 0.3f);
            float randomForward = Random.Range(0f, config.randomOffset.y * 0.5f);
            
            // Velocity in screen space pixels
            Vector3 baseVelocity = new Vector3(moveDirection.x, moveDirection.y, 0f) * config.floatSpeed;
            Vector3 randomVelocity = new Vector3(perpendicular.x * randomPerpendicular, perpendicular.y * randomPerpendicular + randomForward, 0f);
            
            velocity = baseVelocity + randomVelocity;
        }
        
        // Setup scale animation
        startScale = Vector3.one * config.startScale;
        endScale = Vector3.one * config.endScale;
        scaleDuration = config.lifetime * config.scaleAnimationDuration;
        transform.localScale = startScale;
        
        // Store local offset for tracking
        if (targetTransform != null && mainCamera != null)
        {
            localOffset = config.worldOffset;
        }
    }
    
    public void SetFont(TMP_FontAsset font, float size)
    {
        if (damageText != null && font != null)
        {
            damageText.font = font;
            damageText.fontSize = size;
        }
    }
    
    public void SetOutline(Color color, float thickness)
    {
        if (damageText != null)
        {
            damageText.outlineColor = color;
            damageText.outlineWidth = thickness;
        }
    }
}