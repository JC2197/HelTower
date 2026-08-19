using UnityEngine;
using UnityEngine.Rendering;

public class AccessorySortingManager : MonoBehaviour
{
    private SpriteRenderer characterRenderer;
    private CharacterData characterData;

    private const string AccessoryTransformDebugTag = "[Debug]";

    public void Initialize(SpriteRenderer renderer, CharacterData data)
    {
        characterRenderer = renderer;
        characterData = data;
    }

    public enum Direction
    {
        NorthEast,
        NorthWest,
        SouthEast,
        SouthWest
    }

    // Explicit mapping based on CharacterData configuration
    public Direction GetDirectionFromAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName) || characterData == null)
            return Direction.SouthEast; // Default direction instead of Unknown

        // // Direct mapping - NO string comparisons
        // if (animationName == characterData.GetIdleAnimation())
        //     return characterRenderer.flipX ? GetFlippedDirection(characterData.GetIdleDirection()) : characterData.GetIdleDirection();

        // if (animationName == characterData.GetIdleUpAnimation())
        //     return characterRenderer.flipX ? GetFlippedDirection(characterData.GetIdleUpDirection()) : characterData.GetIdleUpDirection();

        // if (animationName == characterData.GetRunAnimation())
        //     return characterRenderer.flipX ? GetFlippedDirection(characterData.GetRunDirection()) : characterData.GetRunDirection();

        // if (animationName == characterData.GetRunUpAnimation())
        //     return characterRenderer.flipX ? GetFlippedDirection(characterData.GetRunUpDirection()) : characterData.GetRunUpDirection();

        // Fallback for abilities
        return DetectDirectionFromKeywords(animationName);
    }

    private Direction GetFlippedDirection(Direction dir)
    {
        return dir switch
        {
            Direction.NorthEast => Direction.NorthWest,
            Direction.NorthWest => Direction.NorthEast,
            Direction.SouthEast => Direction.SouthWest,
            Direction.SouthWest => Direction.SouthEast,
            _ => dir
        };
    }

    private Direction DetectDirectionFromKeywords(string animationName)
    {
        string lowerAnim = animationName.ToLower();

        if (lowerAnim.Contains("up") && !lowerAnim.Contains("idle"))
        {
            return characterRenderer.flipX ? Direction.NorthWest : Direction.NorthEast;
        }

        if (lowerAnim.Contains("down"))
        {
            return characterRenderer.flipX ? Direction.SouthWest : Direction.SouthEast;
        }

        // Default to horizontal diagonal based on flip
        return characterRenderer.flipX ? Direction.SouthWest : Direction.SouthEast;
    }

    // Get Accessory behind setting for a direction
    public bool ShouldAccessoryBeBehind(Direction direction, AccessorySettings AccessorySettings)
    {
        if (AccessorySettings == null)
            return false;
        
        return direction switch
        {
            Direction.NorthEast => AccessorySettings.AccessoryBehindOnNE,
            Direction.NorthWest => AccessorySettings.AccessoryBehindOnNW,
            Direction.SouthEast => AccessorySettings.AccessoryBehindOnSE,
            Direction.SouthWest => AccessorySettings.AccessoryBehindOnSW,
            _ => false
        };
    }

    // Get hand behind setting for a direction
    public bool ShouldHandBeBehind(Direction direction, AccessorySettings AccessorySettings)
    {
        if (AccessorySettings == null)
            return false;

        return direction switch
        {
            Direction.NorthEast => AccessorySettings.handBehindOnNE,
            Direction.NorthWest => AccessorySettings.handBehindOnNW,
            Direction.SouthEast => AccessorySettings.handBehindOnSE,
            Direction.SouthWest => AccessorySettings.handBehindOnSW,
            _ => false
        };
    }

    // Update Accessory sorting with movement context
    public void UpdateAccessorySorting(string animationName, Transform AccessoryTransform, AccessorySettings AccessorySettings, Vector2 movement = default)
    {
        Debug.Log($"[AccessorySortingManager] Updating Accessory sorting for animation: {animationName}");
        if (AccessoryTransform == null || characterRenderer == null)
            return;

        // Find Accessory sprite renderer (exclude HandHolders)
        SpriteRenderer AccessoryRenderer = null;
        Transform AccessorySpriteChild = AccessoryTransform.Find("AccessorySprite");
        if (AccessorySpriteChild != null)
        {
            AccessoryRenderer = AccessorySpriteChild.GetComponent<SpriteRenderer>();
        }
        if (AccessoryRenderer == null)
        {
            foreach (SpriteRenderer sr in AccessoryTransform.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sr.gameObject.name.Contains("HandHolder"))
                {
                    AccessoryRenderer = sr;
                    break;
                }
            }
        }
        if (AccessoryRenderer == null)
            return;

        Direction direction = GetDirectionFromAnimation(animationName);

        // Special case: if using run animation and moving, check if diagonal down
        // if (animationName == characterData.GetRunAnimation() && movement != default)
        // {
        //     if (Mathf.Abs(movement.y) > 0.1f && movement.y < 0)
        //     {
        //         // Moving diagonal down
        //         direction = characterRenderer.flipX ? Direction.SouthWest : Direction.SouthEast;
        //     }
        // }

        bool AccessoryBehind = ShouldAccessoryBeBehind(direction, AccessorySettings);

        int newSortingOrder = characterRenderer.sortingOrder + (AccessoryBehind ? -10 : 10);
        ApplyAccessorySortingOrder(AccessorySpriteChild, AccessoryRenderer, newSortingOrder);

        // Update HandHolder sprites using unified sorting logic
        UpdateHandHolderSorting(AccessoryTransform, newSortingOrder, direction, AccessorySettings);

        // Also update sorting layer for HandHolders
        foreach (Transform child in AccessoryTransform.GetComponentsInChildren<Transform>())
        {
            if (child.name.Contains("HandHolder"))
            {
                SpriteRenderer handSR = child.GetComponent<SpriteRenderer>();
                if (handSR != null)
                {
                    handSR.sortingLayerName = AccessoryRenderer.sortingLayerName;
                }
            }
        }
    }

    /// <summary>
    /// Applies sorting order to the Accessory SpriteRenderer and HandHolder child sprites.
    /// Called from PlayerController for both owner and remote players so this logic
    /// lives next to ShouldAccessoryBeBehind rather than being duplicated in PC.
    /// </summary>
    public void ApplyAccessoryRendererSorting(Transform Accessory, Direction aimDir, AccessorySettings AccessorySettings, SpriteRenderer characterRenderer)
    {
        if (Accessory == null || characterRenderer == null || AccessorySettings == null) return;

        bool AccessoryBehind = ShouldAccessoryBeBehind(aimDir, AccessorySettings);

        SpriteRenderer AccessoryRenderer = null;
        Transform AccessorySpriteChild = Accessory.Find("AccessorySprite");
        if (AccessorySpriteChild != null)
            AccessoryRenderer = AccessorySpriteChild.GetComponent<SpriteRenderer>();
        if (AccessoryRenderer == null)
        {
            foreach (SpriteRenderer sr in Accessory.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sr.gameObject.name.Contains("HandHolder"))
                {
                    AccessoryRenderer = sr;
                    break;
                }
            }
        }
        if (AccessoryRenderer == null) return;

        int newSortingOrder = characterRenderer.sortingOrder + (AccessoryBehind ? -10 : 10);
        ApplyAccessorySortingOrder(AccessorySpriteChild, AccessoryRenderer, newSortingOrder);

        // Update HandHolder sprites using unified sorting logic
        UpdateHandHolderSorting(Accessory, newSortingOrder, aimDir, AccessorySettings);

        // Also update sorting layer for HandHolders
        foreach (Transform child in Accessory.GetComponentsInChildren<Transform>())
        {
            if (child.name.Contains("HandHolder"))
            {
                SpriteRenderer handSR = child.GetComponent<SpriteRenderer>();
                if (handSR != null)
                {
                    handSR.sortingLayerName = AccessoryRenderer.sortingLayerName;
                }
            }
        }
    }

    /// <summary>
    /// Applies the character's directional flip (FeetHolder X-scale) and backpack flip + sorting.
    /// Consolidates all character-facing visual logic so it runs identically for owner and remote clients.
    /// </summary>
    public void ApplyCharacterFlipAndBackpackSorting(Transform feetHolder, Transform backpackHolder, bool aimingLeft, Direction aimDir, SpriteRenderer characterRenderer)
    {
        if (feetHolder != null)
        {
            Vector3 scale = feetHolder.localScale;
            scale.x = aimingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            feetHolder.localScale = scale;
        }

        if (backpackHolder != null)
        {
            Transform equippedBackpack = backpackHolder.Find("EquippedBackpack");
            if (equippedBackpack != null)
            {
                if (characterRenderer != null)
                {
                    SpriteRenderer backpackRenderer = equippedBackpack.GetComponent<SpriteRenderer>();
                    if (backpackRenderer != null)
                    {
                        bool backpackBehind = (aimDir == Direction.SouthEast || aimDir == Direction.SouthWest);
                        int backpackSortingOrder = characterRenderer.sortingOrder + (backpackBehind ? -20 : 20);
                        backpackRenderer.sortingOrder = backpackSortingOrder;

                        foreach (ParticleSystemRenderer psRenderer in equippedBackpack.GetComponentsInChildren<ParticleSystemRenderer>())
                        {
                            psRenderer.sortingLayerName = backpackRenderer.sortingLayerName;
                            psRenderer.sortingOrder = backpackSortingOrder;
                        }
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("[AccessorySortingManager] No backpack holder found for sorting update!");
        }
    }

    /// <summary>
    /// Adjusts sorting order for dual-wielding based on facing direction.
    /// Main-hand renders in front when facing right, offhand renders in front when facing left.
    /// </summary>
    public void ApplyDualWieldSorting(Transform mainAccessory, Transform offhandAccessory, bool facingLeft, Direction aimDir, AccessorySettings mainAccessorySettings, AccessorySettings offhandAccessorySettings)
    {
        if (mainAccessory == null || offhandAccessory == null) return;

        // Find Accessory sprite renderers (exclude HandHolders)
        SpriteRenderer mainRenderer = FindAccessoryRenderer(mainAccessory);
        SpriteRenderer offhandRenderer = FindAccessoryRenderer(offhandAccessory);

        if (mainRenderer == null || offhandRenderer == null) return;

        // Determine which Accessory should be +1 in front based on facing direction
        if (facingLeft)
        {
            // Facing left: offhand is closer to camera, render it in front
            offhandRenderer.sortingOrder += 10;
        }
        else
        {
            // Facing right: main-hand is closer to camera, render it in front
            mainRenderer.sortingOrder += 10;
        }

        // Update HandHolder sprites using HandBehind settings for each Accessory
        UpdateHandHolderSorting(mainAccessory, mainRenderer.sortingOrder, aimDir, mainAccessorySettings);
        UpdateHandHolderSorting(offhandAccessory, offhandRenderer.sortingOrder, aimDir, offhandAccessorySettings);
    }

    /// <summary>
    /// Routes a new sorting order to the Accessory's SortingGroup when one exists on the
    /// AccessorySprite child (e.g. bows that include an Arrow child renderer), or falls back
    /// to setting it directly on the SpriteRenderer. This keeps all children of the
    /// SortingGroup ordered relative to each other while still sorting against the character.
    /// </summary>
    private void ApplyAccessorySortingOrder(Transform AccessorySpriteChild, SpriteRenderer AccessoryRenderer, int order)
    {
        if (AccessorySpriteChild != null)
        {
            SortingGroup sg = AccessorySpriteChild.GetComponent<SortingGroup>();
            if (sg != null)
            {
                sg.sortingOrder = order;
                return;
            }
        }

        if (AccessoryRenderer != null)
            AccessoryRenderer.sortingOrder = order;
    }

    private SpriteRenderer FindAccessoryRenderer(Transform Accessory)
    {
        Transform AccessorySpriteChild = Accessory.Find("AccessorySprite");
        if (AccessorySpriteChild != null)
        {
            SpriteRenderer sr = AccessorySpriteChild.GetComponent<SpriteRenderer>();
            if (sr != null) return sr;
        }

        foreach (SpriteRenderer sr in Accessory.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!sr.gameObject.name.Contains("HandHolder"))
            {
                return sr;
            }
        }
        return null;
    }

    private void UpdateHandHolderSorting(Transform Accessory, int AccessorySortingOrder, Direction aimDir = Direction.SouthEast, AccessorySettings AccessorySettings = null)
    {
        // Determine hand sorting offset based on HandBehind settings
        bool handBehind = false;
        if (AccessorySettings != null)
        {
            handBehind = ShouldHandBeBehind(aimDir, AccessorySettings);
        }
        int handSortingOffset = handBehind ? -2 : 2;

        foreach (Transform child in Accessory.GetComponentsInChildren<Transform>())
        {
            if (child.name.Contains("HandHolder"))
            {
                SpriteRenderer handSR = child.GetComponent<SpriteRenderer>();
                if (handSR != null)
                {
                    handSR.sortingOrder = AccessorySortingOrder + handSortingOffset;
                }
            }
        }
    }

    private float GetLiveUnlockedAngle(Transform Accessory, Camera mainCamera)
    {
        if (Accessory == null || mainCamera == null || UnityEngine.InputSystem.Mouse.current == null)
            return 0f;

        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        mouseWorldPos.z = 0f;

        // Aim origin MUST be a point that is unaffected by the Accessory's firing animation.
        // Previously we used the LaunchZone position, but when the LaunchZone is parented under
        // the animated AccessorySprite child, a shot animation containing forward-X movement pushes
        // that origin across the cursor each frame. When the cursor is close to the player the
        // origin→mouse vector then flips sign every frame, causing the Accessory/character to flip
        // wildly over the axis. Compute the angle from the player root (this component lives on the
        // player) instead — a stable pivot that never moves with the Accessory animation. The
        // LaunchZone is still used for the projectile SPAWN position elsewhere, so barrel accuracy
        // is preserved while the aim direction stays stable.
        Transform stableOrigin = Accessory.root != null ? Accessory.root : transform;
        Vector3 aimOrigin = stableOrigin.position;
        aimOrigin.z = 0f;

        Vector2 originToMouse = (Vector2)(mouseWorldPos - aimOrigin);
        if (originToMouse.sqrMagnitude <= 0.000001f)
            return 0f;

        return Mathf.Atan2(originToMouse.y, originToMouse.x) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Update Accessory aiming, positioning, rotation, and sorting.
    /// Handles both local (mouse-aimed) and remote (synced angle) players.
    /// </summary>
    public void UpdateActiveAimingAccessory(
        Transform Accessory,
        AccessorySettings AccessorySettings,
        string AccessoryName,
        Direction aimDir,
        Transform playerTransform,
        Camera mainCamera,
        SpriteRenderer playerSpriteRenderer,
        bool flipSpriteOnMove,
        Transform backpackHolder,
        System.Func<bool> isFacingLeftGetter,
        System.Action<bool> isFacingLeftSetter,
        System.Action<bool> syncFacingLeftToNetwork,
        bool isNetworkActive,
        bool isOwner,
        float? overrideAngle = null)
    {
        if (Accessory == null || AccessorySettings == null) return;

        // === STEP 1 & 2: Determine Accessory aim angle ===
        float targetAngle;
        Vector2 aimDirection;
            Vector3 ownerMouseWorldPos = Vector3.zero;
        bool hasOwnerMouseWorldPos = false;
        bool isDirectionLocked = false;
        float lockedAngle = 0f;
        Ability[] abilities = null;
        DataDrivenAbility lockedAbility = null;

        if (overrideAngle.HasValue)
        {
            // Remote player: use the synced angle directly (no mouse, no ability-lock)
            targetAngle = overrideAngle.Value;
            aimDirection = new Vector2(
                Mathf.Cos(targetAngle * Mathf.Deg2Rad),
                Mathf.Sin(targetAngle * Mathf.Deg2Rad)
            );

            // Apply Accessory's 2-direction clamping if configured
            if (AccessorySettings.lockTo2Directions)
            {
                targetAngle = SnapToCardinalDirection(targetAngle);
                aimDirection = new Vector2(
                    Mathf.Cos(targetAngle * Mathf.Deg2Rad),
                    Mathf.Sin(targetAngle * Mathf.Deg2Rad)
                );
            }
        }
        else
        {
            // Owner: need camera for mouse-based aiming
            if (mainCamera == null)
            {
                Debug.LogWarning("No MainCamera found!");
                return;
            }

    

            if (isDirectionLocked)
            {
                // Ability has locked Accessory to a specific angle (bypasses all Accessory clamping)
                // This angle was captured from RAW mouse position when ability fired
                if (lockedAbility != null && lockedAbility.ContinueRotatingDuringUnlock)
                {
                    lockedAngle = GetLiveUnlockedAngle(Accessory, mainCamera);
                }

                targetAngle = lockedAngle;
                aimDirection = new Vector2(
                    Mathf.Cos(lockedAngle * Mathf.Deg2Rad),
                    Mathf.Sin(lockedAngle * Mathf.Deg2Rad)
                );
            }
            else
            {
                ownerMouseWorldPos = mainCamera.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
                ownerMouseWorldPos.z = 0f;
                hasOwnerMouseWorldPos = true;

                // Primary direction from player center — used for facing (N/S/E/W) logic only.
                Vector2 playerToMouse = (Vector2)(ownerMouseWorldPos - playerTransform.position);
                float dist = playerToMouse.magnitude;
                aimDirection = dist > 0.001f ? playerToMouse / dist : Vector2.right;

                // Temporary angle from player center; this is refined after we resolve
                // the per-direction Accessory mount offset.
                Vector2 originToMouse = (Vector2)(ownerMouseWorldPos - playerTransform.position);
                targetAngle = Mathf.Atan2(originToMouse.y, originToMouse.x) * Mathf.Rad2Deg;

                // Apply Accessory's 2-direction clamping if configured
                if (AccessorySettings.lockTo2Directions)
                {
                    targetAngle = SnapToCardinalDirection(targetAngle);
                    aimDirection = new Vector2(
                        Mathf.Cos(targetAngle * Mathf.Deg2Rad),
                        Mathf.Sin(targetAngle * Mathf.Deg2Rad)
                    );
                }
            }
        }

        float characterAngle = targetAngle;

        // Use the provided direction (calculated in UpdateMovementAnimation)
        Vector2 AccessoryOffset = Vector2.zero;
        Vector2 handsOffset = Vector2.zero;
        int handsSortingOrder = 0;
        bool aimingLeft = false;
        Transform handsHolder = playerTransform.Find("OffHandHolder");
        Transform equippedHands = handsHolder?.Find("EquippedHands");

        SpriteRenderer handSprite = equippedHands != null ? equippedHands.GetComponent<SpriteRenderer>() : null;

        // Set Accessory offset, hands offset, and determine facing direction based on aim direction
        switch (aimDir)
        {
            case Direction.NorthEast:
                AccessoryOffset = AccessorySettings.northEastOffset;
                handsOffset = new Vector2(0f, 0f);
                handsSortingOrder = -20;
                aimingLeft = false;
                break;
            case Direction.NorthWest:
                AccessoryOffset = AccessorySettings.northWestOffset;
                handsOffset = new Vector2(-0.065f, 0f);
                handsSortingOrder = 20;
                aimingLeft = true;
                break;
            case Direction.SouthEast:
                AccessoryOffset = AccessorySettings.southEastOffset;
                handsOffset = new Vector2(-0.1f, 0f);
                handsSortingOrder = -20;
                aimingLeft = false;
                break;
            case Direction.SouthWest:
                AccessoryOffset = AccessorySettings.southWestOffset;
                handsOffset = new Vector2(0f, 0f);
                handsSortingOrder = 20;
                aimingLeft = true;
                break;
        }

        // Refine owner aim angle by accounting for the configured Accessory mount offset.
        // Use a stable origin (player center + static config offset), never an animated child.
        if (hasOwnerMouseWorldPos && !isDirectionLocked)
        {
            Vector3 aimOrigin = playerTransform.position + new Vector3(AccessoryOffset.x, AccessoryOffset.y, 0f);
            Vector2 offsetOriginToMouse = (Vector2)(ownerMouseWorldPos - aimOrigin);

            if (offsetOriginToMouse.sqrMagnitude > 0.000001f)
                targetAngle = Mathf.Atan2(offsetOriginToMouse.y, offsetOriginToMouse.x) * Mathf.Rad2Deg;

            if (AccessorySettings.lockTo2Directions)
                targetAngle = SnapToCardinalDirection(targetAngle);
        }

        // Apply hands position and sorting
        if (equippedHands != null && handSprite != null)
        {
            equippedHands.localPosition = handsOffset;
            handSprite.sortingOrder = handsSortingOrder;
        }
        if (isDirectionLocked)
        {
            // When Accessory direction is locked, use locked angle directly with radius positioning
            float radius = AccessorySettings.aimingRadius;
            Vector3 AccessoryPosition = new Vector3(
                Mathf.Cos(lockedAngle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(lockedAngle * Mathf.Deg2Rad) * radius,
                0f
            );
            AccessoryPosition += new Vector3(AccessoryOffset.x, AccessoryOffset.y, 0f);
            Accessory.localPosition = AccessoryPosition;

            // Direction-locked aiming uses pure parent rotation by default so a single 360-degree
            // Accessory animation can play unchanged relative to the Accessory root. Optional left-facing
            // Y-flip can be re-enabled per-ability via AbilityDataConfig.flipYOnLeftFacing.
            float normalizedLocked = lockedAngle < 0 ? lockedAngle + 360f : lockedAngle;
            bool lockedPointingLeft = normalizedLocked > 90f && normalizedLocked < 270f;

            bool flipYOnLeftFacingDuringLock = lockedAbility != null && lockedAbility.FlipYOnLeftFacingDuringLock;
            bool flipXOnLeftFacingDuringLock = lockedAbility != null && lockedAbility.FlipXOnLeftFacingDuringLock;
            float rotationAngle = lockedAngle;
            
            Debug.Log($"<color=yellow>[AccessorySortingManager] Locked Accessory - lockedAngle: {lockedAngle:F1}°, normalized: {normalizedLocked:F1}°, pointingLeft: {lockedPointingLeft}, flipYOnLeftFacingDuringLock: {flipYOnLeftFacingDuringLock}, continueRotatingDuringUnlock: {lockedAbility != null && lockedAbility.ContinueRotatingDuringUnlock}, final rotation: {rotationAngle:F1}°</color>");
            
            Accessory.rotation = Quaternion.Euler(0, 0, rotationAngle);
        }
        else if (AccessorySettings.lockTo2Directions)
        {
            // Lock to 2 directions: don't rotate, use sprite flips.
            // (targetAngle is already snapped to 0° or 180° from STEP 2)
            Accessory.rotation = Quaternion.identity;
            Accessory.localPosition = AccessoryOffset;
        }
        else
        {
            // Full 360 rotation: Calculate position based on radius + offset
            float radius = AccessorySettings.aimingRadius;
            Vector3 AccessoryPosition = new Vector3(
                Mathf.Cos(targetAngle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(targetAngle * Mathf.Deg2Rad) * radius,
                0f
            );

            // Apply direction-based offset on top of radius position
            AccessoryPosition += new Vector3(AccessoryOffset.x, AccessoryOffset.y, 0f);

            Accessory.rotation = Quaternion.Euler(0, 0, targetAngle);
            Accessory.localPosition = AccessoryPosition;
        }

        // Handle Accessory flipping (applies to both aiming modes)
        // Flip the parent Accessory GameObject so all children and animation offsets flip together.
        // IMPORTANT: Always write explicit ±1f scale values instead of reading the current
        // localScale and using Mathf.Abs(). Reading back the current scale each frame caused
        // Y-scale drift to tiny values because external systems (Accessory Animator, FishNet sync,
        // Unity TRS decomposition after world-rotation set) can modify scale between frames.
        float scaleX = 1f;
        float scaleY = 1f;
        bool isFlipped = false;

        // Check if any ability allows flipping during rotation lock (timed lock)
        bool allowFlipDuringLock = false;
        if (abilities != null)
        {
            foreach (Ability ability in abilities)
            {
                DataDrivenAbility dataDriven = ability as DataDrivenAbility;
                if (dataDriven != null && dataDriven.AllowFlipDuringLock)
                {
                    allowFlipDuringLock = true;
                    break;
                }
            }
        }

        if (isDirectionLocked && !allowFlipDuringLock)
        {
            // During direction lock, keep the animation space stable relative to the Accessory root.
            // Only apply the optional Y-flip when explicitly requested by the locked ability.
            float normalizedLocked = lockedAngle < 0 ? lockedAngle + 360f : lockedAngle;
            bool lockedPointingLeft = normalizedLocked > 90f && normalizedLocked < 270f;

            bool flipYOnLeftFacingDuringLock = lockedAbility != null && lockedAbility.FlipYOnLeftFacingDuringLock;
            bool flipXOnLeftFacingDuringLock = lockedAbility != null && lockedAbility.FlipXOnLeftFacingDuringLock;
    
            scaleX = (flipXOnLeftFacingDuringLock && AccessorySettings.flipAccessoryOnXAxis && lockedPointingLeft) ? -1f : 1f;
            scaleY = (flipYOnLeftFacingDuringLock && AccessorySettings.flipAccessoryOnYAxis && lockedPointingLeft) ? -1f : 1f;

            isFlipped = false;
        }
        else if (AccessorySettings.flipAccessoryOnTurn)
        {
            // Determine if Accessory should flip based on rotation angle
            // For 360° rotation: flip when between 90° and 270° to prevent upside-down
            // For 2-direction: flip when aiming left
            bool shouldFlip;
            if (AccessorySettings.lockTo2Directions)
            {
                shouldFlip = aimingLeft;
            }
            else
            {
                // Normalize angle to 0-360
                float normalizedTargetAngle = targetAngle;
                if (normalizedTargetAngle < 0) normalizedTargetAngle += 360;
                shouldFlip = normalizedTargetAngle > 90f && normalizedTargetAngle < 270f;
            }

            if (AccessorySettings.flipAccessoryOnXAxis)
            {
                scaleX = shouldFlip ? -1f : 1f;
                isFlipped = shouldFlip;
            }
            if (AccessorySettings.flipAccessoryOnYAxis)
            {
                scaleY = shouldFlip ? -1f : 1f;
            }
        }
        // else: scaleX=1, scaleY=1 (no flip — already initialized above)

        // Apply scale to Accessory parent to flip both visual and animation coordinate space.
        // Animations are authored for one direction and need the coordinate space flipped
        // when facing the opposite direction to mirror their behavior correctly.
        Accessory.localScale = new Vector3(scaleX, scaleY, Accessory.localScale.z);

        // Flip character and backpack — consolidated in AccessorySortingManager so the same path
        // runs for both owner (mouse-aimed) and remote clients (overrideAngle path).
        if (flipSpriteOnMove)
        {
            Transform feetHolder = playerTransform.Find("FeetHolder");
            ApplyCharacterFlipAndBackpackSorting(feetHolder, backpackHolder, aimingLeft, aimDir, playerSpriteRenderer);
            isFacingLeftSetter(aimingLeft);

            // Sync facing direction via SyncVar so late-joining clients get the correct initial flip
            if (isNetworkActive && isOwner && isFacingLeftGetter() != aimingLeft)
                syncFacingLeftToNetwork(aimingLeft);
        }

        // Character animations are now handled in UpdateMovementAnimation()
        // This prevents dual-wielding off-hand from overriding character animations

        // Apply Accessory renderer sorting
        ApplyAccessoryRendererSorting(Accessory, aimDir, AccessorySettings, playerSpriteRenderer);

        LogActiveAccessoryAnimationTransformState(
            Accessory,
            AccessorySettings,
            AccessoryName,
            aimDir,
            targetAngle,
            aimingLeft,
            isDirectionLocked,
            lockedAngle,
            scaleX,
            scaleY,
            isFlipped);
    }

    private void LogActiveAccessoryAnimationTransformState(
        Transform Accessory,
        AccessorySettings AccessorySettings,
        string AccessoryName,
        Direction aimDir,
        float targetAngle,
        bool aimingLeft,
        bool isDirectionLocked,
        float lockedAngle,
        float scaleX,
        float scaleY,
        bool isFlipped)
    {
        if (Accessory == null || AccessorySettings == null)
            return;

        Animator AccessoryAnimator = Accessory.GetComponentInChildren<Animator>();
        if (AccessoryAnimator == null)
            return;

        AnimatorClipInfo[] clipInfo = AccessoryAnimator.GetCurrentAnimatorClipInfo(0);
        if (clipInfo.Length == 0 || clipInfo[0].clip == null)
            return;

        string clipName = clipInfo[0].clip.name;
        if (string.IsNullOrEmpty(clipName) || clipName == "Idle")
            return;

        Transform animatedTransform = AccessoryAnimator.transform;
        Vector3 AccessoryLocalEuler = Accessory.localEulerAngles;
        Vector3 AccessoryWorldEuler = Accessory.eulerAngles;
        Vector3 animatedLocalEuler = animatedTransform.localEulerAngles;
        Vector3 animatedWorldEuler = animatedTransform.eulerAngles;

    }

    private float SnapToCardinalDirection(float angle)
    {
        // Normalize to 0-360
        if (angle < 0) angle += 360;

        // Snap to East or West only
        // 0° = East (right), 180° = West (left)
        // Split at 90° (top) and 270° (bottom)

        if ((angle >= 270f && angle <= 360f) || (angle >= 0f && angle < 90f))
            return 0f; // East (right) - right half of circle
        else
            return 180f; // West (left) - left half of circle
    }
}