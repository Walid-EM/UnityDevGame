using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Système de santé générique pour les ennemis et le joueur.
/// </summary>
public class HealthSystem : MonoBehaviour
{
    [System.Serializable] public class HealthEvent : UnityEvent<float, float> { }
    [System.Serializable] public class DamageEvent : UnityEvent<float, GameObject> { }
    
    [Header("Santé")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool isInvulnerable = false;
    
    [Header("Effets")]
    [SerializeField] private float flashSpeed = 5f;
    [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0.3f);
    
    [Header("Audio")]
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip healSound;
    
    [Header("Événements")]
    public HealthEvent OnHealthChanged = new HealthEvent();
    public DamageEvent OnDamaged = new DamageEvent();
    public UnityEvent OnDeath = new UnityEvent();
    
    // Référence à la source audio
    private AudioSource audioSource;
    private bool isFlashing = false;
    
    // Propriétés
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0;
    public bool IsInvulnerable => isInvulnerable; // Accesseur public pour déboguer
    
    private void Awake()
    {
        currentHealth = maxHealth;
        
        // Récupérer ou créer une source audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (hurtSound != null || deathSound != null || healSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Pour diagnostic, désactivons l'invulnérabilité au démarrage
        isInvulnerable = false;
        Debug.Log($"[DIAGNOSTIC] HealthSystem.Awake() - isInvulnerable = {isInvulnerable}");
    }
    
    /// <summary>
    /// Inflige des dégâts à l'objet
    /// </summary>
    /// <param name="damage">Montant de dégâts</param>
    /// <param name="damageSource">Source des dégâts (optionnel)</param>
    /// <returns>Dégâts réellement infligés</returns>
    
    public float TakeDamage(float damage, GameObject damageSource = null)
    {
        Debug.Log($"[DIAGNOSTIC] HealthSystem.TakeDamage({damage}) appelé pour {gameObject.name}");
        Debug.Log($"[DIAGNOSTIC] État actuel: IsDead={IsDead}, isInvulnerable={isInvulnerable}, CurrentHealth={currentHealth}");

        // Vérifications préliminaires
        if (damage <= 0)
        {
            Debug.Log($"[DIAGNOSTIC] Dégâts ignorés car damage={damage} <= 0");
            return 0;
        }
        
        if (IsDead)
        {
            Debug.Log($"[DIAGNOSTIC] Dégâts ignorés car IsDead=true");
            return 0;
        }
        
        if (isInvulnerable)
        {
            Debug.Log($"[DIAGNOSTIC] Dégâts ignorés car isInvulnerable=true");
            // DIAGNOSTIC: Temporairement ignorer l'invulnérabilité
            Debug.Log($"[DIAGNOSTIC] !!! IMPORTANT !!! IGNORONS L'INVULNÉRABILITÉ POUR LE DIAGNOSTIC");
            // Mais gardez ce commentaire et ne supprimez pas le code - si vous vouliez le réactiver plus tard
            // return 0;
        }
            
        // Calculer les dégâts réels
        float oldHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        float actualDamage = oldHealth - currentHealth;

        Debug.Log($"[DIAGNOSTIC] Dégâts réellement appliqués: {actualDamage}, CurrentHealth après: {currentHealth}");
        
        // Notifier du changement de santé
        OnHealthChanged.Invoke(currentHealth, maxHealth);
        Debug.Log($"[DIAGNOSTIC] OnHealthChanged invoqué avec {currentHealth}/{maxHealth}. Nombre d'abonnés: {OnHealthChanged.GetPersistentEventCount()}");
        
        // Notifier des dégâts subis
        if (actualDamage > 0)
        {
            OnDamaged.Invoke(actualDamage, damageSource);
            Debug.Log($"[DIAGNOSTIC] OnDamaged invoqué avec {actualDamage}");
            
            // Jouer le son de dégâts
            if (audioSource != null && hurtSound != null && !IsDead)
            {
                audioSource.PlayOneShot(hurtSound);
                Debug.Log("[DIAGNOSTIC] Son de dégâts joué");
            }
            
            // Afficher l'effet de flash (si c'est le joueur et qu'il a un PlayerStatsUI)
            if (GetComponent<PlayerStats>() != null)
            {
                // Essayer de trouver un effet de flash dans l'UI
                PlayerStatsUI ui = FindObjectOfType<PlayerStatsUI>();
                if (ui != null && ui.damageFlashImage != null)
                {
                    ui.TriggerDamageFlash();
                    Debug.Log("[DIAGNOSTIC] Effet de flash UI déclenché");
                }
                else
                {
                    Debug.Log("[DIAGNOSTIC] UI ou damageFlashImage non trouvé pour l'effet de flash");
                }
            }
            
            Debug.Log($"[DIAGNOSTIC] {gameObject.name} a subi {actualDamage} dégâts. Santé: {currentHealth}/{maxHealth}");
        }
        else
        {
            Debug.Log($"[DIAGNOSTIC] Aucun dégât réel n'a été appliqué");
        }
        
        // Vérifier si l'objet est mort
        if (currentHealth <= 0 && oldHealth > 0)
        {
            Die();
        }
        
        return actualDamage;
    }
    
    /// <summary>
    /// Restaure la santé de l'objet
    /// </summary>
    /// <param name="amount">Montant de santé à restaurer</param>
    /// <returns>Santé réellement restaurée</returns>
    public float Heal(float amount)
    {
        if (IsDead)
        {
            Debug.Log("[DIAGNOSTIC] Heal ignoré car l'objet est mort");
            return 0;
        }
            
        if (amount <= 0)
        {
            Debug.Log($"[DIAGNOSTIC] Heal ignoré car amount={amount} <= 0");
            return 0;
        }
            
        float oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float actualHeal = currentHealth - oldHealth;
        
        // Notifier du changement
        if (actualHeal > 0)
        {
            OnHealthChanged.Invoke(currentHealth, maxHealth);
            Debug.Log($"[DIAGNOSTIC] OnHealthChanged invoqué après guérison avec {currentHealth}/{maxHealth}");
            
            // Jouer le son de guérison
            if (audioSource != null && healSound != null)
            {
                audioSource.PlayOneShot(healSound);
                Debug.Log("[DIAGNOSTIC] Son de guérison joué");
            }
            
            Debug.Log($"[DIAGNOSTIC] {gameObject.name} a récupéré {actualHeal} points de vie. Santé: {currentHealth}/{maxHealth}");
        }
        else
        {
            Debug.Log("[DIAGNOSTIC] Aucune guérison réelle appliquée (déjà au max?)");
        }
        
        return actualHeal;
    }
    
    /// <summary>
    /// Gère la mort de l'objet
    /// </summary>
    protected virtual void Die()
    {
        Debug.Log($"[DIAGNOSTIC] {gameObject.name} est mort!");
        
        // Jouer le son de mort
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
            Debug.Log("[DIAGNOSTIC] Son de mort joué");
        }
        
        // Déclencher l'événement de mort
        OnDeath.Invoke();
        Debug.Log("[DIAGNOSTIC] OnDeath invoqué");
        
        // Si c'est un ennemi, peut-être le détruire ou jouer une animation
        if (GetComponent<PlayerStats>() == null)
        {
            Debug.Log("[DIAGNOSTIC] Mort d'un ennemi");
            // Ici, vous pouvez implémenter le comportement de mort pour les ennemis
            // Exemple : Destroy(gameObject, 2f);
        }
        else
        {
            Debug.Log("[DIAGNOSTIC] Mort du joueur");
        }
    }
    
    /// <summary>
    /// Définit la vulnérabilité de l'objet
    /// </summary>
    public void SetInvulnerable(bool invulnerable)
    {
        bool oldValue = isInvulnerable;
        isInvulnerable = invulnerable;
        
        Debug.Log($"[DIAGNOSTIC] {gameObject.name} SetInvulnerable: {oldValue} -> {isInvulnerable}");
    }
    
    /// <summary>
    /// Réinitialise la santé au maximum
    /// </summary>
    public void ResetHealth()
    {
        Debug.Log($"[DIAGNOSTIC] ResetHealth appelé. Avant: {currentHealth}, Après: {maxHealth}");
        currentHealth = maxHealth;
        OnHealthChanged.Invoke(currentHealth, maxHealth);
    }
    
    /// <summary>
    /// Modifie la santé maximale et ajuste la santé actuelle
    /// </summary>
    /// <param name="newMaxHealth">Nouvelle valeur de santé maximale</param>
    /// <param name="healToMax">Si vrai, la santé actuelle sera mise au maximum</param>
    public void SetMaxHealth(float newMaxHealth, bool healToMax = false)
    {
        float oldMax = maxHealth;
        maxHealth = Mathf.Max(1, newMaxHealth);
        
        Debug.Log($"[DIAGNOSTIC] SetMaxHealth: {oldMax} -> {maxHealth}, healToMax={healToMax}");
        
        if (healToMax)
        {
            currentHealth = maxHealth;
            Debug.Log($"[DIAGNOSTIC] Santé restaurée au maximum: {currentHealth}");
        }
        else
        {
            // Garder le même pourcentage de santé
            float percentage = oldMax > 0 ? currentHealth / oldMax : 0;
            currentHealth = maxHealth * percentage;
            Debug.Log($"[DIAGNOSTIC] Santé ajustée proportionnellement: {currentHealth} ({percentage*100}%)");
        }
        
        OnHealthChanged.Invoke(currentHealth, maxHealth);
    }
}