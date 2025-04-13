using UnityEngine;
using System.Collections.Generic;

public class GrassGenerator : MonoBehaviour
{
    [Header("Grass Settings")]
    public GameObject grassPrefab; // Prefab with GPU instancing enabled
    public GameObject terrainObject; // The GameObject that holds the terrain mesh
    public float spawnRadius = 10f; // Radius around the character for grass spawning
    public int grassDensity = 100; // Maximum number of grass objects within the radius

    [Header("Height Filtering")]
    public float minHeight = 0f;  // Minimum height for grass to spawn
    public float maxHeight = 5f; // Maximum height for grass to spawn

    private Transform grassParent; // Parent to keep the hierarchy organized
    private MeshCollider terrainCollider;
    private List<GameObject> spawnedGrass = new List<GameObject>(); // Tracks grass instances

    private void Start()
    {
        if (terrainObject == null)
        {
            Debug.LogError("Terrain Object is not assigned!");
            return;
        }

        // Get the MeshCollider from the terrain object
        terrainCollider = terrainObject.GetComponent<MeshCollider>();
        if (terrainCollider == null)
        {
            terrainCollider = terrainObject.AddComponent<MeshCollider>();
        }

        // Create a parent object to hold grass instances
        grassParent = new GameObject("GrassParent").transform;
    }

    private void Update()
    {
        ManageGrass();
    }

    private void ManageGrass()
    {
        // Remove grass that is too far away
        for (int i = spawnedGrass.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(transform.position, spawnedGrass[i].transform.position) > spawnRadius)
            {
                Destroy(spawnedGrass[i]);
                spawnedGrass.RemoveAt(i);
            }
        }

        // Only add grass if we don't yet meet the desired density
        int neededGrass = grassDensity - spawnedGrass.Count;

        if (neededGrass > 0)
        {
            for (int i = 0; i < neededGrass; i++)
            {
                Vector3 randomPosition = GetRandomPositionAround();
                float terrainHeight = SampleHeightAt(randomPosition);

                // Check height constraints and whether there's already grass nearby
                if (terrainHeight >= minHeight && terrainHeight <= maxHeight && !IsGrassNearby(randomPosition))
                {
                    Vector3 grassPosition = new Vector3(randomPosition.x, terrainHeight, randomPosition.z);
                    GameObject newGrass = Instantiate(grassPrefab, grassPosition, Quaternion.identity, grassParent);
                    spawnedGrass.Add(newGrass);

                    // Stop early if the required grass density is reached
                    if (spawnedGrass.Count >= grassDensity)
                        break;
                }
            }
        }
    }

    private Vector3 GetRandomPositionAround()
    {
        // Generate a random position within the spawn radius
        float angle = Random.Range(0f, 2f * Mathf.PI);
        float radius = Random.Range(0f, spawnRadius);
        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

        return transform.position + offset;
    }

    private float SampleHeightAt(Vector3 position)
    {
        RaycastHit hit;
        if (terrainCollider.Raycast(new Ray(new Vector3(position.x, 100f, position.z), Vector3.down), out hit, 200f))
        {
            return hit.point.y; // Return the height at the hit point
        }

        return Mathf.NegativeInfinity; // No valid height
    }

    private bool IsGrassNearby(Vector3 position)
    {
        foreach (GameObject grass in spawnedGrass)
        {
            if (Vector3.Distance(position, grass.transform.position) < 1f) // Avoid overlapping grass
            {
                return true;
            }
        }

        return false;
    }
}
