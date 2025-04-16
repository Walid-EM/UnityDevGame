using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // Cache commonly used vectors
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
    
    // Utiliser un buffer préalloué pour les raycast
    private readonly RaycastHit[] slopeHits = new RaycastHit[4];
    private readonly RaycastHit[] wallHits = new RaycastHit[1];
    
    private bool wasGroundedLastFrame;
    private bool isJumping;
    
    // Calculer à l'avance certaines valeurs pour éviter des recalculs constants
    private float invMaxSlopeAngle;
    private float absGravity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
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
        
    // Ignore les collisions entre le layer du joueur et le layer Pickable
    Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Pickable"), true);
    }
    
    void Update()
    {
        // Suivi de l'état au sol - effectuons une vérification plus robuste
        bool isGroundedThisFrame = controller.isGrounded || IsGroundedCustomCheck();
        
        // Si on vient de toucher le sol après un saut
        if (isGroundedThisFrame && !wasGroundedLastFrame)
        {
            // On a atterri, réinitialiser l'état de saut
            if (isJumping)
            {
                isJumping = false;
            }
        }
        
        // Si on vient de quitter le sol sans sauter, on n'est pas en train de sauter
        if (!isGroundedThisFrame && wasGroundedLastFrame && !isJumping)
        {
            // On est tombé d'un rebord, pas sauté
            wasOnSlideBeforeJump = false;
        }
        
        // Vérifier si on est sur une pente
        CheckSlope();
        
        // Traiter le saut - amélioration de la détection
        bool jumpPressed = Input.GetButtonDown("Jump");
        
        // Debugger l'état au sol et l'appui sur le bouton de saut
        if (jumpPressed)
        {
            Debug.Log($"Bouton de saut pressé! Au sol: {isGroundedThisFrame}, Velocity Y: {velocity.y}");
        }
        
        if (jumpPressed && isGroundedThisFrame)
        {
            // Calcul de la vitesse initiale du saut
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            
            // Si on sautait depuis une pente glissante, conserver l'inertie
            if (isOnSlope)
            {
                wasOnSlideBeforeJump = true;
                // Stocker l'inertie actuelle (smoothedMovement est la vélocité horizontale)
                slideInertia = smoothedMovement * slideInertiaRetention;
                Debug.Log($"Saut depuis une pente! Inertie conservée: {slideInertia}");
            }
            else
            {
                wasOnSlideBeforeJump = false;
                slideInertia = Vector3.zero;
            }
            
            isJumping = true;
        }
        
        // Appliquer la gravité (vitesse verticale)
        ApplyGravity();
        
        // Gérer le mouvement horizontal (et l'inertie de glissement)
        HandleMovement();
        
        // Gestion du collage aux murs
        if (wallStickTimer > 0)
        {
            wallStickTimer -= Time.deltaTime;
        }
        else
        {
            isStickingToWall = false;
        }
        
        // Mise à jour de l'état au sol pour le prochain frame
        wasGroundedLastFrame = isGroundedThisFrame;
    }
    
    private void CheckSlope()
    {
        isOnSlope = false;
        slopeNormal = UpVector;
        slopeAngle = 0f;
        
        // Calculer les positions des raycast une seule fois
        CalculateRaycastPositions();
        
        bool hitAny = false;
        float steepestAngle = 0f;
        int steepestHitIndex = -1;
        
        // S'assurer que le tableau slopeHits est correctement initialisé
        for (int i = 0; i < slopeHits.Length; i++)
        {
            slopeHits[i] = new RaycastHit();
        }
        
        for (int i = 0; i < raycastPositions.Length; i++)
        {
            #if UNITY_EDITOR
            Debug.DrawRay(raycastPositions[i], DownVector * raycastDistance, Color.blue);
            #endif
            
            int hitCount = Physics.RaycastNonAlloc(raycastPositions[i], DownVector, slopeHits, raycastDistance);
            
            // Vérifier si nous avons bien un résultat
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
                
                // Si la surface est dans le layer slidingSurface, elle est toujours glissante
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
                // MODIFICATION: On glisse UNIQUEMENT si c'est une surface explicitement définie comme glissante
                // L'angle de la pente n'importe plus pour déterminer si on glisse
                lastSlopeChangeTime = Time.time;
            }
            
            // Optimisation: calculer la direction de la pente une seule fois quand nécessaire
            if (isOnSlope)
            {
                slopeDirection = Vector3.Cross(Vector3.Cross(UpVector, slopeNormal), slopeNormal).normalized;
                
                #if UNITY_EDITOR
                if (steepestHitIndex >= 0)
                {
                    Debug.DrawRay(slopeHits[0].point, slopeDirection * 2f, Color.yellow);
                    Debug.DrawRay(slopeHits[0].point, slopeNormal * 2f, Color.red);
                }
                #endif
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
        
        Vector3 inputDirection = transform.right * x + transform.forward * z;
        
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }
        
        bool facingSteepSlope = false;
        
        // S'assurer que le tableau wallHits est correctement initialisé
        for (int i = 0; i < wallHits.Length; i++)
        {
            wallHits[i] = new RaycastHit();
        }
        
        // Détection de collision frontale avec parois - utiliser RaycastNonAlloc
        if (inputDirection.sqrMagnitude > 0.01f)
        {
            int hitCount = Physics.RaycastNonAlloc(transform.position, inputDirection, wallHits, minDistanceToSteepSlope * 1.5f);
            
            if (hitCount > 0 && wallHits[0].collider != null)
            {
                RaycastHit wallHit = wallHits[0];
                Vector3 steepSlopeNormal = wallHit.normal;
                float wallAngle = Vector3.Angle(steepSlopeNormal, UpVector);
                
                // Si on fait face à une pente trop raide
                if (wallAngle > maxSlopeAngle && wallAngle < 95f)
                {
                    float dotProduct = Vector3.Dot(inputDirection, -steepSlopeNormal);
                    
                    if (wallHit.distance < wallStickThreshold && dotProduct > 0.5f)
                    {
                        facingSteepSlope = true;
                        isStickingToWall = true;
                        wallStickTimer = wallStickDuration;
                        lastWallNormal = steepSlopeNormal;
                        
                        // Ajouter une petite force de recul pour éviter de pénétrer dans le mur
                        controller.Move(-inputDirection * 0.005f);
                    }
                }
            }
        }
        
        // Si on est collé à un mur ou face à une pente raide
        if (isStickingToWall || facingSteepSlope)
        {
            // Projeter le mouvement le long du mur
            Vector3 wallSlideDirection = Vector3.ProjectOnPlane(inputDirection, lastWallNormal).normalized;
            
            // Réduire la vitesse de mouvement le long du mur pour plus de stabilité
            inputDirection = wallSlideDirection * 0.7f;
        }
        
        // --- GESTION DE L'INERTIE PENDANT LE SAUT ---
        // Appliquer l'inertie si on est en l'air après avoir sauté d'une pente
        if (isJumping && wasOnSlideBeforeJump && !controller.isGrounded)
        {
            // Permettre un certain contrôle en l'air (contrôle réduit)
            Vector3 airControl = inputDirection * currentSpeed * airControlMultiplier;
            Vector3 horizontalMovement = slideInertia + airControl;
            
            // Appliquer une légère réduction d'inertie dans le temps
            slideInertia *= (1f - 0.1f * Time.deltaTime);
            
            // Appliquer le mouvement horizontal (l'inertie)
            controller.Move(horizontalMovement * Time.deltaTime);
            
            // Debug info
            #if UNITY_EDITOR
            Debug.DrawRay(transform.position, horizontalMovement.normalized * 2f, Color.cyan);
            Debug.DrawRay(transform.position, slideInertia.normalized * 3f, Color.magenta);
            #endif
            
            return;
        }
        
        // Si on vient d'atterrir après un saut d'une pente, réinitialiser l'inertie
        if (controller.isGrounded && wasOnSlideBeforeJump && !isJumping)
        {
            wasOnSlideBeforeJump = false;
            slideInertia = Vector3.zero;
        }
        
        // Gestion du glissement sur pente
        if (isOnSlope && controller.isGrounded)
        {
            // Calculs préliminaires pour le mouvement sur pente
            float normalizedAngle = slopeAngle * invMaxSlopeAngle; // Utiliser multiplication au lieu de division
            float slideForce = slideForceMultiplier * Mathf.Pow(normalizedAngle, slidePower) * absGravity;
            
            HandleSlopeMovement(inputDirection, normalizedAngle, slideForce);
            return;
        }
        
        // Mouvement normal
        targetMovement = inputDirection * currentSpeed;
        smoothedMovement = Vector3.SmoothDamp(smoothedMovement, targetMovement, ref currentVelocity, movementSmoothTime * 0.5f);
        
        // Appliquer le mouvement horizontal lissé
        controller.Move(smoothedMovement * Time.deltaTime);
    }
    
    private void HandleSlopeMovement(Vector3 inputDirection, float normalizedAngle, float slideForce)
    {
        // Obtenir les composantes d'entrée du joueur
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        
        // Calculer les vecteurs de base pour le mouvement sur pente
        Vector3 slopeRight = Vector3.ProjectOnPlane(transform.right, slopeNormal).normalized;
        Vector3 slopeForward = Vector3.ProjectOnPlane(transform.forward, slopeNormal).normalized;
        
        // Force principale vers le bas de la pente
        Vector3 slideVector = slopeDirection * slideForce;
        
        // S'assurer que les vecteurs d'orientation ne sont pas zéro (utiliser une comparaison de sqrMagnitude)
        if (slopeRight.sqrMagnitude < 0.001f)
            slopeRight = transform.right;
        
        if (slopeForward.sqrMagnitude < 0.001f)
            slopeForward = transform.forward;
        
        // MOUVEMENT LATÉRAL: Utiliser le vecteur droit du joueur projeté sur la pente
        Vector3 rightMovement = slopeRight * horizontalInput * currentSpeed * lateralSlidingMultiplier;
        
        // MOUVEMENT AVANT/ARRIÈRE: Plus sensible aux inputs mais résiste à la remontée
        Vector3 forwardMovement = Vector3.zero;
        if (verticalInput != 0)
        {
            // Augmenter considérablement le contrôle du mouvement avant/arrière
            float controlFactor = slideControlMultiplier * 2.5f;
            
            // Ajuster selon la direction (Plus difficile de monter, plus facile de descendre)
            if (verticalInput > 0) // Remontée
            {
                // Réduire le contrôle quand on remonte la pente
                float uphillFactor = Mathf.Clamp01(1.0f - normalizedAngle * 0.8f);
                forwardMovement = slopeForward * verticalInput * currentSpeed * controlFactor * uphillFactor;
            }
            else // Descente
            {
                // Augmenter le contrôle quand on descend la pente
                float downhillFactor = 1.0f + normalizedAngle * 0.5f;
                forwardMovement = slopeForward * verticalInput * currentSpeed * controlFactor * downhillFactor;
            }
        }
        
        // --- MOUVEMENT RÉSULTANT ---
        Vector3 movementContribution = slideVector + rightMovement + forwardMovement;
        
        // Appliquer une force minimum garantie pour éviter de rester coincé
        if (movementContribution.sqrMagnitude < minSlideSpeed * minSlideSpeed)
        {
            movementContribution = slopeDirection * minSlideSpeed;
        }
        
        // Réduire la friction sur les pentes raides
        float dynamicFriction = slideFriction * (1f - (normalizedAngle * 0.7f));
        
        // Créer le vecteur de mouvement cible
        targetMovement = movementContribution;
        
        // Appliquer la friction dynamique
        targetMovement *= (1f - dynamicFriction * Time.deltaTime);
        
        // Lissage du mouvement pour éviter les tremblements
        float smoothTime = movementSmoothTime * (1f - (normalizedAngle * 0.5f));
        smoothedMovement = Vector3.SmoothDamp(smoothedMovement, targetMovement, ref currentVelocity, smoothTime);
        
        // Appliquer le mouvement lissé
        controller.Move(smoothedMovement * Time.deltaTime);
    }
    
    private void ApplyGravity()
    {
        // Si on est sur une pente glissante et au sol, le mouvement vertical est géré par HandleMovement
        if (isOnSlope && controller.isGrounded)
        {
            // Assurons-nous que la vitesse Y est correctement réinitialisée même sur les pentes
            velocity.y = -1f;
            return;
        }
        
        // Réinitialiser la force de gravité quand on est au sol
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -1f;
        }
        else
        {
            // Appliquer la gravité
            velocity.y += gravity * Time.deltaTime;
        }
        
        // Appliquer le mouvement vertical
        controller.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
    }
    
    // Méthode de vérification au sol personnalisée pour améliorer la détection
    private bool IsGroundedCustomCheck()
    {
        // Lancer un rayon vers le bas pour vérifier si on est proche du sol
        RaycastHit hit;
        Vector3 rayStart = transform.position + controller.center;
        float rayLength = controller.height * 0.5f + 0.05f; // Un peu plus que la moitié de la hauteur
        
        if (Physics.Raycast(rayStart, DownVector, out hit, rayLength))
        {
            return true;
        }
        
        // Vérification supplémentaire avec plusieurs rayons autour des pieds
        Vector3 feetPosition = transform.position + new Vector3(0, -controller.height * 0.49f, 0);
        float checkRadius = controller.radius * 0.9f;
        
        // Vérifier plusieurs points autour des pieds
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
    
    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        CharacterController controller = GetComponent<CharacterController>();
        if (controller == null) return;
        
        // Réutiliser les positions de raycast pour le debug
        if (Application.isPlaying)
        {
            // Utiliser les positions calculées durant le gameplay
            Gizmos.color = Color.blue;
            foreach (Vector3 pos in raycastPositions)
            {
                Gizmos.DrawRay(pos, DownVector * raycastDistance);
            }
        }
        else
        {
            // Calculer les positions pour visualiser en mode éditeur
            //CalculateRaycastPositions();
            
            Gizmos.color = Color.blue;
            foreach (Vector3 pos in raycastPositions)
            {
                Gizmos.DrawRay(pos, DownVector * raycastDistance);
            }
        }
        
        // Dessiner les capsules du CharacterController
        Vector3 center = transform.position + controller.center;
        float height = controller.height;
        float radius = controller.radius;
        
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(center + new Vector3(0, height/2 - radius, 0), radius);
        Gizmos.DrawWireSphere(center + new Vector3(0, -height/2 + radius, 0), radius);
        
        if (Application.isPlaying)
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            
            if (x != 0 || z != 0)
            {
                Vector3 moveDir = transform.right * x + transform.forward * z;
                moveDir.Normalize();
                
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, moveDir * minDistanceToSteepSlope);
                
                // Visualiser la zone de "collage" au mur
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, wallStickThreshold);
                
                // Visualiser le vecteur latéral quand on glisse
                if (isOnSlope)
                {
                    // Visualiser également la direction latérale corrigée
                    Vector3 correctedSlopeRight = Vector3.ProjectOnPlane(transform.right, slopeNormal).normalized;
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawRay(transform.position, correctedSlopeRight * 2f);
                }
            }
            
            // Visualiser l'inertie pendant le saut
            if (isJumping && wasOnSlideBeforeJump && !controller.isGrounded)
            {
                // Visualiser l'inertie actuelle
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, slideInertia.normalized * 2f);
                Gizmos.DrawSphere(transform.position + slideInertia.normalized * 2f, 0.1f);
                
                // Texte de debug (utiliser string interpolation)
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
                    $"Inertie: {slideInertia.magnitude:F2}");
            }
        }
    }
    #endif
}