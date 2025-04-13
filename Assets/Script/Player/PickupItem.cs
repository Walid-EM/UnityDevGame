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
        
        // Désactiver l'objet dans la scène
        gameObject.SetActive(false);
    }
}