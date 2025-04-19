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

    // Variables d'état pour le saut
    private float lastJumpTime = -10f;  // -10 pour permettre un saut dès le début
    private bool isPrepareJump = false;
    private bool isJumping = false;
    private Vector3 jumpTargetPosition;

    // Variables pour l'animation de squish/stretch
    private Vector3 originalScale;
    private float squishFactor = 1f;

    protected override void Awake()
    {
        base.Awake();
        
        // Configuration spécifique au Slime
        moveSpeed = 2.5f;                // Vitesse réduite
        attackRange = 1.5f;              // Attaque à courte portée
        attackDamage = 10f;              // Dégâts modérés
        attackCooldown = 2f;             // Attaque lente
        
        // Sauvegarder l'échelle originale pour les animations
        originalScale = transform.localScale;
        
        // S'abonner à l'événement de mort
        if (healthSystem != null)
        {
            healthSystem.OnDeath.AddListener(OnDeath);
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
        
        // Animation de pulsation/respiration quand le Slime est au sol
        if (isActive && !isJumping && !isPrepareJump)
        {
            // Effet de pulsation sinusoïdale
            float breatheFactor = Mathf.Sin(Time.time * 2f) * 0.05f + 1f;
            transform.localScale = new Vector3(
                originalScale.x * (1f + breatheFactor * 0.1f),
                originalScale.y * (1f - breatheFactor * 0.1f), 
                originalScale.z * (1f + breatheFactor * 0.1f)
            );
        }
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
    /// Coroutine gérant l'animation et la physique du saut d'attaque
    /// </summary>
    private IEnumerator JumpAttackCoroutine()
    {
        isPrepareJump = true;
        
        // Animation de préparation du saut (écrasement)
        float prepTime = 0f;
        Vector3 squishScale = new Vector3(originalScale.x * 1.3f, originalScale.y * 0.7f, originalScale.z * 1.3f);
        
        // Interpoler vers la forme écrasée
        while (prepTime < jumpPrepareTime)
        {
            float t = prepTime / jumpPrepareTime;
            transform.localScale = Vector3.Lerp(originalScale, squishScale, t);
            prepTime += Time.deltaTime;
            yield return null;
        }
        
        // Calculer la direction vers la cible
        if (target != null)
        {
            Vector3 directionToTarget = target.position - transform.position;
            directionToTarget.y = 0; // Garder le mouvement horizontal
            directionToTarget = directionToTarget.normalized;
            
            // Stocker la position cible du saut
            jumpTargetPosition = target.position;
            
            isJumping = true;
            isPrepareJump = false;
            
            // Animation d'étirement lors du saut
            transform.localScale = new Vector3(originalScale.x * 0.8f, originalScale.y * 1.3f, originalScale.z * 0.8f);
            
            // Appliquer la force de saut
            if (rb != null)
            {
                // Calculer la hauteur du saut en fonction de la distance
                float horizontalDistance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                                          new Vector3(target.position.x, 0, target.position.z));
                float verticalForce = jumpForce * (0.5f + horizontalDistance * 0.1f);
                
                // Réinitialiser la vélocité d'abord
                rb.linearVelocity = Vector3.zero;
                rb.AddForce(directionToTarget * jumpForce + Vector3.up * verticalForce, ForceMode.Impulse);
                lastJumpTime = Time.time;
                
                Debug.Log($"{gameObject.name} effectue un saut d'attaque vers {target.name}");
            }
            
            // Attendre un peu pour permettre au Slime de quitter le sol
            yield return new WaitForSeconds(0.1f);
            
            // Attendre que le Slime touche à nouveau le sol
            yield return new WaitUntil(() => IsGrounded());
            
            // Effet d'écrasement à l'atterrissage
            transform.localScale = new Vector3(originalScale.x * 1.5f, originalScale.y * 0.5f, originalScale.z * 1.5f);
            
            // Vérifier si on est proche de la cible pour infliger des dégâts
            CheckForJumpAttackDamage();
            
            // Revenir progressivement à la forme normale
            float recoveryTime = 0f;
            while (recoveryTime < 0.3f) // Temps de récupération après l'atterrissage
            {
                float t = recoveryTime / 0.3f;
                transform.localScale = Vector3.Lerp(
                    new Vector3(originalScale.x * 1.5f, originalScale.y * 0.5f, originalScale.z * 1.5f),
                    originalScale,
                    t
                );
                recoveryTime += Time.deltaTime;
                yield return null;
            }
        }
        
        // Réinitialiser les états
        isJumping = false;
        transform.localScale = originalScale;
    }
    
    /// <summary>
    /// Vérifie si le Slime est au sol
    /// </summary>
    private bool IsGrounded()
    {
        // On vérifie si le Slime touche le sol en lançant un petit rayon vers le bas
        float rayLength = 0.3f;
        return Physics.Raycast(transform.position, Vector3.down, rayLength, obstacleLayers);
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
                
                // Créer un objet temporaire comme source de dégâts
                GameObject damageSource = gameObject;
                
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
    /// Réaction aux dégâts reçus
    /// </summary>
    public override void OnDamageReceived(float damage, GameObject source)
    {
        base.OnDamageReceived(damage, source);
        
        // Animation de dommage (écrasement rapide)
        StartCoroutine(DamageSquishAnimation());
    }
    
    /// <summary>
    /// Animation d'écrasement quand le Slime reçoit des dégâts
    /// </summary>
    private IEnumerator DamageSquishAnimation()
    {
        // Ne pas perturber l'animation de saut en cours
        if (isJumping || isPrepareJump) yield break;
        
        // Sauvegarder l'échelle actuelle
        Vector3 currentScale = transform.localScale;
        
        // Échelle aplatie
        Vector3 damageScale = new Vector3(currentScale.x * 1.2f, currentScale.y * 0.7f, currentScale.z * 1.2f);
        
        // Rapide écrasement puis retour à la normale
        float animTime = 0f;
        float totalAnimDuration = 0.2f;
        
        while (animTime < totalAnimDuration)
        {
            float t = animTime / totalAnimDuration;
            
            // Aller vers l'écrasement puis revenir à la normale
            if (t < 0.5f)
            {
                // Phase d'écrasement
                transform.localScale = Vector3.Lerp(currentScale, damageScale, t * 2f);
            }
            else
            {
                // Phase de retour
                transform.localScale = Vector3.Lerp(damageScale, currentScale, (t - 0.5f) * 2f);
            }
            
            animTime += Time.deltaTime;
            yield return null;
        }
        
        // S'assurer que l'échelle est revenue à la normale
        transform.localScale = currentScale;
    }
    
    /// <summary>
    /// Comportement à la mort du Slime
    /// </summary>
    public override void OnDeath()
    {
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