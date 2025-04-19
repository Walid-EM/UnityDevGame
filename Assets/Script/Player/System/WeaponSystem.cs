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
    
    // Nouvelles propriétés exposées
    public int WeaponID => weaponID;
    public ItemData WeaponData => weaponData;
    
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
            Debug.LogWarning("PlayerStats non trouvé pendant l'initialisation de WeaponSystem. Cela n'est pas une erreur critique.");
        }
        else
        {
            // Notifier PlayerStats qu'une arme a été équipée, sans déclencher d'attaque
            // IMPORTANT: Ne pas consommer de mana ici
            playerStats.UpdateEquippedWeapon();
        }
    }
    
    /// <summary>
    /// Exécute une attaque avec l'arme
    /// </summary>
    public bool Attack()
    {
        Debug.Log($"[WEAPON DEBUG] WeaponSystem.Attack() appelé");

        // Chercher les playerStats si non trouvés précédemment
        if (playerStats == null)
        {
            playerStats = GetComponentInParent<PlayerStats>();
            if (playerStats == null)
            {
                Debug.LogWarning("PlayerStats non trouvé dans Attack(). L'attaque peut fonctionner sans consommer de mana.");
                // On continue l'attaque sans consommer de mana
            }
        }
        
        if (weaponData == null)
        {
            Debug.LogError("Données de l'arme non chargées!");
            return false;
        }

        // Calculer le coût en mana
        float manaCost = CalculateManaCost();
        
        Debug.Log($"[WEAPON DEBUG] Coût en mana calculé: {manaCost}");
        
        if (playerStats != null)
        {
            Debug.Log($"[WEAPON DEBUG] Avant UseMana, mana actuel: {playerStats.CurrentMana}/{playerStats.MaxMana}");
            bool hasEnoughMana = playerStats.UseMana(manaCost);
            Debug.Log($"[WEAPON DEBUG] Après UseMana, résultat: {hasEnoughMana}, mana restant: {playerStats.CurrentMana}/{playerStats.MaxMana}");
            
            if (!hasEnoughMana)
            {
                // Jouer le son d'échec
                if (audioSource != null && noManaSound != null)
                {
                    audioSource.PlayOneShot(noManaSound);
                }
                
                return false;
            }
        }
        // Vérifier si le joueur a assez de mana (seulement si playerStats existe)
        if (playerStats != null && !playerStats.UseMana(manaCost))
        {
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
        
        bool hitSomething = false;
        
        foreach (Collider enemyCollider in hitEnemies)
        {
            // Ignorer le joueur lui-même
            if (enemyCollider.transform.IsChildOf(transform.root))
            {
                continue;
            }
                
            hitSomething = true;
            
            // Chercher un système de santé sur l'ennemi
            HealthSystem enemyHealth = enemyCollider.GetComponent<HealthSystem>();
            if (enemyHealth != null)
            {
                // Utiliser la valeur de dégâts depuis weaponData
                float damage = weaponData.WeaponDamage;
                
                // Infliger des dégâts
                float appliedDamage = enemyHealth.TakeDamage(damage);
                Debug.Log($"Dégâts infligés: {appliedDamage} (sur {damage} prévus)");
                
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
        }
        
        // Si aucun ennemi n'a été touché, faire un test contre soi-même pour démonstration
        if (!hitSomething && playerStats != null)
        {
            // Utiliser directement la valeur de dégâts définie dans weaponData
            float demoAmount = weaponData.WeaponDamage * 0.5f; // 50% au lieu de 10%
            
            // Ajoutons un minimum pour s'assurer que la valeur n'est pas trop petite
            demoAmount = Mathf.Max(demoAmount, 5f); // Au moins 5 points de dégâts
            
            Debug.Log($"Auto-dégâts de test: {demoAmount} (basé sur les dégâts de l'arme: {weaponData.WeaponDamage})");
            
            // Utilisation de différentes approches pour appliquer les dégâts
            playerStats.TakeDamage(demoAmount);
        }
        
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
        Debug.Log($"[WEAPON DEBUG] SetWeaponID appelé avec id={id}");

        weaponID = id;
        
        if (ItemDatabase.Instance != null)
        {
            weaponData = ItemDatabase.Instance.GetItem(id);
            
            if (weaponData == null)
            {
                Debug.LogError($"Données de l'arme avec ID {id} non trouvées!");
            }
            else if (weaponData.Type != ItemType.Weapon)
            {
                Debug.LogError($"L'item avec ID {id} n'est pas une arme!");
            }
            else
            {
                Debug.Log($"[WEAPON DEBUG] Arme configurée: {weaponData.Name} avec dégâts: {weaponData.WeaponDamage}");
            }
        }
        else
        {
            Debug.LogWarning("ItemDatabase.Instance est null dans SetWeaponID. Cela n'est pas une erreur critique.");
        }
        
        // Si PlayerStats existe, le notifier du changement d'arme, sans consommer de mana
        if (playerStats != null)
        {
            Debug.Log($"[WEAPON DEBUG] Avant UpdateEquippedWeapon, mana actuel: {playerStats.CurrentMana}/{playerStats.MaxMana}");
            playerStats.UpdateEquippedWeapon();
            Debug.Log($"[WEAPON DEBUG] Après UpdateEquippedWeapon, mana actuel: {playerStats.CurrentMana}/{playerStats.MaxMana}");
        }
    }
    
    void Update()
    {
        // Test direct pour infliger des dégâts avec la touche X
        if (Input.GetKeyDown(KeyCode.X))
        {
            // Chercher les playerStats si non trouvés précédemment
            if (playerStats == null)
            {
                playerStats = GetComponentInParent<PlayerStats>();
                if (playerStats == null)
                {
                    Debug.LogWarning("PlayerStats non trouvé dans Update(). Le test de dégâts ne peut pas être effectué.");
                    return;
                }
            }
            
            float damage = 5f;
            playerStats.TakeDamage(damage);
        }
    }
}