using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Référence à la caméra du joueur")]
    public Transform playerCamera;

    [Header("Sensibilité")]
    [Tooltip("Sensibilité horizontale de la souris")]
    public float sensitivityX = 8f;
    [Tooltip("Sensibilité verticale de la souris")]
    public float sensitivityY = 0.5f;
    
    [Header("Limites verticales")]
    [Tooltip("Limite verticale minimale en degrés")]
    public float minimumY = -60f;
    [Tooltip("Limite verticale maximale en degrés")]
    public float maximumY = 60f;
    
    [Header("Lissage")]
    [Tooltip("Activer ou désactiver le lissage")]
    public bool enableSmoothing = true;
    [Tooltip("Facteur de lissage (plus la valeur est basse, plus le mouvement est fluide)")]
    public float smoothTime = 5f;
    
    // Variables privées pour stocker la rotation
    private float rotationX = 0f;
    private float rotationY = 0f;
    
    // Variables pour le lissage
    private float currentRotationX = 0f;
    private float currentRotationY = 0f;
    private float smoothVelocityX = 0f;
    private float smoothVelocityY = 0f;

    private void Start()
    {
        // Verrouiller et cacher le curseur
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Initialiser les rotations avec les valeurs actuelles
        Vector3 rotation = transform.localRotation.eulerAngles;
        rotationX = rotation.y;
        rotationY = rotation.x;
    }

    private void Update()
    {
        // Récupérer les entrées de la souris
        float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;
        
        // Mise à jour des angles de rotation
        rotationX += mouseX;
        rotationY -= mouseY; // Inversé pour avoir un comportement naturel
        
        // Clamp de la rotation verticale pour ne pas dépasser les limites
        rotationY = Mathf.Clamp(rotationY, minimumY, maximumY);
        
        if (enableSmoothing)
        {
            // Appliquer le lissage à la rotation
            currentRotationX = Mathf.SmoothDamp(currentRotationX, rotationX, ref smoothVelocityX, smoothTime * Time.deltaTime);
            currentRotationY = Mathf.SmoothDamp(currentRotationY, rotationY, ref smoothVelocityY, smoothTime * Time.deltaTime);
            
            // Rotation horizontale du joueur (corps)
            transform.rotation = Quaternion.Euler(0f, currentRotationX, 0f);
            
            // Rotation verticale de la caméra seulement
            if (playerCamera != null)
            {
                playerCamera.localRotation = Quaternion.Euler(currentRotationY, 0f, 0f);
            }
        }
        else
        {
            // Application directe sans lissage
            transform.rotation = Quaternion.Euler(0f, rotationX, 0f);
            
            if (playerCamera != null)
            {
                playerCamera.localRotation = Quaternion.Euler(rotationY, 0f, 0f);
            }
        }
    }
    
    // Méthode pour réactiver le curseur (à appeler lors de la pause, menus, etc.)
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    // Méthode pour verrouiller à nouveau le curseur
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}