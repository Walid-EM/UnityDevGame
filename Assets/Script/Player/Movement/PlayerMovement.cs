using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private static readonly Vector3 UpVector = Vector3.up;
    private static readonly Vector3 DownVector = Vector3.down;
    
    private CharacterController controller;
    private Vector3 velocity;
    private float currentSpeed;
    private bool isOnSlope;
    private Vector3 slopeDirection;
    private Vector3 slopeNormal;
    private float slopeAngle;
    private float lastSlopeChangeTime;
    private float wallStickTimer;
    private Vector3 lastWallNormal;
    private bool isStickingToWall;
    private Vector3 currentVelocity;
    private Vector3 targetMovement;
    private Vector3 smoothedMovement;
    private Vector3[] raycastPositions = new Vector3[4];
    private Vector3 slideInertia;
    private bool wasOnSlideBeforeJump;
    private PlayerController playerController;
    
    [Header("Mouvement")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runSpeed = 12f;
    [SerializeField] private float slideControlMultiplier = 0.4f;
    [SerializeField] private float lateralSlidingMultiplier = 1.5f;
    
    [Header("Saut")]
    [SerializeField] private float jumpHeight = 3f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float slideInertiaRetention = 0.8f;
    [SerializeField] private float airControlMultiplier = 0.3f;
    
    [Header("Glissement sur pente")]
    [SerializeField] private float maxSlopeAngle = 45f;
    [SerializeField] private float slideForceMultiplier = 3.5f;
    [SerializeField] private float slideFriction = 0.2f;
    [SerializeField] private float raycastDistance = 1.2f;
    [SerializeField] private float slidePower = 2.0f;
    [SerializeField] private float minSlideSpeed = 1.0f;
    
    [Header("Détection de surface")]
    [SerializeField] private LayerMask walkableSurfaceLayer;
    [SerializeField] private LayerMask slidingSurfaceLayer;
    
    [Header("Configuration des rayons")]
    [SerializeField] private float raycastMargin = 0.05f;
    [SerializeField] private float minTimeBetweenSlopeChanges = 0.2f;
    
    [Header("Anti-tremblement")]
    [SerializeField] private float wallStickThreshold = 0.2f;
    [SerializeField] private float wallStickDuration = 0.25f;
    [SerializeField] private float minDistanceToSteepSlope = 0.1f;
    [SerializeField] private float movementSmoothTime = 0.1f;
    
    private readonly RaycastHit[] slopeHits = new RaycastHit[4];
    private readonly RaycastHit[] wallHits = new RaycastHit[1];
    
    private bool wasGroundedLastFrame;
    private bool isJumping;
    
    private float invMaxSlopeAngle;
    private float absGravity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
        invMaxSlopeAngle = 1f / maxSlopeAngle;
        absGravity = Mathf.Abs(gravity);
    }
    
    void Start()
    {
        controller.slopeLimit = maxSlopeAngle;
        currentSpeed = walkSpeed;
        wasOnSlideBeforeJump = false;
        slideInertia = Vector3.zero;
        wasGroundedLastFrame = true;
        isJumping = false;
        slopeNormal = UpVector;
        
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Pickable"), true);
    }
    
    void Update()
    {
        bool isGroundedThisFrame = controller.isGrounded || IsGroundedCustomCheck();
        
        if (isGroundedThisFrame && !wasGroundedLastFrame && isJumping)
        {
            isJumping = false;
        }
        
        if (!isGroundedThisFrame && wasGroundedLastFrame && !isJumping)
        {
            wasOnSlideBeforeJump = false;
        }
        
        CheckSlope();
        
        bool jumpPressed = Input.GetButtonDown("Jump");
        
        if (jumpPressed && isGroundedThisFrame && playerController != null)
        {
            // Vérifier si le joueur a assez de stamina pour sauter
            if (playerController.CanPerformStaminaAction(playerController.jumpStaminaCost))
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                
                if (isOnSlope)
                {
                    wasOnSlideBeforeJump = true;
                    slideInertia = smoothedMovement * slideInertiaRetention;
                }
                else
                {
                    wasOnSlideBeforeJump = false;
                    slideInertia = Vector3.zero;
                }
                
                isJumping = true;
            }
        }
        
        ApplyGravity();
        HandleMovement();
        
        if (wallStickTimer > 0)
        {
            wallStickTimer -= Time.deltaTime;
        }
        else
        {
            isStickingToWall = false;
        }
        
        wasGroundedLastFrame = isGroundedThisFrame;
    }
    
    private void CheckSlope()
    {
        isOnSlope = false;
        slopeNormal = UpVector;
        slopeAngle = 0f;
        
        CalculateRaycastPositions();
        
        bool hitAny = false;
        float steepestAngle = 0f;
        int steepestHitIndex = -1;
        
        for (int i = 0; i < slopeHits.Length; i++)
        {
            slopeHits[i] = new RaycastHit();
        }
        
        for (int i = 0; i < raycastPositions.Length; i++)
        {
            int hitCount = Physics.RaycastNonAlloc(raycastPositions[i], DownVector, slopeHits, raycastDistance);
            
            if (hitCount > 0)
            {
                RaycastHit hit = slopeHits[0];
                hitAny = true;
                
                float angle = Vector3.Angle(hit.normal, UpVector);
                if (angle > steepestAngle)
                {
                    steepestAngle = angle;
                    steepestHitIndex = i;
                    slopeNormal = hit.normal;
                }
                
                if (hit.collider != null && ((1 << hit.collider.gameObject.layer) & slidingSurfaceLayer) != 0)
                {
                    isOnSlope = true;
                    slopeNormal = hit.normal;
                    break;
                }
            }
        }
        
        if (!hitAny) return;
        
        slopeAngle = steepestAngle;
        
        if (slopeAngle > 0f && slopeAngle < 90f)
        {
            if (Time.time >= lastSlopeChangeTime + minTimeBetweenSlopeChanges)
            {
                lastSlopeChangeTime = Time.time;
            }
            
            if (isOnSlope)
            {
                slopeDirection = Vector3.Cross(Vector3.Cross(UpVector, slopeNormal), slopeNormal).normalized;
            }
        }
    }
    
    private void CalculateRaycastPositions()
    {
        float radius = controller.radius;
        float height = controller.height;
        Vector3 center = transform.position + controller.center;
        
        float xExtent = radius - raycastMargin;
        float zExtent = radius - raycastMargin;
        float yOffset = height / 2 - radius;
        
        raycastPositions[0] = center + new Vector3(xExtent, -yOffset, zExtent);    // frontRight
        raycastPositions[1] = center + new Vector3(-xExtent, -yOffset, zExtent);   // frontLeft
        raycastPositions[2] = center + new Vector3(xExtent, -yOffset, -zExtent);   // backRight
        raycastPositions[3] = center + new Vector3(-xExtent, -yOffset, -zExtent);  // backLeft
    }
    
    private void HandleMovement()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        
        currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        
        // Gestion de la stamina pour la course (déléguée au PlayerController)
        if (Input.GetKey(KeyCode.LeftShift) && playerController != null)
        {
            float staminaToUse = playerController.staminaUseRate * Time.deltaTime;
            if (playerController.PerformStaminaAction(staminaToUse))
            {
                currentSpeed = runSpeed;
            }
            else
            {
                currentSpeed = walkSpeed;
            }
        }
        else
        {
            currentSpeed = walkSpeed;
        }
        
        Vector3 inputDirection = transform.right * x + transform.forward * z;
        
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }
        
        bool facingSteepSlope = false;
        
        for (int i = 0; i < wallHits.Length; i++)
        {
            wallHits[i] = new RaycastHit();
        }
        
        if (inputDirection.sqrMagnitude > 0.01f)
        {
            int hitCount = Physics.RaycastNonAlloc(transform.position, inputDirection, wallHits, minDistanceToSteepSlope * 1.5f);
            
            if (hitCount > 0 && wallHits[0].collider != null)
            {
                RaycastHit wallHit = wallHits[0];
                Vector3 steepSlopeNormal = wallHit.normal;
                float wallAngle = Vector3.Angle(steepSlopeNormal, UpVector);
                
                if (wallAngle > maxSlopeAngle && wallAngle < 95f)
                {
                    float dotProduct = Vector3.Dot(inputDirection, -steepSlopeNormal);
                    
                    if (wallHit.distance < wallStickThreshold && dotProduct > 0.5f)
                    {
                        facingSteepSlope = true;
                        isStickingToWall = true;
                        wallStickTimer = wallStickDuration;
                        lastWallNormal = steepSlopeNormal;
                        
                        controller.Move(-inputDirection * 0.005f);
                    }
                }
            }
        }
        
        if (isStickingToWall || facingSteepSlope)
        {
            Vector3 wallSlideDirection = Vector3.ProjectOnPlane(inputDirection, lastWallNormal).normalized;
            inputDirection = wallSlideDirection * 0.7f;
        }
        
        if (isJumping && wasOnSlideBeforeJump && !controller.isGrounded)
        {
            Vector3 airControl = inputDirection * currentSpeed * airControlMultiplier;
            Vector3 horizontalMovement = slideInertia + airControl;
            
            slideInertia *= (1f - 0.1f * Time.deltaTime);
            
            controller.Move(horizontalMovement * Time.deltaTime);
            
            return;
        }
        
        if (controller.isGrounded && wasOnSlideBeforeJump && !isJumping)
        {
            wasOnSlideBeforeJump = false;
            slideInertia = Vector3.zero;
        }
        
        if (isOnSlope && controller.isGrounded)
        {
            float normalizedAngle = slopeAngle * invMaxSlopeAngle;
            float slideForce = slideForceMultiplier * Mathf.Pow(normalizedAngle, slidePower) * absGravity;
            
            HandleSlopeMovement(inputDirection, normalizedAngle, slideForce);
            return;
        }
        
        targetMovement = inputDirection * currentSpeed;
        smoothedMovement = Vector3.SmoothDamp(smoothedMovement, targetMovement, ref currentVelocity, movementSmoothTime * 0.5f);
        
        controller.Move(smoothedMovement * Time.deltaTime);
    }
    
    private void HandleSlopeMovement(Vector3 inputDirection, float normalizedAngle, float slideForce)
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        
        Vector3 slopeRight = Vector3.ProjectOnPlane(transform.right, slopeNormal).normalized;
        Vector3 slopeForward = Vector3.ProjectOnPlane(transform.forward, slopeNormal).normalized;
        
        Vector3 slideVector = slopeDirection * slideForce;
        
        if (slopeRight.sqrMagnitude < 0.001f)
            slopeRight = transform.right;
        
        if (slopeForward.sqrMagnitude < 0.001f)
            slopeForward = transform.forward;
        
        Vector3 rightMovement = slopeRight * horizontalInput * currentSpeed * lateralSlidingMultiplier;
        
        Vector3 forwardMovement = Vector3.zero;
        if (verticalInput != 0)
        {
            float controlFactor = slideControlMultiplier * 2.5f;
            
            if (verticalInput > 0)
            {
                float uphillFactor = Mathf.Clamp01(1.0f - normalizedAngle * 0.8f);
                forwardMovement = slopeForward * verticalInput * currentSpeed * controlFactor * uphillFactor;
            }
            else
            {
                float downhillFactor = 1.0f + normalizedAngle * 0.5f;
                forwardMovement = slopeForward * verticalInput * currentSpeed * controlFactor * downhillFactor;
            }
        }
        
        Vector3 movementContribution = slideVector + rightMovement + forwardMovement;
        
        if (movementContribution.sqrMagnitude < minSlideSpeed * minSlideSpeed)
        {
            movementContribution = slopeDirection * minSlideSpeed;
        }
        
        float dynamicFriction = slideFriction * (1f - (normalizedAngle * 0.7f));
        
        targetMovement = movementContribution;
        
        targetMovement *= (1f - dynamicFriction * Time.deltaTime);
        
        float smoothTime = movementSmoothTime * (1f - (normalizedAngle * 0.5f));
        smoothedMovement = Vector3.SmoothDamp(smoothedMovement, targetMovement, ref currentVelocity, smoothTime);
        
        controller.Move(smoothedMovement * Time.deltaTime);
    }
    
    private void ApplyGravity()
    {
        if (isOnSlope && controller.isGrounded)
        {
            velocity.y = -1f;
            return;
        }
        
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -1f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
        
        controller.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
    }
    
    private bool IsGroundedCustomCheck()
    {
        RaycastHit hit;
        Vector3 rayStart = transform.position + controller.center;
        float rayLength = controller.height * 0.5f + 0.05f;
        
        if (Physics.Raycast(rayStart, DownVector, out hit, rayLength))
        {
            return true;
        }
        
        Vector3 feetPosition = transform.position + new Vector3(0, -controller.height * 0.49f, 0);
        float checkRadius = controller.radius * 0.9f;
        
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            Vector3 checkPos = feetPosition + new Vector3(
                Mathf.Cos(angle) * checkRadius,
                0,
                Mathf.Sin(angle) * checkRadius
            );
            
            if (Physics.Raycast(checkPos, DownVector, 0.1f))
            {
                return true;
            }
        }
        
        return false;
    }
    }