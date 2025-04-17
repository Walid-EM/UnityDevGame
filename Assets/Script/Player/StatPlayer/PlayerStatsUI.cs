using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Health UI")]
    public Image healthBar;        // Image avec Fill Method pour la barre de santé
    public TextMeshProUGUI healthText; // Texte optionnel pour afficher la valeur numérique
    
    [Header("Mana UI")]
    public Image manaBar;          // Image avec Fill Method pour la barre de mana
    public TextMeshProUGUI manaText; // Texte optionnel pour afficher la valeur numérique
    
    [Header("Hunger UI")]
    public Image hungerBar;        // Image avec Fill Method pour la barre de faim
    public TextMeshProUGUI hungerText; // Texte optionnel pour afficher la valeur numérique
    
    [Header("Stamina UI")]
    public Image staminaBar;       // Image avec Fill Method pour la barre d'endurance
    public TextMeshProUGUI staminaText; // Texte optionnel pour afficher la valeur numérique
    
    [Header("Visual Effects")]
    public bool useColorGradient = false;
    public Color fullColor = Color.green;
    public Color mediumColor = Color.yellow;
    public Color lowColor = Color.red;
    public float lowThreshold = 0.3f;
    public float mediumThreshold = 0.6f;
    
    [Header("Damage Flash Effect")]
    public Image damageFlashImage;
    public float flashSpeed = 5f;
    public Color flashColor = new Color(1f, 0f, 0f, 0.3f);
    
    [Header("Death Screen")]
    public GameObject deathPanel;
    
    // Référence au système de statistiques
    private PlayerStats playerStats;
    private bool isFlashing = false;
    
    private void Start()
    {
        // Trouver le PlayerStats
        playerStats = FindObjectOfType<PlayerStats>();
        
        if (playerStats == null)
        {
            Debug.LogError("PlayerStats non trouvé dans la scène!");
            return;
        }
        
        // S'abonner aux événements
        playerStats.OnHealthChanged.AddListener(OnHealthChanged);
        playerStats.OnManaChanged.AddListener(OnManaChanged);
        playerStats.OnHungerChanged.AddListener(OnHungerChanged);
        playerStats.OnStaminaChanged.AddListener(OnStaminaChanged);
        playerStats.OnPlayerDeath.AddListener(OnPlayerDeath);
        
        // Initialiser l'UI avec les valeurs actuelles
        OnHealthChanged(playerStats.CurrentHealth, playerStats.MaxHealth);
        OnManaChanged(playerStats.CurrentMana, playerStats.MaxMana);
        OnHungerChanged(playerStats.CurrentHunger, playerStats.MaxHunger);
        OnStaminaChanged(playerStats.CurrentStamina, playerStats.MaxStamina);
        
        // Initialiser l'effet de flash
        if (damageFlashImage != null)
        {
            damageFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        }
        
        // Désactiver le panneau de mort au démarrage
        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
        }
    }
    
    private void OnDestroy()
    {
        // Se désabonner des événements pour éviter les fuites de mémoire
        if (playerStats != null)
        {
            playerStats.OnHealthChanged.RemoveListener(OnHealthChanged);
            playerStats.OnManaChanged.RemoveListener(OnManaChanged);
            playerStats.OnHungerChanged.RemoveListener(OnHungerChanged);
            playerStats.OnStaminaChanged.RemoveListener(OnStaminaChanged);
            playerStats.OnPlayerDeath.RemoveListener(OnPlayerDeath);
        }
    }
    
    // Méthode appelée lorsque la santé du joueur change
    public void OnHealthChanged(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            // Calculer la valeur normalisée entre 0 et 1
            float fillAmount = Mathf.Clamp01(currentHealth / maxHealth);
            
            Debug.Log($"Health updated: Current = {currentHealth}, Max = {maxHealth}, Fill = {fillAmount}");    

            // Mettre à jour le remplissage de l'image
            healthBar.fillAmount = fillAmount;
            
            // Si activé, appliquer un gradient de couleur basé sur la santé
            if (useColorGradient)
            {
                if (fillAmount <= lowThreshold)
                {
                    healthBar.color = lowColor;
                }
                else if (fillAmount <= mediumThreshold)
                {
                    healthBar.color = mediumColor;
                }
                else
                {
                    healthBar.color = fullColor;
                }
            }
            
            // Mettre à jour le texte si présent
            if (healthText != null)
            {
                healthText.text = $"{Mathf.Round(currentHealth)}/{Mathf.Round(maxHealth)}";
            }
        }
        
        Debug.Log($"Santé mise à jour: {currentHealth}/{maxHealth}");
    }
    
    // Méthode appelée lorsque le mana du joueur change
    public void OnManaChanged(float currentMana, float maxMana)
    {
        if (manaBar != null)
        {
            // Calculer la valeur normalisée entre 0 et 1
            float fillAmount = Mathf.Clamp01(currentMana / maxMana);
            
            // Mettre à jour le remplissage de l'image
            manaBar.fillAmount = fillAmount;
            
            // Mettre à jour le texte si présent
            if (manaText != null)
            {
                manaText.text = $"{Mathf.Round(currentMana)}/{Mathf.Round(maxMana)}";
            }
        }
        
        //Debug.Log($"Mana mis à jour: {currentMana}/{maxMana}");
    }
    
    // Méthode appelée lorsque la faim du joueur change
    public void OnHungerChanged(float currentHunger, float maxHunger)
    {
        if (hungerBar != null)
        {
            // Calculer la valeur normalisée entre 0 et 1
            float fillAmount = Mathf.Clamp01(currentHunger / maxHunger);
            
            // Mettre à jour le remplissage de l'image
            hungerBar.fillAmount = fillAmount;
            
            // Si activé, appliquer un gradient de couleur basé sur la faim
            if (useColorGradient)
            {
                if (fillAmount <= lowThreshold)
                {
                    hungerBar.color = lowColor;
                }
                else if (fillAmount <= mediumThreshold)
                {
                    hungerBar.color = mediumColor;
                }
                else
                {
                    hungerBar.color = fullColor;
                }
            }
            
            // Mettre à jour le texte si présent
            if (hungerText != null)
            {
                hungerText.text = $"{Mathf.Round(currentHunger)}/{Mathf.Round(maxHunger)}";
            }
        }
        
        //Debug.Log($"Faim mise à jour: {currentHunger}/{maxHunger}");
    }
    
    // Méthode appelée lorsque l'endurance du joueur change
    public void OnStaminaChanged(float currentStamina, float maxStamina)
    {
        if (staminaBar != null)
        {
            // Calculer la valeur normalisée entre 0 et 1
            float fillAmount = Mathf.Clamp01(currentStamina / maxStamina);
            
            // Mettre à jour le remplissage de l'image
            staminaBar.fillAmount = fillAmount;
            
            // Si activé, appliquer un gradient de couleur basé sur l'endurance
            if (useColorGradient)
            {
                if (fillAmount <= lowThreshold)
                {
                    staminaBar.color = lowColor;
                }
                else if (fillAmount <= mediumThreshold)
                {
                    staminaBar.color = mediumColor;
                }
                else
                {
                    staminaBar.color = fullColor;
                }
            }
            
            // Mettre à jour le texte si présent
            if (staminaText != null)
            {
                staminaText.text = $"{Mathf.Round(currentStamina)}/{Mathf.Round(maxStamina)}";
            }
        }
        
        Debug.Log($"Endurance mise à jour: {currentStamina}/{maxStamina}");
    }
    
    // Méthode pour déclencher manuellement l'effet de flash de dégâts
    public void TriggerDamageFlash()
    {
        if (damageFlashImage != null && !isFlashing)
        {
            StartCoroutine(DamageFlashEffect());
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
    
    // Méthode appelée lors de la mort du joueur
    public void OnPlayerDeath()
    {
        Debug.Log("Le joueur est mort! Affichage de l'écran de game over.");
        
        // Activer l'écran de game over
        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
        }
        
        // Ajouter ici d'autres actions à effectuer lors de la mort (animations, sons, etc.)
    }
}