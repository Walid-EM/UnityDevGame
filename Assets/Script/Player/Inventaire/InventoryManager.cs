using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    // Singleton
    public static InventoryManager Instance { get; private set; }
    
    // Liste des objets dans l'inventaire
    public List<PickupItemData> inventory = new List<PickupItemData>();
    
    private void Awake()
    {
        // Configuration du singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log("InventoryManager initialisé avec succès");
    }
    
    // Méthode pour ajouter un PickupItem directement (pour la compatibilité)
    public void AddItem(PickupItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("Tentative d'ajouter un item null à l'inventaire");
            return;
        }
        
        // Créer une copie des données
        PickupItemData itemData = new PickupItemData(item);
        AddItemData(itemData);
    }
    
    // Méthode pour ajouter un PickupItemData
    public void AddItemData(PickupItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogWarning("Tentative d'ajouter un itemData null à l'inventaire");
            return;
        }
        
        Debug.Log($"Tentative d'ajout de l'objet: {itemData.itemName} (Empilable: {itemData.isStackable})");
        
        if (itemData.isStackable)
        {
            // Pour les objets empilables, chercher s'il existe déjà dans l'inventaire
            int existingIndex = inventory.FindIndex(item => item.itemName == itemData.itemName);
            
            if (existingIndex >= 0)
            {
                // Si l'objet existe déjà, augmenter sa quantité
                inventory[existingIndex].quantity += itemData.quantity;
                Debug.Log($"Quantité de {itemData.itemName} augmentée à {inventory[existingIndex].quantity}");
                
                // Vérifier si HotbarManager existe avant d'appeler ses méthodes
                if (HotbarManager.Instance != null)
                {
                    HotbarManager.Instance.UpdateHotbarUI();
                }
                
                return;
            }
        }
        
        // Si l'objet n'est pas empilable OU s'il est empilable mais n'existe pas encore,
        // on l'ajoute comme un nouvel objet
        inventory.Add(itemData);
        Debug.Log($"Objet ajouté à l'inventaire: {itemData.itemName} (ID: {itemData.uniqueID})");
        
        // Notifier le HotbarManager pour mettre à jour l'UI
        // Vérifier si HotbarManager existe avant d'appeler ses méthodes
        if (HotbarManager.Instance != null)
        {
            HotbarManager.Instance.AddItemDataToHotbar(itemData);
        }
        else
        {
            Debug.LogWarning("HotbarManager non disponible lors de l'ajout d'un item");
        }
    }
    
    // Vérifier si un objet est dans l'inventaire
    public bool HasItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            Debug.LogWarning("Vérification avec un nom d'item vide ou null");
            return false;
        }
        
        return inventory.Exists(item => item.itemName == itemName);
    }
    
    // Obtenir la quantité totale d'un objet dans l'inventaire
    public int GetItemQuantity(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            Debug.LogWarning("Vérification de quantité avec un nom d'item vide ou null");
            return 0;
        }
        
        int totalQuantity = 0;
        
        foreach (var item in inventory)
        {
            if (item.itemName == itemName)
            {
                totalQuantity += item.quantity;
            }
        }
        
        return totalQuantity;
    }
    
    // Méthode pour supprimer un objet de l'inventaire par son identifiant unique
    public bool RemoveItemByID(string uniqueID)
    {
        if (string.IsNullOrEmpty(uniqueID))
        {
            Debug.LogWarning("Tentative de suppression avec un ID vide ou null");
            return false;
        }
        
        int index = inventory.FindIndex(item => item != null && item.uniqueID == uniqueID);
        
        if (index >= 0)
        {
            Debug.Log($"Objet {inventory[index].itemName} avec ID {uniqueID} trouvé à l'index {index}");
            inventory.RemoveAt(index);
            Debug.Log($"Objet avec ID {uniqueID} supprimé de l'inventaire");
            return true;
        }
        
        Debug.LogWarning($"Tentative de suppression: Aucun objet trouvé avec ID {uniqueID}");
        return false;
    }
}