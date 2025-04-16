using UnityEngine;

[System.Serializable]
public class PickupItemData
{
    public string itemName = "Objet";
    public Sprite itemIcon;
    public int itemValue = 1;
    public string itemDescription = "Un objet ramassable";
    
    // Nouvel attribut pour déterminer si l'objet peut être cumulé
    public bool isStackable = false;
    
    // Pour les objets cumulables, on ajoute une quantité
    public int quantity = 1;
    
    // Identifiant unique optionnel pour les objets qui doivent être vraiment uniques
    public string uniqueID = "";
    
    // Constructeur par défaut
    public PickupItemData() { }
    
    // Constructeur de copie
    public PickupItemData(PickupItemData original)
    {
        if (original != null)
        {
            this.itemName = original.itemName;
            this.itemIcon = original.itemIcon;
            this.itemValue = original.itemValue;
            this.itemDescription = original.itemDescription;
            this.isStackable = original.isStackable;
            this.quantity = original.quantity;
            this.uniqueID = original.uniqueID;
            
            // Si l'objet n'est pas empilable, on génère un ID unique
            if (!this.isStackable && string.IsNullOrEmpty(this.uniqueID))
            {
                this.uniqueID = System.Guid.NewGuid().ToString();
            }
        }
    }
    
    // Constructeur à partir d'un PickupItem
    public PickupItemData(PickupItem item)
    {
        if (item != null)
        {
            this.itemName = item.itemName;
            this.itemIcon = item.itemIcon;
            this.itemValue = item.itemValue;
            this.itemDescription = item.itemDescription;
            this.isStackable = item.isStackable;
            this.quantity = 1;
            
            // Si l'objet n'est pas empilable, on génère un ID unique
            if (!this.isStackable)
            {
                this.uniqueID = System.Guid.NewGuid().ToString();
            }
        }
    }
}