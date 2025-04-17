using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Paramètres de santé")]
    public float maxHealth = 100f;
    public float currentHealth;
    
    [Header("Interface utilisateur")]
    public Image healthBarImage; // Changé de Slider à Image
    public Image damageFlashImage;
    
    [Header("Effets")]
    public float flashSpeed = 5f;
    public Color flashColor = new Color(1f, 0f, 0f, 0.3f);
    
    [Header("Sons")]
    public AudioClip damageSound;
    public AudioClip healSound;
    
    private AudioSource audioSource;
    private bool isFlashing = false;
    
    private void Start()
    {
        // Initialiser la santé
        currentHealth = maxHealth;
        
        // Configurer l'interface utilisateur
        if (healthBarImage != null)
        {
            // S'assurer que l'image est configurée en mode fill
            healthBarImage.type = Image.Type.Filled;
            // Mettre à jour la barre de santé
            UpdateUI();
        }
        
        // Récupérer le composant audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (damageSound != null || healSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Initialiser l'effet de flash
        if (damageFlashImage != null)
        {
            damageFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        }
    }
    
    // Subir des dégâts
    public void TakeDamage(float amount)
    {
        if (amount <= 0) return;
        
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        Debug.Log($"Santé réduite à {currentHealth}/{maxHealth}");
        
        // Mettre à jour l'interface
        UpdateUI();
        
        // Jouer un son
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
        
        // Afficher l'effet de flash
        if (damageFlashImage != null && !isFlashing)
        {
            StartCoroutine(DamageFlashEffect());
        }
        
        // Vérifier si le joueur est mort
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    // Restaurer de la santé
    public void RestoreHealth(float amount)
    {
        if (amount <= 0) return;
        
        float oldHealth = currentHealth;
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        Debug.Log($"Santé augmentée à {currentHealth}/{maxHealth}");
        
        // Jouer un son seulement si la santé a été restaurée
        if (currentHealth > oldHealth && audioSource != null && healSound != null)
        {
            audioSource.PlayOneShot(healSound);
        }
        
        // Mettre à jour l'interface
        UpdateUI();
    }
    
    // Vérifier si les points de vie sont au maximum
    public bool IsFullHealth()
    {
        return currentHealth >= maxHealth;
    }
    
    // Mettre à jour l'interface utilisateur
    private void UpdateUI()
    {
        if (healthBarImage != null)
        {
            // Calculer le ratio de santé actuel
            float healthRatio = currentHealth / maxHealth;
            // Mettre à jour le fill amount de l'image
            healthBarImage.fillAmount = healthRatio;
        }
    }
    
    // Effet de flash pour les dégâts
    private IEnumerator DamageFlashEffect()
    {
        isFlashing = true;
        
        // Définir la couleur avec alpha complet
        damageFlashImage.color = flashColor;
        
        // Faire diminuer l'alpha progressivement
        while (damageFlashImage.color.a > 0)
        {
            damageFlashImage.color = new Color(
                damageFlashImage.color.r,
                damageFlashImage.color.g,
                damageFlashImage.color.b,
                damageFlashImage.color.a - (Time.deltaTime * flashSpeed)
            );
            
            yield return null;
        }
        
        // Garantir que l'alpha est à 0
        damageFlashImage.color = new Color(
            damageFlashImage.color.r,
            damageFlashImage.color.g,
            damageFlashImage.color.b,
            0f
        );
        
        isFlashing = false;
    }
    
    // Gestion de la mort du joueur
    private void Die()
    {
        Debug.Log("Le joueur est mort!");
        
        // Implémenter ici la logique de mort du joueur
        // Par exemple, animation de mort, écran de game over, etc.
    }
}