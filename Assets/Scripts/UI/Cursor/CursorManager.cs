using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // Add this

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }
    
    [Header("Cursor Settings")]
    [SerializeField] private GameObject cursorReticlePrefab;
    [SerializeField] private GameObject uiCursorPrefab; // Cursor for menus/UI
    [SerializeField] private LayerMask groundLayerMask = 1; // What layers the cursor can target
    [SerializeField] private bool showReticle = true;
    
    [Header("Cursor Constraints")]
    [SerializeField] private float maxCursorDistance = 15f; // Max distance from player
    [SerializeField] private bool constrainToRange = true;
    
    [Header("Viewport Bounds Clamping")]
    [SerializeField] [Tooltip("Enable clamping mouse position to stay within viewport bounds")]
    private bool enableViewportClamping = false;
    
    [SerializeField] [Tooltip("Prevent mouse from exiting left edge")]
    private bool clampLeftEdge = false;
    
    [SerializeField] [Tooltip("Prevent mouse from exiting right edge")]
    private bool clampRightEdge = false;
    
    [SerializeField] [Tooltip("Prevent mouse from exiting top edge")]
    private bool clampTopEdge = false;
    
    [SerializeField] [Tooltip("Prevent mouse from exiting bottom edge (recommended for ability bar UI)")]
    private bool clampBottomEdge = true;
    
    [Header("Viewport Clamp Margins")]
    [SerializeField] [Tooltip("Margin from left edge in viewport units (0-0.5)")]
    [Range(0f, 0.5f)] private float leftEdgeMargin = 0.02f;
    
    [SerializeField] [Tooltip("Margin from right edge in viewport units (0-0.5)")]
    [Range(0f, 0.5f)] private float rightEdgeMargin = 0.02f;
    
    [SerializeField] [Tooltip("Margin from top edge in viewport units (0-0.5)")]
    [Range(0f, 0.5f)] private float topEdgeMargin = 0.02f;
    
    [SerializeField] [Tooltip("Margin from bottom edge in viewport units (0-0.5) - keeps cursor above ability bar")]
    [Range(0f, 0.5f)] private float bottomEdgeMargin = 0.15f;
    
    [Header("Enemy Targeting")]
    [SerializeField] private bool enableEnemyTargeting = true;
    [SerializeField] private float targetingRadius = 1.5f; // Detection radius around cursor
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private Color targetingCursorColor = Color.red;
    [SerializeField] private Color normalCursorColor = Color.white;
    [SerializeField] private Color enemyOutlineColor = Color.red;
    [SerializeField] private Material outlineMaterial; // Assign your OutlineMaterial here
    
    private GameObject currentReticle;
    private GameObject gameplayCursorInstance;
    private GameObject uiCursorInstance;
    private bool isInUIMode = false;
    private Vector2 cursorWorldPosition;
    private Vector2 constrainedCursorPosition;
    private Transform playerTransform;
    
    // Enemy targeting
    private Enemy currentTargetedEnemy;
    
    // Events for other systems to subscribe to
    public System.Action<Vector2> OnCursorPositionChanged;
    
    public Vector2 CursorWorldPosition => cursorWorldPosition;
    public Vector2 ConstrainedCursorPosition => constrainedCursorPosition;
    public Enemy TargetedEnemy => currentTargetedEnemy;
    public bool IsTargetingEnemy => currentTargetedEnemy != null;
    public bool IsInUIMode => isInUIMode;

    // ── Panel stack ───────────────────────────────────────────────────────────
    // Panels push a close-action when they open; ESC pops the top one.
    private readonly Stack<Action> _panelCloseStack = new Stack<Action>();
    public int PanelStackCount => _panelCloseStack.Count;

    /// <summary>
    /// Register a panel as open. Switches to UI cursor mode.
    /// <paramref name="closeAction"/> is the method that should be called to close the panel
    /// (e.g. CloseInventory, CloseCraftingTree). It must call <see cref="PopPanel"/> internally.
    /// </summary>
    public void PushPanel(Action closeAction)
    {
        _panelCloseStack.Push(closeAction);
        Debug.Log($"[CursorManager] PushPanel — stack size now {_panelCloseStack.Count} (top: {closeAction.Method.Name})");
        SetUIMode(true);
    }

    /// <summary>
    /// Deregister the most recently opened panel.
    /// Automatically exits UI cursor mode when no panels remain.
    /// Call this from the panel's own close method.
    /// </summary>
    public void PopPanel()
    {
        if (_panelCloseStack.Count > 0)
        {
            string poppedName = _panelCloseStack.Peek().Method.Name;
            _panelCloseStack.Pop();
            Debug.Log($"[CursorManager] PopPanel — popped '{poppedName}', stack size now {_panelCloseStack.Count}");
        }
        else
        {
            Debug.LogWarning("[CursorManager] PopPanel called but stack was already empty!");
        }
        if (_panelCloseStack.Count == 0)
            SetUIMode(false);
    }

    /// <summary>
    /// Called by the ESC handler. Invokes the topmost panel's close action.
    /// The close action is responsible for calling <see cref="PopPanel"/>.
    /// Returns true if a panel was closed, false if the stack was empty.
    /// </summary>
    public bool TryCloseTopPanel()
    {
        if (_panelCloseStack.Count == 0) return false;
        string topName = _panelCloseStack.Peek().Method.Name;
        Debug.Log($"[CursorManager] TryCloseTopPanel — invoking '{topName}'");
        _panelCloseStack.Peek().Invoke();
        return true;
    }

    /// <summary>
    /// Switches between gameplay cursor and UI cursor
    /// </summary>
    public void SetUIMode(bool enabled)
    {
        if (isInUIMode == enabled) return;
        
        isInUIMode = enabled;
        SwitchCursor();
    }
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Initialize()
    {
        Debug.Log("========================================");
        Debug.Log("[CursorManager] Initializing...");
        
        // Create cursor reticle
        if (showReticle)
        {
            CreateCursorReticle();
        }
        else
        {
            Debug.Log("[CursorManager] Reticle display disabled");
        }
        
        // Hide system cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
        
        Debug.Log("[CursorManager] ✓ Initialization complete");
        Debug.Log("========================================");
    }
    
    private void Start()
    {
        // Find player reference
        FindPlayerReference();
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
        // Only follow the local/owning player — remote instances move independently
        bool isNetworkActive = newPlayer.IsServerStarted || newPlayer.IsClientStarted;
        if (isNetworkActive && !newPlayer.IsOwner) return;

        playerTransform = newPlayer.transform;
    }
    
    private void LateUpdate()
    {
        // Force system cursor to stay hidden
        Cursor.visible = false;
        
        // Clamp mouse position to viewport bounds if enabled
        if (enableViewportClamping)
        {
            ClampMouseToViewportBounds();
        }
        
        UpdateCursorPosition();
        ConstrainCursorToRange();
        
        // Update enemy targeting
        if (enableEnemyTargeting)
        {
            UpdateEnemyTargeting();
        }
        
        UpdateReticlePosition();
    }
    
    private void FindPlayerReference()
    {
        // Find local player
        PlayerController playerController = PlayerController.GetLocalPlayer();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
        }
    }
    
    private void UpdateCursorPosition()
    {
        // Use InputUtility which correctly handles Camera.main (works with Cinemachine)
        Vector3 worldPos = InputUtility.GetMouseWorldPosition();
        cursorWorldPosition = new Vector2(worldPos.x, worldPos.y);
    }
    
    /// <summary>
    /// Clamp mouse position to stay within viewport bounds
    /// Prevents cursor from exiting configured edges
    /// </summary>
    private void ClampMouseToViewportBounds()
    {
        if (Mouse.current == null) return;
        
        Camera cam = Camera.main;
        if (cam == null) return;
        
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseViewportPos = cam.ScreenToViewportPoint(mouseScreenPos);
        
        bool needsClamp = false;
        Vector2 clampedViewportPos = mouseViewportPos;
        
        // Clamp left edge
        if (clampLeftEdge && mouseViewportPos.x < leftEdgeMargin)
        {
            clampedViewportPos.x = leftEdgeMargin;
            needsClamp = true;
        }
        
        // Clamp right edge
        if (clampRightEdge && mouseViewportPos.x > (1f - rightEdgeMargin))
        {
            clampedViewportPos.x = 1f - rightEdgeMargin;
            needsClamp = true;
        }
        
        // Clamp top edge
        if (clampTopEdge && mouseViewportPos.y > (1f - topEdgeMargin))
        {
            clampedViewportPos.y = 1f - topEdgeMargin;
            needsClamp = true;
        }
        
        // Clamp bottom edge
        if (clampBottomEdge && mouseViewportPos.y < bottomEdgeMargin)
        {
            clampedViewportPos.y = bottomEdgeMargin;
            needsClamp = true;
        }
        
        // Apply clamped position if needed
        if (needsClamp)
        {
            if (cam == null) return;
            Vector3 clampedScreenPos = cam.ViewportToScreenPoint(clampedViewportPos);
            Mouse.current.WarpCursorPosition(new Vector2(clampedScreenPos.x, clampedScreenPos.y));
        }
    }
    
    private void ConstrainCursorToRange()
    {
        if (playerTransform == null || !constrainToRange)
        {
            constrainedCursorPosition = cursorWorldPosition;
            return;
        }
        
        Vector2 playerPosition = playerTransform.position;
        float distanceToPlayer = Vector2.Distance(cursorWorldPosition, playerPosition);
        
        if (distanceToPlayer <= maxCursorDistance)
        {
            constrainedCursorPosition = cursorWorldPosition;
        }
        else
        {
            // Constrain to max distance
            Vector2 directionToMouse = (cursorWorldPosition - playerPosition).normalized;
            constrainedCursorPosition = playerPosition + directionToMouse * maxCursorDistance;
        }
        
        // Notify subscribers of position change
        OnCursorPositionChanged?.Invoke(constrainedCursorPosition);
    }
    
    private void CreateCursorReticle()
    {
        Debug.Log("[CursorManager] Creating cursor reticles...");
        
        // Create gameplay cursor
        if (cursorReticlePrefab != null)
        {
            gameplayCursorInstance = Instantiate(cursorReticlePrefab);
            gameplayCursorInstance.name = "GameplayCursor";
            DontDestroyOnLoad(gameplayCursorInstance);
            SetupCursorCanvas(gameplayCursorInstance, 50); // Below tooltips
            Debug.Log("[CursorManager] Gameplay cursor created");
        }
        else
        {
            Debug.LogWarning("[CursorManager] No gameplay cursor prefab assigned!");
        }
        
        // Create UI cursor
        if (uiCursorPrefab != null)
        {
            uiCursorInstance = Instantiate(uiCursorPrefab);
            uiCursorInstance.name = "UICursor";
            DontDestroyOnLoad(uiCursorInstance);
            SetupCursorCanvas(uiCursorInstance, 50); // Below tooltips
            uiCursorInstance.SetActive(false); // Start hidden
            Debug.Log("[CursorManager] UI cursor created (hidden)");
        }
        else
        {
            Debug.LogWarning("[CursorManager] No UI cursor prefab assigned - will use gameplay cursor for menus");
        }
        
        // Set initial cursor
        currentReticle = gameplayCursorInstance;
        
        if (currentReticle != null)
        {
            Debug.Log($"[CursorManager] Active cursor: {currentReticle.name}");
        }
        else
        {
            Debug.LogError("[CursorManager] No cursor active! Assign cursor prefabs in inspector.");
        }
    }
    
    private void SetupCursorCanvas(GameObject cursor, int sortOrder)
    {
        if (cursor == null) return;
            
        // Check if it has a Canvas (UI-based cursor)
        Canvas reticleCanvas = cursor.GetComponent<Canvas>();
        if (reticleCanvas != null)
        {
            // Set Canvas to Screen Space - Overlay for true screen space rendering
            reticleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            reticleCanvas.sortingOrder = sortOrder; // Below tooltips (100) but above most UI
            
            // CRITICAL: Disable Graphic Raycaster so cursor doesn't block UI clicks
            UnityEngine.UI.GraphicRaycaster raycaster = cursor.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
            {
                Destroy(raycaster);
            }
            
            // CRITICAL: Disable raycast target on all cursor graphics so they don't block UI
            var images = cursor.GetComponentsInChildren<UnityEngine.UI.Image>();
            foreach (var img in images)
            {
                img.raycastTarget = false;
            }
            
            // Configure particle systems to render properly with Canvas
            var particleSystems = cursor.GetComponentsInChildren<ParticleSystemRenderer>();
            foreach (var psRenderer in particleSystems)
            {
                psRenderer.sortingLayerID = reticleCanvas.sortingLayerID;
                psRenderer.sortingOrder = sortOrder + 1; // Render particles on top of cursor
            }
        }
        else
        {
            // Fallback: SpriteRenderer based cursor (world space)
            SpriteRenderer reticleRenderer = cursor.GetComponent<SpriteRenderer>();
            if (reticleRenderer != null)
            {
                reticleRenderer.sortingLayerName = "UI";
                reticleRenderer.sortingOrder = sortOrder;
            }
        }
    }
    
    private void SwitchCursor()
    {
        if (isInUIMode)
        {
            // Switch to UI cursor
            if (gameplayCursorInstance != null)
                gameplayCursorInstance.SetActive(false);
            
            if (uiCursorInstance != null)
            {
                uiCursorInstance.SetActive(true);
                currentReticle = uiCursorInstance;
            }
            else
            {
                // Fallback to gameplay cursor if no UI cursor assigned
                if (gameplayCursorInstance != null)
                    gameplayCursorInstance.SetActive(true);
                currentReticle = gameplayCursorInstance;
            }
        }
        else
        {
            // Switch to gameplay cursor
            if (uiCursorInstance != null)
                uiCursorInstance.SetActive(false);
            
            if (gameplayCursorInstance != null)
            {
                gameplayCursorInstance.SetActive(true);
                currentReticle = gameplayCursorInstance;
            }
        }
    }
    
    private void UpdateReticlePosition()
    {
        if (currentReticle == null) return;
        
        // Check if this is a Canvas-based cursor (UI)
        Canvas reticleCanvas = currentReticle.GetComponent<Canvas>();
        if (reticleCanvas != null && (reticleCanvas.renderMode == RenderMode.ScreenSpaceOverlay))
        {
            // For screen-space UI, use mouse screen position directly
            if (Mouse.current != null)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                
                // Find the image child (the actual cursor graphic)
                RectTransform imageTransform = currentReticle.GetComponentInChildren<UnityEngine.UI.Image>()?.GetComponent<RectTransform>();
                if (imageTransform != null)
                {
                    imageTransform.position = mouseScreenPos;
                }
                else
                {
                    // Fallback: use the root RectTransform
                    RectTransform rectTransform = currentReticle.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        rectTransform.position = mouseScreenPos;
                    }
                }
            }
        }
        else
        {
            // For world-space reticle (SpriteRenderer)
            currentReticle.transform.position = constrainedCursorPosition;
        }
        
        // Change color based on targeting or constraint (gameplay cursor only)
        if (!isInUIMode)
        {
            Color targetColor = normalCursorColor;
            
            // Priority: targeting overrides constraint color
            if (enableEnemyTargeting && currentTargetedEnemy != null)
            {
                targetColor = targetingCursorColor;
            }
            else if (constrainToRange && playerTransform != null)
            {
                float distanceToPlayer = Vector2.Distance(cursorWorldPosition, playerTransform.position);
                bool isConstrained = distanceToPlayer > maxCursorDistance;
                targetColor = isConstrained ? Color.red : normalCursorColor;
            }
            
            // Apply color to cursor
            var image = currentReticle.GetComponentInChildren<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.color = targetColor;
            }
            
            var reticleRenderer = currentReticle.GetComponent<SpriteRenderer>();
            if (reticleRenderer != null)
            {
                reticleRenderer.color = targetColor;
            }
        }
    }
    
    public void SetMaxCursorDistance(float distance)
    {
        maxCursorDistance = distance;
    }
    
    public void SetConstrainToRange(bool constrain)
    {
        constrainToRange = constrain;
    }
    
    public void ShowReticle(bool show)
    {
        showReticle = show;
        if (currentReticle != null)
        {
            currentReticle.SetActive(show);
        }
    }
    
    private void UpdateEnemyTargeting()
    {

        
        // Find all enemies within detection radius of cursor
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(cursorWorldPosition, targetingRadius, enemyLayerMask);
       
        
        Enemy closestEnemy = null;
        float closestDistance = float.MaxValue;
        
        // Find enemy closest to cursor center
        foreach (Collider2D col in nearbyColliders)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null && enemy.isActiveAndEnabled)
            {
                float distance = Vector3.Distance(cursorWorldPosition, enemy.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = enemy;
                }
            }
        }
        

        
        // Find enemy closest to cursor center
        foreach (Collider2D col in nearbyColliders)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null && enemy.isActiveAndEnabled)
            {
                float distance = Vector3.Distance(cursorWorldPosition, enemy.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = enemy;
                }
            }
        }

    }
    
    
    
    private void OnDestroy()
    {
        
        // Show system cursor when destroyed
        Cursor.visible = true;
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw cursor range in scene view
        if (playerTransform != null && constrainToRange)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerTransform.position, maxCursorDistance); // Fixed method name
        }
        
        // Draw cursor position
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(constrainedCursorPosition, 0.5f);
        
        // Draw targeting radius
        if (enableEnemyTargeting)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(cursorWorldPosition, targetingRadius);
            
            if (currentTargetedEnemy != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(cursorWorldPosition, currentTargetedEnemy.transform.position);
            }
        }
    }
}

