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
        inventory.Add(item);
        Debug.Log("Objet ajouté à l'inventaire : " + item.itemName);
    }
    
    // Vérifier si un objet est dans l'inventaire
    public bool HasItem(string itemName)
    {
        return inventory.Exists(item => item.itemName == itemName);
    }
}