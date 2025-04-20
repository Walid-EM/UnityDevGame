using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Énumération des différents états de comportement possibles pour une IA
/// </summary>
public enum AIState
{
    Passive,     // L'IA se déplace aléatoirement
    Aggressive,  // L'IA attaque le joueur
    Fleeing,     // L'IA fuit une menace
    Idle         // L'IA est stationnaire
}

/// <summary>
/// Énumération des différents types d'IA
/// </summary>
public enum AIType
{
    Melee,       // IA attaquant au corps-à-corps
    Ranged,      // IA attaquant à distance
    Neutral      // IA neutre (animaux de ferme, etc.)
}

/// <summary>
/// Classe de base abstraite pour toutes les IA du jeu
/// </summary>
public abstract class BaseAI : MonoBehaviour
{
    #region Variables

    [Header("Paramètres généraux")]
    [SerializeField] protected AIType aiType = AIType.Neutral;
    [SerializeField] protected float moveSpeed = 3f;
    [SerializeField] protected float rotationSpeed = 5f;
    [SerializeField] protected float baseDamage = 10f;

    [Header("Détection")]
    [SerializeField] protected float detectionRadius = 10f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float fleeThreshold = 30f;
    [SerializeField] protected LayerMask detectionLayers;
    [SerializeField] protected LayerMask obstacleLayers;

    [Header("Comportement")]
    [SerializeField] protected float passiveMovementRadius = 5f;
    [SerializeField] protected float passiveMovementDuration = 3f;
    [SerializeField] protected float passiveWaitDuration = 2f;

    [Header("Interactions entre IA")]
    [SerializeField] protected float aiAvoidanceRadius = 3f;
    [SerializeField] protected float aiAvoidanceForce = 2f;
    [SerializeField] protected LayerMask aiLayerMask;
    [SerializeField] protected int maxNearbyAI = 5;
    [SerializeField] protected bool considerAITypeForBehavior = true;
    [SerializeField] protected float aggressiveAvoidanceMultiplier = 1.5f;

    // Variables d'état
    protected AIState currentState = AIState.Passive;
    protected Transform target;
    protected Vector3 randomDestination;
    protected bool isMoving = false;
    protected bool isWaiting = false;
    protected bool isActive = false;
    protected bool isAvoidingObstacle = false;
    
    // Timers
    protected float currentMovementTimer = 0f;
    protected float currentWaitTimer = 0f;
    protected float attackCooldown = 1f;
    protected float lastAttackTime = 0f;
    protected float pathRecalculationTime = 0.5f;
    protected float lastPathRecalculation = -10f;
    
    // Positions et directions
    protected Vector3 startPosition;
    protected Vector3 lastTargetPosition;
    protected Vector3 currentMovementDirection;

    // Composants
    protected Rigidbody rb;
    protected Collider aiCollider;
    protected HealthSystem healthSystem;
    
    // Cache pour les IA et colliders proches
    protected readonly Collider[] nearbyAIColliders = new Collider[10];
    protected Collider[] detectedColliders = new Collider[10];

    #endregion

    #region Unity Lifecycle Methods

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        aiCollider = GetComponent<Collider>();
        startPosition = transform.position;
        
        // Récupérer le système de santé
        healthSystem = GetComponent<HealthSystem>();
        if (healthSystem == null)
        {
            Debug.LogError($"HealthSystem manquant sur {gameObject.name}. L'IA ne peut pas fonctionner correctement.");
            enabled = false;
        }
        else
        {
            // S'abonner à l'événement de dégâts du HealthSystem
            healthSystem.OnDamaged.AddListener(OnDamageReceived);
        }
        
        // Configurer le masque de couche des IA si non défini
        ConfigureAILayerMask();

        // Initialiser les variables de pathfinding
        currentMovementDirection = transform.forward;
        lastTargetPosition = transform.position;
        
        // Configurer le Rigidbody correctement pour éviter de rouler
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation; // Empêcher la rotation
            rb.interpolation = RigidbodyInterpolation.Interpolate; // Rendre le mouvement plus fluide
        }
    }

    protected virtual void Start()
    {
        SetRandomDestination();
    }

    protected virtual void Update()
    {
        if (!isActive) return;

        // Mise à jour des timers
        UpdateTimers();

        // Détection de l'environnement
        DetectEnvironment();

        // Mise à jour du comportement en fonction de l'état
        UpdateBehavior();
    }

    #endregion

    #region Core AI Behavior

    /// <summary>
    /// Détecte les éléments importants dans l'environnement (joueur, autres IA, obstacles)
    /// </summary>
    protected virtual void DetectEnvironment()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, detectedColliders, detectionLayers);

        // Réinitialiser la cible si on ne détecte plus rien et qu'on était agressif
        if (hitCount == 0 && currentState == AIState.Aggressive)
        {
            target = null;
            TransitionToState(AIState.Passive);
            return;
        }

        // Recherche du joueur parmi les objets détectés
        for (int i = 0; i < hitCount; i++)
        {
            if (detectedColliders[i].CompareTag("Player"))
            {
                // Vérifier qu'il n'y a pas d'obstacles entre l'IA et le joueur
                if (HasLineOfSight(detectedColliders[i].transform))
                {
                    target = detectedColliders[i].transform;
                    
                    // Si l'IA est de type neutre, elle reste passive
                    if (aiType != AIType.Neutral)
                    {
                        TransitionToState(AIState.Aggressive);
                    }
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Met à jour le comportement de l'IA en fonction de son état actuel
    /// </summary>
    protected virtual void UpdateBehavior()
    {
        switch (currentState)
        {
            case AIState.Passive:
                UpdatePassiveBehavior();
                break;
            case AIState.Aggressive:
                UpdateAggressiveBehavior();
                break;
            case AIState.Fleeing:
                UpdateFleeingBehavior();
                break;
            case AIState.Idle:
                // Ne rien faire en mode Idle
                break;
        }
    }

    /// <summary>
    /// Met à jour les timers utilisés pour les comportements
    /// </summary>
    protected virtual void UpdateTimers()
    {
        // Mise à jour du timer de mouvement
        if (isMoving)
        {
            currentMovementTimer += Time.deltaTime;
            if (currentMovementTimer >= passiveMovementDuration)
            {
                isMoving = false;
                isWaiting = true;
                currentWaitTimer = 0f;
            }
        }
        
        // Mise à jour du timer d'attente
        if (isWaiting)
        {
            currentWaitTimer += Time.deltaTime;
            if (currentWaitTimer >= passiveWaitDuration)
            {
                isWaiting = false;
                SetRandomDestination();
            }
        }
    }

    /// <summary>
    /// Change l'état de l'IA
    /// </summary>
    protected virtual void TransitionToState(AIState newState)
    {
        // Sortie de l'état actuel
        switch (currentState)
        {
            case AIState.Passive:
                // Actions spécifiques à effectuer en sortant de l'état passif
                break;
            case AIState.Aggressive:
                // Actions spécifiques à effectuer en sortant de l'état agressif
                break;
            case AIState.Fleeing:
                // Actions spécifiques à effectuer en sortant de l'état de fuite
                break;
        }

        // Mise à jour de l'état
        currentState = newState;

        // Entrée dans le nouvel état
        switch (currentState)
        {
            case AIState.Passive:
                target = null;
                SetRandomDestination();
                break;
            case AIState.Aggressive:
                isMoving = false;
                isWaiting = false;
                // Réinitialiser les variables de pathfinding lors du passage en mode agressif
                lastPathRecalculation = -10f; // Force une recalculation immédiate
                break;
            case AIState.Fleeing:
                // Actions spécifiques à effectuer en entrant dans l'état de fuite
                break;
        }

        Debug.Log($"{gameObject.name} a changé d'état : {newState}");
    }

    #endregion

    #region State-Specific Behaviors

    /// <summary>
    /// Comportement quand l'IA est en état passif (déplacement aléatoire)
    /// </summary>
    protected virtual void UpdatePassiveBehavior()
    {
        if (isMoving && !isWaiting)
        {
            // Vérifie si on est arrivé à destination
            if (Vector3.Distance(transform.position, randomDestination) < 0.5f)
            {
                isMoving = false;
                isWaiting = true;
                currentWaitTimer = 0f;
                return;
            }

            // Déplacement vers la destination aléatoire
            MoveToPosition(randomDestination);
        }
        else if (!isWaiting)
        {
            SetRandomDestination();
        }
    }

    /// <summary>
    /// Comportement quand l'IA est en état agressif (attaque du joueur)
    /// </summary>
    protected virtual void UpdateAggressiveBehavior()
    {
        if (target == null)
        {
            TransitionToState(AIState.Passive);
            return;
        }

        // Vérification de la santé pour déterminer si l'IA doit fuir
        if (healthSystem != null && healthSystem.HealthPercentage * 100 <= fleeThreshold)
        {
            TransitionToState(AIState.Fleeing);
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        // Si l'IA est hors de portée de vue, elle redevient passive
        if (distanceToTarget > detectionRadius || !HasLineOfSight(target))
        {
            target = null;
            TransitionToState(AIState.Passive);
            return;
        }

        // Vérifier si la cible a bougé de manière significative depuis la dernière recalculation de chemin
        if (target != null && Vector3.Distance(lastTargetPosition, target.position) > 1f ||
            Time.time - lastPathRecalculation > pathRecalculationTime)
        {
            lastTargetPosition = target.position;
            lastPathRecalculation = Time.time;
            isAvoidingObstacle = false; // Réinitialiser l'état d'évitement
        }

        // Vérifier si on peut effectuer une action spéciale
        if (CanPerformSpecialAction())
        {
            PerformSpecialAction();
            return;
        }

        // Comportement spécifique selon le type d'IA
        switch (aiType)
        {
            case AIType.Melee:
                HandleMeleeAggression(distanceToTarget);
                break;

            case AIType.Ranged:
                HandleRangedAggression(distanceToTarget);
                break;

            case AIType.Neutral:
                // Les IA neutres ne devraient pas être en mode agressif
                TransitionToState(AIState.Passive);
                break;
        }
    }

    /// <summary>
    /// Gère le comportement agressif pour les IA de type mêlée
    /// </summary>
    protected virtual void HandleMeleeAggression(float distanceToTarget)
    {
        if (distanceToTarget > attackRange)
        {
            // Déterminer si des IA sont sur le chemin vers la cible
            bool aiInPath = IsAIInPathToTarget();
            
            // Déterminer la position cible du mouvement
            Vector3 moveTarget = aiInPath ? CalculatePathAroundAI() : CalculateTacticalPosition();
            
            // Se déplacer vers la position calculée
            MoveToPosition(moveTarget);
        }
        else
        {
            // À portée d'attaque, on s'arrête et on attaque
            FaceTarget();
            Attack();
        }
    }

    /// <summary>
    /// Gère le comportement agressif pour les IA à distance
    /// </summary>
    protected virtual void HandleRangedAggression(float distanceToTarget)
    {
        float optimalRange = attackRange * 0.8f;
        
        // Vérifier si des IA sont sur le chemin
        bool hasAIInPath = IsAIInPathToTarget();
        Vector3 targetPosition = hasAIInPath 
            ? CalculateRangedPositionAvoidingAI(optimalRange)
            : CalculateOptimalRangedPosition(optimalRange);
        
        float currentDistance = distanceToTarget;
        
        if (currentDistance < optimalRange * 0.7f || currentDistance > optimalRange * 1.3f)
        {
            // Trop proche ou trop loin, on ajuste la position
            MoveToPosition(targetPosition);
        }
        else if (Vector3.Distance(transform.position, targetPosition) > 1.5f)
        {
            // On n'est pas à la position optimale, on s'y déplace
            MoveToPosition(targetPosition);
        }
        else
        {
            // Distance idéale, on s'arrête et on attaque
            FaceTarget();
            Attack();
        }
    }

    /// <summary>
    /// Comportement quand l'IA est en état de fuite
    /// </summary>
    protected virtual void UpdateFleeingBehavior()
    {
        // Si la santé est remontée au-dessus du seuil, on peut revenir en mode passif
        if (healthSystem != null && healthSystem.HealthPercentage * 100 > fleeThreshold * 1.5f)
        {
            TransitionToState(AIState.Passive);
            return;
        }

        // Si on n'a plus de cible, on retourne en mode passif
        if (target == null)
        {
            TransitionToState(AIState.Passive);
            return;
        }

        // Déterminer la direction opposée à la cible
        Vector3 fleeDirection = transform.position - target.position;
        fleeDirection.y = 0; // Maintenir la fuite sur le plan horizontal
        
        // Déterminer les directions d'évitement des autres IA et des obstacles
        Vector3 aiAvoidance = CalculateAIAvoidanceDirection();
        
        // Calculer la direction finale de fuite
        Vector3 finalFleeDirection = fleeDirection.normalized;
        if (aiAvoidance != Vector3.zero)
        {
            finalFleeDirection = (finalFleeDirection + aiAvoidance * 0.5f).normalized;
        }
        
        // Position de fuite à une distance raisonnable
        Vector3 fleePosition = transform.position + finalFleeDirection * 5f;
        
        // Ajuster la position de fuite pour éviter les obstacles
        RaycastHit hit;
        if (Physics.Raycast(transform.position, finalFleeDirection, out hit, 5f, obstacleLayers))
        {
            // On tente de fuir dans une direction légèrement modifiée
            Vector3 alternativeDirection = Quaternion.Euler(0, Random.Range(30f, 90f), 0) * finalFleeDirection;
            fleePosition = transform.position + alternativeDirection.normalized * 5f;
        }

        // Se déplacer dans la direction de fuite
        MoveToPosition(fleePosition);
    }

    #endregion

    #region Combat and Actions

    /// <summary>
    /// Effectue une attaque vers la cible
    /// </summary>
    protected virtual void Attack()
    {
        // Vérification du cooldown d'attaque
        if (Time.time - lastAttackTime < attackCooldown)
        {
            return;
        }

        // Marquer le temps de la dernière attaque
        lastAttackTime = Time.time;

        // Si la cible a un PlayerStats, lui infliger des dégâts
        if (target != null)
        {
            ApplyDamageToTarget(baseDamage);
        }
    }

    /// <summary>
    /// Applique des dégâts à la cible (joueur ou autre entité)
    /// </summary>
    protected virtual void ApplyDamageToTarget(float damageAmount)
    {
        if (target == null) return;

        // Essayer d'abord de trouver PlayerStats
        PlayerStats playerStats = target.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.TakeDamage(damageAmount);
            Debug.Log($"{gameObject.name} attaque {target.name} et inflige {damageAmount} dégâts via PlayerStats");
            return;
        }

        // Si pas de PlayerStats, essayer directement avec HealthSystem
        HealthSystem targetHealth = target.GetComponent<HealthSystem>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damageAmount, gameObject);
            Debug.Log($"{gameObject.name} attaque {target.name} et inflige {damageAmount} dégâts via HealthSystem");
        }
    }

    /// <summary>
    /// Vrai si l'IA peut effectuer une action spéciale
    /// </summary>
    protected virtual bool CanPerformSpecialAction()
    {
        return false; // Par défaut, pas d'action spéciale
    }

    /// <summary>
    /// Méthode appelée pour exécuter une action spéciale
    /// </summary>
    protected virtual void PerformSpecialAction()
    {
        // Implémentation de base vide, à surcharger dans les classes dérivées
    }

    #endregion
    
    #region Movement and Positioning

    /// <summary>
    /// Déplace l'IA vers une position spécifiée - MODIFIÉE pour utiliser Rigidbody
    /// </summary>
    protected virtual void MoveToPosition(Vector3 position)
    {
        // Calculer la direction et la distance
        Vector3 directionToTarget = position - transform.position;
        directionToTarget.y = 0; // Maintenir le mouvement sur le plan XZ
        
        if (directionToTarget.magnitude < 0.1f) return;

        // Calculer la direction d'évitement des autres IA
        Vector3 avoidanceDirection = CalculateAIAvoidanceDirection();
        
        // Calculer le comportement de groupe si applicable
        Vector3 groupDirection = CalculateGroupBehavior();
        
        // Appliquer un poids plus important à l'évitement en mode agressif
        float avoidanceMultiplier = currentState == AIState.Aggressive ? aggressiveAvoidanceMultiplier : 1.0f;
        
        // Combiner les directions avec des poids appropriés
        Vector3 finalDirection = directionToTarget.normalized;
        if (avoidanceDirection != Vector3.zero)
        {
            finalDirection += avoidanceDirection * (1.2f * avoidanceMultiplier);
        }
        if (groupDirection != Vector3.zero)
        {
            finalDirection += groupDirection * 0.8f;
        }
        
        finalDirection.Normalize();
        
        // Mettre à jour la direction actuelle du mouvement avec un lissage
        float smoothingFactor = Time.deltaTime * 3.0f; // Plus la valeur est grande, plus le mouvement est réactif
        currentMovementDirection = Vector3.Slerp(currentMovementDirection, finalDirection, smoothingFactor);

        // Rotation vers la direction finale
        Quaternion targetRotation = Quaternion.LookRotation(currentMovementDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        // Déplacement vers l'avant
        Vector3 movement = transform.forward * moveSpeed * Time.deltaTime;
        
        // Vérifier si le mouvement est possible (pas d'obstacles)
        if (!IsPathBlocked(movement))
        {
            // MODIFICATION: Utiliser Rigidbody.MovePosition au lieu de transform.position
            if (rb != null)
            {
                rb.MovePosition(rb.position + movement);
            }
            else
            {
                transform.position += movement;
            }
            isAvoidingObstacle = false;
        }
        else
        {
            // En cas d'obstacle, essayer de trouver une direction alternative
            FindAlternativePath();
            isAvoidingObstacle = true;
        }
    }

    /// <summary>
    /// Vérifie si le chemin est bloqué par un obstacle
    /// </summary>
    protected virtual bool IsPathBlocked(Vector3 movement)
    {
        // Lancer un rayon devant l'IA pour détecter les obstacles
        return Physics.Raycast(transform.position, transform.forward, movement.magnitude + 0.5f, obstacleLayers);
    }

    /// <summary>
    /// Cherche un chemin alternatif en cas d'obstacle
    /// </summary>
    protected virtual void FindAlternativePath()
    {
        // Essayer plusieurs directions jusqu'à en trouver une libre
        for (float angle = 30f; angle <= 180f; angle += 30f)
        {
            // Essayer à gauche
            Vector3 leftDirection = Quaternion.Euler(0, -angle, 0) * transform.forward;
            if (!Physics.Raycast(transform.position, leftDirection, 1.5f, obstacleLayers))
            {
                transform.rotation = Quaternion.LookRotation(leftDirection);
                return;
            }

            // Essayer à droite
            Vector3 rightDirection = Quaternion.Euler(0, angle, 0) * transform.forward;
            if (!Physics.Raycast(transform.position, rightDirection, 1.5f, obstacleLayers))
            {
                transform.rotation = Quaternion.LookRotation(rightDirection);
                return;
            }
        }

        // Si aucune direction ne fonctionne, inverser la direction
        transform.rotation = Quaternion.LookRotation(-transform.forward);
    }

    /// <summary>
    /// Définit une destination aléatoire pour le déplacement passif
    /// </summary>
    protected virtual void SetRandomDestination()
    {
        // Choisir un point aléatoire dans un cercle autour de la position de départ
        Vector2 randomPoint = Random.insideUnitCircle * passiveMovementRadius;
        randomDestination = startPosition + new Vector3(randomPoint.x, 0, randomPoint.y);
        
        // Vérifier que ce point est accessible (pas dans un obstacle)
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(randomDestination.x, transform.position.y + 10f, randomDestination.z), Vector3.down, out hit, 20f, obstacleLayers))
        {
            randomDestination = hit.point; // Ajuster la position sur la surface détectée
        }
        
        isMoving = true;
        currentMovementTimer = 0f;
    }

    /// <summary>
    /// Oriente l'IA vers sa cible
    /// </summary>
    protected virtual void FaceTarget()
    {
        if (target == null) return;
        
        Vector3 directionToTarget = target.position - transform.position;
        directionToTarget.y = 0; // Maintenir la rotation sur le plan XZ
        
        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Active ou désactive l'IA (pour l'optimisation en fonction de la distance au joueur)
    /// </summary>
    public virtual void SetActive(bool active)
    {
        isActive = active;
        
        // Si on active l'IA après qu'elle ait été désactivée, on réinitialise son état
        if (active && !isActive)
        {
            // Réinitialiser à l'état par défaut
            TransitionToState(AIState.Passive);
        }
    }

    /// <summary>
    /// Méthode appelée lorsque l'IA reçoit des dommages
    /// </summary>
    public virtual void OnDamageReceived(float damage, GameObject source)
    {
        // Vérifier si l'IA doit fuir après avoir reçu des dommages
        if (healthSystem != null && healthSystem.HealthPercentage * 100 <= fleeThreshold)
        {
            if (source != null)
            {
                target = source.transform;
            }
            TransitionToState(AIState.Fleeing);
        }
        // Si l'IA était passive et qu'elle reçoit des dégâts, elle devient agressive envers la source (si elle n'est pas neutre)
        else if (currentState == AIState.Passive && aiType != AIType.Neutral && source != null)
        {
            target = source.transform;
            TransitionToState(AIState.Aggressive);
        }
    }

    /// <summary>
    /// Méthode appelée à la mort de l'IA
    /// </summary>
    public virtual void OnDeath()
    {
        // Comportement de base à la mort
        Debug.Log($"{gameObject.name} est mort !");
        
        // Désinscrire l'IA de l'AIManager
        AIManager aiManager = FindFirstObjectByType<AIManager>();
        if (aiManager != null)
        {
            aiManager.UnregisterAI(this);
        }
        
        // Par défaut, on détruit l'objet
        Destroy(gameObject, 2f); // Laisser un délai pour les animations/effets de mort
    }

    /// <summary>
    /// Récupère le type d'IA (pour les interactions entre IA)
    /// </summary>
    public AIType GetAIType()
    {
        return aiType;
    }

    /// <summary>
    /// Récupère l'état actuel de l'IA
    /// </summary>
    public AIState GetCurrentState()
    {
        return currentState;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Vérifie si l'IA a une ligne de vue directe vers la cible
    /// </summary>
    protected virtual bool HasLineOfSight(Transform target)
    {
        Vector3 directionToTarget = target.position - transform.position;
        float distanceToTarget = directionToTarget.magnitude;
        
        // Lancer un rayon vers la cible
        RaycastHit hit;
        if (Physics.Raycast(transform.position, directionToTarget.normalized, out hit, distanceToTarget, obstacleLayers))
        {
            // Si le rayon touche quelque chose qui n'est pas la cible, pas de ligne de vue
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Vérifie si l'IA est au sol - MODIFIÉE pour une meilleure détection
    /// </summary>
    protected virtual bool IsGrounded()
    {
        // Position de départ du rayon légèrement au-dessus du centre du collider
        Vector3 rayStart = transform.position;
        
        // Distance du rayon basée sur la taille du collider
        float rayLength = 0.8f;
        
        // Visualiser le rayon dans la scène pour le débogage
        Debug.DrawRay(rayStart, Vector3.down * rayLength, Color.yellow, 0.1f);
        
        // Vérifier si l'IA touche le sol
        bool grounded = Physics.Raycast(rayStart, Vector3.down, rayLength, obstacleLayers);
        
        return grounded;
    }

    /// <summary>
    /// Configure le masque de couche des IA si non défini
    /// </summary>
    protected virtual void ConfigureAILayerMask()
    {
        if (aiLayerMask == 0)
        {
            // Essayer de récupérer le layer "AI" ou "Enemy" s'il existe
            int aiLayer = LayerMask.NameToLayer("AI");
            if (aiLayer == -1) aiLayer = LayerMask.NameToLayer("Enemy");
            
            // Si un layer approprié a été trouvé
            if (aiLayer != -1)
            {
                aiLayerMask = 1 << aiLayer;
                Debug.Log($"{gameObject.name}: Le masque de couche AI a été configuré automatiquement.");
            }
            else
            {
                // Utiliser le même layer que cette IA
                aiLayerMask = 1 << gameObject.layer;
                Debug.Log($"{gameObject.name}: Masque de couche AI configuré avec le layer actuel: {LayerMask.LayerToName(gameObject.layer)}");
            }
        }
    }

    #endregion

    #region AI Interaction and Avoidance

    /// <summary>
    /// Calcule la direction d'évitement des autres IA
    /// </summary>
    protected virtual Vector3 CalculateAIAvoidanceDirection()
    {
        // Détecter les IA à proximité
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, aiAvoidanceRadius * 2f, nearbyAIColliders, aiLayerMask);
        if (hitCount <= 1) return Vector3.zero; // Pas d'autres IA à proximité
        
        Vector3 avoidanceDirection = Vector3.zero;
        int avoidCount = 0;
        
        for (int i = 0; i < hitCount; i++)
        {
            // Ignorer cette IA et les nulls
            if (nearbyAIColliders[i] == null || nearbyAIColliders[i] == aiCollider) continue;
            
            // Calculer la direction et la distance à l'autre IA
            Vector3 otherPosition = nearbyAIColliders[i].transform.position;
            Vector3 directionToOther = transform.position - otherPosition;
            float distanceToOther = directionToOther.magnitude;
            
            // Plus l'IA est proche, plus forte est la répulsion
            if (distanceToOther < aiAvoidanceRadius)
            {
                float repulsionStrength = 1.0f - (distanceToOther / aiAvoidanceRadius);
                avoidanceDirection += directionToOther.normalized * repulsionStrength * aiAvoidanceForce;
                avoidCount++;
            }
        }
        
        // Normaliser si nécessaire
        if (avoidCount > 0 && avoidanceDirection.magnitude > 0.1f)
        {
            avoidanceDirection /= avoidCount;
            avoidanceDirection.Normalize();
        }
        
        return avoidanceDirection;
    }

    /// <summary>
    /// Calcule le comportement de groupe pour les IA du même type
    /// </summary>
    protected virtual Vector3 CalculateGroupBehavior()
    {
        // Si la fonctionnalité de comportement de groupe n'est pas activée, ne rien faire
        if (!considerAITypeForBehavior) return Vector3.zero;
        
        // Vecteur pour le comportement de groupe
        Vector3 groupVector = Vector3.zero;
        
        // Réduire le comportement de groupe pour les IA agressives (elles sont plus indépendantes)
        if (currentState == AIState.Aggressive)
        {
            float groupInfluenceMultiplier = 0.3f;
            
            // Détecter les IA proches
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius * 0.6f, nearbyAIColliders, aiLayerMask);
            
            if (hitCount == 0) return Vector3.zero;
            
            // Compteur d'IA du même type
            int sameTypeCount = 0;
            Vector3 averagePosition = Vector3.zero;
            Vector3 averageDirection = Vector3.zero;
            
            // Pour chaque IA proche
            for (int i = 0; i < hitCount; i++)
            {
                // Ne pas se considérer soi-même
                if (nearbyAIColliders[i] == aiCollider || nearbyAIColliders[i] == null) continue;
                
                // Récupérer le BaseAI de l'autre entité
                BaseAI otherAI = nearbyAIColliders[i].GetComponent<BaseAI>();
                if (otherAI == null) continue;
                
                // Si l'IA est du même type et dans le même état, la prendre en compte
                if (otherAI.aiType == aiType && otherAI.currentState == AIState.Aggressive)
                {
                    sameTypeCount++;
                    averagePosition += nearbyAIColliders[i].transform.position;
                    
                    // Prendre la direction vers le joueur comme direction commune pour les IA agressives
                    if (target != null)
                    {
                        Vector3 directionToTarget = (target.position - nearbyAIColliders[i].transform.position).normalized;
                        averageDirection += directionToTarget;
                    }
                    else
                    {
                        averageDirection += nearbyAIColliders[i].transform.forward;
                    }
                }
            }
            
            // S'il y a des IA du même type à proximité
            if (sameTypeCount > 0)
            {
                // Calculer la position moyenne du groupe
                averagePosition /= sameTypeCount;
                // Calculer la direction moyenne du groupe
                averageDirection /= sameTypeCount;
                
                // Pour les IA agressives, le but est d'encercler la cible
                if (target != null)
                {
                    // Calculer un offset unique pour cette IA
                    float uniqueAngle = (GetInstanceID() % 360) * Mathf.Deg2Rad;
                    Vector3 uniqueOffset = new Vector3(
                        Mathf.Cos(uniqueAngle) * attackRange * 0.8f,
                        0,
                        Mathf.Sin(uniqueAngle) * attackRange * 0.8f
                    );
                    
                    // Position idéale pour encercler
                    Vector3 idealPosition = target.position + uniqueOffset;
                    
                    // Si l'IA est trop proche d'une autre IA agressive, ajuster sa position
                    if (sameTypeCount > 1 && Vector3.Distance(transform.position, averagePosition) < aiAvoidanceRadius)
                    {
                        Vector3 awayFromGroup = (transform.position - averagePosition).normalized;
                        groupVector += awayFromGroup * 0.7f;
                    }
                    // Si l'IA est trop loin de sa position idéale, s'en rapprocher
                    else if (Vector3.Distance(transform.position, idealPosition) > attackRange * 0.5f)
                    {
                        Vector3 towardIdeal = (idealPosition - transform.position).normalized;
                        groupVector += towardIdeal * 0.5f;
                    }
                }
            }
            
            return groupVector.normalized * groupInfluenceMultiplier;
        }
        else
        {
            // Comportement de groupe standard pour les IA non-agressives
            float standardGroupRadius = detectionRadius * 0.8f;
            
            // Détecter les IA proches
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, standardGroupRadius, nearbyAIColliders, aiLayerMask);
            
            if (hitCount == 0) return Vector3.zero;
            
            // Compteur d'IA du même type
            int sameTypeCount = 0;
            Vector3 averagePosition = Vector3.zero;
            Vector3 averageDirection = Vector3.zero;
            
            // Pour chaque IA proche
            for (int i = 0; i < hitCount; i++)
            {
                // Ne pas se considérer soi-même
                if (nearbyAIColliders[i] == aiCollider || nearbyAIColliders[i] == null) continue;
                
                // Récupérer le BaseAI de l'autre entité
                BaseAI otherAI = nearbyAIColliders[i].GetComponent<BaseAI>();
                if (otherAI == null) continue;
                
                // Si l'IA est du même type et dans le même état, la prendre en compte
                if (otherAI.aiType == aiType && otherAI.currentState == currentState)
                {
                    sameTypeCount++;
                    averagePosition += nearbyAIColliders[i].transform.position;
                    
                    // Si l'autre IA a un rigidbody, prendre en compte sa direction
                    Rigidbody otherRb = nearbyAIColliders[i].GetComponent<Rigidbody>();
                    if (otherRb != null)
                    {
                      averageDirection += otherRb.linearVelocity.normalized;  
                    }
                    else
                    {
                        // Sinon, utiliser la direction de son regard
                        averageDirection += nearbyAIColliders[i].transform.forward;
                    }
                }
            }
            
            // S'il y a des IA du même type à proximité
            if (sameTypeCount > 0)
            {
                // Calculer la position moyenne du groupe
                averagePosition /= sameTypeCount;
                // Calculer la direction moyenne du groupe
                averageDirection /= sameTypeCount;
                
                // Si l'IA est trop loin du groupe, elle tend à s'en rapprocher
                float distanceToGroup = Vector3.Distance(transform.position, averagePosition);
                if (distanceToGroup > aiAvoidanceRadius * 1.5f)
                {
                    // Direction vers le groupe
                    Vector3 towardGroup = (averagePosition - transform.position).normalized;
                    groupVector += towardGroup * 0.5f;
                }
                
                // L'IA tend à s'aligner avec la direction moyenne du groupe
                groupVector += averageDirection * 0.3f;
            }
            
            return groupVector.normalized * 0.5f; // Influence limitée pour ne pas perturber le comportement principal
        }
    }

    #endregion

    #region Tactical Positioning

    /// <summary>
    /// Vérifie si d'autres IA sont sur le chemin direct vers la cible
    /// </summary>
    protected virtual bool IsAIInPathToTarget()
    {
        if (target == null) return false;
        
        // Direction et distance vers la cible
        Vector3 directionToTarget = target.position - transform.position;
        float distanceToTarget = directionToTarget.magnitude;
        directionToTarget.Normalize();
        
        // Détecter les IA proches
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, distanceToTarget, nearbyAIColliders, aiLayerMask);
        
        for (int i = 0; i < hitCount; i++)
        {
            // Ignorer cette IA
            if (nearbyAIColliders[i] == null || nearbyAIColliders[i] == aiCollider) continue;
            
            // Vérifier si l'IA est sur le chemin vers la cible
            Vector3 aiPosition = nearbyAIColliders[i].transform.position;
            Vector3 aiDirection = aiPosition - transform.position;
            
            // Projeter la position de l'IA sur la direction vers la cible
            float dotProduct = Vector3.Dot(aiDirection, directionToTarget);
            
            // Si la projection est positive (l'IA est devant) et inférieure à la distance à la cible
            if (dotProduct > 0 && dotProduct < distanceToTarget)
            {
                // Calculer la distance perpendiculaire de l'IA à la ligne directe
                Vector3 projection = transform.position + directionToTarget * dotProduct;
                float perpendicularDistance = Vector3.Distance(aiPosition, projection);
                
                // Si l'IA est suffisamment proche de la ligne directe
                if (perpendicularDistance < aiAvoidanceRadius * 1.2f)
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Calcule une position tactique pour contourner les autres IA
    /// </summary>
    protected virtual Vector3 CalculatePathAroundAI()
    {
        if (target == null) return transform.position;
        
        // Direction de base vers la cible
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        
        // Position cible par défaut
        Vector3 targetPosition = target.position;
        
        // Déterminer les IA qui sont sur le chemin
        Vector3 avoidanceDirection = Vector3.zero;
        bool foundObstruction = false;
        
        // Détecter les IA proches
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, nearbyAIColliders, aiLayerMask);
        
        for (int i = 0; i < hitCount; i++)
        {
            // Ignorer cette IA
            if (nearbyAIColliders[i] == null || nearbyAIColliders[i] == aiCollider) continue;
            
            // Calculer la direction et la distance vers l'autre IA
            Vector3 otherAIPosition = nearbyAIColliders[i].transform.position;
            Vector3 directionToAI = otherAIPosition - transform.position;
            float distanceToAI = directionToAI.magnitude;
            
            // Si l'IA est trop proche, on commence à l'éviter
            if (distanceToAI < aiAvoidanceRadius * 2f)
            {
                // Vérifier si cette IA est sur le chemin vers la cible
                Vector3 perpendicularToTarget = Vector3.Cross(Vector3.up, directionToTarget).normalized;
                
                // Déterminer de quel côté contourner
                float dotProduct = Vector3.Dot(directionToAI, perpendicularToTarget);
                Vector3 sideDirection = dotProduct >= 0 ? -perpendicularToTarget : perpendicularToTarget;
                
                // Ajouter la contribution à la direction d'évitement
                avoidanceDirection += sideDirection * (1.0f - Mathf.Clamp01(distanceToAI / (aiAvoidanceRadius * 2f)));
                foundObstruction = true;
            }
        }
        
        if (foundObstruction)
        {
            // Normaliser la direction d'évitement
            avoidanceDirection.Normalize();
            
            // Calculer un point intermédiaire pour contourner les obstacles
            float avoidanceDistance = attackRange * 1.5f;
            Vector3 intermediatePoint = transform.position + (directionToTarget + avoidanceDirection).normalized * avoidanceDistance;
            
            // Vérifier que ce point est accessible
            if (!Physics.CheckSphere(intermediatePoint, 0.5f, obstacleLayers))
            {
                return intermediatePoint;
            }
        }
        
        // Si aucun contournement n'est nécessaire ou possible, utiliser la position tactique standard
        return CalculateTacticalPosition();
    }

    /// <summary>
    /// Calcule une position tactique pour l'IA en fonction de son type
    /// </summary>
    protected virtual Vector3 CalculateTacticalPosition()
    {
        if (target == null) return transform.position;
        
        // Pour les IA de mêlée, on essaie de se positionner légèrement décalé de la cible
        if (aiType == AIType.Melee)
        {
            // Détecter combien d'IA de mêlée sont déjà autour de la cible
            int meleesAroundTarget = 0;
            
            // Détection autour de la cible
            int hitCount = Physics.OverlapSphereNonAlloc(target.position, attackRange * 1.5f, nearbyAIColliders, aiLayerMask);
            
            for (int i = 0; i < hitCount; i++)
            {
                // Ignorer cette IA et les nulls
                if (nearbyAIColliders[i] == null || nearbyAIColliders[i] == aiCollider) continue;
                
                // Vérifier si c'est une IA de mêlée
                BaseAI otherAI = nearbyAIColliders[i].GetComponent<BaseAI>();
                if (otherAI != null && otherAI.aiType == AIType.Melee && otherAI.currentState == AIState.Aggressive)
                {
                    meleesAroundTarget++;
                }
            }
            
            // Ajuster la position en fonction du nombre d'IA de mêlée déjà présentes
            if (meleesAroundTarget > 0)
            {
                // Plus il y a d'IA, plus on s'éloigne de la position moyenne pour éviter le stacking
                float spreadFactor = 1.0f + (meleesAroundTarget * 0.2f);
                
                // Générer un angle unique pour cette IA basé sur son instanceID
                float uniqueAngle = (GetInstanceID() % 360) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(uniqueAngle) * (attackRange * 0.9f) * spreadFactor,
                    0,
                    Mathf.Sin(uniqueAngle) * (attackRange * 0.9f) * spreadFactor
                );
                
                Vector3 offsetPosition = target.position + offset;
                
                // Vérifier que cette position est accessible
                if (!Physics.CheckSphere(offsetPosition, 0.5f, obstacleLayers))
                {
                    return offsetPosition;
                }
                
                // Si la position n'est pas accessible, essayer différents angles
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * (Mathf.PI / 4f);
                    Vector3 testOffset = new Vector3(
                        Mathf.Cos(angle) * (attackRange * 0.9f) * spreadFactor,
                        0,
                        Mathf.Sin(angle) * (attackRange * 0.9f) * spreadFactor
                    );
                    
                    Vector3 testPosition = target.position + testOffset;
                    if (!Physics.CheckSphere(testPosition, 0.5f, obstacleLayers))
                    {
                        return testPosition;
                    }
                }
            }
            else
            {
                // Si on est seul, utiliser un décalage unique basé sur l'ID de l'IA
                float uniqueAngle = (GetInstanceID() % 360) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(uniqueAngle) * (attackRange * 0.9f),
                    0,
                    Mathf.Sin(uniqueAngle) * (attackRange * 0.9f)
                );
                return target.position + offset;
            }
        }
        
        // Position par défaut
        return target.position;
    }

    /// <summary>
    /// Calcule une position optimale pour une IA à distance
    /// </summary>
    protected virtual Vector3 CalculateOptimalRangedPosition(float optimalRange)
    {
        if (target == null) return transform.position;
        
        // Générer un angle unique basé sur l'ID de l'IA
        float uniqueAngle = (GetInstanceID() % 360) * Mathf.Deg2Rad;
        
        // Calculer une position à la distance optimale
        Vector3 basePosition = target.position + new Vector3(
            Mathf.Cos(uniqueAngle) * optimalRange,
            0,
            Mathf.Sin(uniqueAngle) * optimalRange
        );
        
        // Vérifier si cette position est accessible
        if (!Physics.CheckSphere(basePosition, 1f, obstacleLayers))
        {
            return basePosition;
        }
        
        // Si la position n'est pas accessible, essayer différents angles
        for (int i = 1; i <= 8; i++)
        {
            float testAngle = uniqueAngle + (i * Mathf.PI / 4f);
            Vector3 testPosition = target.position + new Vector3(
                Mathf.Cos(testAngle) * optimalRange,
                0,
                Mathf.Sin(testAngle) * optimalRange
            );
            
            if (!Physics.CheckSphere(testPosition, 1f, obstacleLayers))
            {
                return testPosition;
            }
        }
        
        // Si aucune position n'est accessible, revenir à la position actuelle
        return transform.position;
    }

    /// <summary>
    /// Calcule une position optimale pour une IA à distance en évitant les autres IA
    /// </summary>
    protected virtual Vector3 CalculateRangedPositionAvoidingAI(float optimalRange)
    {
        if (target == null) return transform.position;
        
        // Position de base à la distance optimale
        Vector3 baseDirection = transform.position - target.position;
        
        // Si on est trop proche ou à distance nulle, générer une direction aléatoire
        if (baseDirection.magnitude < 1f)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            baseDirection = new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle));
        }
        else
        {
            baseDirection.y = 0;
            baseDirection.Normalize();
        }
        
        // Position de base à distance optimale
        Vector3 basePosition = target.position + baseDirection * optimalRange;
        
        // Détecter les IA proches de la position cible
        int hitCount = Physics.OverlapSphereNonAlloc(basePosition, aiAvoidanceRadius * 1.5f, nearbyAIColliders, aiLayerMask);
        
        // S'il n'y a pas d'IA proches de la position cible, on peut l'utiliser
        if (hitCount <= 1) return basePosition;
        
        // Sinon, on cherche une position alternative
        // Essayer différents angles autour de la cible
        for (int i = 1; i <= 12; i++)
        {
            float testAngle = (i * 30f) * Mathf.Deg2Rad;
            Vector3 testDirection = new Vector3(Mathf.Cos(testAngle), 0, Mathf.Sin(testAngle));
            Vector3 testPosition = target.position + testDirection * optimalRange;
            
            // Vérifier si cette position est libre d'IA et d'obstacles
            bool isPositionClear = true;
            
            // Vérifier les obstacles
            if (Physics.CheckSphere(testPosition, 1f, obstacleLayers))
            {
                isPositionClear = false;
                continue;
            }
            
            // Vérifier les autres IA
            int aiCount = Physics.OverlapSphereNonAlloc(testPosition, aiAvoidanceRadius, nearbyAIColliders, aiLayerMask);
            for (int j = 0; j < aiCount; j++)
            {
                if (nearbyAIColliders[j] != null && nearbyAIColliders[j] != aiCollider)
                {
                    isPositionClear = false;
                    break;
                }
            }
            
            if (isPositionClear)
            {
                return testPosition;
            }
        }
        
        // Si aucune position n'est idéale, utiliser la position standard
        return CalculateOptimalRangedPosition(optimalRange);
    }

    #endregion

    #region Debug Visualization

    /// <summary>
    /// Dessine des éléments de debug dans l'éditeur
    /// </summary>
    protected virtual void OnDrawGizmosSelected()
    {
        // Dessiner le rayon de détection
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Dessiner le rayon d'attaque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Dessiner la destination passive si elle existe
        if (isMoving)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(randomDestination, 0.3f);
            Gizmos.DrawLine(transform.position, randomDestination);
        }
        
        // Dessiner les rayons d'évitement et de comportement de groupe
        if (target != null && currentState == AIState.Aggressive)
        {
            // Visualiser le comportement d'évitement des autres IA
            Vector3 avoidanceDirection = CalculateAIAvoidanceDirection();
            if (avoidanceDirection.magnitude > 0.1f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(transform.position, avoidanceDirection.normalized * 2f);
            }
            
            // Calculer une position tactique pour l'IA
            Vector3 tacticalPosition = CalculateTacticalPosition();
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, tacticalPosition);
            Gizmos.DrawSphere(tacticalPosition, 0.3f);
        }
    }

    /// <summary>
    /// Dessine le rayon d'évitement entre IA en mode debug
    /// </summary>
    protected void OnDrawGizmos()
    {
        // Ne dessiner que si l'option de visualisation est activée
        if (!isActiveAndEnabled) return;
        
        // Dessiner le rayon d'évitement des IA
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, aiAvoidanceRadius);
    }
    }
    #endregion