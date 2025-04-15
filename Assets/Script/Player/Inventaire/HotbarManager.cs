using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HotbarManager : MonoBehaviour
{
    public static HotbarManager Instance { get; private set; }
    
    [Header("Référence vers l'inventaire")]
    public InventoryManager inventoryManager;
    
    [Header("Configuration de la Hotbar")]
    public int hotbarSlots = 8; // Nombre de slots dans la hotbar
    public Image[] slotImages; // Références aux Images des slots de la hotbar
    
    [Header("Messages")]
    public GameObject inventoryFullMessage; // Référence vers un GameObject avec un texte "Inventaire plein"
    public float messageDisplayTime = 2f; // Durée d'affichage du message en secondes
    
    private List<PickupItem> hotbarItems = new List<PickupItem>();
    private int nextAvailableSlot = 0; // Pour suivre le prochain slot disponible
    
    private void Awake()
    {
        // Configuration du singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }
    
    private void Start()
    {
        // S'assurer que nous avons une référence vers l'InventoryManager
        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null)
            {
                Debug.LogError("Impossible de trouver l'InventoryManager !");
            }
        }
        
        // Initialiser les slots de la hotbar vides
        InitializeHotbar();
        
        // Cacher le message "Inventaire plein" s'il existe
        if (inventoryFullMessage != null)
        {
            inventoryFullMessage.SetActive(false);
        }
    }
    
    private void InitializeHotbar()
    {
        // Vérifier que nous avons suffisamment d'images de slots
        if (slotImages.Length < hotbarSlots)
        {
            Debug.LogWarning("Pas assez d'images de slots assignées !");
            hotbarSlots = slotImages.Length;
        }
        
        // Initialiser chaque slot comme vide
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (slotImages[i] != null)
            {
                slotImages[i].sprite = null;
                slotImages[i].enabled = false; // Désactiver l'image si aucun objet
            }
        }
        
        // Initialiser la liste des objets de la hotbar avec des valeurs null
        hotbarItems = new List<PickupItem>();
        for (int i = 0; i < hotbarSlots; i++)
        {
            hotbarItems.Add(null);
        }
        
        // Réinitialiser le compteur de slot disponible
        nextAvailableSlot = 0;
        
        Debug.Log("Hotbar initialisée avec " + hotbarSlots + " slots vides");
    }
    
    // Cette méthode est appelée pour mettre à jour l'UI sans changer les objets
    public void UpdateHotbarUI()
    {
        // Mettre à jour les images pour chaque slot basé sur les objets dans hotbarItems
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (hotbarItems[i] != null && hotbarItems[i].itemIcon != null)
            {
                // Si un objet existe à ce slot, afficher son icône
                slotImages[i].sprite = hotbarItems[i].itemIcon;
                slotImages[i].enabled = true;
            }
            else
            {
                // Si aucun objet n'est à ce slot, désactiver l'image
                slotImages[i].sprite = null;
                slotImages[i].enabled = false;
            }
        }
        
        // Mettre à jour le prochain slot disponible
        UpdateNextAvailableSlot();
        
        Debug.Log("UI de la hotbar mise à jour");
    }
    
    // Méthode pour trouver le prochain slot disponible
    private void UpdateNextAvailableSlot()
    {

 
        // Rechercher le premier slot vide
        for (int i = 0; i < hotbarSlots; i++)
        {
         
            Debug.Log(hotbarItems[i]);

        }

        // Rechercher le premier slot vide
        for (int i = 0; i < hotbarSlots; i++)
        {
            Debug.Log("Zebi"+i+"zebi"+hotbarItems[i]);
            if (hotbarItems[i] == null)
            {
                nextAvailableSlot = i;
                return;
            }
        }
        // Si on arrive ici, tous les slots sont occupés
        nextAvailableSlot = -1;
        return;
    }
    
    // Méthode pour ajouter directement un objet à la hotbar
    public void AddItemToHotbar(PickupItem item)
    {
        // Vérifier d'abord si l'objet est déjà dans la hotbar (pour éviter les doublons)
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (hotbarItems[i] != null && hotbarItems[i].itemName == item.itemName)
            {
                Debug.Log("Cet objet est déjà dans la hotbar !");
                return; // L'objet est déjà dans la hotbar, on ne fait rien
            }
        }
        
        // Mettre à jour le prochain slot disponible pour s'assurer qu'il est correct
        UpdateNextAvailableSlot();
        
        // Vérifier si la hotbar est pleine
        if (nextAvailableSlot == -1)
        {
            Debug.Log("La hotbar est pleine !");
            ShowInventoryFullMessage();
            return;
        }
        
        // Ajouter l'objet au slot suivant disponible
        hotbarItems[nextAvailableSlot] = item;
        Debug.Log("pornhub,le next avable slot = "+ nextAvailableSlot);
        if (slotImages[nextAvailableSlot] != null && item.itemIcon != null)
        {
            slotImages[nextAvailableSlot].sprite = item.itemIcon;
            slotImages[nextAvailableSlot].enabled = true;
        }
        
        Debug.Log("Objet " + item.itemName + " ajouté au slot " + nextAvailableSlot);
        
        // Mettre à jour le prochain slot disponible pour le prochain objet
        //UpdateNextAvailableSlot();
    }
    
    // Méthode pour afficher le message "Inventaire plein"
    private void ShowInventoryFullMessage()
    {
        if (inventoryFullMessage != null)
        {
            inventoryFullMessage.SetActive(true);
            
            // Cacher le message après un délai
            Invoke("HideInventoryFullMessage", messageDisplayTime);
        }
        else
        {
            // Si aucun GameObject n'est assigné, afficher un message dans la console
            Debug.LogWarning("Inventaire plein ! Assignez un GameObject au champ 'inventoryFullMessage' pour afficher un message à l'écran.");
        }
    }
    
    // Méthode pour cacher le message "Inventaire plein"
    private void HideInventoryFullMessage()
    {
        if (inventoryFullMessage != null)
        {
            inventoryFullMessage.SetActive(false);
        }
    }
    
    // Méthode pour récupérer un objet dans un slot spécifique
    public PickupItem GetItemAtSlot(int index)
    {
        if (index >= 0 && index < hotbarItems.Count)
        {
            return hotbarItems[index];
        }
        return null;
    }
    
    // Méthode pour vérifier si la hotbar est pleine
    public bool IsHotbarFull()
    {
        return nextAvailableSlot == -1;
    }
}