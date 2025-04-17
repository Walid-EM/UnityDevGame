using UnityEngine;

public class PickupItem : MonoBehaviour
{
    [Header("Configuration de l'item")]
    [Tooltip("ID de l'item dans la base de données")]
    public int itemID;
    
    [Header("Effets visuels")]
    public GameObject pickupEffectPrefab;
    
    // Variables privées
    private string itemName;
    private bool hasBeenPickedUp = false;
    
    private void Start()
    {
        // Récupérer les données de l'item depuis la base de données
        LoadItemData();
    }
    
    private void LoadItemData()
    {
        if (ItemDatabase.Instance == null)
        {
            Debug.LogError("ItemDatabase non disponible");
            return;
        }
        
        ItemData data = ItemDatabase.Instance.GetItem(itemID);
        if (data == null)
        {
            Debug.LogError($"Item avec ID {itemID} non trouvé dans la base de données");
            return;
        }
        
        // Mettre à jour les propriétés
        itemName = data.Name;
    }
    
    // Méthode pour obtenir le nom de l'item
    public string GetItemName()
    {
        if (string.IsNullOrEmpty(itemName))
        {
            LoadItemData();
        }
        return itemName ?? "Item inconnu";
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Vérifier si c'est le joueur qui touche l'objet
        if (other.CompareTag("Player") && !hasBeenPickedUp)
        {
            // La logique de ramassage est maintenant gérée par le PlayerController
            // pour une meilleure séparation des responsabilités
        }
    }
}