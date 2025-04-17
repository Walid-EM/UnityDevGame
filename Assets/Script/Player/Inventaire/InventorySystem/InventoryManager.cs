using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ===============================================================
// Gestionnaire centralisé de l'inventaire
// ===============================================================

public class InventoryManager : MonoBehaviour
{
    // Singleton
    public static InventoryManager Instance { get; private set; }
    
    // Liste des objets dans l'inventaire global
    private List<ItemInstance> inventory = new List<ItemInstance>();
    
    [Header("Hotbar Configuration")]
    public int hotbarSlots = 8;
    public Image[] slotImages;
    public TextMeshProUGUI[] quantityTexts;
    public Color selectedSlotColor = new Color(1f, 1f, 1f, 1f);
    public Color defaultSlotColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
    
    [Header("Messages")]
    public GameObject inventoryFullMessage;
    public float messageDisplayTime = 2f;
    
    // Items dans la hotbar
    private List<ItemInstance> hotbarItems = new List<ItemInstance>();
    private int nextAvailableSlot = 0;
    private int selectedSlot = -1;
    
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
        
        Debug.Log("InventoryManager initialisé");
    }
    
    private void Start()
    {
        // Initialiser la hotbar
        InitializeHotbar();
        
        // Cacher le message d'inventaire plein
        if (inventoryFullMessage != null)
            inventoryFullMessage.SetActive(false);
    }
    
    // ------------------- Méthodes d'inventaire général -------------------

// Ajouter un item à l'inventaire
public void AddItem(int itemID, int quantity = 1)
{
    ItemData itemData = ItemDatabase.Instance.GetItem(itemID);
    if (itemData == null) return;
    
    Debug.Log($"Ajout de {quantity}x {itemData.Name} (ID: {itemID}) à l'inventaire");
    
    // Vérifier si l'item existe déjà dans l'inventaire
    bool itemFound = false;
    
    // Essayer d'empiler si l'item est empilable
    if (itemData.IsStackable)
    {
        // Chercher dans l'inventaire global
        foreach (var item in inventory)
        {
            if (item.itemID == itemID)
            {
                itemFound = true;
                item.quantity += quantity;
                Debug.Log($"Quantité augmentée à {item.quantity}");
                
                // Mettre à jour les UI des slots contenant cet item
                for (int i = 0; i < hotbarItems.Count; i++)
                {
                    if (hotbarItems[i] != null && hotbarItems[i].itemID == itemID)
                    {
                        UpdateSlotUI(i);
                        
                        // Si c'est le slot actuellement sélectionné, réactiver l'équipement
                        if (i == selectedSlot)
                        {
                            OnItemEquipped?.Invoke(hotbarItems[i]);
                        }
                    }
                }
                
                break;
            }
        }
    }
    
    // Si l'item n'a pas été trouvé, créer une nouvelle instance
    if (!itemFound)
    {
        ItemInstance newItem = new ItemInstance(itemID, quantity);
        inventory.Add(newItem);
        
        // Ajouter l'item à la hotbar
        AddItemToHotbar(newItem);
        
        // Si le slot où l'item a été ajouté est le slot sélectionné, équiper l'item
        int slotIndex = GetHotbarIndexByUniqueID(newItem.uniqueID);
        if (slotIndex >= 0 && slotIndex == selectedSlot)
        {
            OnItemEquipped?.Invoke(newItem);
        }
    }
}
    
    // Vérifier si l'inventaire contient un item spécifique
    public bool HasItem(int itemID)
    {
        return inventory.Exists(item => item.itemID == itemID);
    }
    
    // Obtenir la quantité totale d'un item dans l'inventaire
    public int GetItemQuantity(int itemID)
    {
        int totalQuantity = 0;
        foreach (var item in inventory)
        {
            if (item.itemID == itemID)
                totalQuantity += item.quantity;
        }
        return totalQuantity;
    }
    
    // Supprimer un item de l'inventaire par son ID unique
    public bool RemoveItem(int itemID, int quantity = 1)
{
    if (quantity <= 0) return false;
    
    Debug.Log($"Tentative de suppression de {quantity}x item ID: {itemID}");
    int remainingToRemove = quantity;
    
    // Chercher tous les items correspondants
    List<ItemInstance> matchingItems = inventory.FindAll(item => item.itemID == itemID);
    
    if (matchingItems.Count == 0)
    {
        Debug.Log($"Aucun item avec ID {itemID} trouvé dans l'inventaire");
        return false;
    }
    
    // Créer une copie car nous allons modifier pendant l'itération
    List<ItemInstance> itemsToProcess = new List<ItemInstance>(matchingItems);
    
    foreach (var item in itemsToProcess)
    {
        if (item.IsStackable)
        {
            Debug.Log($"Traitement de l'item empilable: {item.Name}, Quantité: {item.quantity}");
            if (item.quantity <= remainingToRemove)
            {
                // Supprimer l'item complètement
                remainingToRemove -= item.quantity;
                Debug.Log($"Suppression complète de l'item (quantité: {item.quantity})");
                
                // Vérifier si cet item est dans la hotbar
                int hotbarIndex = GetHotbarIndexByUniqueID(item.uniqueID);
                if (hotbarIndex >= 0)
                {
                    Debug.Log($"L'item est dans la hotbar au slot {hotbarIndex}");
                    
                    // Le slot deviendra vide car c'était le dernier de cet item
                    hotbarItems[hotbarIndex] = null;
                    UpdateSlotUI(hotbarIndex);
                    
                    // Mettre à jour le prochain slot disponible
                    if (nextAvailableSlot == -1 || hotbarIndex < nextAvailableSlot)
                        nextAvailableSlot = hotbarIndex;
                        
                    // Si c'était le slot sélectionné, déclencher un événement de déséquipement
                    if (hotbarIndex == selectedSlot)
                    {
                        OnItemUnequipped?.Invoke();
                    }
                }
                
                // Supprimer l'item de l'inventaire global
                inventory.Remove(item);
            }
            else
            {
                // Réduire la quantité
                item.quantity -= remainingToRemove;
                Debug.Log($"Réduction de la quantité à {item.quantity}");
                remainingToRemove = 0;
                
                // Mettre à jour l'UI
                UpdateHotbarUI();
            }
        }
        else
        {
            // Item non empilable
            Debug.Log($"Suppression de l'item non empilable: {item.Name}");
            
            // Vérifier si cet item est dans la hotbar
            int hotbarIndex = GetHotbarIndexByUniqueID(item.uniqueID);
            if (hotbarIndex >= 0)
            {
                hotbarItems[hotbarIndex] = null;
                UpdateSlotUI(hotbarIndex);
                
                // Mettre à jour le prochain slot disponible
                if (nextAvailableSlot == -1 || hotbarIndex < nextAvailableSlot)
                    nextAvailableSlot = hotbarIndex;
                    
                // Si c'était le slot sélectionné, déclencher un événement de déséquipement
                if (hotbarIndex == selectedSlot)
                {
                    OnItemUnequipped?.Invoke();
                }
            }
            
            inventory.Remove(item);
            remainingToRemove--;
        }
        
        if (remainingToRemove <= 0) break;
    }
    
    Debug.Log($"Suppression terminée. Reste à supprimer: {remainingToRemove}");
    return remainingToRemove < quantity;
}

// Méthode auxiliaire pour trouver l'index d'un item dans la hotbar
private int GetHotbarIndexByUniqueID(string uniqueID)
{
    for (int i = 0; i < hotbarItems.Count; i++)
    {
        if (hotbarItems[i] != null && hotbarItems[i].uniqueID == uniqueID)
        {
            return i;
        }
    }
    return -1;
}
    
    // ------------------- Méthodes de gestion de la Hotbar -------------------
    
    // Initialiser la hotbar
    private void InitializeHotbar()
    {
        hotbarItems = new List<ItemInstance>();
        
        for (int i = 0; i < hotbarSlots; i++)
        {
            hotbarItems.Add(null);
            
            // Initialiser l'UI
            if (i < slotImages.Length && slotImages[i] != null)
            {
                slotImages[i].sprite = null;
                slotImages[i].enabled = false;
            }
            
            if (i < quantityTexts.Length && quantityTexts[i] != null)
            {
                quantityTexts[i].text = "";
                quantityTexts[i].gameObject.SetActive(false);
            }
        }
        
        nextAvailableSlot = 0;
        Debug.Log($"Hotbar initialisée avec {hotbarSlots} slots");
    }
    
    // Ajouter un item à la hotbar
    private void AddItemToHotbar(ItemInstance item)
    {
        if (item == null) return;
        
        // Si l'item est empilable, chercher s'il existe déjà dans la hotbar
        if (item.IsStackable)
        {
            for (int i = 0; i < hotbarItems.Count; i++)
            {
                if (hotbarItems[i] != null && hotbarItems[i].itemID == item.itemID)
                {
                    // L'item est déjà dans la hotbar, pas besoin de l'ajouter à nouveau
                    UpdateHotbarUI();
                    return;
                }
            }
        }
        
        // Si on arrive ici, l'item n'est pas dans la hotbar
        if (nextAvailableSlot >= 0 && nextAvailableSlot < hotbarItems.Count)
        {
            hotbarItems[nextAvailableSlot] = item;
            Debug.Log($"Item {item.Name} ajouté au slot {nextAvailableSlot}");
            
            // Mettre à jour l'UI
            UpdateSlotUI(nextAvailableSlot);
            
            // Calculer le prochain slot disponible
            UpdateNextAvailableSlot();
        }
        else
        {
            Debug.Log("Hotbar pleine, impossible d'ajouter l'item");
            ShowInventoryFullMessage();
        }
    }
    
    // Mettre à jour l'UI de la hotbar
    public void UpdateHotbarUI()
    {
        for (int i = 0; i < hotbarSlots && i < hotbarItems.Count; i++)
        {
            UpdateSlotUI(i);
        }
    }
    
    // Mettre à jour l'UI d'un slot spécifique
    private void UpdateSlotUI(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= hotbarSlots || slotIndex >= slotImages.Length) return;
        
        ItemInstance item = hotbarItems[slotIndex];
        
        if (item != null)
        {
            // Mettre à jour l'icône
            slotImages[slotIndex].sprite = item.Icon;
            slotImages[slotIndex].enabled = true;
            
            // Mettre à jour la couleur
            slotImages[slotIndex].color = (slotIndex == selectedSlot) ? selectedSlotColor : defaultSlotColor;
            
            // Mettre à jour le texte de quantité
            if (slotIndex < quantityTexts.Length && quantityTexts[slotIndex] != null)
            {
                if (item.IsStackable && item.quantity > 1)
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
            // Slot vide
            slotImages[slotIndex].sprite = null;
            slotImages[slotIndex].enabled = false;
            slotImages[slotIndex].color = defaultSlotColor;
            
            if (slotIndex < quantityTexts.Length && quantityTexts[slotIndex] != null)
            {
                quantityTexts[slotIndex].text = "";
                quantityTexts[slotIndex].gameObject.SetActive(false);
            }
        }
    }
    
    // Calculer le prochain slot disponible
    private void UpdateNextAvailableSlot()
    {
        for (int i = 0; i < hotbarItems.Count; i++)
        {
            if (hotbarItems[i] == null)
            {
                nextAvailableSlot = i;
                return;
            }
        }
        
        nextAvailableSlot = -1; // Aucun slot disponible
    }
    
    // Supprimer un item de la hotbar par son ID unique
    private void RemoveItemFromHotbarByUniqueID(string uniqueID)
    {
        for (int i = 0; i < hotbarItems.Count; i++)
        {
            if (hotbarItems[i] != null && hotbarItems[i].uniqueID == uniqueID)
            {
                hotbarItems[i] = null;
                UpdateSlotUI(i);
                
                // Mettre à jour le prochain slot disponible
                if (nextAvailableSlot == -1 || i < nextAvailableSlot)
                    nextAvailableSlot = i;
                
                break;
            }
        }
    }
    
    // Obtenir l'item dans un slot spécifique
    public ItemInstance GetItemAtSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < hotbarItems.Count)
            return hotbarItems[slotIndex];
        return null;
    }
    
    // Sélectionner un slot et équiper l'item correspondant
    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= hotbarSlots) return;
        
        // Mettre à jour le slot sélectionné
        int previousSlot = selectedSlot;
        selectedSlot = slotIndex;
        
        Debug.Log($"Slot {slotIndex} sélectionné");
        
        // Mettre à jour l'UI
        if (previousSlot >= 0 && previousSlot < slotImages.Length)
            slotImages[previousSlot].color = defaultSlotColor;
        
        if (selectedSlot >= 0 && selectedSlot < slotImages.Length)
            slotImages[selectedSlot].color = selectedSlotColor;
        
        // Équiper l'item
        ItemInstance item = GetItemAtSlot(selectedSlot);
        if (item != null && (item.CanBeEquipped || item.Type == ItemType.Consumable))
        {
            // Notifier les listeners
            OnItemEquipped?.Invoke(item);
        }
        else
        {
            // Déséquiper
            OnItemUnequipped?.Invoke();
        }
    }
    
    // Vérifie si la hotbar est pleine
    public bool IsHotbarFull()
    {
        return nextAvailableSlot == -1;
    }
    
    // ------------------- Messages UI -------------------
    
    // Afficher le message d'inventaire plein
    private void ShowInventoryFullMessage()
    {
        if (inventoryFullMessage != null)
        {
            inventoryFullMessage.SetActive(true);
            Invoke(nameof(HideInventoryFullMessage), messageDisplayTime);
        }
    }
    
    // Cacher le message d'inventaire plein
    private void HideInventoryFullMessage()
    {
        if (inventoryFullMessage != null)
            inventoryFullMessage.SetActive(false);
    }
    
    // ------------------- Événements -------------------
    
    // Événement déclenché quand un item est équipé
    public delegate void ItemEquippedHandler(ItemInstance item);
    public event ItemEquippedHandler OnItemEquipped;
    
    // Événement déclenché quand un item est déséquipé
    public delegate void ItemUnequippedHandler();
    public event ItemUnequippedHandler OnItemUnequipped;
    
    // ------------------- Méthodes d'extension (anciennement dans InventorySystemExtension.cs) -------------------
    
    // Obtenir le slot actuellement sélectionné
    public int GetCurrentSelectedSlot()
    {
        return selectedSlot;
    }
    
    // Récupère un item basé sur son nom (pour la compatibilité avec l'ancien système)
    public bool HasItemByName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return false;
        
        ItemData itemData = ItemDatabase.Instance.GetItemByName(itemName);
        if (itemData == null) return false;
        
        return HasItem(itemData.ID);
    }
    
    // Méthode de debug pour visualiser le contenu de l'inventaire
    public void DebugInventoryContents()
    {
        Debug.Log("==== CONTENU DE L'INVENTAIRE ====");
        Debug.Log($"Items dans l'inventaire: {inventory.Count}");
        
        foreach (var item in inventory)
        {
            string itemInfo = item.IsStackable 
                ? $"{item.Name} (ID: {item.itemID}, Quantité: {item.quantity})" 
                : $"{item.Name} (ID: {item.itemID}, Instance ID: {item.uniqueID})";
                
            Debug.Log(itemInfo);
        }
        
        Debug.Log("==== CONTENU DE LA HOTBAR ====");
        Debug.Log($"Slots: {hotbarSlots}, Slot sélectionné: {selectedSlot}, Prochain disponible: {nextAvailableSlot}");
        
        for (int i = 0; i < hotbarItems.Count; i++)
        {
            string slotInfo = hotbarItems[i] == null 
                ? "Vide" 
                : $"{hotbarItems[i].Name} (ID: {hotbarItems[i].itemID})";
                
            Debug.Log($"Slot {i}: {slotInfo}");
        }
        
        Debug.Log("===============================");
    }
}

// ------------------- Utilitaires pour l'utilisation des items -------------------

public static class ItemUsageHelper
{
    // Utiliser un item directement par son ID
    public static void UseItem(int itemID)
    {
        if (InventoryManager.Instance == null) return;
        
        // Vérifier si l'item est dans l'inventaire
        if (!InventoryManager.Instance.HasItem(itemID))
        {
            Debug.LogWarning($"Item avec ID {itemID} non présent dans l'inventaire");
            return;
        }
        
        // Récupérer les données de l'item
        ItemData itemData = ItemDatabase.Instance.GetItem(itemID);
        if (itemData == null) return;
        
        // Comportement selon le type d'item
        switch (itemData.Type)
        {
            case ItemType.Consumable:
                UseConsumable(itemID);
                break;
                
            case ItemType.Weapon:
            case ItemType.Equipment:
                EquipItem(itemID);
                break;
                
            default:
                Debug.Log($"Pas d'action spécifique pour l'item {itemData.Name} de type {itemData.Type}");
                break;
        }
    }
    
    // Équiper un item
    private static void EquipItem(int itemID)
    {
        if (InventoryManager.Instance == null) return;
        
        // Chercher un slot contenant cet item
        for (int i = 0; i < InventoryManager.Instance.hotbarSlots; i++)
        {
            ItemInstance item = InventoryManager.Instance.GetItemAtSlot(i);
            if (item != null && item.itemID == itemID)
            {
                InventoryManager.Instance.SelectSlot(i);
                return;
            }
        }
        
        Debug.LogWarning($"Item avec ID {itemID} non trouvé dans la hotbar");
    }
    
    // Utiliser un consommable
    private static void UseConsumable(int itemID)
    {
        if (InventoryManager.Instance == null) return;
        
        // Récupérer les données
        ItemData itemData = ItemDatabase.Instance.GetItem(itemID);
        if (itemData == null) return;
        
        Debug.Log($"Consommation de {itemData.Name}");
        
        // Appliquer l'effet
        if (itemData.HealthRestore > 0)
        {
            // Restaurer la santé (à implémenter avec le système de santé)
            Debug.Log($"Santé restaurée: {itemData.HealthRestore}");
        }
        
        // Réduire la quantité
        InventoryManager.Instance.RemoveItem(itemID, 1);
    }
}