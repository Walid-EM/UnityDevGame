using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [Header("Configuration")]
    public float maxHealth = 100f;
    public float currentHealth;
    
    [Header("UI")]
    public Image healthBarImage; // Référence à l'image qui sera masquée (Mask)
    public TextMeshProUGUI healthText; // Optionnel
    
    [Header("Effects")]
    public GameObject damageEffect;
    public GameObject healEffect;
    
    // RectTransform de la barre de santé (alternative à fillAmount)
    private RectTransform healthBarRect;
    private float initialWidth;
    
    private void Start()
    {
        currentHealth = maxHealth;
        
        // Vérifiez si healthBarImage est assigné
        if (healthBarImage == null)
        {
            Debug.LogError("healthBarImage n'est pas assigné! Veuillez le configurer dans l'inspecteur.");
        }
        else
        {
            Debug.Log("healthBarImage est correctement assigné: " + healthBarImage.name);
            
            // Initialiser le RectTransform
            healthBarRect = healthBarImage.rectTransform;
            
            if (healthBarRect != null)
            {
                initialWidth = healthBarRect.sizeDelta.x;
                Debug.Log("RectTransform initialisé. Largeur initiale: " + initialWidth);
            }
            else
            {
                Debug.LogError("healthBarRect est null malgré une image valide!");
            }
        }
        
        UpdateHealthUI();
        
        // Test automatique du système après 1 seconde
        Invoke("TestHealthSystem", 1.0f);
    }
    
    public void TakeDamage(float amount)
    {
        float oldHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"TakeDamage appelé: {amount} de dégâts. Santé: {oldHealth} -> {currentHealth}/{maxHealth}");
        
        UpdateHealthUI();
        
        if (damageEffect != null)
            Instantiate(damageEffect, transform.position, Quaternion.identity);
            
        if (currentHealth <= 0)
            OnPlayerDeath();
    }
    
    public void RestoreHealth(float amount)
    {
        float oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Debug.Log($"RestoreHealth appelé: {amount} de santé restaurée. Santé: {oldHealth} -> {currentHealth}/{maxHealth}");
        
        UpdateHealthUI();
        
        if (healEffect != null)
            Instantiate(healEffect, transform.position, Quaternion.identity);
    }
    
    private void UpdateHealthUI()
    {
        // Calculer le pourcentage de santé (vérifiez que maxHealth n'est pas 0)
        float healthPercentage = (maxHealth > 0) ? currentHealth / maxHealth : 0;
        
        Debug.Log($"UpdateHealthUI appelé: currentHealth={currentHealth}, maxHealth={maxHealth}, pourcentage={healthPercentage}");
        
        // N'utilisez qu'une seule méthode à la fois pour déboguer
        if (healthBarImage != null)
        {
            float oldFillAmount = healthBarImage.fillAmount;
            healthBarImage.fillAmount = healthPercentage;
            Debug.Log($"fillAmount mis à jour: {oldFillAmount} -> {healthPercentage} pour l'image {healthBarImage.name}");
        }
        else
        {
            Debug.LogError("healthBarImage est null lors de l'appel à UpdateHealthUI!");
        }
        
        // Mettre à jour par RectTransform aussi par sécurité
        if (healthBarRect != null)
        {
            Vector2 oldSize = healthBarRect.sizeDelta;
            Vector2 newSize = healthBarRect.sizeDelta;
            newSize.x = initialWidth * healthPercentage;
            healthBarRect.sizeDelta = newSize;
            Debug.Log($"Largeur RectTransform mise à jour: {oldSize.x} -> {newSize.x}");
        }
        
        // Mettre à jour le texte
        if (healthText != null)
            healthText.text = $"{Mathf.Round(currentHealth)}/{maxHealth}";
    }
    
    private void OnPlayerDeath()
    {
        Debug.Log("Le joueur est mort!");
        // Implémentez la logique de mort ici
    }
    private void TestHealing()
    {
        RestoreHealth(maxHealth * 0.1f);
    }
    
    // Pour tests manuels dans l'éditeur
    [ContextMenu("Infliger 10 dégâts")]
    private void DebugTakeDamage()
    {
        TakeDamage(10f);
    }
    
    [ContextMenu("Restaurer 10 santé")]
    private void DebugRestoreHealth()
    {
        RestoreHealth(10f);
    }
}