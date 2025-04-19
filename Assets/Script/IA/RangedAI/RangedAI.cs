using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe pour les IA attaquant à distance
/// </summary>
public class RangedAI : BaseAI
{
    [Header("Paramètres spécifiques Ranged")]
    [SerializeField] private GameObject projectilePrefab;           // Prefab du projectile
    [SerializeField] private Transform firePoint;                   // Point de départ du projectile
    [SerializeField] private float projectileSpeed = 15f;           // Vitesse du projectile
    [SerializeField] private float projectileDamage = 15f;          // Dégâts du projectile
    [SerializeField] private float projectileLifetime = 5f;         // Durée de vie du projectile
    [SerializeField] private float optimalDistance = 8f;            // Distance optimale de combat
    [SerializeField] private float retreatDistance = 5f;            // Distance en dessous de laquelle l'IA recule
    [SerializeField] private float fireRate = 1f;                   // Tirs par seconde
    [SerializeField] private bool canChargeShot = false;            // L'IA peut-elle charger un tir puissant
    [SerializeField] private float chargedShotDamageMultiplier = 2f; // Multiplicateur de dégâts pour le tir chargé
    [SerializeField] private float chargeDuration = 2f;             // Durée de charge pour un tir puissant

    // Variables d'état
    private bool isCharging = false;
    private float chargeStartTime = 0f;
    private float lastFireTime = 0f;

    protected override void Awake()
    {
        base.Awake();
        aiType = AIType.Ranged;
        
        // Créer un point de tir s'il n'existe pas
        if (firePoint == null)
        {
            GameObject newFirePoint = new GameObject("FirePoint");
            newFirePoint.transform.parent = transform;
            newFirePoint.transform.localPosition = new Vector3(0, 0.5f, 0.5f); // Position par défaut
            firePoint = newFirePoint.transform;
        }
    }

    protected override void UpdateAggressiveBehavior()
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

        // Gestion de la distance optimale
        if (distanceToTarget < retreatDistance)
        {
            // Trop proche, on recule
            Vector3 directionFromTarget = transform.position - target.position;
            Vector3 retreatPosition = transform.position + directionFromTarget.normalized * 2f;
            MoveToPosition(retreatPosition);
        }
        else if (distanceToTarget > optimalDistance * 1.3f)
        {
            // Trop loin, on s'approche
            MoveToPosition(target.position);
        }
        else
        {
            // Distance idéale, on s'arrête et on attaque
            FaceTarget();
            
            // Si on est en train de charger un tir
            if (isCharging)
            {
                // Vérifier si le temps de charge est suffisant
                if (Time.time - chargeStartTime >= chargeDuration)
                {
                    FireChargedProjectile();
                }
            }
            else if (Time.time - lastFireTime >= 1f / fireRate)
            {
                // Tir normal
                FireProjectile(projectileDamage);
            }
        }
    }

    /// <summary>
    /// Vérifie si l'IA peut charger un tir puissant
    /// </summary>
    protected override bool CanPerformSpecialAction()
    {
        // Si l'IA ne peut pas charger de tir ou est déjà en train de charger
        if (!canChargeShot || isCharging) return false;
        
        // Vérifier si le cooldown est passé
        if (Time.time - lastFireTime < 3f) return false;
        
        // 20% de chance de déclencher un tir chargé
        return Random.value < 0.2f && HasLineOfSight(target);
    }

    /// <summary>
    /// Charge un tir puissant
    /// </summary>
    protected override void PerformSpecialAction()
    {
        if (!canChargeShot) return;
        
        isCharging = true;
        chargeStartTime = Time.time;
        
        Debug.Log($"{gameObject.name} commence à charger un tir puissant!");
        
        // Pourrait déclencher des effets visuels de charge ici
    }

    /// <summary>
    /// Tire un projectile normal
    /// </summary>
    protected virtual void FireProjectile(float damage)
    {
        if (projectilePrefab == null || firePoint == null) return;
        
        lastFireTime = Time.time;
        
        // Créer le projectile
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        RangedProjectile projectileComponent = projectile.GetComponent<RangedProjectile>();
        
        // Configurer le projectile si le composant existe
        if (projectileComponent != null)
        {
            projectileComponent.Initialize(damage, gameObject, projectileSpeed, projectileLifetime);
        }
        else
        {
            // Configuration de base si le composant n'existe pas
            Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
            if (projectileRb != null)
            {
                // Ajouter une force pour propulser le projectile
                projectileRb.linearVelocity = firePoint.forward * projectileSpeed;
            }
            
            // Destruction automatique après la durée de vie
            Destroy(projectile, projectileLifetime);
        }
        
        Debug.Log($"{gameObject.name} tire un projectile!");
    }

    /// <summary>
    /// Tire un projectile chargé plus puissant
    /// </summary>
    protected virtual void FireChargedProjectile()
    {
        // Dégâts augmentés pour le tir chargé
        float chargedDamage = projectileDamage * chargedShotDamageMultiplier;
        
        // Tirer le projectile
        FireProjectile(chargedDamage);
        
        Debug.Log($"{gameObject.name} tire un projectile chargé avec {chargedDamage} dégâts!");
        
        // Réinitialiser l'état de charge
        isCharging = false;
    }

    /// <summary>
    /// Réaction aux dégâts reçus
    /// </summary>
    public override void OnDamageReceived(float damage, GameObject source)
    {
        base.OnDamageReceived(damage, source);
        
        // Si on était en train de charger, interrompre la charge
        if (isCharging)
        {
            isCharging = false;
            Debug.Log($"{gameObject.name} a interrompu sa charge suite à des dégâts!");
        }
    }
}