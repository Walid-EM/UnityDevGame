using System.Collections;
using UnityEngine;
using TMPro;

public class PromptText : MonoBehaviour
{
    public static PromptText Instance { get; private set; }
    
    [Header("Configuration")]
    public TextMeshProUGUI promptTextUI;
    public float defaultDisplayTime = 2f;
    public Color defaultTextColor = Color.white;
    public Color warningColor = Color.red;
    
    [Header("Animation (optionnel)")]
    public bool useAnimation = true;
    public float fadeInTime = 0.2f;
    public float fadeOutTime = 0.3f;
    
    private void Awake()
    {
        // Configuration du singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }
    
    private void Start()
    {
        // Vérifier que le composant TextMeshProUGUI est assigné
        if (promptTextUI == null)
        {
            Debug.LogError("PromptText: TextMeshProUGUI n'est pas assigné!");
        }
        else
        {
            // Cacher le texte au démarrage
            promptTextUI.gameObject.SetActive(false);
        }
    }
    
    // Méthode pour afficher un message avec les paramètres par défaut
    public void ShowMessage(string message)
    {
        ShowMessage(message, defaultDisplayTime, defaultTextColor);
    }
    
    // Méthode pour afficher un message d'avertissement
    public void ShowWarning(string message)
    {
        ShowMessage(message, defaultDisplayTime, warningColor);
    }
    
    // Méthode complète pour afficher un message avec tous les paramètres
    public void ShowMessage(string message, float displayTime, Color textColor)
    {
        if (promptTextUI == null) return;
        
        // Arrêter toutes les coroutines en cours (pour éviter les conflits)
        StopAllCoroutines();
        
        // Définir le texte et la couleur
        promptTextUI.text = message;
        promptTextUI.color = textColor;
        
        // Afficher le message avec ou sans animation
        if (useAnimation)
        {
            StartCoroutine(AnimateMessage(displayTime));
        }
        else
        {
            promptTextUI.gameObject.SetActive(true);
            StartCoroutine(HideAfterDelay(displayTime));
        }
    }
    
    // Coroutine pour animer l'apparition et la disparition du message
    private IEnumerator AnimateMessage(float displayTime)
    {
        // Préparer pour animation
        promptTextUI.gameObject.SetActive(true);
        Color startColor = promptTextUI.color;
        startColor.a = 0f;
        promptTextUI.color = startColor;
        
        // Animation de fade in
        float elapsedTime = 0f;
        while (elapsedTime < fadeInTime)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / fadeInTime);
            Color newColor = promptTextUI.color;
            newColor.a = alpha;
            promptTextUI.color = newColor;
            yield return null;
        }
        
        // Attendre la durée d'affichage
        yield return new WaitForSeconds(displayTime);
        
        // Animation de fade out
        elapsedTime = 0f;
        while (elapsedTime < fadeOutTime)
        {
            elapsedTime += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsedTime / fadeOutTime);
            Color newColor = promptTextUI.color;
            newColor.a = alpha;
            promptTextUI.color = newColor;
            yield return null;
        }
        
        // Cacher le texte
        promptTextUI.gameObject.SetActive(false);
    }
    
    // Coroutine pour cacher le message après un délai (sans animation)
    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        promptTextUI.gameObject.SetActive(false);
    }
}