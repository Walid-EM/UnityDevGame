using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Interaction")]
    public float interactionDistance = 3f;
    public LayerMask interactableLayers;
    public GameObject pickupPrompt;
    public TMP_Text promptText;
    
    [Header("Équipement")]
    public Transform handTransform;
    
    [Header("Messages")]
    public GameObject messagePanel;
    public TMP_Text messageText;
    public float messageDisplayTime = 2f;
    
    [Header("Input Configuration")]
    public KeyCode[] hotbarKeys = new KeyCode[8] 
    { 
        KeyCode.Alpha1, 
        KeyCode.Alpha2, 
        KeyCode.Alpha3, 
        KeyCode.Alpha4, 
        KeyCode.Alpha5, 
        KeyCode.Alpha6, 
        KeyCode.Alpha7, 
        KeyCode.Alpha8 
    };
    
    [Header("Mouvement")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float staminaUseRate = 10f; // Consommation de stamina par seconde lors de la course
    public float jumpStaminaCost = 15f; // Consommation de stamina pour un saut
    
    [Header("Combat")]
    public float weaponManaCost = 5f; // Coût en mana pour utiliser une arme
    
    // Variables privées
    private Camera playerCamera;
    private PickupItem currentTarget;
    private GameObject currentEquippedObject;
    private ItemInstance currentEquippedItem;
    private PlayerStats playerStats; // Référence au système de statistiques
    private float currentSpeed;
    private bool isRunning = false;
    
    private void Start()
    {
        playerCamera = Camera.main;
        
        // Désactiver le prompt au démarrage
        if (pickupPrompt != null)
            pickupPrompt.SetActive(false);
            
        // Désactiver le panel de message au démarrage
        if (messagePanel != null)
            messagePanel.SetActive(false);
        
        // Vérifier si handTransform est configuré
        if (handTransform == null)
            Debug.LogWarning("HandTransform non configuré. Les objets ne seront pas visibles en main.");
        
        // S'abonner aux événements d'inventaire
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemEquipped += EquipItem;
            InventoryManager.Instance.OnItemUnequipped += UnequipCurrentItem;
        }
        
        // Récupérer la référence au système de statistiques
        playerStats = GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning("PlayerStats non trouvé! Veuillez ajouter ce composant au joueur.");
            // Essayer d'ajouter automatiquement le composant
            playerStats = gameObject.AddComponent<PlayerStats>();
        }
        
        // Initialiser la vitesse
        currentSpeed = walkSpeed;
    }
    
    private void OnDestroy()
    {
        // Se désabonner des événements
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemEquipped -= EquipItem;
            InventoryManager.Instance.OnItemUnequipped -= UnequipCurrentItem;
        }
    }
    
    private void Update()
    {
        // Vérifier si le joueur regarde un objet ramassable
        CheckForInteractable();
        
        // Si un objet est ciblé et que le joueur appuie sur E
        if (currentTarget != null && Input.GetKeyDown(KeyCode.E))
        {
            // Ramasser l'objet
            PickupTargetItem();
        }
        
        // Gestion des touches de hotbar
        HandleHotbarInput();
        
        // Gestion de la molette de souris
        HandleScrollWheelInput();
        
        // Utiliser l'item équipé avec le clic gauche
        if (Input.GetMouseButtonDown(0))
        {
            // Si un item est équipé, l'utiliser
            if (currentEquippedItem != null)
            {
                UseEquippedItem();
            }
        }

        // Gestion de la course avec consommation de stamina
        HandleRunning();
        
        // Gestion du saut avec consommation de stamina
        if (Input.GetKeyDown(KeyCode.Space))
        {
            JumpAction();
        }

        CheckEquippedItemExists();
    }
    
    private void CheckEquippedItemExists()
    {
        // Si un item est équipé mais qu'il n'existe plus dans l'inventaire, le déséquiper
        if (currentEquippedItem != null && InventoryManager.Instance != null)
        {
            // Vérifier que l'item existe encore dans l'inventaire
            int quantity = InventoryManager.Instance.GetItemQuantity(currentEquippedItem.itemID);
            if (quantity <= 0)
            {
                Debug.Log($"L'item équipé {currentEquippedItem.Name} n'existe plus dans l'inventaire, déséquipement automatique");
                UnequipCurrentItem();
            }
        }
    }

    // Vérifier si le joueur regarde un objet interactable
    private void CheckForInteractable()
    {
        RaycastHit hit;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        
        // Lancer un rayon devant le joueur
        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayers))
        {
            // Vérifier si l'objet touché est ramassable
            PickupItem item = hit.collider.GetComponentInParent<PickupItem>();
            
            if (item != null)
            {
                // Si on vise un nouvel objet différent de l'actuel ou si aucun objet n'était visé auparavant
                if (currentTarget != item)
                {
                    // Afficher le prompt
                    ShowPickupPrompt(item.GetItemName());
                    currentTarget = item;
                }
                return;
            }
        }
        
        // Si le rayon ne touche pas d'objet ramassable, masquer le prompt
        if (currentTarget != null)
        {
            HidePickupPrompt();
            currentTarget = null;
        }
    }
    
    // Ramasser l'objet ciblé
    private void PickupTargetItem()
    {
        if (currentTarget == null) return;
        
        // Vérifier si l'inventaire est plein
        if (InventoryManager.Instance != null && InventoryManager.Instance.IsHotbarFull())
        {
            Debug.Log("Inventaire plein, impossible de ramasser l'objet!");
            ShowMessage("Inventaire plein!");
            // Le prompt restera affiché
            return;
        }
        
        // Ajouter l'item à l'inventaire
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(currentTarget.itemID);
            
            // Jouer un effet si disponible
            if (currentTarget.pickupEffectPrefab != null)
            {
                Instantiate(currentTarget.pickupEffectPrefab, currentTarget.transform.position, Quaternion.identity);
            }
            
            // Supprimer l'objet du monde
            Destroy(currentTarget.gameObject);
            
            // Masquer le prompt
            HidePickupPrompt();
            currentTarget = null;
        }
    }
    
    // Afficher le prompt de ramassage
    private void ShowPickupPrompt(string itemName)
    {
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(true);
            if (promptText != null)
            {
                promptText.text = $"Press 'e' to pick up {itemName}";
            }
        }
    }
    
    // Cacher le prompt de ramassage
    private void HidePickupPrompt()
    {
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }
    }
    
    // Afficher un message temporaire
    public void ShowMessage(string message)
    {
        if (messagePanel != null && messageText != null)
        {
            // Définir le texte du message
            messageText.text = message;
            
            // Activer le panel
            messagePanel.SetActive(true);
            
            // Démarrer la coroutine pour cacher le message après un délai
            StartCoroutine(HideMessageAfterDelay());
        }
        else
        {
            Debug.LogWarning("messagePanel ou messageText non configuré!");
        }
    }
    
    // Coroutine pour cacher le message après un délai
    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDisplayTime);
        
        if (messagePanel != null)
            messagePanel.SetActive(false);
    }
    
    // Gestion des touches de hotbar
    private void HandleHotbarInput()
    {
        if (InventoryManager.Instance == null) return;
        
        for (int i = 0; i < Mathf.Min(hotbarKeys.Length, InventoryManager.Instance.hotbarSlots); i++)
        {
            if (Input.GetKeyDown(hotbarKeys[i]))
            {
                InventoryManager.Instance.SelectSlot(i);
                Debug.Log($"Slot sélectionné: {i}");
                break;
            }
        }
    }
    
    // Gestion de la molette de souris
    private void HandleScrollWheelInput()
    {
        if (InventoryManager.Instance == null) return;
        
        float scrollWheel = Input.GetAxis("Mouse ScrollWheel");
        if (scrollWheel == 0) return;
        
        int direction = scrollWheel > 0 ? -1 : 1;
        int currentSlot = InventoryManager.Instance.GetCurrentSelectedSlot();
        int newSlot;
        
        if (currentSlot == -1)
        {
            newSlot = direction > 0 ? 0 : InventoryManager.Instance.hotbarSlots - 1;
        }
        else
        {
            newSlot = (currentSlot + direction) % InventoryManager.Instance.hotbarSlots;
            if (newSlot < 0) newSlot = InventoryManager.Instance.hotbarSlots - 1;
        }
        
        InventoryManager.Instance.SelectSlot(newSlot);
    }
    
    // Équiper un item
    private void EquipItem(ItemInstance item)
    {
        // D'abord déséquiper l'item actuel
        UnequipCurrentItem();
        
        if (item == null || handTransform == null) return;
        
        Debug.Log($"Équipement de l'item: {item.Name}, Type: {item.Type}, ID: {item.itemID}");
        
        // Obtenir le prefab à instancier
        GameObject prefabToInstantiate = item.EquipPrefab;
        
        if (prefabToInstantiate == null)
        {
            Debug.LogWarning($"Pas de prefab trouvé pour l'item: {item.Name}");
            return;
        }
        
        // Instancier le prefab
        currentEquippedObject = Instantiate(prefabToInstantiate, handTransform);
        currentEquippedObject.transform.localPosition = Vector3.zero;
        currentEquippedObject.transform.localRotation = Quaternion.identity;
        
        // Configurer la physique
        ConfigureEquippedObjectPhysics(currentEquippedObject);
        
        // Si c'est une arme, configurer le WeaponSystem
        WeaponSystem weaponSystem = currentEquippedObject.GetComponent<WeaponSystem>();
        if (weaponSystem != null && item.Type == ItemType.Weapon)
        {
            weaponSystem.SetWeaponID(item.itemID);
        }
        
        // Stocker les données de l'item équipé
        currentEquippedItem = item;
        
        Debug.Log($"Item équipé avec succès: {item.Name}, Type: {item.Type}");
    }
    
    // Déséquiper l'item actuel
    private void UnequipCurrentItem()
    {
        if (currentEquippedObject != null)
        {
            Destroy(currentEquippedObject);
            currentEquippedObject = null;
            currentEquippedItem = null;
            Debug.Log("Item déséquipé");
        }
    }
    
    // Configurer la physique de l'objet équipé
    private void ConfigureEquippedObjectPhysics(GameObject obj)
    {
        if (obj == null) return;
        
        // Désactiver les rigidbodies
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Désactiver les rigidbodies des enfants
        foreach (Rigidbody childRb in obj.GetComponentsInChildren<Rigidbody>())
        {
            childRb.isKinematic = true;
            childRb.useGravity = false;
        }
        
        // Désactiver les colliders
        DisableCollidersRecursively(obj);
    }
    
    // Désactiver les colliders récursivement
    private void DisableCollidersRecursively(GameObject obj)
    {
        // Désactiver les colliders sur l'objet et tous ses enfants
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }
    
    // Utiliser l'item actuellement équipé
    public void UseEquippedItem()
    {
        if (currentEquippedItem == null)
        {
            Debug.Log("Aucun item équipé à utiliser");
            return;
        }
        
        Debug.Log($"Utilisation de l'item: {currentEquippedItem.Name}, Type: {currentEquippedItem.Type}");
        
        // Comportement spécifique selon le type d'item
        switch (currentEquippedItem.Type)
        {
            case ItemType.Weapon:
                Debug.Log("Utilisation de l'arme");
                UseWeapon();
                break;
                
            case ItemType.Consumable:
                Debug.Log("Utilisation du consommable");
                UseConsumable(currentEquippedItem);
                break;
                
            case ItemType.Equipment:
                Debug.Log("Utilisation de l'équipement");
                UseEquipment(currentEquippedItem);
                break;
                
            default:
                Debug.Log($"Pas d'action spécifique pour l'item de type {currentEquippedItem.Type}");
                break;
        }
    }
    
    // Utiliser une arme
    private void UseWeapon()
    {
    // Utiliser le WeaponSystem si disponible
    WeaponSystem weaponSystem = currentEquippedObject?.GetComponent<WeaponSystem>();
    if (weaponSystem != null)
    {
        Debug.Log("[FIX] Avant appel à weaponSystem.Attack()");
        
        // Le WeaponSystem gère la consommation de mana et les dégâts
        bool attackResult = weaponSystem.Attack();
        
        Debug.Log($"[FIX] Résultat de l'attaque: {attackResult}");
        
        if (attackResult)
        {
            // Attaque réussie
            Debug.Log("[FIX] Attaque effectuée avec succès!");
            
            // Force l'application des auto-dégâts si aucun ennemi n'est touché
            if (playerStats != null)
            {
                // Valeur fixe pour le test
                float testDamage = 5f;
                Debug.Log($"[FIX] Application directe de {testDamage} dégâts de test");
                playerStats.TakeDamage(testDamage);
            }
        }
        else
        {
            // Attaque échouée (pas assez de mana)
            ShowMessage("Pas assez de mana!");
        }
    }
    else
    {
        Debug.LogError("[FIX] Aucun WeaponSystem trouvé sur l'objet équipé!");
    }
    }
    
    // Utiliser un consommable
    private void UseConsumable(ItemInstance consumable)
    {
        Debug.Log($"Tentative de consommation de {consumable.Name}");
        
        // Vérifier si l'item a des effets
        ItemData itemData = consumable.Data;
        bool itemUsed = false;
        
        // Récupérer les statistiques du joueur
        if (playerStats == null)
        {
            Debug.LogWarning("PlayerStats non trouvé! Les effets des items ne seront pas appliqués.");
            return;
        }
        
        // Restauration de santé
        if (itemData.HealthRestore > 0)
        {
            // Vérifier si les HP sont déjà au maximum
            if (playerStats.IsFullHealth)
            {
                Debug.Log("Santé déjà au maximum");
            }
            else
            {
                // Restaurer de la santé
                float healthAmount = itemData.HealthRestore;
                Debug.Log($"Santé restaurée: {healthAmount}");
                
                playerStats.RestoreHealth(healthAmount);
                itemUsed = true;
            }
        }
        
        // Restauration de mana
        if (itemData.ManaRestore > 0)
        {
            // Vérifier si le mana est déjà au maximum
            if (playerStats.IsFullMana)
            {
                Debug.Log("Mana déjà au maximum");
            }
            else
            {
                // Restaurer du mana
                float manaAmount = itemData.ManaRestore;
                Debug.Log($"Mana restauré: {manaAmount}");
                
                playerStats.RestoreMana(manaAmount);
                itemUsed = true;
            }
        }
        
        // Restauration de faim
        if (itemData.HungerRestore > 0)
        {
            // Vérifier si la faim est déjà au maximum
            if (playerStats.IsFullHunger)
            {
                Debug.Log("Faim déjà au maximum");
            }
            else
            {
                // Restaurer de la faim
                float hungerAmount = itemData.HungerRestore;
                Debug.Log($"Faim restaurée: {hungerAmount}");
                
                playerStats.RestoreHunger(hungerAmount);
                itemUsed = true;
            }
        }
        
        // Si au moins un effet a été appliqué, supprimer l'item
        if (itemUsed)
        {
            // Stocker l'ID de l'item avant de le supprimer
            int itemID = consumable.itemID;
            
            // Réduire la quantité (supprimer l'item)
            if (InventoryManager.Instance != null)
            {
                // Vérifier la quantité avant suppression
                int currentQuantity = InventoryManager.Instance.GetItemQuantity(itemID);
                
                // Supprimer l'item de l'inventaire
                InventoryManager.Instance.RemoveItem(itemID, 1);
                
                // Si c'était le dernier item et qu'il était équipé, le déséquiper manuellement
                if (currentQuantity <= 1 && currentEquippedItem != null && currentEquippedItem.itemID == itemID)
                {
                    UnequipCurrentItem();
                }
            }
        }
        else
        {
            // Aucun effet n'a été appliqué
            ShowMessage("Cet item n'a aucun effet sur vous actuellement.");
        }
    }
    
    // Utiliser un équipement
    private void UseEquipment(ItemInstance equipment)
    {
        Debug.Log($"Utilisation de l'équipement {equipment.Name}");
        
        // Implémenter l'utilisation d'équipement
        // Par exemple, activer une capacité spéciale
    }
    
    // Gestion de la course avec consommation de stamina
    private void HandleRunning()
    {
        if (playerStats == null) return;
        
        // Vérifier l'input Sprint (par exemple, Shift)
        bool sprintInput = Input.GetKey(KeyCode.LeftShift);
        
        // Si le joueur essaie de courir et a de la stamina
        if (sprintInput && playerStats.CurrentStamina > 0)
        {
            // Consommer de la stamina
            float staminaToUse = staminaUseRate * Time.deltaTime;
            if (playerStats.UseStamina(staminaToUse))
            {
                isRunning = true;
                currentSpeed = runSpeed;
            }
            else
            {
                // Si pas assez de stamina, marcher
                isRunning = false;
                currentSpeed = walkSpeed;
            }
        }
        else
        {
            // Si le joueur ne veut pas courir ou n'a plus de stamina
            isRunning = false;
            currentSpeed = walkSpeed;
        }
    }
    
    // Méthode pour vérifier si le joueur peut réaliser une action qui consomme de la stamina
    public bool CanPerformStaminaAction(float staminaCost)
    {
        if (playerStats == null) return false;
        
        return playerStats.CurrentStamina >= staminaCost;
    }
    
    // Méthode pour effectuer une action qui consomme de la stamina
    public bool PerformStaminaAction(float staminaCost)
    {
        if (playerStats == null) return false;
        
        return playerStats.UseStamina(staminaCost);
    }
    
    // Action de saut consommant de la stamina
    public void JumpAction()
    {
        if (CanPerformStaminaAction(jumpStaminaCost))
        {
            // Exécuter le saut
            PerformStaminaAction(jumpStaminaCost);
            Debug.Log($"Saut effectué! Stamina consommée: {jumpStaminaCost}");
            
            // Code d'animation du saut ou autre logique ici...
            // Par exemple:
            // characterController.Jump();
            // animator.SetTrigger("Jump");
        }
        else
        {
            // Pas assez de stamina pour sauter
            Debug.Log("Pas assez d'énergie pour sauter!");
            ShowMessage("Pas assez d'énergie!");
        }
    }
}