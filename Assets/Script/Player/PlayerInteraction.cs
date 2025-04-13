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
    }
    
    private void Update()
    {
        // Vérifier si le joueur regarde un objet ramassable
        CheckForInteractable();
        
        // Si un objet est ciblé et que le joueur appuie sur E
        if (currentTarget != null && Input.GetKeyDown(KeyCode.E))
        {
            // Ramasser l'objet
            currentTarget.Pickup();
            
            // Désactiver le prompt
            if (pickupPrompt != null)
            {
                pickupPrompt.SetActive(false);
            }
            
            currentTarget = null;
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
                        promptText.text = " " + item.itemName;
                    }
                }
                
                currentTarget = item;
                return;
            }
        }
        
        // Si aucun objet n'est ciblé ou si le rayon ne touche rien
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }
        
        currentTarget = null;
    }
}