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
    public GameObject messagePanel; // Nouveau panel pour afficher les messages système
    public TMP_Text messageText;    // Texte du message
    public float messageDisplayTime = 2f; // Durée d'affichage du message
    
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
    
    // Variables privées
    private Camera playerCamera;
    private PickupItem currentTarget;
    private GameObject currentEquippedObject;
    private ItemInstance currentEquippedItem;
    private PlayerHealth playerHealth; // Référence au système de santé
    
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
        
        // Récupérer la référence au système de santé
        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogWarning("PlayerHealth non trouvé! Les effets des items ne seront pas appliqués.");
        }
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
    private void ShowMessage(string message)
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
                Debug.Log($"Dans le slot hotbarKeys[{i}]");
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
                UseWeapon(currentEquippedItem);
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
    private void UseWeapon(ItemInstance weapon)
    {
        // Récupérer les dégâts depuis la base de données
        float damage = weapon.Data.WeaponDamage;
        Debug.Log($"Attaque avec {weapon.Name} ! Dégâts: {damage}");
        
        // S'infliger des dégâts à soi-même
        if (playerHealth != null && damage > 0)
        {
            playerHealth.TakeDamage(damage);
        }
    }
    
    // Utiliser un consommable
    private void UseConsumable(ItemInstance consumable)
    {
        Debug.Log($"Tentative de consommation de {consumable.Name}");
        
        // Vérifier si l'effet du consommable peut être appliqué
        if (consumable.Data.HealthRestore > 0 && playerHealth != null)
        {
            // Vérifier si les HP sont déjà au maximum
            if (playerHealth.IsFullHealth())
            {
                Debug.Log("Santé déjà au maximum, consommable non utilisé");
                ShowMessage("Votre santé est déjà au maximum!");
                return;
            }
            
            // Restaurer de la santé
            float healthAmount = consumable.Data.HealthRestore;
            Debug.Log($"Santé restaurée: {healthAmount}");
            
            playerHealth.RestoreHealth(healthAmount);
            
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
    }
    
    // Utiliser un équipement
    private void UseEquipment(ItemInstance equipment)
    {
        Debug.Log($"Utilisation de l'équipement {equipment.Name}");
        
        // Implémenter l'utilisation d'équipement
        // Par exemple, activer une capacité spéciale
    }
}