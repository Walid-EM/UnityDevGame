using UnityEngine;
using System.Collections;
using UnityEngine.Events;

[RequireComponent(typeof(HealthSystem))]
public class PlayerStats : MonoBehaviour
{
    // Système d'événements pour les changements de statistiques
    [System.Serializable] public class StatEvent : UnityEvent<float, float> { }
    
    // Événements exposés pour permettre à l'UI ou à d'autres systèmes de s'abonner
    public StatEvent OnHealthChanged = new StatEvent();
    public StatEvent OnManaChanged = new StatEvent();
    public StatEvent OnHungerChanged = new StatEvent();
    public StatEvent OnStaminaChanged = new StatEvent();
    public UnityEvent OnPlayerDeath = new UnityEvent();
    
    [Header("Health Settings")]
    [SerializeField] private float healthRegenRate = 0f; // 0 = pas de régénération naturelle
    [SerializeField] private bool canRegenerateHealth = false;
    [SerializeField] private float healthRegenDelay = 5f; // Temps avant le début de la régénération
    
    [Header("Mana Settings")]
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float currentMana;
    [SerializeField] private float manaRegenRate = 2f; // Par seconde
    
    [Header("Hunger Settings")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float currentHunger;
    [SerializeField] private float hungerDecreaseRate = 0.5f; // Par seconde
    [SerializeField] private float hungerHealthThreshold = 20f; // En dessous de ce seuil, la faim affecte la santé
    [SerializeField] private float hungerHealthPenalty = 1f; // Dégâts de santé par seconde quand la faim est basse
    
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina;
    [SerializeField] private float staminaRegenRate = 5f; // Par seconde
    [SerializeField] private float staminaRegenDelay = 1f; // Délai avant la régénération
    
    [Header("Debug")]
    [SerializeField] private bool forceUpdateUI = false; // Pour forcer la mise à jour de l'UI
    
    // Variables privées pour la gestion des délais
    private float lastStaminaUseTime;
    private float lastHealthLossTime;
    private bool isStaminaRegenerating = true;
    private Coroutine hungerCoroutine;
    private HealthSystem healthSystem;
    
    // Propriétés publiques pour accéder aux valeurs
    public float MaxHealth => healthSystem.MaxHealth;
    public float CurrentHealth => healthSystem.CurrentHealth;
    public float MaxMana => maxMana;
    public float CurrentMana => currentMana;
    public float MaxHunger => maxHunger;
    public float CurrentHunger => currentHunger;
    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;
    
    // Propriétés pour vérifier si les statistiques sont au maximum
    public bool IsFullHealth => healthSystem.CurrentHealth >= healthSystem.MaxHealth;
    public bool IsFullMana => currentMana >= maxMana;
    public bool IsFullHunger => currentHunger >= maxHunger;
    public bool IsFullStamina => currentStamina >= maxStamina;
    
    void Awake()
    {
        // Récupérer le HealthSystem
        healthSystem = GetComponent<HealthSystem>();
        
        if (healthSystem == null)
        {
            Debug.LogError("[DIAGNOSTIC] HealthSystem requis sur le même GameObject!");
            enabled = false;
            return;
        }
        else
        {
            Debug.Log("[DIAGNOSTIC] HealthSystem récupéré avec succès dans PlayerStats.Awake()");
        }
        
        // S'abonner aux événements du HealthSystem
        healthSystem.OnHealthChanged.AddListener((current, max) => {
            Debug.Log($"[DIAGNOSTIC] HealthSystem Event reçu: Health = {current}/{max}");
            OnHealthChanged.Invoke(current, max);
            
            // Ajout d'un log pour vérifier combien de listeners sont abonnés
            Debug.Log($"[DIAGNOSTIC] Nombre de listeners sur PlayerStats.OnHealthChanged: {OnHealthChanged.GetPersistentEventCount()}");
        });
        
        healthSystem.OnDeath.AddListener(() => {
            Debug.Log("[DIAGNOSTIC] Événement OnDeath du HealthSystem reçu dans PlayerStats");
            OnPlayerDeath.Invoke();
        });
        
        // Initialisation des statistiques
        currentMana = maxMana;
        currentHunger = maxHunger;
        currentStamina = maxStamina;
        
        Debug.Log($"[DIAGNOSTIC] PlayerStats.Awake() terminé. PV: {CurrentHealth}/{MaxHealth}, Mana: {currentMana}/{maxMana}");
    }
    
    void Start()
    {
        // Vérification de l'invulnérabilité au démarrage
        Debug.Log($"[DIAGNOSTIC] Au démarrage, isInvulnerable = {healthSystem.IsInvulnerable}");
        
        // Forcer le joueur à être vulnérable pour le diagnostic
        healthSystem.SetInvulnerable(false);
        Debug.Log($"[DIAGNOSTIC] Après reset, isInvulnerable = {healthSystem.IsInvulnerable}");
        
        // Démarrer la coroutine pour la diminution progressive de la faim
        if (hungerCoroutine != null)
            StopCoroutine(hungerCoroutine);
        
        hungerCoroutine = StartCoroutine(HungerSystem());
        
        // Émettre des événements initiaux pour s'assurer que l'UI est correctement mise à jour
        OnHealthChanged.Invoke(healthSystem.CurrentHealth, healthSystem.MaxHealth);
        OnManaChanged.Invoke(currentMana, maxMana);
        OnHungerChanged.Invoke(currentHunger, maxHunger);
        OnStaminaChanged.Invoke(currentStamina, maxStamina);

        Debug.Log($"[DIAGNOSTIC] PlayerStats.Start() terminé. État des statistiques:");
        Debug.Log($"[DIAGNOSTIC] PV: {CurrentHealth}/{MaxHealth}");
        Debug.Log($"[DIAGNOSTIC] Mana: {currentMana}/{maxMana}");
        Debug.Log($"[DIAGNOSTIC] Faim: {currentHunger}/{maxHunger}");
        Debug.Log($"[DIAGNOSTIC] Endurance: {currentStamina}/{maxStamina}");
    }
    
    void Update()
    {
        // Gestion de la régénération de l'endurance
        ManageStaminaRegeneration();
        
        // Gestion de la régénération du mana
        RegenerateMana(manaRegenRate * Time.deltaTime);
        
        // Gestion de la régénération de la santé si activée
        if (canRegenerateHealth && Time.time - lastHealthLossTime > healthRegenDelay)
        {
            RegenerateHealth(healthRegenRate * Time.deltaTime);
        }
        
        // Test direct des dégâts avec la touche T
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestDamage();
        }
        
        // Forcer la mise à jour de l'UI si nécessaire (pour débogage)
        if (forceUpdateUI)
        {
            forceUpdateUI = false;
            OnHealthChanged.Invoke(healthSystem.CurrentHealth, healthSystem.MaxHealth);
            OnManaChanged.Invoke(currentMana, maxMana);
            OnHungerChanged.Invoke(currentHunger, maxHunger);
            OnStaminaChanged.Invoke(currentStamina, maxStamina);
            Debug.Log("[DIAGNOSTIC] UI forcée à se mettre à jour via forceUpdateUI");
        }
    }
    
    void OnDisable()
    {
        // Arrêter les coroutines en cours si le composant est désactivé
        if (hungerCoroutine != null)
            StopCoroutine(hungerCoroutine);
    }
    
    // Méthode de test pour les dégâts directs
    public void TestDamage()
    {
        float amount = 10f; // Montant fixe pour le test
        Debug.Log($"[DIAGNOSTIC] Test de dégâts: {amount} - PV actuels: {healthSystem.CurrentHealth}/{healthSystem.MaxHealth}");
        
        // Vérifier si le HealthSystem est invulnérable
        bool isInvulnerable = healthSystem.IsInvulnerable;
        Debug.Log($"[DIAGNOSTIC] isInvulnerable = {isInvulnerable}");
        
        // Appliquer les dégâts
        healthSystem.TakeDamage(amount);
        lastHealthLossTime = Time.time;
        
        // Vérifier après dégâts
        Debug.Log($"[DIAGNOSTIC] Après dégâts: PV = {healthSystem.CurrentHealth}/{healthSystem.MaxHealth}");
    }
    
    #region Health Management
    
    // Infliger des dégâts au joueur
    public void TakeDamage(float amount)
    {
        if (amount <= 0) 
        {
            Debug.Log($"[DIAGNOSTIC] PlayerStats.TakeDamage ignoré car amount={amount} <= 0");
            return;
        }
        
        Debug.Log($"[DIAGNOSTIC] PlayerStats.TakeDamage({amount}) appelé - PV avant: {healthSystem.CurrentHealth}");
        
        // Vérifier si HealthSystem est null - ajouter une protection
        if (healthSystem == null)
        {
            Debug.LogError("[DIAGNOSTIC] ERREUR: healthSystem est null dans PlayerStats.TakeDamage!");
            // Essayer de récupérer le HealthSystem à nouveau
            healthSystem = GetComponent<HealthSystem>();
            if (healthSystem == null)
            {
                Debug.LogError("[DIAGNOSTIC] ERREUR CRITIQUE: Impossible de récupérer le HealthSystem!");
                return;
            }
        }
        
        // Désactiver temporairement l'invulnérabilité pour le diagnostic
        bool wasInvulnerable = healthSystem.IsInvulnerable;
        healthSystem.SetInvulnerable(false);
        
        // Appliquer les dégâts
        float actualDamage = healthSystem.TakeDamage(amount);
        
        // Restaurer l'état d'invulnérabilité
        healthSystem.SetInvulnerable(wasInvulnerable);
        
        lastHealthLossTime = Time.time;
        
        // Forcer la mise à jour de l'UI
        OnHealthChanged.Invoke(healthSystem.CurrentHealth, healthSystem.MaxHealth);
        
        Debug.Log($"[DIAGNOSTIC] Dégâts réels: {actualDamage} - PV après: {healthSystem.CurrentHealth}");
        
        // Vérifier si l'UI est à jour
        Debug.Log($"[DIAGNOSTIC] Après TakeDamage, CurrentHealth dans PlayerStats: {CurrentHealth}");
        Debug.Log($"[DIAGNOSTIC] Après TakeDamage, CurrentHealth dans HealthSystem: {healthSystem.CurrentHealth}");
    }
    
    // Restaurer la santé du joueur
    public void RestoreHealth(float amount)
    {
        if (amount <= 0) 
        {
            Debug.Log($"[DIAGNOSTIC] PlayerStats.RestoreHealth ignoré car amount={amount} <= 0");
            return;
        }
        
        Debug.Log($"[DIAGNOSTIC] PlayerStats.RestoreHealth({amount}) appelé - PV avant: {healthSystem.CurrentHealth}");
        
        float healedAmount = healthSystem.Heal(amount);
        
        Debug.Log($"[DIAGNOSTIC] Santé réellement restaurée: {healedAmount} - PV après: {healthSystem.CurrentHealth}");
    }
    
    // Régénération passive de la santé
    private void RegenerateHealth(float amount)
    {
        if (amount <= 0 || IsFullHealth) return;
        
        healthSystem.Heal(amount);
    }
    #endregion
    
    #region Mana Management
    
    // Consommer du mana
    public bool UseMana(float amount)
    {
        if (amount <= 0) return true;
        
        if (currentMana >= amount)
        {
            float oldMana = currentMana;
            currentMana -= amount;
            
            // S'assurer que la valeur ne descend pas en-dessous de zéro
            currentMana = Mathf.Max(0, currentMana);
            
            // Déclencher l'événement seulement si la valeur a changé
            if (oldMana != currentMana)
            {
                OnManaChanged.Invoke(currentMana, maxMana);
                Debug.Log($"[DIAGNOSTIC] Mana consommé: {amount}, Nouveau mana: {currentMana}/{maxMana}");
            }
            return true;
        }
        
        Debug.Log($"[DIAGNOSTIC] Pas assez de mana: {currentMana}/{maxMana}, requis: {amount}");
        return false;
    }
    
    // Restaurer du mana
    public void RestoreMana(float amount)
    {
        if (amount <= 0) return;
        
        currentMana = Mathf.Min(maxMana, currentMana + amount);
        OnManaChanged.Invoke(currentMana, maxMana);
    }
    
    // Régénération passive du mana
    private void RegenerateMana(float amount)
    {
        if (amount <= 0 || currentMana >= maxMana) return;
        
        currentMana = Mathf.Min(maxMana, currentMana + amount);
        OnManaChanged.Invoke(currentMana, maxMana);
    }
    #endregion
    
    #region Hunger Management
    
    // Gestion continue de la faim
    private IEnumerator HungerSystem()
    {
        while (true)
        {
            // Attendre une seconde
            yield return new WaitForSeconds(1f);
            
            // Diminuer la faim
            DecreaseHunger(hungerDecreaseRate);
            
            // Si la faim est trop basse, affecter la santé
            if (currentHunger < hungerHealthThreshold)
            {
                float healthPenalty = hungerHealthPenalty * (1 - (currentHunger / hungerHealthThreshold));
                TakeDamage(healthPenalty * Time.deltaTime);
            }
        }
    }
    
    // Diminuer la faim
    public void DecreaseHunger(float amount)
    {
        if (amount <= 0) return;
        
        float oldHunger = currentHunger;
        currentHunger = Mathf.Max(0, currentHunger - amount);
        
        if (oldHunger != currentHunger)
        {
            OnHungerChanged.Invoke(currentHunger, maxHunger);
        }
    }
    
    // Restaurer la faim
    public void RestoreHunger(float amount)
    {
        if (amount <= 0) return;
        
        float oldHunger = currentHunger;
        currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
        
        if (oldHunger != currentHunger)
        {
            OnHungerChanged.Invoke(currentHunger, maxHunger);
        }
    }
    #endregion
    
    #region Stamina Management
    
    // Utiliser de l'endurance
    public bool UseStamina(float amount)
    {
        if (amount <= 0) return true;
        
        if (currentStamina >= amount)
        {
            currentStamina = Mathf.Max(0, currentStamina - amount);
            lastStaminaUseTime = Time.time;
            isStaminaRegenerating = false;
            
            OnStaminaChanged.Invoke(currentStamina, maxStamina);
            return true;
        }
        return false;
    }
    
    // Gestion de la régénération de l'endurance
    private void ManageStaminaRegeneration()
    {
        // Si nous ne sommes pas en train de régénérer et que le délai est passé
        if (!isStaminaRegenerating && Time.time - lastStaminaUseTime > staminaRegenDelay)
        {
            isStaminaRegenerating = true;
        }
        
        // Si nous sommes en mode régénération et que l'endurance n'est pas pleine
        if (isStaminaRegenerating && currentStamina < maxStamina)
        {
            float oldStamina = currentStamina;
            currentStamina = Mathf.Min(maxStamina, currentStamina + (staminaRegenRate * Time.deltaTime));
            
            if (Mathf.Abs(oldStamina - currentStamina) > 0.01f)
            {
                OnStaminaChanged.Invoke(currentStamina, maxStamina);
            }
        }
    }
    
    // Restaurer directement l'endurance
    public void RestoreStamina(float amount)
    {
        if (amount <= 0) return;
        
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        OnStaminaChanged.Invoke(currentStamina, maxStamina);
    }
    #endregion
    
    // Méthode pour réinitialiser toutes les statistiques (respawn)
    public void ResetAllStats()
    {
        Debug.Log("[DIAGNOSTIC] ResetAllStats appelé - réinitialisation de toutes les statistiques");
        healthSystem.ResetHealth();
        currentMana = maxMana;
        currentHunger = maxHunger;
        currentStamina = maxStamina;
        
        // Notifier les changements
        OnManaChanged.Invoke(currentMana, maxMana);
        OnHungerChanged.Invoke(currentHunger, maxHunger);
        OnStaminaChanged.Invoke(currentStamina, maxStamina);
        
        Debug.Log($"[DIAGNOSTIC] Stats après reset: PV={CurrentHealth}/{MaxHealth}, Mana={currentMana}/{maxMana}");
    }
    
    // Définir des valeurs maximales (pour les améliorations de statistiques)
    public void SetMaxMana(float newMax)
    {
        maxMana = Mathf.Max(1, newMax);
        currentMana = Mathf.Min(currentMana, maxMana);
        OnManaChanged.Invoke(currentMana, maxMana);
    }
    
    public void SetMaxStamina(float newMax)
    {
        maxStamina = Mathf.Max(1, newMax);
        currentStamina = Mathf.Min(currentStamina, maxStamina);
        OnStaminaChanged.Invoke(currentStamina, maxStamina);
    }
    
    public void SetMaxHunger(float newMax)
    {
        maxHunger = Mathf.Max(1, newMax);
        currentHunger = Mathf.Min(currentHunger, maxHunger);
        OnHungerChanged.Invoke(currentHunger, maxHunger);
    }
    
    public void SetMaxHealth(float newMax)
    {
        Debug.Log($"[DIAGNOSTIC] Changement de santé max: {MaxHealth} -> {newMax}");
        healthSystem.SetMaxHealth(newMax);
    }
    
    // Pour logging et debugging
    public void LogAllStats()
    {
        Debug.Log($"==== [DIAGNOSTIC] STATISTIQUES DU JOUEUR ====");
        Debug.Log($"Santé: {CurrentHealth}/{MaxHealth}");
        Debug.Log($"Mana: {currentMana}/{maxMana}");
        Debug.Log($"Stamina: {currentStamina}/{maxStamina}");
        Debug.Log($"Faim: {currentHunger}/{maxHunger}");
        Debug.Log($"===============================");
    }
}