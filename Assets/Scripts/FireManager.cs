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
///  4. Optionally call SpawnFire() from other scripts or click "Spawn Initial Fires" via the Inspector button.
/// </summary>
public class FireManager : MonoBehaviour
{
    public static FireManager Instance { get; private set; }

    [Header("Fire Prefab")]
    [Tooltip("Prefab that has the FireTile component + your fire sprite/particle.")]
    public GameObject fireTilePrefab;

    [Header("Layer Settings")]
    [Tooltip("The Sorting Layer name you've created for fire. It must be above all other layers.")]
    public string fireSortingLayerName = "Fire";
    [Tooltip("Order within the Fire sorting layer.")]
    public int fireSortingOrder = 0;

    [Header("Initial Fires")]
    [Tooltip("World-space positions where fire starts at the beginning of the scene.")]
    public List<Vector2> initialFirePositions = new List<Vector2>();

    // Internal registry: tracks every active fire by grid position (snapped to int).
    private Dictionary<Vector2Int, FireTile> activeFires = new Dictionary<Vector2Int, FireTile>();

    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        foreach (var pos in initialFirePositions)
            SpawnFire(pos);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Spawn a fire tile at the given world position (snapped to grid).</summary>
    public FireTile SpawnFire(Vector2 worldPos)
    {
        Vector2Int gridPos = WorldToGrid(worldPos);

        // Don't double-spawn
        if (activeFires.ContainsKey(gridPos))
            return activeFires[gridPos];

        Vector3 spawnPos = new Vector3(gridPos.x, gridPos.y, 0f);
        GameObject go = Instantiate(fireTilePrefab, spawnPos, Quaternion.identity, transform);
        go.name = $"Fire_{gridPos.x}_{gridPos.y}";

        // Force the fire to render above everything else
        ApplyFireSortingLayer(go);

        FireTile tile = go.GetComponent<FireTile>();
        if (tile == null)
            tile = go.AddComponent<FireTile>();

        tile.GridPosition = gridPos;
        activeFires[gridPos] = tile;
        return tile;
    }

    /// <summary>Called by FireTile when it is extinguished or burns out.</summary>
    public void UnregisterFire(Vector2Int gridPos)
    {
        activeFires.Remove(gridPos);
    }

    /// <summary>Returns true if the given grid cell is currently on fire.</summary>
    public bool IsBurning(Vector2Int gridPos) => activeFires.ContainsKey(gridPos);

    /// <summary>Returns the FireTile at a grid position, or null.</summary>
    public FireTile GetFireAt(Vector2Int gridPos)
    {
        activeFires.TryGetValue(gridPos, out FireTile tile);
        return tile;
    }

    public int ActiveFireCount => activeFires.Count;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    public static Vector2Int WorldToGrid(Vector2 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));
    }

    private void ApplyFireSortingLayer(GameObject go)
    {
        // Apply to all renderers (SpriteRenderer, ParticleSystemRenderer, etc.)
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingLayerName = fireSortingLayerName;
            renderer.sortingOrder = fireSortingOrder;
        }
    }
}