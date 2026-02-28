using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Manages fire spawning, spreading, and extinguishing.
/// Uses the Tilemap coordinate system directly so fires are always
/// snapped to tile centers and only spawn on burnable tiles.
///
/// SETUP:
///  1. Attach to a GameObject in your scene.
///  2. Assign fireTilePrefab, buildingTilemap, and optionally sidewalkTilemap.
///     Fire spawns on buildingTilemap tiles only.
///     sidewalkTilemap is used to prevent fire spreading onto paths.
///  3. Configure spawning and spread settings in the Inspector.
/// </summary>
public class FireManager : MonoBehaviour
{
    public static FireManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Fire Prefab")]
    public GameObject fireTilePrefab;

    [Header("Tilemaps — Burnable")]
    [Tooltip("Fire spawns and spreads on tiles that exist on this tilemap.")]
    public Tilemap buildingTilemap;
    [Tooltip("Fire can also spread to tree tiles.")]
    public Tilemap treesTilemap;

    [Header("Tilemaps — Fireproof (fire never spawns or spreads here)")]
    [Tooltip("Road tilemap.")]
    public Tilemap groundTilemap;
    [Tooltip("Pathwalk/sidewalk tilemap.")]
    public Tilemap sidewalkTilemap;

    [Header("Layer Settings")]
    public string fireSortingLayerName = "Fire";
    public int    fireSortingOrder     = 0;

    [Header("Initial Fires")]
    [Tooltip("World-space positions where fire starts at scene load. " +
             "Each position is snapped to the nearest burnable tile.")]
    public List<Vector2> initialFirePositions = new List<Vector2>();

    [Header("Random Spawning")]
    public bool  enableRandomSpawning = true;
    public float firstSpawnDelay      = 15f;
    public float spawnInterval        = 25f;
    public int   firesPerSpawn        = 1;

    [Header("Win/Lose Events")]
    public UnityEngine.Events.UnityEvent onAllFiresExtinguished;

    // -------------------------------------------------------------------------
    // Runtime — keyed by Vector3Int tilemap cell position
    // -------------------------------------------------------------------------

    private Dictionary<Vector3Int, FireTile> activeFires  = new Dictionary<Vector3Int, FireTile>();
    private List<Vector3Int>                 burnableCells = new List<Vector3Int>();

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
        CacheBurnableCells();

        foreach (var pos in initialFirePositions)
        {
            Vector3Int cell = WorldToCell(pos);
            if (IsBurnable(cell))
                SpawnFireAtCell(cell);
        }

        if (enableRandomSpawning)
            StartCoroutine(RandomSpawnRoutine());
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Spawn fire at a world position, snapped to the nearest burnable tile.</summary>
    public FireTile SpawnFire(Vector2 worldPos)
    {
        Vector3Int cell = WorldToCell(worldPos);

        // If the exact cell isn't burnable, search the immediate neighbours —
        // this handles cases where a world position lands on a cell boundary.
        if (!IsBurnable(cell))
        {
            cell = FindNearestBurnableCell(cell, 2);
            if (cell == Vector3Int.back) return null; // nothing found
        }

        return SpawnFireAtCell(cell);
    }

    /// <summary>Spawn fire at an exact tilemap cell.</summary>
    public FireTile SpawnFireAtCell(Vector3Int cell)
    {
        // Double-check both conditions atomically to prevent race conditions
        // where two spread attempts target the same empty cell in the same frame.
        if (!IsBurnable(cell)) return null;
        if (activeFires.ContainsKey(cell)) return activeFires[cell];

        // Use whichever burnable tilemap owns this cell for accurate centering.
        // All tilemaps share the same Grid so GetCellCenterWorld gives the same
        // result from any of them — but we pick one that actually has the tile
        // to be explicit.
        Tilemap sourceTilemap = (buildingTilemap != null && buildingTilemap.HasTile(cell))
            ? buildingTilemap : treesTilemap;
        Vector3 worldCenter = sourceTilemap.GetCellCenterWorld(cell);
        GameObject go = Instantiate(fireTilePrefab, worldCenter, Quaternion.identity, transform);
        go.name = $"Fire_{cell.x}_{cell.y}";

        ApplyFireSortingLayer(go);

        FireTile tile = go.GetComponent<FireTile>();
        if (tile == null) tile = go.AddComponent<FireTile>();

        tile.CellPosition = cell;          // store full Vector3Int — no z truncation
        activeFires[cell] = tile;
        return tile;
    }

    /// <summary>Called by FireTile.FullyExtinguish() to remove itself from the registry.</summary>
    public void UnregisterFire(Vector3Int cell)
    {
        activeFires.Remove(cell);

        if (activeFires.Count == 0)
            onAllFiresExtinguished?.Invoke();
    }

    public bool IsBurning(Vector3Int cell) => activeFires.ContainsKey(cell);

    public FireTile GetFireAt(Vector3Int cell)
    {
        activeFires.TryGetValue(cell, out FireTile tile);
        return tile;
    }

    /// <summary>
    /// Finds the nearest burning FireTile within maxRadius and calls Extinguish() on it.
    /// </summary>
    public FireTile ExtinguishNearest(Vector2 worldPosition, float maxRadius)
    {
        FireTile best     = null;
        float    bestDist = float.MaxValue;

        foreach (var kvp in activeFires)
        {
            Vector3 fireWorld = buildingTilemap.GetCellCenterWorld(kvp.Key);
            float   dist      = Vector2.Distance(worldPosition, new Vector2(fireWorld.x, fireWorld.y));

            if (dist <= maxRadius && dist < bestDist)
            {
                bestDist = dist;
                best     = kvp.Value;
            }
        }

        best?.Extinguish();
        return best;
    }

    /// <summary>Returns true if any fire is within radius of worldPosition.</summary>
    public bool HasNearbyFire(Vector2 worldPosition, float radius)
    {
        foreach (var kvp in activeFires)
        {
            Vector3 fireWorld = buildingTilemap.GetCellCenterWorld(kvp.Key);
            if (Vector2.Distance(worldPosition, new Vector2(fireWorld.x, fireWorld.y)) <= radius)
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
        if (burnableCells.Count == 0) return;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3Int candidate = burnableCells[Random.Range(0, burnableCells.Count)];
            if (!activeFires.ContainsKey(candidate))
            {
                SpawnFireAtCell(candidate);
                return;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// A cell is burnable if it has a tile on the buildings OR trees tilemap,
    /// and is not on a fireproof layer (road, pathwalk).
    /// </summary>
    public bool IsBurnable(Vector3Int cell)
    {
        // Fireproof layers always win
        if (groundTilemap   != null && groundTilemap.HasTile(cell))   return false;
        if (sidewalkTilemap != null && sidewalkTilemap.HasTile(cell))  return false;

        // Burnable if it has a building or tree tile
        bool hasBuilding = buildingTilemap != null && buildingTilemap.HasTile(cell);
        bool hasTrees    = treesTilemap    != null && treesTilemap.HasTile(cell);
        return hasBuilding || hasTrees;
    }

    /// <summary>Search outward from origin for the nearest burnable, unoccupied cell.</summary>
    private Vector3Int FindNearestBurnableCell(Vector3Int origin, int searchRadius)
    {
        for (int r = 1; r <= searchRadius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue; // ring edge only
                    Vector3Int candidate = origin + new Vector3Int(x, y, 0);
                    // Skip already-burning cells — prevents spawning on top of existing fire
                    if (IsBurnable(candidate) && !activeFires.ContainsKey(candidate))
                        return candidate;
                }
            }
        }
        return Vector3Int.back; // sentinel — nothing found
    }

    private Vector3Int WorldToCell(Vector2 worldPos)
        => buildingTilemap.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0f));

    private void CacheBurnableCells()
    {
        burnableCells.Clear();

        // Scan building tilemap
        if (buildingTilemap != null)
        {
            foreach (var pos in buildingTilemap.cellBounds.allPositionsWithin)
                if (IsBurnable(pos) && !burnableCells.Contains(pos))
                    burnableCells.Add(pos);
        }

        // Also scan trees tilemap
        if (treesTilemap != null)
        {
            foreach (var pos in treesTilemap.cellBounds.allPositionsWithin)
                if (IsBurnable(pos) && !burnableCells.Contains(pos))
                    burnableCells.Add(pos);
        }

        Debug.Log($"[FireManager] Found {burnableCells.Count} burnable cells.");
    }

    private void ApplyFireSortingLayer(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerName = fireSortingLayerName;
            r.sortingOrder     = fireSortingOrder;
        }
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (activeFires == null || buildingTilemap == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
        foreach (var cell in activeFires.Keys)
            Gizmos.DrawCube(buildingTilemap.GetCellCenterWorld(cell), Vector3.one * 0.8f);
    }
#endif
}