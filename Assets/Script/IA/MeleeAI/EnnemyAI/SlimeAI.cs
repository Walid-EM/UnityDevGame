using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IA spécifique pour l'ennemi de type Slime
/// Hérite de MeleeAI pour les comportements de base de corps-à-corps
/// </summary>
public class SlimeAI : MeleeAI
{
    [Header("Paramètres spécifiques du Slime")]
    [SerializeField] private float jumpForce = 5f;              // Force du saut
    [SerializeField] private float jumpCooldown = 3f;           // Temps entre deux sauts
    [SerializeField] private float jumpPrepareTime = 0.5f;      // Temps de préparation avant le saut
    [SerializeField] private float jumpDamageMultiplier = 1.5f; // Multiplicateur de dégâts pour l'attaque de saut
    [SerializeField] private float jumpAttackRadius = 2f;       // Rayon de l'attaque de saut
    [SerializeField] private bool canSplitOnDeath = false;      // Le Slime peut se diviser en mourant
    [SerializeField] private GameObject smallerSlimePrefab;     // Prefab pour les petits slimes
    [SerializeField] private int splitCount = 2;                // Nombre de petits slimes créés
    [SerializeField] private float slimeAvoidanceMultiplier = 1.8f; // Multiplicateur d'évitement pour les slimes

    [Header("Références d'animation")]
    [SerializeField] private Animator animator;                 // Référence à l'Animator du Slime
    
    // Constantes pour les paramètres d'animation
    private static readonly string ANIM_IS_MOVING = "IsMoving";
    private static readonly string ANIM_IS_JUMPING = "IsJumping";
    private static readonly string ANIM_PREPARE_JUMP = "PrepareJump";
    private static readonly string ANIM_ATTACK = "Attack";
    private static readonly string ANIM_TAKE_DAMAGE = "TakeDamage";
    private static readonly string ANIM_DIE = "Die";
    private static readonly string ANIM_LAND = "Land";
    private static readonly string ANIM_IS_AGGRESSIVE = "IsAggressive";

    // Variables d'état pour le saut
    private float lastJumpTime = -10f;  // -10 pour permettre un saut dès le début
    private bool isPrepareJump = false;
    private bool isJumping = false;
    private Vector3 jumpTargetPosition;

    protected override void Awake()
    {
        base.Awake();
        
        Debug.Log($"{gameObject.name}: moveSpeed={moveSpeed}, attackRange={attackRange}, attackDamage={attackDamage}");

        // Configuration spécifique au Slime
        moveSpeed = 2.5f;                // Vitesse réduite
        attackRange = 1.5f;              // Attaque à courte portée
        attackDamage = 10f;              // Dégâts modérés
        attackCooldown = 2f;             // Attaque lente
        
        // S'abonner à l'événement de mort
        if (healthSystem != null)
        {
            healthSystem.OnDeath.AddListener(OnDeath);
        }
        
        // Augmenter le facteur d'évitement pour les slimes
        aggressiveAvoidanceMultiplier = slimeAvoidanceMultiplier;
        
        // S'assurer que l'Animator est référencé
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            
            if (animator == null)
            {
                Debug.LogError("Animator component not found on SlimeAI GameObject!");
            }
        }
    }

    protected override void Start()
    {
        base.Start();

        // S'assurer que le Rigidbody est configuré correctement pour les sauts
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation; // Empêcher la rotation du Slime
        }
    }

    protected override void Update()
    {
        base.Update();
        
        // Ne pas exécuter les animations si l'IA n'est pas active
        if (!isActive) return;
        
        // Mettre à jour l'animation de déplacement
        if (animator != null)
        {
            // Détecter si le slime est en mouvement
            bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.1f && !isJumping && !isPrepareJump;
            animator.SetBool(ANIM_IS_MOVING, isMoving);
            
            // Synchroniser l'état agressif avec l'Animator
            animator.SetBool(ANIM_IS_AGGRESSIVE, GetCurrentState() == AIState.Aggressive);
        }
        
        // Si un saut est bloqué depuis trop longtemps, le réinitialiser
        if ((isPrepareJump || isJumping) && Time.time - lastJumpTime > 5f)
        {
            CancelJumpAnimation();
            Debug.Log("Animation de saut forcée à terminer après timeout");
        }
    }
    
    /// <summary>
    /// Surcharge du comportement agressif pour tenir compte de l'état de saut
    /// </summary>
    protected override void UpdateAggressiveBehavior()
    {
        Debug.Log($"{gameObject.name}: Distance au joueur: {Vector3.Distance(transform.position, target.position)}, AttackRange: {attackRange}");

        // Si le slime est en phase de saut ou de préparation, ne pas exécuter le comportement standard
        if (isPrepareJump || isJumping)
        {
            // Continuer à faire face à la cible pendant le saut
            if (target != null)
            {
                FaceTarget();
            }
            return;
        }
        
        // Sinon, utiliser le comportement agressif standard
        base.UpdateAggressiveBehavior();
    }

    /// <summary>
    /// Surcharge de la méthode de calcul de position tactique pour les slimes
    /// </summary>
    protected override Vector3 CalculateTacticalPosition()
    {
        if (target == null) return transform.position;
        
        // Position de base calculée par la méthode parent
        Vector3 basePosition = base.CalculateTacticalPosition();
        
        // Pour les slimes, s'assurer qu'ils se répartissent encore plus pour préparer leurs sauts
        // Détecter les autres slimes autour de la cible
        int slimesAroundTarget = 0;
        
        int hitCount = Physics.OverlapSphereNonAlloc(target.position, jumpAttackRadius * 2f, nearbyAIColliders, aiLayerMask);
        
        for (int i = 0; i < hitCount; i++)
        {
            if (nearbyAIColliders[i] == null || nearbyAIColliders[i] == aiCollider) continue;
            
            // Vérifier si c'est un autre slime
            SlimeAI otherSlime = nearbyAIColliders[i].GetComponent<SlimeAI>();
            if (otherSlime != null && otherSlime.GetCurrentState() == AIState.Aggressive)
            {
                slimesAroundTarget++;
            }
        }
        
        // S'il y a d'autres slimes, augmenter la distance de positionnement
        if (slimesAroundTarget > 0)
        {
            // Direction de la cible vers notre position de base
            Vector3 directionFromTarget = basePosition - target.position;
            
            if (directionFromTarget.magnitude < 0.1f)
            {
                // Si la direction est trop petite, générer une direction aléatoire
                float randomAngle = ((GetInstanceID() % 360) + Time.time * 10f) % 360f * Mathf.Deg2Rad;
                directionFromTarget = new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle));
            }
            
            // Normaliser et étendre la distance pour éviter le chevauchement des slimes
            directionFromTarget = directionFromTarget.normalized;
            float distanceMultiplier = 1.0f + (slimesAroundTarget * 0.4f);
            
            // Calculer une nouvelle position qui tient compte des autres slimes
            Vector3 adjustedPosition = target.position + directionFromTarget * (attackRange * distanceMultiplier);
            
            // Vérifier que cette position est accessible
            if (!Physics.CheckSphere(adjustedPosition, 0.5f, obstacleLayers))
            {
                return adjustedPosition;
            }
        }
        
        // Si aucun ajustement n'est nécessaire ou possible, utiliser la position de base
        return basePosition;
    }

    /// <summary>
    /// Détermine si le Slime peut effectuer son attaque spéciale (saut)
    /// </summary>
    protected override bool CanPerformSpecialAction()
    {
        // Ne pas effectuer d'action spéciale si on est déjà en train de sauter
        if (isPrepareJump || isJumping) return false;
        
        // Vérifier si le cooldown du saut est passé
        if (Time.time - lastJumpTime < jumpCooldown) return false;
        
        // Vérifier si on a une cible
        if (target == null) return false;
        
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        
        // Sauter si la distance est appropriée (ni trop près, ni trop loin)
        return distanceToTarget > attackRange * 1.5f && distanceToTarget < detectionRadius * 0.7f;
    }

    /// <summary>
    /// Exécute l'attaque spéciale de saut du Slime
    /// </summary>
    protected override void PerformSpecialAction()
    {
        StartCoroutine(JumpAttackCoroutine());
    }
    
    /// <summary>
    /// Coroutine gérant la physique du saut d'attaque et déclenche les animations correspondantes
    /// </summary>
    private IEnumerator JumpAttackCoroutine()
    {
        Debug.Log("Début de JumpAttackCoroutine");
        float totalCoroutineTime = 0f;
        float maxCoroutineTime = 5f; // 5 secondes max pour toute l'animation
        
        isPrepareJump = true;
        lastJumpTime = Time.time;
        
        // Déclencher l'animation de préparation
        animator.SetBool(ANIM_PREPARE_JUMP, true);
        
        // Attendre le temps de préparation (l'animation sera jouée pendant ce temps)
        yield return new WaitForSeconds(jumpPrepareTime);
        
        // Calculer la direction vers la cible
        if (target != null)
        {
            Vector3 directionToTarget = target.position - transform.position;
            directionToTarget.y = 0; // Garder le mouvement horizontal
            directionToTarget = directionToTarget.normalized;
            
            // Stocker la position cible du saut
            jumpTargetPosition = target.position;
            
            // Passage à l'état de saut
            isPrepareJump = false;
            isJumping = true;
            
            // Désactiver l'animation de préparation et activer l'animation de saut
            animator.SetBool(ANIM_PREPARE_JUMP, false);
            animator.SetBool(ANIM_IS_JUMPING, true);
            
            // Appliquer la force de saut
            if (rb != null)
            {
                // Calculer la hauteur du saut en fonction de la distance
                float horizontalDistance = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z), 
                    new Vector3(target.position.x, 0, target.position.z)
                );
                float verticalForce = jumpForce * (0.5f + horizontalDistance * 0.1f);
                
                // Réinitialiser la vélocité d'abord
                rb.linearVelocity = Vector3.zero;
                rb.AddForce(directionToTarget * jumpForce + Vector3.up * verticalForce, ForceMode.Impulse);
                
                Debug.Log($"{gameObject.name} effectue un saut d'attaque vers {target.name}");
            }

            float groundCheckTime = 0f;
            float maxGroundCheckTime = 3f; // Maximum 3 secondes pour attendre de toucher le sol
            
            // Attendre un peu pour permettre au Slime de quitter le sol
            yield return new WaitForSeconds(0.1f);
            
            // Attendre que le Slime touche à nouveau le sol
            while (!IsGrounded() && groundCheckTime < maxGroundCheckTime && totalCoroutineTime < maxCoroutineTime)
            {
                groundCheckTime += Time.deltaTime;
                totalCoroutineTime += Time.deltaTime;
                yield return null;
            }
            
            Debug.Log("Slime a atterri ou le temps maximum est écoulé");
            
            // Déclencher l'animation d'atterrissage
            animator.SetBool(ANIM_IS_JUMPING, false);
            animator.SetTrigger(ANIM_LAND);
            
            // Vérifier si on est proche de la cible pour infliger des dégâts
            CheckForJumpAttackDamage();
            
            // Attendre que l'animation d'atterrissage se termine
            yield return new WaitForSeconds(0.3f);
            
            // Réinitialiser les états
            isJumping = false;
            
            Debug.Log($"{gameObject.name}: Animation de saut terminée correctement");
        }
        else
        {
            // Annuler l'animation si aucune cible n'est disponible
            CancelJumpAnimation();
        }
    }
    
    /// <summary>
    /// Annule toutes les animations de saut et réinitialise les états
    /// </summary>
    private void CancelJumpAnimation()
    {
        isPrepareJump = false;
        isJumping = false;
        
        if (animator != null)
        {
            animator.SetBool(ANIM_PREPARE_JUMP, false);
            animator.SetBool(ANIM_IS_JUMPING, false);
        }
    }
    
    /// <summary>
    /// Vérifie si le Slime est au sol
    /// </summary>
    protected override bool IsGrounded()    
    {
        // Augmentez la longueur du rayon pour mieux détecter le sol
        float rayLength = 0.5f;
        // Vérifiez que les couches détectées sont correctes
        Debug.DrawRay(transform.position, Vector3.down * rayLength, Color.red, 0.1f);
        bool grounded = Physics.Raycast(transform.position, Vector3.down, rayLength, obstacleLayers);
        Debug.Log($"IsGrounded check: {grounded}");
        return grounded;
    }
    
    /// <summary>
    /// Vérifie si des cibles se trouvent dans la zone d'atterrissage du saut et leur inflige des dégâts
    /// </summary>
    private void CheckForJumpAttackDamage()
    {
        // Détecter toutes les cibles dans le rayon d'attaque du saut
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, jumpAttackRadius, detectionLayers);
        
        foreach (var hitCollider in hitColliders)
        {
            // Ne pas s'infliger des dégâts à soi-même
            if (hitCollider.gameObject == gameObject) continue;
            
            // Infliger des dégâts aux cibles touchées
            if (hitCollider.CompareTag("Player"))
            {
                // Calculer les dégâts du saut (dégâts de base * multiplicateur)
                float jumpDamage = attackDamage * jumpDamageMultiplier;
                
                // Appliquer les dégâts
                PlayerStats playerStats = hitCollider.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    playerStats.TakeDamage(jumpDamage);
                    Debug.Log($"{gameObject.name} a écrasé {hitCollider.name} avec son saut et infligé {jumpDamage} dégâts");
                    
                    // Appliquer un knockback plus fort que l'attaque normale
                    ApplyKnockback(hitCollider.transform, knockbackForce * 1.5f);
                }
                else
                {
                    // Alternative avec HealthSystem
                    HealthSystem targetHealth = hitCollider.GetComponent<HealthSystem>();
                    if (targetHealth != null)
                    {
                        targetHealth.TakeDamage(jumpDamage, gameObject);
                        
                        // Appliquer un knockback plus fort
                        ApplyKnockback(hitCollider.transform, knockbackForce * 1.5f);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Surcharge la méthode Attack pour utiliser l'Animator
    /// </summary>
    protected override void Attack()
    {
        // Ne pas attaquer si le cooldown n'est pas terminé ou si on est déjà en train d'attaquer
        if (Time.time - lastAttackTime < attackCooldown || isAttacking) return;

        lastAttackTime = Time.time;
        isAttacking = true;

        // Déclencher l'animation d'attaque via l'Animator et la coroutine
        StartCoroutine(SlimeAttackCoroutine());
    }
    
    /// <summary>
    /// Coroutine gérant l'animation et les dégâts d'une attaque de slime
    /// </summary>
    protected virtual IEnumerator SlimeAttackCoroutine()
    {
        // Déclencher l'animation d'attaque via l'Animator
        if (animator != null)
        {
            animator.SetTrigger(ANIM_ATTACK);
        }
        
        Debug.Log($"{gameObject.name} commence une attaque de slime !");

        // Attendre que l'animation se termine
        yield return new WaitForSeconds(attackAnimationDuration);

        // Appliquer les dégâts si la cible est toujours à portée
        if (target != null && Vector3.Distance(transform.position, target.position) <= attackRange)
        {
            // Appliquer les dégâts
            ApplyDamageToTarget(attackDamage);
            
            // Appliquer un effet de recul
            ApplyKnockback(target);

            // Gérer les combos si activés
            if (useComboAttacks)
            {
                // Si on est dans la fenêtre de temps du combo
                if (Time.time - lastComboTime < comboTimeWindow)
                {
                    currentComboCount++;
                    
                    // Si on n'a pas atteint le nombre maximum de coups
                    if (currentComboCount < maxComboHits)
                    {
                        // Réduire le cooldown pour enchaîner plus vite
                        lastAttackTime -= attackCooldown * 0.5f;
                    }
                    else
                    {
                        // Réinitialiser le compteur si on a atteint le max
                        currentComboCount = 0;
                    }
                }
                else
                {
                    // Hors de la fenêtre de temps, réinitialiser le compteur
                    currentComboCount = 1;
                }
                
                lastComboTime = Time.time;
            }
        }

        isAttacking = false;
    }
    
    /// <summary>
    /// Applique une force de knockback spécifique à une cible
    /// </summary>
    /// <param name="targetTransform">Transform de la cible</param>
    /// <param name="force">Force du knockback</param>
    private void ApplyKnockback(Transform targetTransform, float force)
    {
        if (targetTransform == null) return;
        
        // Direction du knockback depuis le centre du Slime vers la cible
        Vector3 knockbackDirection = (targetTransform.position - transform.position).normalized;
        knockbackDirection.y = 0.5f; // Ajouter une composante verticale pour un effet plus dynamique
        
        // Appliquer la force
        Rigidbody targetRb = targetTransform.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.AddForce(knockbackDirection * force, ForceMode.Impulse);
            Debug.Log($"Knockback puissant appliqué à {targetTransform.name} avec force {force}");
        }
    }
    
    /// <summary>
    /// Réaction aux dégâts reçus - déclenche l'animation de dégâts
    /// </summary>
    public override void OnDamageReceived(float damage, GameObject source)
    {
        base.OnDamageReceived(damage, source);
        
        // Déclencher l'animation de dégâts reçus
        if (animator != null)
        {
            animator.SetTrigger(ANIM_TAKE_DAMAGE);
        }
    }
    
    /// <summary>
    /// Comportement à la mort du Slime - déclenche l'animation de mort
    /// </summary>
    public override void OnDeath()
    {
        // Arrêter toutes les animations en cours
        CancelJumpAnimation();
        
        // Déclencher l'animation de mort
        if (animator != null)
        {
            animator.SetTrigger(ANIM_DIE);
        }
        
        // Division en petits slimes si configuré
        if (canSplitOnDeath && smallerSlimePrefab != null)
        {
            SplitIntoSmallerSlimes();
        }
        
        // Appeler le comportement de base
        base.OnDeath();
    }
    
    /// <summary>
    /// Divise le Slime en plusieurs petits Slimes à sa mort
    /// </summary>
    private void SplitIntoSmallerSlimes()
    {
        for (int i = 0; i < splitCount; i++)
        {
            // Calculer une position légèrement décalée
            Vector3 offset = new Vector3(
                Random.Range(-0.5f, 0.5f),
                0f,
                Random.Range(-0.5f, 0.5f)
            );
            
            // Instancier le petit slime
            GameObject smallSlime = Instantiate(smallerSlimePrefab, transform.position + offset, Quaternion.identity);
            
            // Configurer le petit slime
            SlimeAI smallSlimeAI = smallSlime.GetComponent<SlimeAI>();
            if (smallSlimeAI != null)
            {
                // Hériter de certaines propriétés du parent mais réduire les statistiques
                smallSlimeAI.SetActive(true);
                
                // Donner un seuil de fuite plus élevé aux petits slimes
                HealthSystem smallHealthSystem = smallSlime.GetComponent<HealthSystem>();
                if (smallHealthSystem != null)
                {
                    smallHealthSystem.SetMaxHealth(healthSystem.MaxHealth * 0.4f, true);
                }
            }
            
            // Donner une petite impulsion au petit slime
            Rigidbody smallRb = smallSlime.GetComponent<Rigidbody>();
            if (smallRb != null)
            {
                Vector3 bounceDirection = new Vector3(offset.x, 1f, offset.z).normalized;
                smallRb.AddForce(bounceDirection * 3f, ForceMode.Impulse);
            }
            
            // Enregistrer le petit slime dans l'AIManager si disponible
            AIManager aiManager = FindFirstObjectByType<AIManager>();
            if (aiManager != null && smallSlimeAI != null)
            {
                aiManager.RegisterAI(smallSlimeAI);
            }
        }
        
        Debug.Log($"{gameObject.name} s'est divisé en {splitCount} petits slimes!");
    }
    
    /// <summary>
    /// Dessine les gizmos pour visualiser les zones d'attaque spécifiques au Slime
    /// </summary>
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Dessiner le rayon d'attaque de saut
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, jumpAttackRadius);
    }
}