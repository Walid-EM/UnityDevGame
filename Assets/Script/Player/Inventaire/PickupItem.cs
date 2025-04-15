using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public string itemName = "Objet";
    public Sprite itemIcon;
    
    // Vous pouvez ajouter d'autres propriétés selon vos besoins
    public int itemValue = 1;
    public string itemDescription = "Un objet ramassable";
    
    // Cette méthode est appelée quand l'objet est ramassé
    public void Pickup()
    {
        // Ajouter l'objet à l'inventaire
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(this);
        }
        else
        {
            Debug.LogWarning("InventoryManager non trouvé!");
        }
        
        // Détruire l'objet dans la scène au lieu de le désactiver
        Destroy(gameObject);
    }
    
    // Cette méthode est appelée quand l'objet est utilisé depuis la hotbar
    public virtual void UseItem()
    {
        Debug.Log("Utilisation de l'objet : " + itemName);
        
        // Vous pouvez implémenter ici un comportement par défaut
        // ou laisser les classes dérivées surcharger cette méthode
    }
}