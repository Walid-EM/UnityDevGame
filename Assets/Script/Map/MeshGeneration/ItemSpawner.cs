using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ItemSpawner : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int maxItemsPerChunk = 5; // Nombre maximum d'items par chunk
    [SerializeField] private bool showDebugInfo = false;

    private ItemDatabase itemDatabase;
    private TerrainGenerator terrainGenerator;

    // Dictionnaire pour suivre le nombre d'items spawnés par type et par chunk
    private Dictionary<Vector2, Dictionary<int, int>> itemCountByChunk = new Dictionary<Vector2, Dictionary<int, int>>();

    private void Awake()
    {
        // Récupérer la base de données d'items
        itemDatabase = ItemDatabase.Instance;
        if (itemDatabase == null)
        {
            Debug.LogError("ItemSpawner: Impossible de trouver la base de données d'items!");
        }

        // Obtenir une référence au générateur de terrain
        terrainGenerator = FindFirstObjectByType<TerrainGenerator>();
        if (terrainGenerator == null)
        {
            Debug.LogError("ItemSpawner: Impossible de trouver le TerrainGenerator!");
            return;
        }
    }

    private void Start()
    {
        // S'abonner à l'événement de création de chunk du TerrainGenerator
        terrainGenerator.RegisterItemSpawner(this);
        
        if (showDebugInfo)
            Debug.Log("ItemSpawner: Initialisé et prêt à spawner des items dans les chunks");
    }

    /// <summary>
    /// Méthode appelée par le TerrainGenerator quand un nouveau chunk est créé
    /// </summary>
    public void OnChunkCreated(TerrainChunk chunk, Vector3 chunkPosition, Vector3 chunkSize)
    {
        if (showDebugInfo)
            Debug.Log($"ItemSpawner: Nouveau chunk créé à {chunkPosition}, génération d'items...");
        
        // Obtenir les coordonnées du chunk
        Vector2 chunkCoord = new Vector2(
            Mathf.RoundToInt(chunkPosition.x / terrainGenerator.meshSettings.meshWorldSize),
            Mathf.RoundToInt(chunkPosition.z / terrainGenerator.meshSettings.meshWorldSize)
        );
        
        // Initialiser le compteur pour ce chunk s'il n'existe pas
        if (!itemCountByChunk.ContainsKey(chunkCoord))
        {
            itemCountByChunk[chunkCoord] = new Dictionary<int, int>();
        }
        
        // Attendre que le collider soit généré avant de spawner les items
        StartCoroutine(SpawnItemsWithDelay(chunk, chunkPosition, chunkSize, chunkCoord));
    }

    /// <summary>
    /// Coroutine qui attend un délai avant de spawner les items pour s'assurer que les colliders sont générés
    /// </summary>
    private IEnumerator SpawnItemsWithDelay(TerrainChunk chunk, Vector3 chunkPosition, Vector3 chunkSize, Vector2 chunkCoord)
    {
        // Attendre un délai pour que les colliders soient générés
        float delayTime = 1.0f;
        
        if (showDebugInfo)
            Debug.Log($"ItemSpawner: Attente de {delayTime}s pour génération complète du chunk à {chunkCoord}");
        
        yield return new WaitForSeconds(delayTime);
        
        if (showDebugInfo)
            Debug.Log($"ItemSpawner: Délai terminé, spawn des items sur le chunk à {chunkCoord}");
        
        SpawnItemsInChunk(chunkPosition, chunkSize, chunkCoord);
    }

    /// <summary>
    /// Spawn des items dans un chunk nouvellement créé
    /// </summary>
    public void SpawnItemsInChunk(Vector3 chunkPosition, Vector3 chunkSize, Vector2 chunkCoord)
    {
        if (itemDatabase == null)
            return;

        // Récupérer tous les items disponibles
        List<ItemData> allItems = itemDatabase.GetAllItems();
        
        // Filtrer uniquement les items qui ont une chance de spawn supérieure à 0
        List<ItemData> spawnableItems = new List<ItemData>();
        foreach (var item in allItems)
        {
            if (item.SpawnChance > 0f)
            {
                spawnableItems.Add(item);
            }
        }

        if (spawnableItems.Count == 0)
        {
            if (showDebugInfo)
                Debug.Log("Aucun item avec une chance de spawn > 0 trouvé dans la base de données");
            return;
        }

        // Déterminer combien d'items vont être générés dans ce chunk (entre 0 et maxItemsPerChunk)
        int itemsToSpawn = Random.Range(0, maxItemsPerChunk + 1);

        if (showDebugInfo && itemsToSpawn > 0)
            Debug.Log($"Spawn de {itemsToSpawn} items dans le chunk à {chunkPosition}");

        // Spawner les items
        int attemptsPerItem = 5; // Nombre de tentatives par item en cas d'échec (item déjà au max)
        int successfulSpawns = 0;
        
        for (int i = 0; i < itemsToSpawn; i++)
        {
            // Essayer plusieurs fois au cas où on tombe sur un item déjà au maximum
            for (int attempt = 0; attempt < attemptsPerItem; attempt++)
            {
                if (SpawnRandomItem(chunkPosition, chunkSize, spawnableItems, chunkCoord))
                {
                    successfulSpawns++;
                    break;
                }
            }
        }
        
        if (showDebugInfo && successfulSpawns > 0)
            Debug.Log($"Spawné avec succès {successfulSpawns} items sur {itemsToSpawn} planifiés");
    }

    /// <summary>
    /// Tente de spawner un seul item aléatoire dans une zone donnée
    /// </summary>
    /// <returns>True si l'item a été spawné avec succès, false sinon</returns>
    private bool SpawnRandomItem(Vector3 centerPosition, Vector3 chunkSize, List<ItemData> spawnableItems, Vector2 chunkCoord)
    {
        // Sélectionner un item aléatoirement avec prise en compte des probabilités
        ItemData selectedItem = GetRandomItemByProbability(spawnableItems);
        if (selectedItem == null)
            return false;
            
        // Vérifier si on a déjà atteint le maximum pour ce type d'item dans ce chunk
        int currentCount = 0;
        Dictionary<int, int> itemCounts = itemCountByChunk[chunkCoord];
        if (itemCounts.TryGetValue(selectedItem.ID, out currentCount))
        {
            // Utiliser la valeur MaxInstancesPerChunk définie dans l'ItemData
            if (currentCount >= selectedItem.MaxInstancesPerChunk)
            {
                if (showDebugInfo)
                    Debug.Log($"Item {selectedItem.Name} (ID: {selectedItem.ID}) a déjà atteint le maximum de {selectedItem.MaxInstancesPerChunk} dans ce chunk");
                return false;
            }
        }

        // Obtenir une position aléatoire dans le chunk
        Vector3 spawnPosition = GetRandomPositionInChunk(centerPosition, chunkSize);

        // Instancier l'item dans le monde
        if (selectedItem.WorldPrefab != null)
        {
            GameObject spawnedItem = Instantiate(selectedItem.WorldPrefab, spawnPosition, Quaternion.identity);
            
            // Incrémenter le compteur pour ce type d'item dans ce chunk
            if (itemCounts.ContainsKey(selectedItem.ID))
                itemCounts[selectedItem.ID]++;
            else
                itemCounts[selectedItem.ID] = 1;
            
            if (showDebugInfo)
                Debug.Log($"Item {selectedItem.Name} (ID: {selectedItem.ID}) spawné à {spawnPosition}. Total dans ce chunk: {itemCounts[selectedItem.ID]}/{selectedItem.MaxInstancesPerChunk}");
            
            // Vérifier la position de l'item après le spawn pour s'assurer qu'il ne traverse pas le sol
            StartCoroutine(VerifyItemPosition(spawnedItem, spawnPosition));
                
            return true;
        }
        else
        {
            Debug.LogWarning($"Impossible de spawner l'item {selectedItem.Name} : WorldPrefab manquant");
            return false;
        }
    }

    /// <summary>
    /// Vérifie que l'item est correctement positionné et le repositionne si nécessaire
    /// </summary>
    private IEnumerator VerifyItemPosition(GameObject item, Vector3 originalPosition)
    {
        // Attendre une frame pour que la physique s'applique
        yield return null;
        
        // Vérifier si l'objet est tombé ou a traversé le terrain
        if (item != null)
        {
            // Si l'objet est tombé trop bas, le repositionner
            if (item.transform.position.y < originalPosition.y - 5f)
            {
                // Refaire un raycast pour trouver une meilleure position
                Vector3 rayStart = new Vector3(originalPosition.x, originalPosition.y + 50f, originalPosition.z);
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f))
                {
                    // Placer l'objet sur la surface avec un offset plus important
                    item.transform.position = new Vector3(hit.point.x, hit.point.y + 1.0f, hit.point.z);
                    
                    if (showDebugInfo)
                        Debug.Log($"Item repositionné à {item.transform.position} après avoir traversé le sol");
                }
            }
        }
    }

    /// <summary>
    /// Retourne une position aléatoire à l'intérieur d'un chunk
    /// </summary>
    private Vector3 GetRandomPositionInChunk(Vector3 chunkCenter, Vector3 chunkSize)
    {
        // Génération d'une position aléatoire dans le chunk
        float halfSizeX = chunkSize.x * 0.5f;
        float halfSizeZ = chunkSize.z * 0.5f;

        float randomX = Random.Range(chunkCenter.x - halfSizeX, chunkCenter.x + halfSizeX);
        float randomZ = Random.Range(chunkCenter.z - halfSizeZ, chunkCenter.z + halfSizeZ);
        
        // Faire un rayon pour trouver le sol
        Vector3 rayStart = new Vector3(randomX, chunkCenter.y + 100f, randomZ); // Augmenter la hauteur du raycast
        
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 150f)) // Augmenter la distance du raycast
        {
            // Calculer l'offset en fonction de la normale (plus plat = moins d'offset)
            float normalOffset = Vector3.Dot(hit.normal, Vector3.up) >= 0.9f ? 0.5f : 1.0f;
            
            // Obtenir une valeur par défaut pour la hauteur de l'item
            float itemHeight = 0.5f;
            
            // Ajuster l'offset en fonction de la pente et de la taille de l'objet
            return new Vector3(randomX, hit.point.y + normalOffset + itemHeight * 0.5f, randomZ);
        }
        
        // Si aucun sol n'est trouvé, utiliser la même coordonnée Y que le centre du chunk
        Debug.LogWarning($"Aucun sol trouvé pour la position ({randomX}, {chunkCenter.y}, {randomZ})");
        return new Vector3(randomX, chunkCenter.y, randomZ);
    }

    /// <summary>
    /// Sélectionne un item aléatoirement en tenant compte des probabilités de spawn
    /// </summary>
    private ItemData GetRandomItemByProbability(List<ItemData> items)
    {
        // Calculer la somme totale des probabilités
        float totalProbability = 0f;
        foreach (var item in items)
        {
            totalProbability += item.SpawnChance;
        }

        // Si la somme est trop faible, retourner null
        if (totalProbability <= 0.001f)
            return null;

        // Sélectionner un nombre aléatoire entre 0 et la somme totale
        float randomValue = Random.Range(0f, totalProbability);
        float currentProbability = 0f;

        // Parcourir les items et sélectionner celui qui correspond à la valeur aléatoire
        foreach (var item in items)
        {
            currentProbability += item.SpawnChance;
            if (randomValue <= currentProbability)
            {
                return item;
            }
        }

        // Si on arrive ici, retourner le dernier item (normalement, cela ne devrait pas arriver)
        return items[items.Count - 1];
    }
    
    /// <summary>
    /// Méthode publique pour déclencher manuellement un spawn d'item à une position spécifique
    /// </summary>
    public GameObject SpawnItemAtPosition(Vector3 position, int itemID = -1)
    {
        if (itemDatabase == null)
            return null;

        ItemData itemToSpawn;
        
        // Sélectionner l'item à spawner
        if (itemID >= 0)
        {
            // Item spécifique demandé
            itemToSpawn = itemDatabase.GetItem(itemID);
            if (itemToSpawn == null)
                return null;
        }
        else
        {
            // Item aléatoire
            List<ItemData> allItems = itemDatabase.GetAllItems();
            List<ItemData> spawnableItems = new List<ItemData>();
            
            foreach (var item in allItems)
            {
                if (item.SpawnChance > 0f)
                {
                    spawnableItems.Add(item);
                }
            }
            
            if (spawnableItems.Count == 0)
                return null;
                
            itemToSpawn = GetRandomItemByProbability(spawnableItems);
        }

        // Spawner l'item
        if (itemToSpawn != null && itemToSpawn.WorldPrefab != null)
        {
            GameObject spawnedItem = Instantiate(itemToSpawn.WorldPrefab, position, Quaternion.identity);
            
            // Vérifier la position après le spawn
            StartCoroutine(VerifyItemPosition(spawnedItem, position));
            
            return spawnedItem;
        }
        
        return null;
    }

    /// <summary>
    /// Réinitialise le compteur d'items pour un chunk spécifique
    /// Utile pour les tests ou si les items sont détruits
    /// </summary>
    public void ResetChunkItemCount(Vector2 chunkCoord)
    {
        if (itemCountByChunk.ContainsKey(chunkCoord))
        {
            itemCountByChunk[chunkCoord].Clear();
        }
    }
}