using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire d'IA pour gérer l'activation/désactivation en fonction de la distance au joueur
/// </summary>
public class AIManager : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float activationDistance = 50f;
    [SerializeField] private float checkInterval = 1f;

    private List<BaseAI> allAIs = new List<BaseAI>();
    private float timer = 0f;

    private void Awake()
    {
        // Si aucun joueur n'est assigné, essayer de le trouver automatiquement
        if (player == null)
        {
            PlayerStats playerStats = FindObjectOfType<PlayerStats>();
            if (playerStats != null)
            {
                player = playerStats.transform;
                Debug.Log("AIManager: Joueur trouvé automatiquement");
            }
            else
            {
                Debug.LogWarning("AIManager: Aucun joueur trouvé. Veuillez assigner manuellement la référence au joueur.");
            }
        }
    }

    private void Update()
    {
        if (player == null) return;

        // Vérifier périodiquement les IA à activer/désactiver
        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;
            UpdateActiveAIs();
        }
    }

    /// <summary>
    /// Met à jour les IA actives en fonction de la distance au joueur
    /// </summary>
    private void UpdateActiveAIs()
    {
        if (player == null) return;

        foreach (var ai in allAIs)
        {
            if (ai == null) continue;

            float distanceToPlayer = Vector3.Distance(ai.transform.position, player.position);
            bool shouldBeActive = distanceToPlayer <= activationDistance;

            // Activer/désactiver l'IA
            ai.SetActive(shouldBeActive);
        }
    }

    /// <summary>
    /// Enregistre une nouvelle IA dans le gestionnaire
    /// </summary>
    public void RegisterAI(BaseAI ai)
    {
        if (!allAIs.Contains(ai))
        {
            allAIs.Add(ai);
        }
    }

    /// <summary>
    /// Supprime une IA du gestionnaire
    /// </summary>
    public void UnregisterAI(BaseAI ai)
    {
        allAIs.Remove(ai);
    }

    /// <summary>
    /// Cherche et enregistre automatiquement toutes les IA de la scène
    /// </summary>
    public void FindAndRegisterAllAIs()
    {
        BaseAI[] sceneAIs = FindObjectsOfType<BaseAI>();
        foreach (BaseAI ai in sceneAIs)
        {
            RegisterAI(ai);
        }
        Debug.Log($"AIManager: {sceneAIs.Length} IA trouvées et enregistrées");
    }
}