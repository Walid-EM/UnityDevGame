using UnityEngine;

// Cette classe aide à utiliser les objets basés sur PickupItemData
public class ItemUsageHelper : MonoBehaviour
{
    public static ItemUsageHelper Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    // Méthode pour utiliser un objet depuis la hotbar
    public void UseItem(PickupItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogWarning("Tentative d'utiliser un objet null!");
            return;
        }
        
        Debug.Log($"Utilisation de l'objet: {itemData.itemName}");
        
        // Vous pouvez implémenter ici différentes actions selon le type d'objet
        // Par exemple:
        switch (itemData.itemName.ToLower())
        {
            case "sword":
                UseWeapon(itemData);
                break;
            case "potion":
                UseConsumable(itemData);
                break;
            case "key":
                UseKey(itemData);
                break;
            default:
                Debug.Log($"Action par défaut pour {itemData.itemName}");
                break;
        }
        
        // Si c'est un consommable et qu'il est empilable, réduire sa quantité
        if (itemData.isStackable && IsConsumable(itemData.itemName))
        {
            DecreaseItemQuantity(itemData);
        }
    }
    
    // Vérifier si un objet est consommable
    private bool IsConsumable(string itemName)
    {
        // Liste des objets consommables
        string[] consumables = { "potion", "food", "ammo", "scroll" };
        
        foreach (string consumable in consumables)
        {
            if (itemName.ToLower().Contains(consumable))
            {
                return true;
            }
        }
        
        return false;
    }
    
    // Réduire la quantité d'un objet
    private void DecreaseItemQuantity(PickupItemData itemData)
    {
        if (itemData == null) return;

        itemData.quantity--;
        Debug.Log($"Quantité de {itemData.itemName} réduite à {itemData.quantity}");
        
        // Si la quantité atteint zéro, supprimer l'objet
        if (itemData.quantity <= 0)
        {
            // Trouver dans quel slot de la hotbar se trouve cet objet
            int slotIndex = -1;
            if (HotbarManager.Instance != null)
            {
                for (int i = 0; i < HotbarManager.Instance.hotbarSlots; i++)
                {
                    PickupItemData slotItem = HotbarManager.Instance.GetItemAtSlot(i);
                    if (slotItem == itemData)
                    {
                        slotIndex = i;
                        break;
                    }
                }
                
                if (slotIndex >= 0)
                {
                    // Utiliser la méthode publique pour retirer l'objet du slot
                    HotbarManager.Instance.RemoveItemFromSlot(slotIndex);
                    Debug.Log($"Objet {itemData.itemName} retiré du slot {slotIndex}");
                }
            }
            else
            {
                Debug.LogWarning("HotbarManager non disponible lors de la suppression d'un item");
            }
            
            // Supprimer l'objet de l'inventaire aussi
            if (InventoryManager.Instance != null)
            {
                bool removed = InventoryManager.Instance.RemoveItemByID(itemData.uniqueID);
                if (removed)
                {
                    Debug.Log($"Objet {itemData.itemName} supprimé de l'inventaire");
                }
                else
                {
                    Debug.LogWarning($"Impossible de supprimer l'objet {itemData.itemName} de l'inventaire");
                }
            }
            else
            {
                Debug.LogWarning("InventoryManager non disponible lors de la suppression d'un item");
            }
        }
        else
        {
            // Mettre à jour l'UI
            if (HotbarManager.Instance != null)
            {
                HotbarManager.Instance.UpdateHotbarUI();
            }
        }
    }
    
    // Exemples de différentes actions d'utilisation
    private void UseWeapon(PickupItemData weaponData)
    {
        Debug.Log($"Attaque avec {weaponData.itemName} ! Dégâts potentiels: {weaponData.itemValue}");
        // Implémentez ici la logique d'attaque
    }
    
    private void UseConsumable(PickupItemData consumableData)
    {
        Debug.Log($"Consommation de {consumableData.itemName} ! Effet: {consumableData.itemDescription}");
        // Implémentez ici l'effet de consommation (soins, etc.)
    }
    
    private void UseKey(PickupItemData keyData)
    {
        Debug.Log($"Utilisation de {keyData.itemName} pour déverrouiller quelque chose!");
        // Implémentez ici la logique de déverrouillage
    }
}