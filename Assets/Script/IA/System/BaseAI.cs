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
    [Header("Paramètres généraux")]
    [SerializeField] protected AIType aiType = AIType.Neutral;
    [SerializeField] protected float moveSpeed = 3f;
    [SerializeField] protected float rotationSpeed = 5f;
    [SerializeField] protected float baseDamage = 10f; // Dégâts de base pour les attaques

    [Header("Détection")]
    [SerializeField] protected float detectionRadius = 10f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float fleeThreshold = 30f; // % de santé en dessous duquel l'IA fuit
    [SerializeField] protected LayerMask detectionLayers;
    [SerializeField] protected LayerMask obstacleLayers;

    [Header("Comportement")]
    [SerializeField] protected float passiveMovementRadius = 5f;
    [SerializeField] protected float passiveMovementDuration = 3f;
    [SerializeField] protected float passiveWaitDuration = 2f;

    // Variables d'état
    protected AIState currentState = AIState.Passive;
    protected Transform target;
    protected Vector3 randomDestination;
    protected Rigidbody rb;
    protected Collider aiCollider;
    protected bool isMoving = false;
    protected bool isWaiting = false;
    protected float currentMovementTimer = 0f;
    protected float currentWaitTimer = 0f;
    protected Vector3 startPosition;

    // Cache pour les colliders détectés
    protected Collider[] detectedColliders = new Collider[10];

    // Système de cooldown pour les attaques
    protected float attackCooldown = 1f;
    protected float lastAttackTime = 0f;

    // Système pour l'optimisation
    protected bool isActive = false;
    
    // Référence au système de santé
    protected HealthSystem healthSystem;
    
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
                // Pour le melee, on s'approche jusqu'à être à portée d'attaque
                if (distanceToTarget > attackRange)
                {
                    MoveToPosition(target.position);
                }
                else
                {
                    // À portée d'attaque, on s'arrête et on attaque
                    FaceTarget();
                    Attack();
                }
                break;

            case AIType.Ranged:
                // Pour le ranged, on maintient une distance optimale
                float optimalRange = attackRange * 0.8f;
                if (distanceToTarget < optimalRange * 0.7f)
                {
                    // Trop proche, on recule
                    Vector3 directionFromTarget = transform.position - target.position;
                    Vector3 retreatPosition = transform.position + directionFromTarget.normalized * 2f;
                    MoveToPosition(retreatPosition);
                }
                else if (distanceToTarget > optimalRange * 1.3f)
                {
                    // Trop loin, on s'approche
                    MoveToPosition(target.position);
                }
                else
                {
                    // Distance idéale, on s'arrête et on attaque
                    FaceTarget();
                    Attack();
                }
                break;

            case AIType.Neutral:
                // Les IA neutres ne devraient pas être en mode agressif
                TransitionToState(AIState.Passive);
                break;
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
        Vector3 fleePosition = transform.position + fleeDirection.normalized * 5f;

        // Ajuster la position de fuite pour éviter les obstacles
        RaycastHit hit;
        if (Physics.Raycast(transform.position, fleeDirection.normalized, out hit, 5f, obstacleLayers))
        {
            // On tente de fuir dans une direction légèrement modifiée
            Vector3 alternativeDirection = Quaternion.Euler(0, Random.Range(30f, 90f), 0) * fleeDirection;
            fleePosition = transform.position + alternativeDirection.normalized * 5f;
        }

        // Se déplacer dans la direction de fuite
        MoveToPosition(fleePosition);
    }

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
    /// <param name="damageAmount">Montant des dégâts à infliger</param>
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
    /// Déplace l'IA vers une position spécifiée
    /// </summary>
    protected virtual void MoveToPosition(Vector3 position)
    {
        // Calculer la direction et la distance
        Vector3 directionToTarget = position - transform.position;
        directionToTarget.y = 0; // Maintenir le mouvement sur le plan XZ
        
        if (directionToTarget.magnitude < 0.1f) return;

        // Rotation vers la cible
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        // Déplacement vers l'avant
        Vector3 movement = transform.forward * moveSpeed * Time.deltaTime;
        
        // Vérifier si le mouvement est possible (pas d'obstacles)
        if (!IsPathBlocked(movement))
        {
            transform.position += movement;
        }
        else
        {
            // En cas d'obstacle, essayer de trouver une direction alternative
            FindAlternativePath();
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
                break;
            case AIState.Fleeing:
                // Actions spécifiques à effectuer en entrant dans l'état de fuite
                break;
        }

        // Informations de debug
        Debug.Log($"{gameObject.name} a changé d'état : {newState}");
    }

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
    /// <param name="damage">Quantité de dommages</param>
    /// <param name="source">Source des dommages</param>
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
    }
}