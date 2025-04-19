using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe pour le spawner d'IA dans un environnement généré procéduralement
/// </summary>
public class AISpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnData
    {
        public GameObject aiPrefab;
        public int minCount;
        public int maxCount;
        public float minSpawnDistance = 10f;
        public float maxSpawnDistance = 100f;
        [Range(0f, 1f)]
        public float spawnProbability = 1f;
    }

    [SerializeField] private Transform player;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private AIManager aiManager;
    [SerializeField] private List<SpawnData> spawnDatas = new List<SpawnData>();
    [SerializeField] private int maxSpawnAttempts = 30;
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool autoSpawnOnStart = false;
    [SerializeField] private bool hasSpawned = false; // Pour éviter les spawns multiples

    // Propriétés publiques pour permettre les accès depuis d'autres scripts
    public bool HasSpawned => hasSpawned;

    private void Awake()
    {
        // Si aucun joueur n'est assigné, essayer de le trouver automatiquement
        if (player == null)
        {
            PlayerStats playerStats = Object.FindFirstObjectByType<PlayerStats>();
            if (playerStats != null)
            {
                player = playerStats.transform;
                Debug.Log("AISpawner: Joueur trouvé automatiquement");
            }
        }

        // Si aucun AIManager n'est assigné, essayer de le trouver automatiquement
        if (aiManager == null)
        {
            aiManager = Object.FindFirstObjectByType<AIManager>();
            if (aiManager == null)
            {
                Debug.LogWarning("AISpawner: Aucun AIManager trouvé, création d'une instance.");
                GameObject managerObj = new GameObject("AI Manager");
                aiManager = managerObj.AddComponent<AIManager>();
            }
        }
    }

    private void Start()
    {
        if (autoSpawnOnStart)
        {
            SpawnAIs();
        }
    }

    /// <summary>
    /// Définit le joueur pour le spawner d'IA
    /// </summary>
    /// <param name="playerTransform">Transform du joueur</param>
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    /// <summary>
    /// Génère des IA après la création de la carte
    /// </summary>
    public void SpawnAIs()
    {
        // Si le spawn a déjà été effectué, ne rien faire
        if (hasSpawned)
        {
            if (debugMode)
                Debug.Log("Le spawn des IA a déjà été effectué.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("Référence au joueur manquante dans le AISpawner");
            return;
        }

        if (aiManager == null)
        {
            Debug.LogError("Référence à l'AIManager manquante dans le AISpawner");
            return;
        }

        // Parcourir toutes les données de spawn
        foreach (SpawnData spawnData in spawnDatas)
        {
            if (spawnData.aiPrefab == null) continue;

            // Déterminer le nombre d'IA à spawner pour ce type
            int countToSpawn = Random.Range(spawnData.minCount, spawnData.maxCount + 1);
            
            if (debugMode)
                Debug.Log($"Tentative de spawn de {countToSpawn} instances de {spawnData.aiPrefab.name}");

            for (int i = 0; i < countToSpawn; i++)
            {
                // Appliquer la probabilité de spawn
                if (Random.value > spawnData.spawnProbability) continue;

                // Tenter de spawn l'IA
                TrySpawnAI(spawnData);
            }
        }

        // Marquer que le spawn a été effectué
        hasSpawned = true;

        if (debugMode)
        {
            Debug.Log("Génération des IA terminée");
        }
    }

    /// <summary>
    /// Tente de spawner une IA selon les paramètres donnés
    /// </summary>
    private void TrySpawnAI(SpawnData spawnData)
    {
        // Effectuer plusieurs tentatives pour trouver un point de spawn valide
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Choisir une distance aléatoire du joueur
            float spawnDistance = Random.Range(spawnData.minSpawnDistance, spawnData.maxSpawnDistance);
            
            // Choisir un angle aléatoire
            float spawnAngle = Random.Range(0f, 360f);
            
            // Calculer la position de spawn dans le plan XZ
            Vector3 spawnDirection = Quaternion.Euler(0, spawnAngle, 0) * Vector3.forward;
            Vector3 spawnPos = player.position + spawnDirection * spawnDistance;
            
            // Réaliser un raycast vers le bas pour trouver le sol
            RaycastHit hit;
            if (Physics.Raycast(spawnPos + Vector3.up * 100f, Vector3.down, out hit, 200f, groundLayer))
            {
                spawnPos = hit.point + Vector3.up * 0.1f; // Légèrement au-dessus du sol
                
                // Vérifier qu'aucun obstacle n'est présent à ce point
                if (!Physics.CheckSphere(spawnPos, 1f, obstacleLayer))
                {
                    // Créer l'IA
                    GameObject aiInstance = Instantiate(spawnData.aiPrefab, spawnPos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
                    
                    // Enregistrer l'IA dans le gestionnaire
                    BaseAI aiComponent = aiInstance.GetComponent<BaseAI>();
                    if (aiComponent != null && aiManager != null)
                    {
                        aiManager.RegisterAI(aiComponent);
                    }
                    
                    // S'assurer que l'IA a un HealthSystem
                    if (!aiInstance.GetComponent<HealthSystem>())
                    {
                        HealthSystem healthSystem = aiInstance.AddComponent<HealthSystem>();
                        
                        // CORRECTION: Utiliser instanceof pour vérifier le type d'IA
                        if (aiComponent is SlimeAI)
                        {
                            healthSystem.SetMaxHealth(70f, true);
                            if (debugMode)
                                Debug.Log($"SlimeAI détecté pour {aiInstance.name}, santé configurée à 70");
                        }
                        else if (aiComponent is MeleeAI)
                        {
                            healthSystem.SetMaxHealth(80f, true);
                        }
                        else if (aiComponent is RangedAI)
                        {
                            healthSystem.SetMaxHealth(60f, true);
                        }
                        else if (aiComponent.GetType().Name == "SlimeAI")
                        {
                            healthSystem.SetMaxHealth(40f, true);
                        }
                        else
                        {
                            healthSystem.SetMaxHealth(100f, true);
                            if (debugMode)
                                Debug.Log($"Type d'IA non reconnu: {aiComponent.GetType().Name}");
                        }
                    }
                    
                    if (debugMode)
                    {
                        Debug.Log($"IA {spawnData.aiPrefab.name} spawnée à {spawnPos}");
                    }
                    
                    return; // Spawn réussi
                }
            }
        }
        
        // Si on arrive ici, toutes les tentatives ont échoué
        if (debugMode)
        {
            Debug.LogWarning($"Impossible de trouver un point de spawn valide pour {spawnData.aiPrefab.name} après {maxSpawnAttempts} tentatives");
        }
    }

    /// <summary>
    /// Réinitialise le spawner pour permettre un nouveau spawn
    /// </summary>
    public void ResetSpawner()
    {
        hasSpawned = false;
    }
}