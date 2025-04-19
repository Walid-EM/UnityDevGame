using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe gérant les projectiles tirés par les IA à distance
/// </summary>
public class RangedProjectile : MonoBehaviour
{
    [SerializeField] private float damage = 10f;               // Dégâts infligés par le projectile
    [SerializeField] private float speed = 15f;                // Vitesse du projectile
    [SerializeField] private float lifetime = 5f;              // Durée de vie en secondes
    [SerializeField] private float impactRadius = 0f;          // Rayon de l'explosion (0 = pas d'explosion)
    [SerializeField] private GameObject impactEffect;          // Effet visuel d'impact
    [SerializeField] private LayerMask targetLayers;           // Couches affectées par le projectile
    
    // Référence à celui qui a tiré le projectile
    private GameObject owner;
    
    // Composants
    private Rigidbody rb;
    private TrailRenderer trailRenderer;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        trailRenderer = GetComponent<TrailRenderer>();
    }
    
    private void Start()
    {
        // Destruction automatique après la durée de vie
        Destroy(gameObject, lifetime);
    }
    
    /// <summary>
    /// Initialise le projectile avec ses paramètres
    /// </summary>
    public void Initialize(float newDamage, GameObject newOwner, float newSpeed, float newLifetime)
    {
        damage = newDamage;
        owner = newOwner;
        speed = newSpeed;
        lifetime = newLifetime;
        
        // Appliquer la vitesse initiale
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Ignorer les collisions avec le propriétaire
        if (owner != null && collision.gameObject == owner)
            return;
            
        // Vérifier si l'objet touché est dans les couches cibles
        if (((1 << collision.gameObject.layer) & targetLayers) == 0)
        {
            // Pas une cible valide, simplement détruire le projectile
            DestroyProjectile();
            return;
        }
        
        // Traiter l'impact
        HandleImpact(collision.gameObject, collision.contacts[0].point);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Ignorer les collisions avec le propriétaire
        if (owner != null && other.gameObject == owner)
            return;
            
        // Vérifier si l'objet touché est dans les couches cibles
        if (((1 << other.gameObject.layer) & targetLayers) == 0)
        {
            // Pas une cible valide, simplement détruire le projectile
            DestroyProjectile();
            return;
        }
        
        // Traiter l'impact
        HandleImpact(other.gameObject, other.ClosestPoint(transform.position));
    }
    
    /// <summary>
    /// Gère l'impact du projectile
    /// </summary>
    private void HandleImpact(GameObject hitObject, Vector3 hitPoint)
    {
        // Gérer les dégâts directs ou les dégâts de zone
        if (impactRadius <= 0f)
        {
            // Dégâts directs
            ApplyDamage(hitObject, damage);
        }
        else
        {
            // Dégâts de zone
            Collider[] hitColliders = Physics.OverlapSphere(hitPoint, impactRadius, targetLayers);
            foreach (var col in hitColliders)
            {
                // Calculer les dégâts en fonction de la distance
                float distance = Vector3.Distance(hitPoint, col.transform.position);
                float damageMultiplier = 1f - (distance / impactRadius);
                float actualDamage = damage * Mathf.Clamp01(damageMultiplier);
                
                ApplyDamage(col.gameObject, actualDamage);
            }
        }
        
        // Créer un effet d'impact si disponible
        if (impactEffect != null)
        {
            Instantiate(impactEffect, hitPoint, Quaternion.identity);
        }
        
        // Détruire le projectile
        DestroyProjectile();
    }
    
    /// <summary>
    /// Applique des dégâts à une cible
    /// </summary>
    private void ApplyDamage(GameObject target, float damageAmount)
    {
        // Vérifier si la cible a un PlayerStats (joueur)
        PlayerStats playerStats = target.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.TakeDamage(damageAmount);
            return;
        }
        
        // Sinon, vérifier un HealthSystem générique
        HealthSystem healthSystem = target.GetComponent<HealthSystem>();
        if (healthSystem != null)
        {
            healthSystem.TakeDamage(damageAmount, owner);
        }
    }
    
    /// <summary>
    /// Détruit proprement le projectile
    /// </summary>
    private void DestroyProjectile()
    {
        // Détacher le trail renderer pour qu'il disparaisse progressivement
        if (trailRenderer != null)
        {
            trailRenderer.transform.parent = null;
            trailRenderer.autodestruct = true;
        }
        
        // Détruire le projectile
        Destroy(gameObject);
    }
}