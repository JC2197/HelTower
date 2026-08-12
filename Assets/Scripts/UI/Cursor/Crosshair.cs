using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Crosshair Settings")]
    [SerializeField] private Sprite crosshairSprite;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private float maxDistance = 15f;
    [SerializeField] private bool clampToMaxDistance = false;
    
    [Header("Animation")]
    [SerializeField] private bool enablePulse = true;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float pulseAmount = 0.1f;
    [SerializeField] private bool enableColorAnimation = true;
    [SerializeField] private float colorSpeed = 6f;
    [SerializeField] private float colorAmount = 0.2f;
    
    [Header("Combat Feedback")]
    [SerializeField] private Color targetAvailableColor = Color.red;
    [SerializeField] private float targetCheckRadius = 1f;
    [SerializeField] private LayerMask targetLayers = -1;
    
    private Camera playerCamera;
    private SpriteRenderer spriteRenderer;
    private Transform playerTransform;
    private bool hasTargetInRange = false;
    
    // Properties
    public Vector3 WorldPosition => transform.position;
    public Vector3 DirectionFromPlayer => playerTransform != null ? 
        (transform.position - playerTransform.position).normalized : Vector3.right;
    
    private void Awake()
    {
        SetupCrosshair();
    }
    
    private void Start()
    {
        playerCamera = Camera.main;
        
        // Find the local player only
        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            bool isNetworkActive = player.IsServerStarted || player.IsClientStarted;
            bool isLocalPlayer = !isNetworkActive || player.IsOwner;
            if (isLocalPlayer)
            {
                playerTransform = player.transform;
                Debug.Log($"[Crosshair] Found local player: {player.gameObject.name}");
                break;
            }
        }
    }
    
    private void OnEnable()
    {
        PlayerController.OnPlayerSpawned += HandlePlayerSpawned;
    }
    
    private void OnDisable()
    {
        PlayerController.OnPlayerSpawned -= HandlePlayerSpawned;
    }
    
    private void HandlePlayerSpawned(PlayerController newPlayer)
    {
        // Only follow the local player
        bool isNetworkActive = newPlayer.IsServerStarted || newPlayer.IsClientStarted;
        bool isLocalPlayer = !isNetworkActive || newPlayer.IsOwner;
        
        if (isLocalPlayer)
        {
            playerTransform = newPlayer.transform;
            Debug.Log($"[Crosshair] Player spawned and target updated to local player: {newPlayer.gameObject.name}");
        }
        else
        {
            Debug.Log($"[Crosshair] Skipping remote player: {newPlayer.gameObject.name}");
        }
    }
    
    private void Update()
    {
        UpdatePosition();
        UpdateAnimation();
        CheckForTargets();
    }
    
    private void SetupCrosshair()
    {
        // Get or add sprite renderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        // Use the assigned crosshair sprite
        if (crosshairSprite != null)
        {
            spriteRenderer.sprite = crosshairSprite;
        }
        else
        {
            Debug.LogWarning("No crosshair sprite assigned! Please assign a sprite in the inspector.");
        }
        
        spriteRenderer.color = crosshairColor;
        spriteRenderer.sortingLayerName = "UI";
        spriteRenderer.sortingOrder = 100; // Always on top
        
        // Hide mouse cursor
        Cursor.visible = false;
    }
    
    
    
    private void UpdatePosition()
    {
        if (playerCamera == null || playerTransform == null) return;
        
        // Get mouse position in world space
        Vector3 mousePosition = Input.mousePosition;
        Vector3 worldMousePosition = playerCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, playerCamera.nearClipPlane));
        worldMousePosition.z = 0f;
        
        if (clampToMaxDistance)
        {
            // Clamp crosshair to max distance from player
            Vector3 directionToMouse = (worldMousePosition - playerTransform.position).normalized;
            float distanceToMouse = Vector3.Distance(playerTransform.position, worldMousePosition);
            
            if (distanceToMouse <= maxDistance)
            {
                transform.position = worldMousePosition;
            }
            else
            {
                transform.position = playerTransform.position + directionToMouse * maxDistance;
            }
        }
        else
        {
            transform.position = worldMousePosition;
        }
    }
    
    private void UpdateAnimation()
    {
        if (spriteRenderer == null) return;
        
        if (enablePulse)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = Vector3.one * pulse;
        }
        
        if (enableColorAnimation)
        {
            float colorPulse = 1f + Mathf.Sin(Time.time * colorSpeed) * colorAmount;
            Color animatedColor = hasTargetInRange ? targetAvailableColor : crosshairColor;
            animatedColor *= colorPulse;
            animatedColor.a = crosshairColor.a;
            spriteRenderer.color = animatedColor;
        }
        else
        {
            spriteRenderer.color = hasTargetInRange ? targetAvailableColor : crosshairColor;
        }
    }
    
    private void CheckForTargets()
    {
        if (playerTransform == null) return;
        
        Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, targetCheckRadius, targetLayers);
        hasTargetInRange = targets.Length > 0;
        
        // Filter out the player itself
        foreach (var target in targets)
        {
            if (target.transform != playerTransform)
            {
                hasTargetInRange = true;
                return;
            }
        }
        hasTargetInRange = false;
    }
    
    // Public methods for external configuration
    public void SetColor(Color newColor)
    {
        crosshairColor = newColor;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = newColor;
        }
    }
    
    public void SetMaxDistance(float distance)
    {
        maxDistance = distance;
    }
    
    public void SetClampDistance(bool clamp)
    {
        clampToMaxDistance = clamp;
    }
    
    public void SetTargetCheckRadius(float radius)
    {
        targetCheckRadius = radius;
    }
    
    public void SetSprite(Sprite newSprite)
    {
        crosshairSprite = newSprite;
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = newSprite;
        }
    }
    
    // Get distance from player
    public float GetDistanceFromPlayer()
    {
        if (playerTransform == null) return 0f;
        return Vector3.Distance(transform.position, playerTransform.position);
    }
    
    // Check if crosshair is within range of player
    public bool IsInRange()
    {
        return GetDistanceFromPlayer() <= maxDistance;
    }
    
    private void OnDestroy()
    {
        // Show mouse cursor again when crosshair is destroyed
        Cursor.visible = true;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (playerTransform != null)
        {
            // Draw max distance range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerTransform.position, maxDistance);
            
            // Draw target check radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, targetCheckRadius);
        }
    }
}