using UnityEngine;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Paramètres de détection")]
    public float interactionDistance = 3f;
    public LayerMask interactableLayers;
    
    [Header("UI")]
    public GameObject pickupPrompt;
    public TMP_Text promptText;
    
    private Camera playerCamera;
    private PickupItem currentTarget;
    
    private void Start()
    {
        playerCamera = Camera.main;
        
        // Désactiver le prompt au démarrage
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
            
            // S'assurer que le texte est vide au démarrage
            if (promptText != null)
            {
                promptText.text = "";
            }
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
            
            // Désactiver le prompt et effacer le texte
            HidePrompt();
            
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
                ShowPrompt("Press 'e' to pick up " + item.itemName);
                
                currentTarget = item;
                return;
            }
        }
        
        // Si aucun objet n'est ciblé ou si le rayon ne touche rien
        HidePrompt();
        
        currentTarget = null;
    }
    
    // Nouvelle méthode pour afficher le prompt avec un texte spécifique
    private void ShowPrompt(string text)
    {
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(true);
            
            if (promptText != null)
            {
                promptText.text = text;
            }
        }
    }
    
    // Nouvelle méthode pour cacher le prompt et effacer son texte
    private void HidePrompt()
    {
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
            
            // Important : effacer le texte quand on cache le prompt
            if (promptText != null)
            {
                promptText.text = "";
            }
        }
    }
}