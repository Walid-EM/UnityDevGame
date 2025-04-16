using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public string itemName = "Objet";
    public Sprite itemIcon;
    public int itemValue = 1;
    public string itemDescription = "Un objet ramassable";
    
    // Est-ce que l'objet peut être cumulé ?
    public bool isStackable = false;
    
    // Effet visuel lors du ramassage
    public GameObject pickupEffectPrefab;
    
    // Est-ce que l'objet a déjà été ramassé
    private bool hasBeenPickedUp = false;
    
    private void OnTriggerEnter(Collider other)
    {
        // Vérifier si c'est le joueur qui touche l'objet
        if (other.CompareTag("Player") && !hasBeenPickedUp)
        {
            PickUp();
        }
    }
    
    // Méthode pour ramasser l'objet
    public void PickUp()
    {
        if (hasBeenPickedUp) return;
        
        Debug.Log($"Tentative de ramassage: {itemName}");
        
        // Vérifier si l'InventoryManager existe
        if (InventoryManager.Instance != null)
        {
            // Vérifier si la hotbar est pleine avant d'ajouter l'objet
            if (HotbarManager.Instance != null && HotbarManager.Instance.IsHotbarFull())
            {
                Debug.Log("Inventaire plein, impossible de ramasser l'objet!");
                
                // Afficher le message avec le nouveau système PromptText
                if (PromptText.Instance != null)
                {
                    PromptText.Instance.ShowWarning("Inventaire plein!");
                }
                // Fallback vers l'ancien système si PromptText n'est pas disponible
                else if (HotbarManager.Instance.inventoryFullMessage != null)
                {
                    HotbarManager.Instance.inventoryFullMessage.SetActive(true);
                    HotbarManager.Instance.Invoke("HideInventoryFullMessage", 
                                                 HotbarManager.Instance.messageDisplayTime);
                }
                
                // Ne pas marquer l'objet comme ramassé et ne pas le détruire
                return;
            }
            
            // Si l'inventaire n'est pas plein, procéder normalement
            hasBeenPickedUp = true;
            InventoryManager.Instance.AddItem(this);
            
            // Jouer un effet si disponible
            if (pickupEffectPrefab != null)
            {
                Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
            }
            
            // Détruire l'objet dans le monde
            Destroy(gameObject);
        }
        else
        {
            Debug.LogError("InventoryManager non trouvé ! Impossible d'ajouter l'objet à l'inventaire.");
        }
    }
}