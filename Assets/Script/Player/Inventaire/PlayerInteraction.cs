using UnityEngine;
using TMPro; // Utilise TextMeshPro à la place de UnityEngine.UI.Text

public class PlayerInteraction : MonoBehaviour
{
    [Header("Paramètres de détection")]
    public float interactionDistance = 3f;
    public LayerMask interactableLayers;
    
    [Header("UI")]
    public GameObject pickupPrompt;
    public TMP_Text promptText; // Utilise TMP_Text au lieu de Text
    
    [Header("Équipement")]
    public Transform handTransform; // Emplacement où l'objet sera tenu
    private GameObject currentEquippedObject; // L'objet actuellement en main
    private PickupItemData currentEquippedItem; // Les données de l'objet équipé
    
    private Camera playerCamera;
    private PickupItem currentTarget;
    
    private void Start()
    {
        playerCamera = Camera.main;
        
        // Désactiver le prompt au démarrage
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }
        
        // Vérifier si handTransform est configuré
        if (handTransform == null)
        {
            Debug.LogWarning("HandTransform non configuré dans PlayerInteraction. Les objets ne seront pas visibles en main.");
        }
    }
    
    private void Update()
    {
        // Vérifier si le joueur regarde un objet ramassable
        CheckForInteractable();
        
        // Si un objet est ciblé et que le joueur appuie sur E
        if (currentTarget != null && Input.GetKeyDown(KeyCode.E))
        {
            // Ramasser l'objet
            currentTarget.PickUp();
            
            // Désactiver le prompt
            if (pickupPrompt != null)
            {
                pickupPrompt.SetActive(false);
            }
            
            currentTarget = null;
        }
        
        // Gestion des touches numériques pour les slots de hotbar (1-8)
        for (int i = 0; i < 8; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                EquipItemFromHotbarSlot(i);
            }
        }
    }
    
    private void CheckForInteractable()
    {
        RaycastHit hit;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        
        // Lancer un rayon devant le joueur
        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayers))
        {
            // Vérifier si l'objet touché est ramassable
            PickupItem item = hit.collider.GetComponentInParent<PickupItem>();
            
            if (item != null)
            {
                // Afficher le prompt
                if (pickupPrompt != null)
                {
                    pickupPrompt.SetActive(true);
                    if (promptText != null)
                    {
                        promptText.text = "Press 'e' to pick up " + item.itemName;
                    }
                }
                
                currentTarget = item;
                return;
            }
        }
        
        // Si le rayon ne touche pas d'objet ramassable, désactiver le prompt et effacer le texte
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
            if (promptText != null)
            {
                promptText.text = ""; // Effacer le texte lorsque le joueur ne regarde plus d'objet
            }
        }
        
        currentTarget = null;
    }
    
    // Méthode pour équiper un objet depuis un slot de la hotbar
    public void EquipItemFromHotbarSlot(int slotIndex)
    {
        // Vérifier si HotbarManager existe
        if (HotbarManager.Instance == null)
        {
            Debug.LogWarning("HotbarManager non disponible lors de l'équipement d'un item");
            return;
        }
        
        // Obtenir l'objet du slot
        PickupItemData itemData = HotbarManager.Instance.GetItemAtSlot(slotIndex);
        
        if (itemData != null)
        {
            // Équiper l'objet
            EquipItem(itemData);
            
            Debug.Log($"Objet équipé depuis le slot {slotIndex}: {itemData.itemName}");
        }
        else
        {
            // Si le slot est vide, déséquiper l'objet actuel
            UnequipCurrentItem();
            Debug.Log($"Slot {slotIndex} vide, objet déséquipé");
        }
    }
    
    // Méthode pour équiper un objet
    public void EquipItem(PickupItemData itemData)
    {
        // Ne rien faire si les données sont nulles
        if (itemData == null)
        {
            Debug.LogWarning("Tentative d'équiper un itemData null");
            return;
        }
            
        // Si l'objet n'est pas équipable, ne rien faire
        if (!itemData.canBeEquipped)
        {
            Debug.Log($"L'objet {itemData.itemName} ne peut pas être équipé");
            return;
        }
            
        // Si l'objet actuel est le même, ne rien faire
        if (currentEquippedItem != null && currentEquippedItem.uniqueID == itemData.uniqueID)
        {
            Debug.Log($"L'objet {itemData.itemName} est déjà équipé");
            return;
        }
            
        // Déséquiper l'objet actuel si nécessaire
        UnequipCurrentItem();
        
        // Vérifier si handTransform est configuré
        if (handTransform == null)
        {
            Debug.LogWarning("Impossible d'équiper l'objet: handTransform non configuré");
            return;
        }
        
        // Vérifier si equipPrefab est configuré
        if (itemData.equipPrefab == null)
        {
            // Si equipPrefab n'est pas configuré, essayer de le trouver
            GameObject prefab = FindItemPrefab(itemData.itemName);
            if (prefab == null)
            {
                Debug.LogWarning($"Aucun prefab trouvé pour l'objet: {itemData.itemName}");
                return;
            }
            
            // Instancier le prefab trouvé
            currentEquippedObject = Instantiate(prefab, handTransform);
        }
        else
        {
            // Instancier le prefab configuré
            currentEquippedObject = Instantiate(itemData.equipPrefab, handTransform);
        }
        
        // Positionner correctement l'objet
        currentEquippedObject.transform.localPosition = Vector3.zero;
        currentEquippedObject.transform.localRotation = Quaternion.identity;
        
        //désactiver le rigidbody
        Rigidbody rb = currentEquippedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
        rb.isKinematic = true;
        rb.useGravity = false;
        }

        //désactiver le rigidbody des prefabs enfants également
        foreach (Rigidbody childRb in currentEquippedObject.GetComponentsInChildren<Rigidbody>())
        {
            childRb.isKinematic = true;
            childRb.useGravity = false;
        }
        
        // Désactiver les colliders pour éviter les interactions avec le joueur
        DisableCollidersRecursively(currentEquippedObject);
        
        // Stocker les données de l'objet équipé
        currentEquippedItem = itemData;
        
        Debug.Log($"Objet équipé: {itemData.itemName}");
    }
    
    // Méthode pour déséquiper l'objet actuel
    public void UnequipCurrentItem()
    {
        if (currentEquippedObject != null)
        {
            Destroy(currentEquippedObject);
            currentEquippedObject = null;
            currentEquippedItem = null;
            Debug.Log("Objet déséquipé");
        }
    }
    
    // Méthode pour trouver le prefab correspondant à un objet (à adapter à votre système)
    private GameObject FindItemPrefab(string itemName)
    {
        // Cette méthode doit être adaptée à votre système de préfabs
        // Vous pouvez utiliser un dictionnaire, des scriptable objects, ou des resources
        
        // Exemple d'implémentation simple basée sur les Resources
        // Vous devez avoir des prefabs dans un dossier Resources/Items
        GameObject prefab = Resources.Load<GameObject>($"Items/{itemName}");
        
        if (prefab == null)
        {
            // Tentative de fallback sur un nom générique (par exemple "default_item")
            prefab = Resources.Load<GameObject>("Items/default_item");
            
            if (prefab == null)
            {
                Debug.LogWarning($"Aucun prefab trouvé pour {itemName} et pas de fallback disponible");
                return null;
            }
            
            Debug.LogWarning($"Prefab pour {itemName} non trouvé, utilisation du prefab par défaut");
        }
        
        return prefab;
    }
    
    // Désactiver les colliders de manière récursive
    private void DisableCollidersRecursively(GameObject obj)
    {
        // Désactiver les colliders sur l'objet et tous ses enfants
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }
}