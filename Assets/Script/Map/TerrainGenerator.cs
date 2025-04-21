using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TerrainGenerator : MonoBehaviour {

	const float viewerMoveThresholdForChunkUpdate = 25f;
	const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;


	public int colliderLODIndex;
	public LODInfo[] detailLevels;

	public MeshSettings meshSettings;
	public HeightMapSettings heightMapSettings;
	public TextureData textureSettings;

	public Transform viewer;
	public Material mapMaterial;

	private ItemSpawner itemSpawner;

	/// <summary>
	/// Enregistre le spawner d'items et connecte les événements de création de chunks
	/// </summary>
	public void RegisterItemSpawner(ItemSpawner spawner)
	{
		this.itemSpawner = spawner;
		
		// Connecter l'événement onChunkCreated pour tous les chunks existants
		foreach (var entry in terrainChunkDictionary)
		{
			TerrainChunk chunk = entry.Value;
			chunk.onChunkCreated += spawner.OnChunkCreated;
		}
		
		Debug.Log("TerrainGenerator: ItemSpawner enregistré avec succès");
	}


	// le spawn des IA
	[Header("IA Spawning")]
	public bool spawnAIAfterGeneration = true;

	// Spawn D'Item
	[Header("Item Spawning")]
	public bool spawnItemsAfterGeneration = true;


	Vector2 viewerPosition;
	Vector2 viewerPositionOld;

	float meshWorldSize;
	int chunksVisibleInViewDst;

	Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
	List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

	void Start() {

		 textureSettings.ApplyToMaterial(mapMaterial);
    	textureSettings.UpdateMeshHeights(mapMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

   		float maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
    	meshWorldSize = meshSettings.meshWorldSize;
    	chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / meshWorldSize);

    	UpdateVisibleChunks();

		// Démarrer la coroutine pour attendre que le terrain soit généré
		if (spawnAIAfterGeneration) {
			StartCoroutine(WaitForTerrainAndSpawnAI());
		}

		// Démarer la coroutine pour le spawn d'items
		if (spawnItemsAfterGeneration) {
        StartCoroutine(WaitForTerrainAndSpawnItems());
    	}
	}

	void Update() {
		viewerPosition = new Vector2(viewer.position.x, viewer.position.z);

		if (viewerPosition != viewerPositionOld) {
			foreach (TerrainChunk chunk in visibleTerrainChunks) {
				chunk.UpdateCollisionMesh();
			}
		}

		if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate) {
			viewerPositionOld = viewerPosition;
			UpdateVisibleChunks();
		}
	}
		
	void UpdateVisibleChunks() {
		HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
		for (int i = visibleTerrainChunks.Count-1; i >= 0; i--) {
			alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
			visibleTerrainChunks[i].UpdateTerrainChunk();
		}
			
		int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / meshWorldSize);
		int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / meshWorldSize);

		for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++) {
			for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++) {
				Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
				if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord)) {
					if (terrainChunkDictionary.ContainsKey(viewedChunkCoord)) {
						terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
					} else {
						TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, heightMapSettings, meshSettings, detailLevels, colliderLODIndex, transform, viewer, mapMaterial);
						terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
						newChunk.onVisibilityChanged += OnTerrainChunkVisibilityChanged;

						if (itemSpawner != null) {
            			newChunk.onChunkCreated += itemSpawner.OnChunkCreated;
        				}

						newChunk.Load();
					}
				}
			}
		}
	}

	void OnTerrainChunkVisibilityChanged(TerrainChunk chunk, bool isVisible) {
		if (isVisible) {
			visibleTerrainChunks.Add(chunk);
		} else {
			visibleTerrainChunks.Remove(chunk);
		}
	}

	/// <summary>
	/// Attend que suffisamment de terrain soit généré avant de spawner les IA
	/// </summary>
	
	private IEnumerator WaitForTerrainAndSpawnItems() {
		
			// Attendre qu'un nombre minimum de chunks soit visible
		int minChunksNeeded = 9; // Typiquement un carré 3x3 autour du joueur
		Debug.Log("TerrainGenerator: En attente de génération de terrain suffisante pour items...");
		
			// Attendre que le nombre minimum de chunks soit atteint
		yield return new WaitUntil(() => visibleTerrainChunks.Count >= minChunksNeeded);
		
		Debug.Log($"TerrainGenerator: {visibleTerrainChunks.Count} chunks générés, prêt à spawner les items");
		
			// Attendre un petit délai supplémentaire pour stabiliser le terrain
		yield return new WaitForSeconds(1.0f);
	}
	private IEnumerator WaitForTerrainAndSpawnAI() {
		// Attendre qu'un nombre minimum de chunks soit visible
		int minChunksNeeded = 9; // Typiquement un carré 3x3 autour du joueur
		Debug.Log("TerrainGenerator: En attente de génération de terrain suffisante...");
		
		// Attendre que le nombre minimum de chunks soit atteint
		yield return new WaitUntil(() => visibleTerrainChunks.Count >= minChunksNeeded);
		
		Debug.Log($"TerrainGenerator: {visibleTerrainChunks.Count} chunks générés, prêt à spawner les IA");
		
		// Attendre un petit délai supplémentaire pour stabiliser le terrain
		yield return new WaitForSeconds(1.0f); // Délai augmenté pour s'assurer que tout est prêt
		
		// Déclencher le spawn des IA
		AISpawner spawner = Object.FindFirstObjectByType<AISpawner>();
		if (spawner != null) {
			Debug.Log("TerrainGenerator: Déclenchement du spawn des IA");
			
			// Réinitialiser le spawner si nécessaire
			if (spawner.HasSpawned) {
				spawner.ResetSpawner();
			}
			
			spawner.SpawnAIs();
		}
		else {
			Debug.LogError("TerrainGenerator: Aucun AISpawner trouvé dans la scène!");
		}
	}
}

[System.Serializable]
public struct LODInfo {
	[Range(0,MeshSettings.numSupportedLODs-1)]
	public int lod;
	public float visibleDstThreshold;


	public float sqrVisibleDstThreshold {
		get {
			return visibleDstThreshold * visibleDstThreshold;
		}
	}
}