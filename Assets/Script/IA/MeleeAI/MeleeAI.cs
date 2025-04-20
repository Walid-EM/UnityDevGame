using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe pour les IA de corps-à-corps
/// </summary>
public class MeleeAI : BaseAI
{
    [Header("Paramètres spécifiques Melee")]
    [SerializeField] protected float attackDamage = 20f;
    [SerializeField] protected float attackAnimationDuration = 0.5f;
    [SerializeField] protected float knockbackForce = 2f; // Force de recul appliquée à la cible
    [SerializeField] protected bool useComboAttacks = false; // L'IA peut-elle enchaîner des attaques
    [SerializeField] protected int maxComboHits = 3; // Nombre maximum de coups dans un combo
    [SerializeField] protected float comboTimeWindow = 1.5f; // Fenêtre de temps pour enchaîner les combos
    [SerializeField] protected float meleePositioningRadius = 1.2f; // Rayon pour le positionnement tactique en mêlée

    // Variables d'état pour l'attaque
    protected bool isAttacking = false;
    protected int currentComboCount = 0;
    protected float lastComboTime = 0f;
    protected Vector3 tacticalOffset = Vector3.zero; // Offset tactique pour éviter le stacking

    protected override void Awake()
    {
        base.Awake();
        aiType = AIType.Melee;
        baseDamage = attackDamage; // Utiliser les dégâts spécifiques du melee
        
        // Initialiser un offset tactique unique pour cette IA
        float uniqueAngle = (GetInstanceID() % 360) * Mathf.Deg2Rad;
        tacticalOffset = new Vector3(
            Mathf.Cos(uniqueAngle) * meleePositioningRadius,
            0,
            Mathf.Sin(uniqueAngle) * meleePositioningRadius
        );
    }

    protected override void UpdateAggressiveBehavior()
    {
        // Ne pas attaquer pendant l'animation d'attaque
        if (isAttacking)
        {
            // Continuer à faire face à la cible pendant l'attaque
            if (target != null)
            {
                FaceTarget();
            }
            return;
        }
        
        // Utiliser le comportement de base
        base.UpdateAggressiveBehavior();
    }

    /// <summary>
    /// Calcule une position tactique pour l'IA de mêlée
    /// </summary>
    protected override Vector3 CalculateTacticalPosition()
    {
        if (target == null) return transform.position;
        
        // Position de base (position cible)
        Vector3 basePosition = target.position;
        
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
            if (otherAI != null && otherAI.GetAIType() == AIType.Melee && otherAI.GetCurrentState() == AIState.Aggressive)
            {
                meleesAroundTarget++;
            }
        }
        
        // Ajuster la position en fonction du nombre d'IA de mêlée déjà présentes
        if (meleesAroundTarget > 0)
        {
            // Plus il y a d'IA, plus on s'éloigne de la position moyenne pour éviter le stacking
            float spreadFactor = 1.0f + (meleesAroundTarget * 0.2f);
            
            // Utiliser l'offset tactique unique pour cette IA
            Vector3 offsetPosition = basePosition + tacticalOffset * spreadFactor;
            
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
                    Mathf.Cos(angle) * meleePositioningRadius * spreadFactor,
                    0,
                    Mathf.Sin(angle) * meleePositioningRadius * spreadFactor
                );
                
                Vector3 testPosition = basePosition + testOffset;
                if (!Physics.CheckSphere(testPosition, 0.5f, obstacleLayers))
                {
                    return testPosition;
                }
            }
        }
        
        // Si aucune position spéciale n'est requise ou accessible, revenir à la cible
        return basePosition;
    }

    protected override void Attack()
    {
        if (Time.time - lastAttackTime < attackCooldown || isAttacking) return;

        lastAttackTime = Time.time;
        isAttacking = true;

        // Déclencher l'animation d'attaque
        StartCoroutine(AttackCoroutine());
    }

    /// <summary>
    /// Coroutine gérant l'animation et les dégâts d'une attaque
    /// </summary>
    protected virtual IEnumerator AttackCoroutine()
    {
        // Simuler une animation d'attaque
        Debug.Log($"{gameObject.name} commence une attaque de mêlée !");

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
    /// Applique un effet de recul à la cible
    /// </summary>
    protected virtual void ApplyKnockback(Transform targetTransform)
    {
        if (targetTransform == null || knockbackForce <= 0f) return;

        // Direction du knockback (depuis l'IA vers la cible)
        Vector3 knockbackDirection = (targetTransform.position - transform.position).normalized;
        
        // S'assurer que le knockback est principalement horizontal
        knockbackDirection.y = 0.1f;
        knockbackDirection = knockbackDirection.normalized;

        // Appliquer la force via Rigidbody si disponible
        Rigidbody targetRb = targetTransform.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.AddForce(knockbackDirection * knockbackForce, ForceMode.Impulse);
            Debug.Log($"Knockback appliqué à {targetTransform.name} avec force {knockbackForce}");
        }
    }
}