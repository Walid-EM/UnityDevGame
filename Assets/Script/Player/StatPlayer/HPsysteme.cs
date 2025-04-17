/*using UnityEngine;
using UnityEngine.UI;

public class HealthBarController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Référence à l'Image de la barre de HP")]
    public Image hpBarImage;

    [Header("Paramètres")]
    [Tooltip("Touche pour réduire les HP")]
    public KeyCode damageKey = KeyCode.Space;
    
    [Tooltip("Pourcentage de HP perdu à chaque pression (entre 0 et 1)")]
    [Range(0.01f, 1f)]
    public float damagePercentage = 0.1f;
    
    [Tooltip("Valeur minimale de HP (0 = vide, 1 = plein)")]
    [Range(0f, 1f)]
    public float minHealth = 0f;
    
    [Tooltip("Valeur maximale de HP (0 = vide, 1 = plein)")]
    [Range(0f, 1f)]
    public float maxHealth = 1f;
    
    [Tooltip("HP actuels (modifiable dans l'éditeur)")]
    [Range(0f, 1f)]
    public float currentHealth = 1f;

    private void Start()
    {
        // Vérifier que la référence à l'image de la barre HP existe
        if (hpBarImage == null)
        {
            Debug.LogError("Erreur: HpBar Image n'est pas assignée dans l'inspecteur!");
            enabled = false;
            return;
        }
        
        // Initialiser la barre de HP avec la valeur actuelle
        UpdateHealthBar();
    }

    private void Update()
    {
        // Détecter l'appui sur la touche configurée
        if (Input.GetKeyDown(damageKey))
        {
            // Réduire les HP d'un pourcentage
            TakeDamage(damagePercentage);
        }
    }
    
    // Fonction pour infliger des dégâts et mettre à jour la barre HP
    public void TakeDamage(float damageAmount)
    {
        // Réduire les HP
        currentHealth -= damageAmount;
        
        // Limiter à la valeur minimum
        currentHealth = Mathf.Max(currentHealth, minHealth);
        
        // Mettre à jour l'affichage de la barre
        UpdateHealthBar();
        
        // Afficher l'information dans la console (utile pour le débogage)
        Debug.Log("Dégâts infligés! HP restants: " + (currentHealth * 100) + "%");
        
        // Vérifier si les HP sont à zéro
        if (currentHealth <= minHealth)
        {
            Debug.Log("HP à zéro!");
            // Vous pouvez ajouter ici du code pour gérer la mort ou autre événement
        }
    }
    
    // Fonction pour soigner et mettre à jour la barre HP
    public void Heal(float healAmount)
    {
        // Augmenter les HP
        currentHealth += healAmount;
        
        // Limiter à la valeur maximum
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        
        // Mettre à jour l'affichage de la barre
        UpdateHealthBar();
        
        Debug.Log("Soins appliqués! HP restants: " + (currentHealth * 100) + "%");
    }
    
    // Mettre à jour l'affichage de la barre de HP
    private void UpdateHealthBar()
    {
        if (hpBarImage != null)
        {
            hpBarImage.fillAmount = currentHealth;
        }
    }
}*/