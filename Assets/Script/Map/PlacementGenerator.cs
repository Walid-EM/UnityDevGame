using UnityEngine;

public class PlacementGenerator : MonoBehaviour
{
    [SerializeField] GameObject prefab;

    [Header("Raycast Settings")]
    [SerializeField] int density;

    [Space]
    [SerializeField] float minHeight;
    [SerializeField] float maxHeight;
    [SerializeField] Vector2 xRange;
    [SerializeField] Vector2 zRange;

    [Header("Prefab Variation Settings")]
    [SerializeField, Range(0, 1)] float rotateTowardsNormal;
    [SerializeField] Vector3 rotationRange;
    [SerializeField] Vector3 minScale;
    [SerializeField] Vector3 maxScale;

    private GameObject container; // The parent GameObject for all instances

    public void Generate()
    {
        // Ensure a clean setup by clearing previous instances
        Clear();

        // Create a container to hold all prefabs
        container = new GameObject("PrefabContainer");
        container.transform.parent = transform;

        for (int i = 0; i < density; i++)
        {
            float sampleX = Random.Range(xRange.x, xRange.y);
            float sampleZ = Random.Range(zRange.x, zRange.y);
            Vector3 rayStart = new Vector3(sampleX, maxHeight, sampleZ);

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, Mathf.Infinity))
            {
                if (hit.point.y < minHeight)
                    continue;

                // Instantiate prefab and set its parent to the container
                GameObject instantiatedPrefab = Instantiate(prefab, hit.point, Quaternion.identity, container.transform);
                instantiatedPrefab.transform.localRotation = Quaternion.Lerp(
                    instantiatedPrefab.transform.rotation, 
                    Quaternion.FromToRotation(Vector3.up, hit.normal), 
                    rotateTowardsNormal
                );
                instantiatedPrefab.transform.localScale = new Vector3(
                    Random.Range(minScale.x, maxScale.x),
                    Random.Range(minScale.y, maxScale.y),
                    Random.Range(minScale.z, maxScale.z)
                );
            }
        }
    }

    public void Clear()
    {
        // Destroy the container and all its children if it exists
        if (container != null)
        {
            DestroyImmediate(container);
        }
    }
}
