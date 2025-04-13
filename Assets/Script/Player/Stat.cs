using UnityEngine;
using System.Collections;

public class PlayerStats : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    
    [Header("Hunger Settings")]
    public float maxHunger = 100f;
    public float currentHunger;
    public float hungerDecreaseRate = 0.5f; // Diminution par seconde
    
    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaDecreaseRate = 10f; // Diminution lors d'actions
    public float staminaRegenRate = 5f;     // Régénération par seconde
    
    void Start()
    {
        // Initialiser les valeurs au démarrage
        currentHealth = maxHealth;
        currentHunger = maxHunger;
        currentStamina = maxStamina;
        
        StartCoroutine(HungerDecrease());
    }
    
    void Update()
    {
        // Régénération de l'endurance quand le joueur n'utilise pas de stamina
        if (!IsUsingStamina())
        {
            RegenerateStamina();
        }
        
        // Vérifier si la faim affecte la santé
        if (currentHunger <= 0)
        {
            DecreaseHealth(0.5f * Time.deltaTime);
        }
    }
    
    IEnumerator HungerDecrease()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            DecreaseHunger(hungerDecreaseRate);
        }
    }
    
    // Méthodes pour modifier les stats
    public void DecreaseHealth(float amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void IncreaseHealth(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }
    
    public void DecreaseHunger(float amount)
    {
        currentHunger = Mathf.Max(0, currentHunger - amount);
    }
    
    public void IncreaseHunger(float amount)
    {
        currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
    }
    
    public bool UseStamina(float amount)
    {
        if (currentStamina >= amount)
        {
            currentStamina -= amount;
            return true;
        }
        return false;
    }
    
    private void RegenerateStamina()
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + (staminaRegenRate * Time.deltaTime));
    }
    
    // Cette méthode serait remplacée par votre vérification réelle
    private bool IsUsingStamina()
    {
        // Par exemple, vérifier si le joueur court ou saute
        return false;
    }
    
    private void Die()
    {
        // Logique de mort du joueur
        Debug.Log("Le joueur est mort");
    }
}
