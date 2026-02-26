using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that manages fire spawning, the fire sorting layer,
/// and provides a registry of all currently burning tiles.
///
/// SETUP:
///  1. Create an empty GameObject called "FireManager" in your scene.
///  2. Attach this script to it.
///  3. Assign your FireTile prefab in the Inspector.
///  4. Set initialFirePositions and/or configure random spawning.
/// </summary>
public class FireManager : MonoBehaviour
{
    public static FireManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Fire Prefab")]
    [Tooltip("Prefab that has the FireTile component + your fire sprite/particle.")]
    public GameObject fireTilePrefab;

    [Header("Layer Settings")]
    [Tooltip("The Sorting Layer name you've created for fire. Must sit above all other layers.")]
    public string fireSortingLayerName = "Fire";
    [Tooltip("Order within the Fire sorting layer.")]
    public int fireSortingOrder = 0;

    [Header("Initial Fires")]
    [Tooltip("World-space positions where fire starts at scene load.")]
    public List<Vector2> initialFirePositions = new List<Vector2>();

    [Header("Random Spawning")]
    [Tooltip("Enable automatic random fire spawning over time.")]
    public bool enableRandomSpawning = true;
    [Tooltip("Seconds before the first random fire spawns.")]
    public float firstSpawnDelay = 15f;
    [Tooltip("Seconds between each random fire spawn.")]
    public float spawnInterval = 25f;
    [Tooltip("How many fires to spawn per interval.")]
    public int firesPerSpawn = 1;

    [Header("Spawn Area")]
    [Tooltip("World-space bounds within which random fires can spawn. " +
             "Set this to cover your city tilemap area.")]
    public Bounds spawnBounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 0f));

    [Header("Win/Lose Events")]
    public UnityEngine.Events.UnityEvent onAllFiresExtinguished;

    // -------------------------------------------------------------------------
    // Runtime
    // -------------------------------------------------------------------------

    // Key: grid position (snapped to int).  Value: the FireTile at that position.
    private Dictionary<Vector2Int, FireTile> activeFires = new Dictionary<Vector2Int, FireTile>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        foreach (var pos in initialFirePositions)
            SpawnFire(pos);

        if (enableRandomSpawning)
            StartCoroutine(RandomSpawnRoutine());
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Spawn a fire tile at the given world position (snapped to grid).</summary>
    public FireTile SpawnFire(Vector2 worldPos)
    {
        Vector2Int gridPos = WorldToGrid(worldPos);

        if (activeFires.ContainsKey(gridPos))
            return activeFires[gridPos]; // already burning

        Vector3 spawnPos = new Vector3(gridPos.x, gridPos.y, 0f);
        GameObject go = Instantiate(fireTilePrefab, spawnPos, Quaternion.identity, transform);
        go.name = $"Fire_{gridPos.x}_{gridPos.y}";

        ApplyFireSortingLayer(go);

        FireTile tile = go.GetComponent<FireTile>();
        if (tile == null) tile = go.AddComponent<FireTile>();

        tile.GridPosition = gridPos;
        activeFires[gridPos] = tile;
        return tile;
    }

    /// <summary>Called by FireTile.FullyExtinguish() to remove itself from the registry.</summary>
    public void UnregisterFire(Vector2Int gridPos)
    {
        activeFires.Remove(gridPos);

        if (activeFires.Count == 0)
            onAllFiresExtinguished?.Invoke();
    }

    /// <summary>Returns true if the given grid cell is currently on fire.</summary>
    public bool IsBurning(Vector2Int gridPos) => activeFires.ContainsKey(gridPos);

    /// <summary>Returns the FireTile at a grid position, or null.</summary>
    public FireTile GetFireAt(Vector2Int gridPos)
    {
        activeFires.TryGetValue(gridPos, out FireTile tile);
        return tile;
    }

    /// <summary>
    /// Finds the nearest burning FireTile within maxRadius of worldPosition and
    /// calls Extinguish() on it (reduces by one stage). Returns the tile, or null.
    /// Called each tick by FireTruck's extinguish coroutine.
    /// </summary>
    public FireTile ExtinguishNearest(Vector2 worldPosition, float maxRadius)
    {
        FireTile best     = null;
        float    bestDist = float.MaxValue;

        foreach (var kvp in activeFires)
        {
            float dist = Vector2.Distance(worldPosition, new Vector2(kvp.Key.x, kvp.Key.y));
            if (dist <= maxRadius && dist < bestDist)
            {
                bestDist = dist;
                best     = kvp.Value;
            }
        }

        if (best != null)
            best.Extinguish();

        return best;
    }

    /// <summary>Returns true if any fire exists within radius of worldPosition.</summary>
    public bool HasNearbyFire(Vector2 worldPosition, float radius)
    {
        foreach (var kvp in activeFires)
        {
            if (Vector2.Distance(worldPosition, new Vector2(kvp.Key.x, kvp.Key.y)) <= radius)
                return true;
        }
        return false;
    }

    public int ActiveFireCount => activeFires.Count;

    // -------------------------------------------------------------------------
    // Random spawning
    // -------------------------------------------------------------------------

    private IEnumerator RandomSpawnRoutine()
    {
        yield return new WaitForSeconds(firstSpawnDelay);

        while (true)
        {
            for (int i = 0; i < firesPerSpawn; i++)
                SpawnRandomFire();

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnRandomFire()
    {
        // Try a few random positions within the spawn bounds; skip already-burning ones.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = Random.Range(spawnBounds.min.x, spawnBounds.max.x);
            float y = Random.Range(spawnBounds.min.y, spawnBounds.max.y);
            Vector2Int gridPos = WorldToGrid(new Vector2(x, y));

            if (!activeFires.ContainsKey(gridPos))
            {
                SpawnFire(new Vector2(gridPos.x, gridPos.y));
                return;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    public static Vector2Int WorldToGrid(Vector2 worldPos)
        => new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));

    private void ApplyFireSortingLayer(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerName = fireSortingLayerName;
            r.sortingOrder     = fireSortingOrder;
        }
    }
}