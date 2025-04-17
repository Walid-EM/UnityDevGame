using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    [Header("Interaction")]
    public float interactionDistance = 3f;
    public LayerMask interactableLayers;
    public GameObject pickupPrompt;
    public TMP_Text promptText;
    
    [Header("Équipement")]
    public Transform handTransform;
    
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
    
    // Gestion des touches de hotbar
    private void HandleHotbarInput()
    {
        if (InventoryManager.Instance == null) return;
        
        for (int i = 0; i < Mathf.Min(hotbarKeys.Length, InventoryManager.Instance.hotbarSlots); i++)
        {
            if (Input.GetKeyDown(hotbarKeys[i]))
            {
                InventoryManager.Instance.SelectSlot(i);
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
        
        Debug.Log($"Item équipé: {item.Name}");
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
        if (currentEquippedItem == null) return;
        
        Debug.Log($"Utilisation de l'item: {currentEquippedItem.Name}");
        
        // Comportement spécifique selon le type d'item
        switch (currentEquippedItem.Type)
        {
            case ItemType.Weapon:
                UseWeapon(currentEquippedItem);
                break;
                
            case ItemType.Consumable:
                UseConsumable(currentEquippedItem);
                break;
                
            case ItemType.Equipment:
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
        Debug.Log($"Consommation de {consumable.Name}");
        
        // Appliquer l'effet du consommable
        if (consumable.Data.HealthRestore > 0 && playerHealth != null)
        {
            // Restaurer de la santé
            float healthAmount = consumable.Data.HealthRestore;
            Debug.Log($"Santé restaurée: {healthAmount}");
            
            playerHealth.RestoreHealth(healthAmount);
            
            // Réduire la quantité (supprimer l'item)
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.RemoveItem(consumable.itemID, 1);
                
                // Si c'était l'item équipé, le déséquiper (car il a été consommé)
                if (currentEquippedItem != null && currentEquippedItem.itemID == consumable.itemID)
                {
                    // Déséquiper l'item
                    UnequipCurrentItem();
                    
                    // Désélectionner le slot si nécessaire
                    int currentSlot = InventoryManager.Instance.GetCurrentSelectedSlot();
                    if (currentSlot >= 0)
                    {
                        // Vérifier si le slot est maintenant vide, si oui, désélectionner
                        if (InventoryManager.Instance.GetItemAtSlot(currentSlot) == null)
                        {
                            // Option 1: Laisser le slot sélectionné mais vide
                            // Option 2: Désélectionner le slot (moins courant, mais possible)
                            // InventoryManager.Instance.SelectSlot(-1);
                        }
                    }
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