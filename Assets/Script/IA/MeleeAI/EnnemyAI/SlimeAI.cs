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
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float jumpCooldown = 3f;
    [SerializeField] private float jumpPrepareTime = 0.5f;
    [SerializeField] private float jumpDamageMultiplier = 1.5f;
    [SerializeField] private float jumpAttackRadius = 2f;
    [SerializeField] private bool canSplitOnDeath = false;
    [SerializeField] private GameObject smallerSlimePrefab;
    [SerializeField] private int splitCount = 2;
    [SerializeField] private float slimeAvoidanceMultiplier = 1.8f;

    [Header("Références d'animation")]
    [SerializeField] private Animator animator; // Référence directe à l'animator de SlimeV1

    // Constantes pour les paramètres d'animation
    private static readonly string ANIM_IS_MOVING = "IsMoving";
    private static readonly string ANIM_IS_JUMPING = "IsJumping";
    private static readonly string ANIM_PREPARE_JUMP = "PrepareJump";
    private static readonly string ANIM_ATTACK = "Attack";
    private static readonly string ANIM_TAKE_DAMAGE = "TakeDamage";
    private static readonly string ANIM_DIE = "Die";
    private static readonly string ANIM_LAND = "Land";
    private static readonly string ANIM_IS_AGGRESSIVE = "IsAggressive";
    private static readonly string ANIM_IS_IDLE = "IsIdle"; 

    // Variables d'état pour le saut
    private float lastJumpTime = -10f;
    private bool isPrepareJump = false;
    private bool isJumping = false;
    private Vector3 jumpTargetPosition;

    protected override void Awake()
    {
        base.Awake();
        
        Debug.Log($"{gameObject.name}: moveSpeed={moveSpeed}, attackRange={attackRange}, attackDamage={attackDamage}");

        // Configuration spécifique au Slime
        moveSpeed = 2.5f;
        attackRange = 1.5f;
        attackDamage = 10f;
        attackCooldown = 2f;
        
        if (healthSystem != null)
        {
            healthSystem.OnDeath.AddListener(OnDeath);
        }
        
        aggressiveAvoidanceMultiplier = slimeAvoidanceMultiplier;
        
        // Vérification de l'Animator
        if (animator != null)
        {
            Debug.Log($"Animator trouvé sur {animator.gameObject.name}, état: {animator.enabled}");
            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError("Animator controller is missing on " + animator.gameObject.name);
            }
        }
        else
        {
            Debug.LogError($"Animator non assigné sur {gameObject.name}. Assurez-vous d'assigner l'Animator de SlimeV1 dans l'inspecteur.");
        }
    }

    protected override void Start()
    {
        base.Start();

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
        
        // Initialisation des paramètres d'animation au démarrage
        InitializeAnimationParameters();
    }
    
    private void InitializeAnimationParameters()
    {
        if (animator != null && animator.enabled)
        {
            // Initialisation de tous les paramètres de l'animator
            animator.SetBool(ANIM_IS_MOVING, false);
            animator.SetBool(ANIM_IS_JUMPING, false);
            animator.SetBool(ANIM_PREPARE_JUMP, false);
            animator.SetBool(ANIM_IS_AGGRESSIVE, false);
            animator.SetBool(ANIM_IS_IDLE, true); // Définir IsIdle à true pour déclencher l'animation idle
            
            Debug.Log($"{gameObject.name}: Paramètres d'animation initialisés, IsIdle défini à TRUE");
        }
    }

    protected override void Update()
    {
        base.Update();
        
        if (!isActive) return;
        
        if (animator != null && animator.enabled)
        {
            bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.1f && !isJumping && !isPrepareJump;
            bool isAggressive = GetCurrentState() == AIState.Aggressive;
            
            animator.SetBool(ANIM_IS_MOVING, isMoving);
            animator.SetBool(ANIM_IS_AGGRESSIVE, isAggressive);
            
            // Mise à jour de IsIdle (en état idle quand pas en mouvement, pas en saut, et pas agressif)
            bool isIdle = !isMoving && !isJumping && !isPrepareJump && !isAggressive;
            animator.SetBool(ANIM_IS_IDLE, isIdle);
            
            if (Time.frameCount % 60 == 0)
            {
                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"État actuel de l'Animator: {currentState.fullPathHash}, " +
                          $"IsName(Jumping): {currentState.IsName("Jumping")}, " +
                          $"Paramètres - IsJumping: {animator.GetBool(ANIM_IS_JUMPING)}, " +
                          $"PrepareJump: {animator.GetBool(ANIM_PREPARE_JUMP)}, " +
                          $"IsMoving: {animator.GetBool(ANIM_IS_MOVING)}, " +
                          $"IsIdle: {animator.GetBool(ANIM_IS_IDLE)}");
            }
        }
        
        if ((isPrepareJump || isJumping) && Time.time - lastJumpTime > 5f)
        {
            CancelJumpAnimation();
            Debug.Log("Animation de saut forcée à terminer après timeout");
        }
    }

    protected override void UpdateAggressiveBehavior()
    {
        if (isPrepareJump || isJumping)
        {
            if (target != null)
            {
                FaceTarget();
            }
            return;
        }
        
        base.UpdateAggressiveBehavior();
    }

    protected override Vector3 CalculateTacticalPosition()
    {
        if (target == null) return transform.position;
        
        Vector3 basePosition = base.CalculateTacticalPosition();
        
        int slimesAroundTarget = 0;
        int hitCount = Physics.OverlapSphereNonAlloc(target.position, jumpAttackRadius * 2f, nearbyAIColliders, aiLayerMask);
        
        for (int i = 0; i < hitCount; i++)
        {
            if (nearbyAIColliders[i] == null || nearbyAIColliders[i] == aiCollider) continue;
            
            SlimeAI otherSlime = nearbyAIColliders[i].GetComponent<SlimeAI>();
            if (otherSlime != null && otherSlime.GetCurrentState() == AIState.Aggressive)
            {
                slimesAroundTarget++;
            }
        }
        
        if (slimesAroundTarget > 0)
        {
            Vector3 directionFromTarget = basePosition - target.position;
            
            if (directionFromTarget.magnitude < 0.1f)
            {
                float randomAngle = ((GetInstanceID() % 360) + Time.time * 10f) % 360f * Mathf.Deg2Rad;
                directionFromTarget = new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle));
            }
            
            directionFromTarget = directionFromTarget.normalized;
            float distanceMultiplier = 1.0f + (slimesAroundTarget * 0.4f);
            
            Vector3 adjustedPosition = target.position + directionFromTarget * (attackRange * distanceMultiplier);
            
            if (!Physics.CheckSphere(adjustedPosition, 0.5f, obstacleLayers))
            {
                return adjustedPosition;
            }
        }
        
        return basePosition;
    }

    protected override bool CanPerformSpecialAction()
    {
        if (isPrepareJump || isJumping) return false;
        if (Time.time - lastJumpTime < jumpCooldown) return false;
        if (target == null) return false;
        
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        return distanceToTarget > attackRange * 1.5f && distanceToTarget < detectionRadius * 0.7f;
    }

    protected override void PerformSpecialAction()
    {
        StartCoroutine(JumpAttackCoroutine());
    }
    
    private IEnumerator JumpAttackCoroutine()
    {
        Debug.Log("Début de JumpAttackCoroutine");
        float totalCoroutineTime = 0f;
        float maxCoroutineTime = 5f;
        
        isPrepareJump = true;
        lastJumpTime = Time.time;
        
        if (animator != null && animator.enabled)
        {
            animator.SetBool(ANIM_IS_IDLE, false); // Désactiver l'état idle
            animator.SetBool(ANIM_PREPARE_JUMP, true);
            Debug.Log($"ANIMATION: PrepareJump défini à TRUE, IsIdle défini à FALSE");
        }
        
        yield return new WaitForSeconds(jumpPrepareTime);
        
        if (target != null)
        {
            Vector3 directionToTarget = target.position - transform.position;
            directionToTarget.y = 0;
            directionToTarget = directionToTarget.normalized;
            
            jumpTargetPosition = target.position;
            
            isPrepareJump = false;
            isJumping = true;
            
            if (animator != null && animator.enabled)
            {
                animator.SetBool(ANIM_PREPARE_JUMP, false);
                animator.SetBool(ANIM_IS_JUMPING, true);
                Debug.Log($"ANIMATION: PrepareJump défini à FALSE, IsJumping défini à TRUE");
            }
            
            if (rb != null)
            {
                float horizontalDistance = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z), 
                    new Vector3(target.position.x, 0, target.position.z)
                );
                float verticalForce = jumpForce * (0.5f + horizontalDistance * 0.1f);
                
                rb.linearVelocity = Vector3.zero;
                rb.AddForce(directionToTarget * jumpForce + Vector3.up * verticalForce, ForceMode.Impulse);
                
                Debug.Log($"{gameObject.name} effectue un saut d'attaque vers {target.name}");
            }

            float groundCheckTime = 0f;
            float maxGroundCheckTime = 3f;
            
            yield return new WaitForSeconds(0.1f);
            
            while (!IsGrounded() && groundCheckTime < maxGroundCheckTime && totalCoroutineTime < maxCoroutineTime)
            {
                groundCheckTime += Time.deltaTime;
                totalCoroutineTime += Time.deltaTime;
                yield return null;
            }
            
            Debug.Log("Slime a atterri ou le temps maximum est écoulé");
            
            if (animator != null && animator.enabled)
            {
                animator.SetBool(ANIM_IS_JUMPING, false);
                animator.SetTrigger(ANIM_LAND);
                Debug.Log($"ANIMATION: IsJumping défini à FALSE, Land trigger activé");
            }
            
            CheckForJumpAttackDamage();
            
            yield return new WaitForSeconds(0.3f);
            
            isJumping = false;
            
            // Rétablir l'état idle si applicable
            bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.1f;
            bool isAggressive = GetCurrentState() == AIState.Aggressive;
            bool shouldBeIdle = !isMoving && !isAggressive;
            
            if (animator != null && animator.enabled && shouldBeIdle)
            {
                animator.SetBool(ANIM_IS_IDLE, true);
                Debug.Log($"ANIMATION: IsIdle défini à TRUE après l'atterrissage");
            }
            
            Debug.Log($"{gameObject.name}: Animation de saut terminée correctement");
        }
        else
        {
            CancelJumpAnimation();
        }
    }
    
    private void CancelJumpAnimation()
    {
        isPrepareJump = false;
        isJumping = false;
        
        if (animator != null && animator.enabled)
        {
            animator.SetBool(ANIM_PREPARE_JUMP, false);
            animator.SetBool(ANIM_IS_JUMPING, false);
            
            // Rétablir l'état idle si applicable
            bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.1f;
            bool isAggressive = GetCurrentState() == AIState.Aggressive;
            bool shouldBeIdle = !isMoving && !isAggressive;
            
            animator.SetBool(ANIM_IS_IDLE, shouldBeIdle);
            Debug.Log("ANIMATION: Toutes les animations de saut annulées, IsIdle mis à jour");
        }
    }
    
    protected override bool IsGrounded()    
    {
        float rayLength = 0.5f;
        Debug.DrawRay(transform.position, Vector3.down * rayLength, Color.red, 0.1f);
        bool grounded = Physics.Raycast(transform.position, Vector3.down, rayLength, obstacleLayers);
        
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"IsGrounded check: {grounded}");
        }
        return grounded;
    }
    
    private void CheckForJumpAttackDamage()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, jumpAttackRadius, detectionLayers);
        
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject) continue;
            
            if (hitCollider.CompareTag("Player"))
            {
                float jumpDamage = attackDamage * jumpDamageMultiplier;
                
                PlayerStats playerStats = hitCollider.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    playerStats.TakeDamage(jumpDamage);
                    Debug.Log($"{gameObject.name} a écrasé {hitCollider.name} avec son saut et infligé {jumpDamage} dégâts");
                    ApplyKnockback(hitCollider.transform, knockbackForce * 1.5f);
                }
                else
                {
                    HealthSystem targetHealth = hitCollider.GetComponent<HealthSystem>();
                    if (targetHealth != null)
                    {
                        targetHealth.TakeDamage(jumpDamage, gameObject);
                        ApplyKnockback(hitCollider.transform, knockbackForce * 1.5f);
                    }
                }
            }
        }
    }
    
    protected override void Attack()
    {
        if (Time.time - lastAttackTime < attackCooldown || isAttacking) return;

        lastAttackTime = Time.time;
        isAttacking = true;

        StartCoroutine(SlimeAttackCoroutine());
    }
    
    protected virtual IEnumerator SlimeAttackCoroutine()
    {
        if (animator != null && animator.enabled)
        {
            animator.SetBool(ANIM_IS_IDLE, false); // Désactiver l'état idle pendant l'attaque
            animator.SetTrigger(ANIM_ATTACK);
            Debug.Log($"ANIMATION: Attack trigger activé, IsIdle défini à FALSE");
        }
        
        Debug.Log($"{gameObject.name} commence une attaque de slime !");

        yield return new WaitForSeconds(attackAnimationDuration);

        if (target != null && Vector3.Distance(transform.position, target.position) <= attackRange)
        {
            ApplyDamageToTarget(attackDamage);
            ApplyKnockback(target);

            if (useComboAttacks)
            {
                if (Time.time - lastComboTime < comboTimeWindow)
                {
                    currentComboCount++;
                    
                    if (currentComboCount < maxComboHits)
                    {
                        lastAttackTime -= attackCooldown * 0.5f;
                    }
                    else
                    {
                        currentComboCount = 0;
                    }
                }
                else
                {
                    currentComboCount = 1;
                }
                
                lastComboTime = Time.time;
            }
        }

        isAttacking = false;
        
        // Rétablir l'état idle si applicable
        bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.1f;
        bool isAggressive = GetCurrentState() == AIState.Aggressive;
        bool shouldBeIdle = !isMoving && !isAggressive && !isJumping && !isPrepareJump;
        
        if (animator != null && animator.enabled && shouldBeIdle)
        {
            animator.SetBool(ANIM_IS_IDLE, true);
            Debug.Log($"ANIMATION: IsIdle défini à TRUE après l'attaque");
        }
    }
    
    private void ApplyKnockback(Transform targetTransform, float force)
    {
        if (targetTransform == null) return;
        
        Vector3 knockbackDirection = (targetTransform.position - transform.position).normalized;
        knockbackDirection.y = 0.5f;
        
        Rigidbody targetRb = targetTransform.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.AddForce(knockbackDirection * force, ForceMode.Impulse);
            Debug.Log($"Knockback puissant appliqué à {targetTransform.name} avec force {force}");
        }
    }
    
    public override void OnDamageReceived(float damage, GameObject source)
    {
        base.OnDamageReceived(damage, source);
        
        if (animator != null && animator.enabled)
        {
            animator.SetBool(ANIM_IS_IDLE, false); // Désactiver l'état idle pendant la prise de dégâts
            animator.SetTrigger(ANIM_TAKE_DAMAGE);
            Debug.Log($"ANIMATION: TakeDamage trigger activé, IsIdle défini à FALSE");
            
            // Réactiver IsIdle après un court délai si nécessaire
            StartCoroutine(ResetIdleAfterDelay(0.3f));
        }
    }
    
    private IEnumerator ResetIdleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.1f;
        bool isAggressive = GetCurrentState() == AIState.Aggressive;
        bool shouldBeIdle = !isMoving && !isAggressive && !isJumping && !isPrepareJump && !isAttacking;
        
        if (animator != null && animator.enabled && shouldBeIdle)
        {
            animator.SetBool(ANIM_IS_IDLE, true);
            Debug.Log($"ANIMATION: IsIdle réinitialisé à TRUE après un délai");
        }
    }
    
    public override void OnDeath()
    {
        CancelJumpAnimation();
        
        if (animator != null && animator.enabled)
        {
            animator.SetBool(ANIM_IS_IDLE, false); // Désactiver l'état idle pendant la mort
            animator.SetTrigger(ANIM_DIE);
            Debug.Log($"ANIMATION: Die trigger activé, IsIdle défini à FALSE");
        }
        
        if (canSplitOnDeath && smallerSlimePrefab != null)
        {
            SplitIntoSmallerSlimes();
        }
        
        base.OnDeath();
    }
    
    private void SplitIntoSmallerSlimes()
    {
        for (int i = 0; i < splitCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-0.5f, 0.5f),
                0f,
                Random.Range(-0.5f, 0.5f)
            );
            
            GameObject smallSlime = Instantiate(smallerSlimePrefab, transform.position + offset, Quaternion.identity);
            
            SlimeAI smallSlimeAI = smallSlime.GetComponent<SlimeAI>();
            if (smallSlimeAI != null)
            {
                smallSlimeAI.SetActive(true);
                
                HealthSystem smallHealthSystem = smallSlime.GetComponent<HealthSystem>();
                if (smallHealthSystem != null)
                {
                    smallHealthSystem.SetMaxHealth(healthSystem.MaxHealth * 0.4f, true);
                }
            }
            
            Rigidbody smallRb = smallSlime.GetComponent<Rigidbody>();
            if (smallRb != null)
            {
                Vector3 bounceDirection = new Vector3(offset.x, 1f, offset.z).normalized;
                smallRb.AddForce(bounceDirection * 3f, ForceMode.Impulse);
            }
            
            AIManager aiManager = FindFirstObjectByType<AIManager>();
            if (aiManager != null && smallSlimeAI != null)
            {
                aiManager.RegisterAI(smallSlimeAI);
            }
        }
        
        Debug.Log($"{gameObject.name} s'est divisé en {splitCount} petits slimes!");
    }
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, jumpAttackRadius);
    }
    
    public override void SetActive(bool active)
    {
        base.SetActive(active);
        
        if (active && animator == null)
        {
            Debug.LogError($"L'Animator n'est pas assigné sur {gameObject.name}. Assurez-vous d'assigner l'Animator de SlimeV1 dans l'inspecteur.");
        }
        
        // Initialiser les paramètres d'animation lors de l'activation
        if (active)
        {
            InitializeAnimationParameters();
        }
    }
}