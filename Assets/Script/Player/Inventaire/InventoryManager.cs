using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    // Singleton
    public static InventoryManager Instance { get; private set; }
    
    // Liste des objets dans l'inventaire
    public List<PickupItem> inventory = new List<PickupItem>();
    
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
    }
    
    // Ajouter un objet à l'inventaire
    public void AddItem(PickupItem item)
    {
        // Vérifier si l'objet est déjà dans l'inventaire
        bool alreadyInInventory = inventory.Exists(existingItem => existingItem.itemName == item.itemName);
        
        if (!alreadyInInventory)
        {
            // Si l'objet n'est pas déjà dans l'inventaire, l'ajouter
            inventory.Add(item);
            Debug.Log("Objet ajouté à l'inventaire : " + item.itemName);
            
            // Notifier le HotbarManager pour mettre à jour l'UI
            if (HotbarManager.Instance != null)
            {
                HotbarManager.Instance.AddItemToHotbar(item);
            }
        }
        else
        {
            Debug.Log("L'objet " + item.itemName + " est déjà dans l'inventaire");
            // Ici, vous pourriez gérer l'incrémentation d'une quantité si vous implémentez cette fonctionnalité
        }
    }
    
    // Vérifier si un objet est dans l'inventaire
    public bool HasItem(string itemName)
    {
        return inventory.Exists(item => item.itemName == itemName);
    }
}