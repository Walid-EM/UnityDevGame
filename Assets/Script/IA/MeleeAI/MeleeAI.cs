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

    // Variables d'état pour l'attaque
    protected bool isAttacking = false;
    protected int currentComboCount = 0;
    protected float lastComboTime = 0f;

    protected override void Awake()
    {
        base.Awake();
        aiType = AIType.Melee;
        baseDamage = attackDamage; // Utiliser les dégâts spécifiques du melee
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