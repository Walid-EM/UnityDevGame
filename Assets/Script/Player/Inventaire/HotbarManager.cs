using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotbarManager : MonoBehaviour
{
    public static HotbarManager Instance { get; private set; }
    
    [Header("Référence vers l'inventaire")]
    public InventoryManager inventoryManager;
    
    [Header("Configuration de la Hotbar")]
    public int hotbarSlots = 8; // Nombre de slots dans la hotbar
    public Image[] slotImages; // Références aux Images des slots de la hotbar
    
    [Header("Affichage des quantités")]
    public TextMeshProUGUI[] quantityTexts; // Textes pour afficher les quantités
    
    [Header("Messages")]
    public GameObject inventoryFullMessage; // Référence vers un GameObject avec un texte "Inventaire plein"
    public float messageDisplayTime = 2f; // Durée d'affichage du message en secondes
    
    private List<PickupItemData> hotbarItems = new List<PickupItemData>();
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
            // Essayer de trouver l'instance existante
            inventoryManager = InventoryManager.Instance;
            
            // Si toujours null, loguer une erreur mais ne pas bloquer
            if (inventoryManager == null)
            {
                Debug.LogWarning("InventoryManager non trouvé, certaines fonctionnalités peuvent être limitées");
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
    
    // Méthode pour initialiser la hotbar
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
            
            // Initialiser les textes de quantité si disponibles
            if (quantityTexts != null && i < quantityTexts.Length && quantityTexts[i] != null)
            {
                quantityTexts[i].text = "";
                quantityTexts[i].gameObject.SetActive(false);
            }
        }
        
        // Réinitialiser la liste des objets de la hotbar
        hotbarItems.Clear();
        
        // Remplir avec des valeurs null
        for (int i = 0; i < hotbarSlots; i++)
        {
            hotbarItems.Add(null);
        }
        
        // Réinitialiser le compteur de slot disponible
        nextAvailableSlot = 0;
        
        Debug.Log($"Hotbar initialisée avec {hotbarSlots} slots vides");
        DebugHotbarState(); // Afficher l'état initial pour débogage
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
                
                // Mettre à jour le texte de quantité si c'est un objet empilable
                if (quantityTexts != null && i < quantityTexts.Length && quantityTexts[i] != null)
                {
                    if (hotbarItems[i].isStackable && hotbarItems[i].quantity > 1)
                    {
                        quantityTexts[i].text = hotbarItems[i].quantity.ToString();
                        quantityTexts[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        quantityTexts[i].text = "";
                        quantityTexts[i].gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                // Si aucun objet n'est à ce slot, désactiver l'image
                slotImages[i].sprite = null;
                slotImages[i].enabled = false;
                
                // Désactiver le texte de quantité
                if (quantityTexts != null && i < quantityTexts.Length && quantityTexts[i] != null)
                {
                    quantityTexts[i].text = "";
                    quantityTexts[i].gameObject.SetActive(false);
                }
            }
        }
        
        // Mettre à jour le prochain slot disponible
        UpdateNextAvailableSlot();
        
        Debug.Log("UI de la hotbar mise à jour");
    }
    
    // Méthode pour trouver le prochain slot disponible
    private void UpdateNextAvailableSlot()
    {
        // Afficher l'état actuel des slots pour débogage
        Debug.Log("État actuel des slots de la hotbar:");
        for (int i = 0; i < hotbarSlots; i++)
        {
            string itemStatus = (hotbarItems[i] != null) ? $"{hotbarItems[i].itemName} (x{hotbarItems[i].quantity})" : "vide";
            Debug.Log($"Slot {i}: {itemStatus}");
        }
        
        // Parcourir les slots pour trouver le premier disponible
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (hotbarItems[i] == null)
            {
                nextAvailableSlot = i;
                Debug.Log($"Prochain slot disponible trouvé: {nextAvailableSlot}");
                return;
            }
        }
        
        // Si aucun slot n'est disponible
        nextAvailableSlot = -1;
        Debug.Log("La hotbar est pleine, aucun slot disponible.");
    }
    
    // Méthode pour la compatibilité avec l'ancien système
    public void AddItemToHotbar(PickupItem item)
    {
        // Créer une copie des données
        PickupItemData itemData = new PickupItemData(item);
        AddItemDataToHotbar(itemData);
    }
    
    // Méthode pour ajouter un PickupItemData à la hotbar
    public void AddItemDataToHotbar(PickupItemData item)
    {
        // Vérification du prefab
        if (item != null) {
            if (item.equipPrefab != null) {
                Debug.Log($"HotbarManager: Objet {item.itemName} a un prefab: {item.equipPrefab.name}");
            } else {
                Debug.LogWarning($"HotbarManager: Objet {item.itemName} n'a PAS de prefab");
            }
        }
        
        Debug.Log($"Tentative d'ajout de l'objet: {item.itemName} (Empilable: {item.isStackable})");
        
        // Si l'objet est empilable, vérifier s'il existe déjà dans la hotbar
        if (item.isStackable)
        {
            for (int i = 0; i < hotbarSlots; i++)
            {
                if (hotbarItems[i] != null && hotbarItems[i].itemName == item.itemName)
                {
                    // Mettre à jour la quantité
                    hotbarItems[i].quantity += item.quantity;
                    Debug.Log($"Quantité de {item.itemName} augmentée à {hotbarItems[i].quantity} dans le slot {i}");
                    
                    // Mettre à jour l'UI
                    UpdateHotbarUI();
                    return;
                }
            }
        }
        
        // Si on arrive ici, soit l'objet n'est pas empilable, soit il est empilable mais n'existe pas encore dans la hotbar
        
        // 1. Vérifier le prochain slot disponible
        UpdateNextAvailableSlot();
        Debug.Log($"Résultat de UpdateNextAvailableSlot: prochain slot = {nextAvailableSlot}");
        
        // 2. Vérifier si la hotbar est pleine
        if (nextAvailableSlot == -1)
        {
            Debug.Log("Impossible d'ajouter l'objet: la hotbar est pleine!");
            ShowInventoryFullMessage();
            return;
        }
        
        // 3. Ajouter l'objet au slot disponible
        int slotIndex = nextAvailableSlot;
        Debug.Log($"Ajout de {item.itemName} au slot {slotIndex}");
        
        // S'assurer que l'index est valide
        if (slotIndex >= 0 && slotIndex < hotbarSlots && slotIndex < hotbarItems.Count)
        {
            hotbarItems[slotIndex] = item;
            
            // Mettre à jour l'image du slot
            if (slotImages[slotIndex] != null && item.itemIcon != null)
            {
                slotImages[slotIndex].sprite = item.itemIcon;
                slotImages[slotIndex].enabled = true;
                Debug.Log($"Image pour {item.itemName} définie dans le slot {slotIndex}");
                
                // Mettre à jour le texte de quantité si c'est un objet empilable
                if (quantityTexts != null && slotIndex < quantityTexts.Length && quantityTexts[slotIndex] != null)
                {
                    if (item.isStackable && item.quantity > 1)
                    {
                        quantityTexts[slotIndex].text = item.quantity.ToString();
                        quantityTexts[slotIndex].gameObject.SetActive(true);
                    }
                    else
                    {
                        quantityTexts[slotIndex].text = "";
                        quantityTexts[slotIndex].gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Problème avec l'image de l'objet {item.itemName} ou du slot {slotIndex}");
            }
            
            // Mise à jour du prochain slot disponible APRÈS avoir ajouté l'objet
            UpdateNextAvailableSlot();
            Debug.Log($"Après ajout, prochain slot disponible = {nextAvailableSlot}");
        }
        else
        {
            Debug.LogError($"Index de slot invalide: {slotIndex}. Hotbar slots: {hotbarSlots}, Hotbar items count: {hotbarItems.Count}");
        }
    }
    
    // Méthode pour afficher l'état de la hotbar
    public void DebugHotbarState()
    {
        Debug.Log("--- ÉTAT ACTUEL DE LA HOTBAR ---");
        Debug.Log($"Nombre de slots: {hotbarSlots}, Prochain slot disponible: {nextAvailableSlot}");
        for (int i = 0; i < hotbarItems.Count; i++)
        {
            string itemInfo = "vide";
            if (hotbarItems[i] != null)
            {
                itemInfo = hotbarItems[i].isStackable ? 
                    $"{hotbarItems[i].itemName} (x{hotbarItems[i].quantity})" : 
                    $"{hotbarItems[i].itemName} (ID: {hotbarItems[i].uniqueID})";
            }
            Debug.Log($"Slot {i}: {itemInfo}");
        }
        Debug.Log("--------------------------------");
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
    public PickupItemData GetItemAtSlot(int index)
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
    
    // Méthode publique pour supprimer un objet d'un slot
    public void RemoveItemFromSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < hotbarItems.Count)
        {
            hotbarItems[slotIndex] = null;
            Debug.Log($"Objet retiré du slot {slotIndex}");
            UpdateHotbarUI();
        }
    } 
}