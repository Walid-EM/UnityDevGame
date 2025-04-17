using UnityEngine;

/// <summary>
/// Gère le comportement des armes et leur interaction avec le système de statistiques du joueur.
/// Permet aux armes de consommer du mana et d'infliger des dégâts.
/// </summary>
public class WeaponSystem : MonoBehaviour
{
    [Header("Arme")]
    [SerializeField] private int weaponID = -1; // ID de l'arme dans l'ItemDatabase
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private LayerMask attackableLayers;
    [SerializeField] private Transform attackPoint;
    
    [Header("Consommation de Mana")]
    [SerializeField] private float baseManaCost = 5f;
    [SerializeField] private float manaCostMultiplier = 1.0f;
    [SerializeField] private bool scaleManaCostWithDamage = true;
    
    [Header("Effets")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject swingEffectPrefab;
    
    [Header("Audio")]
    [SerializeField] private AudioClip swingSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip noManaSound;
    
    // Références
    private PlayerStats playerStats;
    private AudioSource audioSource;
    private ItemData weaponData;
    
    // Propriétés
    public float Damage => weaponData != null ? weaponData.WeaponDamage : 0f;
    public float ManaCost => CalculateManaCost();
    
    private void Start()
    {
        // Trouver les références nécessaires
        playerStats = GetComponentInParent<PlayerStats>();
        audioSource = GetComponent<AudioSource>();
        
        // Ajouter une source audio si nécessaire
        if (audioSource == null && (swingSound != null || hitSound != null || noManaSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Charger les données de l'arme depuis la base de données
        if (weaponID >= 0 && ItemDatabase.Instance != null)
        {
            weaponData = ItemDatabase.Instance.GetItem(weaponID);
            
            if (weaponData == null)
            {
                Debug.LogError($"Données de l'arme avec ID {weaponID} non trouvées!");
            }
            else if (weaponData.Type != ItemType.Weapon)
            {
                Debug.LogError($"L'item avec ID {weaponID} n'est pas une arme!");
            }
            else 
            {
                Debug.Log($"[DIAGNOSTIC] Arme chargée: ID={weaponID}, Nom={weaponData.Name}, Dégâts={weaponData.WeaponDamage}");
            }
        }
        
        // Vérifier que le point d'attaque est configuré
        if (attackPoint == null)
        {
            attackPoint = transform;
            Debug.LogWarning("Point d'attaque non configuré, utilisation de la position de l'arme.");
        }

        // Vérifier que playerStats est bien trouvé
        if (playerStats == null)
        {
            Debug.LogError("PlayerStats non trouvé pendant l'initialisation de WeaponSystem!");
        }
        else
        {
            Debug.Log($"[DIAGNOSTIC] PlayerStats correctement référencé dans WeaponSystem. Health={playerStats.CurrentHealth}/{playerStats.MaxHealth}");
        }
    }
    
    /// <summary>
    /// Exécute une attaque avec l'arme
    /// </summary>
    /// <returns>True si l'attaque a été effectuée</returns>
    public bool Attack()
    {
        Debug.Log($"=========== DÉBUT DIAGNOSTIC ATTAQUE ===========");
        
        if (playerStats == null)
        {
            Debug.LogError("[DIAGNOSTIC] PlayerStats est null dans Attack()!");
            return false;
        }
        
        if (weaponData == null)
        {
            Debug.LogError("[DIAGNOSTIC] Données de l'arme non chargées!");
            return false;
        }
        
        Debug.Log($"[DIAGNOSTIC] WeaponID: {weaponID}, WeaponName: {weaponData.Name}, WeaponDamage: {weaponData.WeaponDamage}");
        Debug.Log($"[DIAGNOSTIC] Player Health before: {playerStats.CurrentHealth}/{playerStats.MaxHealth}");

        // Calculer le coût en mana
        float manaCost = CalculateManaCost();
        
        // Vérifier si le joueur a assez de mana
        if (!playerStats.UseMana(manaCost))
        {
            Debug.Log($"[DIAGNOSTIC] Pas assez de mana pour utiliser cette arme! (Requis: {manaCost})");
            
            // Jouer le son d'échec
            if (audioSource != null && noManaSound != null)
            {
                audioSource.PlayOneShot(noManaSound);
            }
            
            return false;
        }
        
        // Jouer le son de swing
        if (audioSource != null && swingSound != null)
        {
            audioSource.PlayOneShot(swingSound);
        }
        
        // Créer l'effet de swing
        if (swingEffectPrefab != null)
        {
            Instantiate(swingEffectPrefab, attackPoint.position, attackPoint.rotation);
        }
        
        // Effectuer une détection d'ennemis dans la zone d'attaque
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, attackableLayers);
        Debug.Log($"[DIAGNOSTIC] Détection d'ennemis: {hitEnemies.Length} colliders trouvés");
        
        bool hitSomething = false;
        
        foreach (Collider enemyCollider in hitEnemies)
        {
            // Ignorer le joueur lui-même
            if (enemyCollider.transform.IsChildOf(transform.root))
            {
                Debug.Log($"[DIAGNOSTIC] Collider ignoré car c'est le joueur: {enemyCollider.name}");
                continue;
            }
                
            Debug.Log($"[DIAGNOSTIC] Touché: {enemyCollider.name}");
            hitSomething = true;
            
            // Chercher un système de santé sur l'ennemi
            HealthSystem enemyHealth = enemyCollider.GetComponent<HealthSystem>();
            if (enemyHealth != null)
            {
                // Infliger des dégâts
                float appliedDamage = enemyHealth.TakeDamage(weaponData.WeaponDamage);
                Debug.Log($"[DIAGNOSTIC] Dégâts infligés à l'ennemi: {appliedDamage}");
                
                // Créer un effet d'impact
                if (hitEffectPrefab != null)
                {
                    Vector3 hitPosition = enemyCollider.ClosestPoint(attackPoint.position);
                    Instantiate(hitEffectPrefab, hitPosition, Quaternion.identity);
                }
                
                // Jouer le son d'impact
                if (audioSource != null && hitSound != null)
                {
                    audioSource.PlayOneShot(hitSound);
                }
            }
            else
            {
                Debug.Log($"[DIAGNOSTIC] Pas de HealthSystem trouvé sur: {enemyCollider.name}");
            }
        }
        
        // Si aucun ennemi n'a été touché, faire un test contre soi-même pour démonstration
        if (!hitSomething)
        {
            Debug.Log("[DIAGNOSTIC] Aucun ennemi touché, tentative d'auto-dégâts...");
            
            // Augmentons la valeur des auto-dégâts pour un test
            float demoAmount = weaponData.WeaponDamage * 0.5f; // 50% au lieu de 10%
            
            // Ajoutons un minimum pour s'assurer que la valeur n'est pas trop petite
            demoAmount = Mathf.Max(demoAmount, 5f); // Au moins 5 points de dégâts
            
            Debug.Log($"[DIAGNOSTIC] Auto-dégâts calculés: {demoAmount} (WeaponDamage: {weaponData.WeaponDamage})");
            
            // Utilisation de différentes approches pour appliquer les dégâts
            if (playerStats != null)
            {
                Debug.Log($"[DIAGNOSTIC] AVANT auto-dégâts: PV = {playerStats.CurrentHealth}/{playerStats.MaxHealth}");
                
                // Méthode 1: Via PlayerStats
                Debug.Log("[DIAGNOSTIC] Méthode 1: Application via playerStats.TakeDamage");
                playerStats.TakeDamage(demoAmount);
                
                Debug.Log($"[DIAGNOSTIC] APRÈS Méthode 1: PV = {playerStats.CurrentHealth}/{playerStats.MaxHealth}");

                // Méthode 2: Directement sur le HealthSystem
                HealthSystem playerHealthSys = playerStats.GetComponent<HealthSystem>();
                if (playerHealthSys != null)
                {
                    Debug.Log("[DIAGNOSTIC] Méthode 2: Application directe via healthSystem.TakeDamage");
                    float damageApplied = playerHealthSys.TakeDamage(demoAmount);
                    Debug.Log($"[DIAGNOSTIC] Dégâts réellement appliqués (Méthode 2): {damageApplied}");
                    Debug.Log($"[DIAGNOSTIC] APRÈS Méthode 2: PV = {playerHealthSys.CurrentHealth}/{playerHealthSys.MaxHealth}");
                }
                else
                {
                    Debug.LogError("[DIAGNOSTIC] Impossible de récupérer le HealthSystem du joueur!");
                }
                
                // Vérifions si les stats sont à jour
                Debug.Log($"[DIAGNOSTIC] État final du joueur après auto-dégâts:");
                Debug.Log($"[DIAGNOSTIC] PlayerStats.CurrentHealth = {playerStats.CurrentHealth}");
                Debug.Log($"[DIAGNOSTIC] HealthSystem.CurrentHealth = {playerStats.GetComponent<HealthSystem>()?.CurrentHealth}");
            }
            else
            {
                Debug.LogError("[DIAGNOSTIC] PlayerStats est null pendant l'application des auto-dégâts!");
            }
        }
        
        Debug.Log($"[DIAGNOSTIC] Player health after attack: {playerStats.CurrentHealth}/{playerStats.MaxHealth}");
        Debug.Log($"=========== FIN DIAGNOSTIC ATTAQUE ===========");
        return true;
    }
    
    /// <summary>
    /// Calculer le coût en mana pour utiliser cette arme
    /// </summary>
    /// <returns>Coût en mana</returns>
    private float CalculateManaCost()
    {
        float cost = baseManaCost * manaCostMultiplier;
        
        // Ajuster le coût en fonction des dégâts si activé
        if (scaleManaCostWithDamage && weaponData != null)
        {
            // Formule: Coût de base + (dégâts / 10)
            cost += weaponData.WeaponDamage / 10f;
        }
        
        return Mathf.Round(cost);
    }
    
    /// <summary>
    /// Dessine des gizmos pour visualiser la portée d'attaque dans l'éditeur
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            attackPoint = transform;
            
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
    
    /// <summary>
    /// Définit l'ID de l'arme et charge ses données
    /// </summary>
    /// <param name="id">ID de l'arme dans l'ItemDatabase</param>
    public void SetWeaponID(int id)
    {
        Debug.Log($"[DIAGNOSTIC] SetWeaponID appelé avec ID={id}");
        weaponID = id;
        
        if (ItemDatabase.Instance != null)
        {
            weaponData = ItemDatabase.Instance.GetItem(id);
            
            if (weaponData == null)
            {
                Debug.LogError($"[DIAGNOSTIC] Données de l'arme avec ID {id} non trouvées!");
            }
            else if (weaponData.Type != ItemType.Weapon)
            {
                Debug.LogError($"[DIAGNOSTIC] L'item avec ID {id} n'est pas une arme!");
            }
            else
            {
                Debug.Log($"[DIAGNOSTIC] Arme mise à jour: ID={id}, Nom={weaponData.Name}, Dégâts={weaponData.WeaponDamage}");
            }
        }
        else
        {
            Debug.LogError("[DIAGNOSTIC] ItemDatabase.Instance est null dans SetWeaponID!");
        }
    }
    
    void Update()
    {
        // Test direct pour infliger des dégâts avec la touche X
        if (Input.GetKeyDown(KeyCode.X) && playerStats != null)
        {
            float damage = 5f;
            Debug.Log($"[DIAGNOSTIC] Test d'attaque directe avec X: {damage} dégâts");
            
            Debug.Log($"[DIAGNOSTIC] AVANT touche X: PV = {playerStats.CurrentHealth}/{playerStats.MaxHealth}");
            playerStats.TakeDamage(damage);
            Debug.Log($"[DIAGNOSTIC] APRÈS touche X: PV = {playerStats.CurrentHealth}/{playerStats.MaxHealth}");
        }
    }
}