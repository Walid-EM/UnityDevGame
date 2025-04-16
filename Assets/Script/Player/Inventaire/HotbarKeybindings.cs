using UnityEngine;
using System.Collections.Generic;

public class HotbarKeybindings : MonoBehaviour
{
    [Header("Références")]
    public HotbarManager hotbarManager;
    
    [Header("Configuration")]
    [Tooltip("Définir les touches pour chaque slot (doit correspondre au nombre de slots dans HotbarManager)")]
    public KeyCode[] slotKeys = new KeyCode[8] 
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
    
    [Header("Feedback visuel")]
    public bool showSelectedSlot = true;
    public Color selectedSlotColor = new Color(1f, 1f, 1f, 1f);
    public Color defaultSlotColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
    
    private int currentSelectedSlot = -1; // -1 signifie qu'aucun slot n'est sélectionné
    
    private void Start()
    {
        // S'assurer que nous avons une référence vers l'HotbarManager
        if (hotbarManager == null)
        {
            hotbarManager = HotbarManager.Instance;
            if (hotbarManager == null)
            {
                Debug.LogError("Impossible de trouver l'HotbarManager !");
                return;
            }
        }
        
        // Vérifier que nous avons assez de touches configurées
        if (slotKeys.Length < hotbarManager.hotbarSlots)
        {
            Debug.LogWarning("Pas assez de touches assignées pour tous les slots de la hotbar !");
        }
        
        // Initialiser le système de sélection
        ResetSlotColors();
    }
    
    private void Update()
    {
        // Vérifier si une des touches configurées est pressée
        for (int i = 0; i < Mathf.Min(slotKeys.Length, hotbarManager.hotbarSlots); i++)
        {
            if (Input.GetKeyDown(slotKeys[i]))
            {
                SelectSlot(i);
                break;
            }
        }
        
        // Support pour la sélection par molette de souris (optionnel)
        float scrollWheel = Input.GetAxis("Mouse ScrollWheel");
        if (scrollWheel != 0)
        {
            int direction = scrollWheel > 0 ? -1 : 1; // Inverser si nécessaire
            int newSlot = (currentSelectedSlot + direction) % hotbarManager.hotbarSlots;
            if (newSlot < 0) newSlot = hotbarManager.hotbarSlots - 1; // Boucler vers le dernier slot
            
            SelectSlot(newSlot);
        }
    }
    
    /// <summary>
    /// Sélectionne un slot spécifique de la hotbar
    /// </summary>
    private void SelectSlot(int slotIndex)
    {
        // Vérifier que l'index est valide
        if (slotIndex < 0 || slotIndex >= hotbarManager.hotbarSlots)
        {
            Debug.LogWarning("Index de slot invalide : " + slotIndex);
            return;
        }
        
        // Mettre à jour le slot sélectionné
        int previousSlot = currentSelectedSlot;
        currentSelectedSlot = slotIndex;
        
        Debug.Log("Slot " + slotIndex + " sélectionné");
        
        // Mettre à jour la visualisation des slots si activé
        if (showSelectedSlot)
        {
            // Réinitialiser le slot précédemment sélectionné
            if (previousSlot >= 0 && previousSlot < hotbarManager.slotImages.Length)
            {
                hotbarManager.slotImages[previousSlot].color = defaultSlotColor;
            }
            
            // Mettre en évidence le nouveau slot sélectionné
            if (currentSelectedSlot >= 0 && currentSelectedSlot < hotbarManager.slotImages.Length)
            {
                hotbarManager.slotImages[currentSelectedSlot].color = selectedSlotColor;
            }
        }
        
        // Utiliser l'objet si présent dans ce slot
        UseSelectedItem();
    }
    
    /// <summary>
    /// Utilise l'objet actuellement sélectionné dans la hotbar
    /// </summary>
    private void UseSelectedItem()
    {
        // Si aucun slot n'est sélectionné, ne rien faire
        if (currentSelectedSlot < 0 || currentSelectedSlot >= hotbarManager.hotbarSlots)
        {
            return;
        }
        
        // Récupérer l'objet dans le slot sélectionné
        PickupItemData selectedItem = GetItemAtSlot(currentSelectedSlot);
        
        if (selectedItem != null)
        {
            Debug.Log("Utilisation de l'objet : " + selectedItem.itemName);
            //selectedItem.UseItem();
        }
        else
        {
            Debug.Log("Aucun objet dans le slot " + currentSelectedSlot);
        }
        
    }
    
    /// <summary>
    /// Récupère l'objet dans un slot spécifique de la hotbar
    private PickupItemData GetItemAtSlot(int slotIndex)
    {
    if (hotbarManager != null)
    {
        return hotbarManager.GetItemAtSlot(slotIndex);
    }
    return null;
    }
    /// <summary>
    /// Réinitialise les couleurs de tous les slots à la valeur par défaut
    /// </summary>
    private void ResetSlotColors()
    {
        if (hotbarManager != null && showSelectedSlot)
        {
            for (int i = 0; i < hotbarManager.slotImages.Length; i++)
            {
                if (hotbarManager.slotImages[i] != null)
                {
                    hotbarManager.slotImages[i].color = defaultSlotColor;
                }
            }
        }
    }
}