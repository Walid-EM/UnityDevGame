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
    
    [Header("Stamina et Mana")]
    public float staminaUseRate = 10f;
    public float jumpStaminaCost = 15f;
    public float weaponManaCost = 5f;
    
    // Variables privées
    private Camera playerCamera;
    private PickupItem currentTarget;
    private GameObject currentEquippedObject;
    private ItemInstance currentEquippedItem;
    private PlayerStats playerStats;
    
    private void Start()
    {
        playerCamera = Camera.main;
        
        if (pickupPrompt != null)
            pickupPrompt.SetActive(false);
            
        if (messagePanel != null)
            messagePanel.SetActive(false);
        
        if (handTransform == null)
            Debug.LogWarning("HandTransform non configuré. Les objets ne seront pas visibles en main.");
        
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemEquipped += EquipItem;
            InventoryManager.Instance.OnItemUnequipped += UnequipCurrentItem;
        }
        
        playerStats = GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning("PlayerStats non trouvé! Veuillez ajouter ce composant au joueur.");
            playerStats = gameObject.AddComponent<PlayerStats>();
        }
    }
    
    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemEquipped -= EquipItem;
            InventoryManager.Instance.OnItemUnequipped -= UnequipCurrentItem;
        }
    }
    
    private void Update()
    {
        CheckForInteractable();
        
        if (currentTarget != null && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("E pressed, attempting to pick up " + currentTarget.GetItemName());
            PickupTargetItem();
        }
        
        HandleHotbarInput();
        HandleScrollWheelInput();
        
        if (Input.GetMouseButtonDown(0) && currentEquippedItem != null)
        {
            UseEquippedItem();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            JumpAction();
        }

        CheckEquippedItemExists();
    }
    
    private void CheckEquippedItemExists()
    {
        if (currentEquippedItem != null && InventoryManager.Instance != null)
        {
            int quantity = InventoryManager.Instance.GetItemQuantity(currentEquippedItem.itemID);
            if (quantity <= 0)
            {
                UnequipCurrentItem();
            }
        }
    }

    private void CheckForInteractable()
    {
        RaycastHit hit;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        
        Debug.DrawRay(ray.origin, ray.direction * interactionDistance, Color.red);

        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayers))
        {
            Debug.Log($"Hit: {hit.collider.gameObject.name}, Layer: {hit.collider.gameObject.layer}");

            PickupItem item = hit.collider.GetComponentInParent<PickupItem>();
            
            if (item != null)
            {
                if (currentTarget != item)
                {
                    // Vérifier si c'est une arme et si les emplacements d'armes sont pleins
                    ItemData itemData = ItemDatabase.Instance?.GetItem(item.itemID);
                    if (itemData != null && itemData.Type == ItemType.Weapon && 
                        InventoryManager.Instance != null && InventoryManager.Instance.AreWeaponSlotsFull())
                    {
                        ShowPickupPrompt("Les emplacements d'armes sont pleins!");
                    }
                    else if (InventoryManager.Instance != null && InventoryManager.Instance.IsHotbarFull())
                    {
                        ShowPickupPrompt("Inventaire plein!");
                    }
                    else
                    {
                        ShowPickupPrompt($"Press 'e' to pick up {item.GetItemName()}");
                    }
                    currentTarget = item;
                }
                return;
            }
        }
        
        if (currentTarget != null)
        {
            HidePickupPrompt();
            currentTarget = null;
        }
    }
    
    private void PickupTargetItem()
    {
        if (currentTarget == null) 
            {
                Debug.LogWarning("currentTarget is null");

            return;
            }
        
        if (InventoryManager.Instance == null) return;
        
        // Vérifier si c'est une arme et si les emplacements d'armes sont pleins
        ItemData itemData = ItemDatabase.Instance?.GetItem(currentTarget.itemID);
        if (itemData != null && itemData.Type == ItemType.Weapon && InventoryManager.Instance.AreWeaponSlotsFull())
        {
            ShowMessage("Les emplacements d'armes sont pleins!");
            return;
        }
        
        // Vérifier si c'est un item non-arme et si les emplacements non-armes sont pleins
        if (itemData != null && itemData.Type != ItemType.Weapon && 
            InventoryManager.Instance.IsHotbarFull(itemData.Type))
        {
            ShowMessage("Inventaire plein pour ce type d'objet!");
            return;
        }
        
        InventoryManager.Instance.AddItem(currentTarget.itemID);
        
        if (currentTarget.pickupEffectPrefab != null)
        {
            Instantiate(currentTarget.pickupEffectPrefab, currentTarget.transform.position, Quaternion.identity);
        }
        
        Destroy(currentTarget.gameObject);
        HidePickupPrompt();
        currentTarget = null;
    }
    
    private void ShowPickupPrompt(string message)
    {
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(true);
            if (promptText != null)
            {
                promptText.text = message;
            }
        }
    }
    
    private void HidePickupPrompt()
    {
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }
    }
    
    public void ShowMessage(string message)
    {
        if (messagePanel != null && messageText != null)
        {
            messageText.text = message;
            messagePanel.SetActive(true);
            StartCoroutine(HideMessageAfterDelay());
        }
    }
    
    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDisplayTime);
        
        if (messagePanel != null)
            messagePanel.SetActive(false);
    }
    
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
    
    private void EquipItem(ItemInstance item)
    {
        UnequipCurrentItem();
        
        if (item == null || handTransform == null) return;
        
        GameObject prefabToInstantiate = item.EquipPrefab;
        
        if (prefabToInstantiate == null)
        {
            Debug.LogWarning($"Pas de prefab trouvé pour l'item: {item.Name}");
            return;
        }
        
        currentEquippedObject = Instantiate(prefabToInstantiate, handTransform);
        currentEquippedObject.transform.localPosition = Vector3.zero;
        currentEquippedObject.transform.localRotation = Quaternion.identity;
        
        ConfigureEquippedObjectPhysics(currentEquippedObject);
        
        WeaponSystem weaponSystem = currentEquippedObject.GetComponent<WeaponSystem>();
        if (weaponSystem != null && item.Type == ItemType.Weapon)
        {
            weaponSystem.SetWeaponID(item.itemID);
        }
        
        currentEquippedItem = item;
    }
    
    private void UnequipCurrentItem()
    {
        if (currentEquippedObject != null)
        {
            Destroy(currentEquippedObject);
            currentEquippedObject = null;
            currentEquippedItem = null;
        }
    }
    
    private void ConfigureEquippedObjectPhysics(GameObject obj)
    {
        if (obj == null) return;
        
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (Rigidbody childRb in obj.GetComponentsInChildren<Rigidbody>())
        {
            childRb.isKinematic = true;
            childRb.useGravity = false;
        }
        
        DisableCollidersRecursively(obj);
    }
    
    private void DisableCollidersRecursively(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }
    
    public void UseEquippedItem()
    {
        if (currentEquippedItem == null) return;
        
        switch (currentEquippedItem.Type)
        {
            case ItemType.Weapon:
                UseWeapon();
                break;
                
            case ItemType.Consumable:
                UseConsumable(currentEquippedItem);
                break;
                
            case ItemType.Equipment:
                UseEquipment(currentEquippedItem);
                break;
        }
    }
    
     private void UseWeapon()
    {
        WeaponSystem weaponSystem = currentEquippedObject?.GetComponent<WeaponSystem>();
        if (weaponSystem != null)
        {
            // Cette ligne appelle Attack() qui consommera du mana uniquement lors de l'utilisation
            bool attackResult = weaponSystem.Attack();
            
            if (!attackResult)
            {
                ShowMessage("Pas assez de mana!");
            }
        }
    }
    
    private void UseConsumable(ItemInstance consumable)
    {
        ItemData itemData = consumable.Data;
        bool itemUsed = false;
        
        if (playerStats == null) return;
        
        if (itemData.HealthRestore > 0 && !playerStats.IsFullHealth)
        {
            playerStats.RestoreHealth(itemData.HealthRestore);
            itemUsed = true;
        }
        
        if (itemData.ManaRestore > 0 && !playerStats.IsFullMana)
        {
            playerStats.RestoreMana(itemData.ManaRestore);
            itemUsed = true;
        }
        
        if (itemData.HungerRestore > 0 && !playerStats.IsFullHunger)
        {
            playerStats.RestoreHunger(itemData.HungerRestore);
            itemUsed = true;
        }
        
        if (itemUsed)
        {
            int itemID = consumable.itemID;
            
            if (InventoryManager.Instance != null)
            {
                int currentQuantity = InventoryManager.Instance.GetItemQuantity(itemID);
                
                InventoryManager.Instance.RemoveItem(itemID, 1);
                
                if (currentQuantity <= 1 && currentEquippedItem != null && currentEquippedItem.itemID == itemID)
                {
                    UnequipCurrentItem();
                }
            }
        }
        else
        {
            ShowMessage("Cet item n'a aucun effet sur vous actuellement.");
        }
    }
    
    private void UseEquipment(ItemInstance equipment)
    {
        // Implémenter l'utilisation d'équipement
    }
    
    public bool CanPerformStaminaAction(float staminaCost)
    {
        if (playerStats == null) return false;
        return playerStats.CurrentStamina >= staminaCost;
    }
    
    public bool PerformStaminaAction(float staminaCost)
    {
        if (playerStats == null) return false;
        return playerStats.UseStamina(staminaCost);
    }
    
    public void JumpAction()
    {
        if (CanPerformStaminaAction(jumpStaminaCost))
        {
            PerformStaminaAction(jumpStaminaCost);
            // Le saut est maintenant entièrement géré par PlayerMovement
        }
        else
        {
            ShowMessage("Pas assez d'énergie!");
        }
    }
}