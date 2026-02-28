using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Manages fire entirely through two Tilemaps (FireBackground + FireForeground)
/// using AnimatedTile assets for each stage. No FireTile prefabs needed.
///
/// SETUP:
///  1. Assign all tilemap references in the Inspector.
///  2. Assign your three AnimatedTile assets (smallFireTile, mediumFireTile, largeFireTile).
///  3. Set FireBackground Tilemap Renderer: Sorting Layer = Fire
///  4. Set FireForeground Tilemap Renderer: Sorting Layer = Buildings, Order in Layer = 1
///  5. Configure spread and escalation settings below.
/// </summary>
public class FireManager : MonoBehaviour
{
    public static FireManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector — Tilemaps
    // -------------------------------------------------------------------------

    [Header("Fire Tilemaps")]
    [Tooltip("Small and Medium fires render here — sorting layer below Buildings.")]
    public Tilemap fireBackground;
    [Tooltip("Large fires render here — sorting layer above Buildings.")]
    public Tilemap fireForeground;

    [Header("Burnable Tilemaps")]
    public Tilemap buildingTilemap;
    public Tilemap treesTilemap;
    [Tooltip("Initial fires spawn on random cells from this tilemap.")]
    public Tilemap windowsTilemap;

    [Header("Fireproof Tilemaps")]
    public Tilemap groundTilemap;
    public Tilemap sidewalkTilemap;

    // -------------------------------------------------------------------------
    // Inspector — Animated Tiles
    // -------------------------------------------------------------------------

    [Header("Fire Stage Tiles")]
    [Tooltip("AnimatedTile asset for Small fire stage.")]
    public TileBase smallFireTile;
    [Tooltip("AnimatedTile asset for Medium fire stage.")]
    public TileBase mediumFireTile;
    [Tooltip("AnimatedTile asset for Large fire stage.")]
    public TileBase largeFireTile;

    // -------------------------------------------------------------------------
    // Inspector — Spawning
    // -------------------------------------------------------------------------

    [Header("Initial Fires")]
    [Tooltip("How many fires to spawn on random window cells at scene start.")]
    public int initialFireCount = 1;

    [Header("Random Spawning")]
    public bool  enableRandomSpawning = true;
    public float firstSpawnDelay      = 15f;
    public float spawnInterval        = 25f;
    public int   firesPerSpawn        = 1;

    // -------------------------------------------------------------------------
    // Inspector — Per-cell defaults (overridable per cell via FireCellData)
    // -------------------------------------------------------------------------

    [Header("Escalation Timings (seconds)")]
    public float smallToMediumTime = 8f;
    public float mediumToLargeTime = 10f;

    [Header("Spread Settings")]
    [Range(0.5f, 30f)] public float spreadIntervalMedium = 12f;
    [Range(0.5f, 30f)] public float spreadIntervalLarge  = 4f;
    [Range(1, 10)]     public int   maxSpreads            = 4;
    [Range(0f, 1f)]    public float spreadChanceMedium    = 0.3f;
    [Range(0f, 1f)]    public float spreadChanceLarge     = 0.7f;

    // -------------------------------------------------------------------------
    // Inspector — Events
    // -------------------------------------------------------------------------

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onAllFiresExtinguished;

    // -------------------------------------------------------------------------
    // Fire cell state
    // -------------------------------------------------------------------------

    public enum FireStage { Small, Medium, Large }

    private class FireCellData
    {
        public FireStage stage           = FireStage.Small;
        public int       spreadsLeft;
        public Coroutine escalationCoroutine;
        public Coroutine spreadCoroutine;
    }

    private Dictionary<Vector3Int, FireCellData> _activeFires
        = new Dictionary<Vector3Int, FireCellData>();

    private List<Vector3Int> _burnableCells = new List<Vector3Int>();

    private static readonly Vector3Int[] CardinalDirs = {
        Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
    };

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

        SpawnInitialFires();

        if (enableRandomSpawning)
            StartCoroutine(RandomSpawnRoutine());
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Spawn initial fires on random window or tree cells at scene start.</summary>
    private void SpawnInitialFires()
    {
        if (initialFireCount <= 0) return;

        // Collect all burnable window and tree cells
        List<Vector3Int> candidates = new List<Vector3Int>();

        if (windowsTilemap != null)
            foreach (var pos in windowsTilemap.cellBounds.allPositionsWithin)
                if (windowsTilemap.HasTile(pos) && IsBurnable(pos))
                    candidates.Add(pos);

        if (treesTilemap != null)
            foreach (var pos in treesTilemap.cellBounds.allPositionsWithin)
                if (treesTilemap.HasTile(pos) && IsBurnable(pos) && !candidates.Contains(pos))
                    candidates.Add(pos);

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[FireManager] No burnable window or tree cells found for initial fire spawn.");
            return;
        }

        // Shuffle and pick initialFireCount unique cells
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int spawned = 0;
        foreach (var cell in candidates)
        {
            if (spawned >= initialFireCount) break;
            SpawnFireAtCell(cell);
            spawned++;
        }
    }

    /// <summary>Spawn fire at a tilemap cell position.</summary>
    public void SpawnFireAtCell(Vector3Int cell)
    {
        if (!IsBurnable(cell))          return;
        if (_activeFires.ContainsKey(cell)) return;

        FireCellData data = new FireCellData
        {
            stage        = FireStage.Small,
            spreadsLeft  = maxSpreads
        };

        _activeFires[cell] = data;
        PaintFireTile(cell, FireStage.Small);
        SmokeManager.Instance?.UpdateSmoke(cell, FireStage.Small);

        data.escalationCoroutine = StartCoroutine(EscalationRoutine(cell));
        data.spreadCoroutine     = StartCoroutine(SpreadRoutine(cell));
    }

    /// <summary>
    /// Reduce fire at cell by one stage. Called by firetruck extinguish.
    /// Returns the tile that was hit, or null if no fire there.
    /// </summary>
    public bool ExtinguishAt(Vector3Int cell)
    {
        if (!_activeFires.TryGetValue(cell, out FireCellData data)) return false;

        switch (data.stage)
        {
            case FireStage.Large:
                // Stop escalation — won't re-grow after being knocked down
                if (data.escalationCoroutine != null)
                {
                    StopCoroutine(data.escalationCoroutine);
                    data.escalationCoroutine = null;
                }
                SetStage(cell, data, FireStage.Medium);
                break;

            case FireStage.Medium:
                SetStage(cell, data, FireStage.Small);
                break;

            case FireStage.Small:
                FullyExtinguish(cell, data);
                break;
        }

        return true;
    }

    /// <summary>
    /// Find and extinguish the nearest fire within maxRadius of worldPosition.
    /// Returns true if a fire was hit.
    /// </summary>
    public bool ExtinguishNearest(Vector2 worldPosition, float maxRadius)
    {
        Vector3Int bestCell = default;
        float      bestDist = float.MaxValue;
        bool       found    = false;

        foreach (var cell in _activeFires.Keys)
        {
            Vector3 center = GetCellCenter(cell);
            float   dist   = Vector2.Distance(worldPosition,
                                              new Vector2(center.x, center.y));
            if (dist <= maxRadius && dist < bestDist)
            {
                bestDist = dist;
                bestCell = cell;
                found    = true;
            }
        }

        if (found) ExtinguishAt(bestCell);
        return found;
    }

    /// <summary>
    /// Returns the world position of the nearest fire within radius, or null if none.
    /// Used by FireTruck to orient the water spray VFX toward the fire.
    /// </summary>
    public Vector2? GetNearestFirePosition(Vector2 worldPosition, float radius)
    {
        Vector2? best     = null;
        float    bestDist = float.MaxValue;

        foreach (var cell in _activeFires.Keys)
        {
            Vector3 center = GetCellCenter(cell);
            float   dist   = Vector2.Distance(worldPosition, new Vector2(center.x, center.y));

            if (dist <= radius && dist < bestDist)
            {
                bestDist = dist;
                best     = new Vector2(center.x, center.y);
            }
        }

        return best;
    }

    /// <summary>Returns true if any fire is within radius of worldPosition.</summary>
    public bool HasNearbyFire(Vector2 worldPosition, float radius)
    {
        foreach (var cell in _activeFires.Keys)
        {
            Vector3 center = GetCellCenter(cell);
            if (Vector2.Distance(worldPosition, new Vector2(center.x, center.y)) <= radius)
                return true;
        }
        return false;
    }

    public bool      IsBurningAt(Vector3Int cell) => _activeFires.ContainsKey(cell);
    public int        ActiveFireCount              => _activeFires.Count;
    public FireStage? GetStageAt(Vector3Int cell)  => _activeFires.TryGetValue(cell, out var d) ? d.stage : (FireStage?)null;

    // -------------------------------------------------------------------------
    // Escalation coroutine
    // -------------------------------------------------------------------------

    private IEnumerator EscalationRoutine(Vector3Int cell)
    {
        yield return new WaitForSeconds(smallToMediumTime);
        if (!_activeFires.TryGetValue(cell, out FireCellData data)) yield break;
        SetStage(cell, data, FireStage.Medium);

        yield return new WaitForSeconds(mediumToLargeTime);
        if (!_activeFires.TryGetValue(cell, out data)) yield break;
        SetStage(cell, data, FireStage.Large);
    }

    // -------------------------------------------------------------------------
    // Spread coroutine
    // -------------------------------------------------------------------------

    private IEnumerator SpreadRoutine(Vector3Int cell)
    {
        // Wait until Medium before spreading
        while (_activeFires.TryGetValue(cell, out FireCellData d)
               && d.stage == FireStage.Small)
            yield return new WaitForSeconds(1f);

        while (_activeFires.TryGetValue(cell, out FireCellData data)
               && data.spreadsLeft > 0)
        {
            bool  isLarge  = data.stage == FireStage.Large;
            float interval = isLarge ? spreadIntervalLarge  : spreadIntervalMedium;
            float chance   = isLarge ? spreadChanceLarge    : spreadChanceMedium;

            yield return new WaitForSeconds(interval);

            if (!_activeFires.ContainsKey(cell)) yield break;

            TrySpread(cell, chance);
            data.spreadsLeft--;
        }
    }

    private void TrySpread(Vector3Int cell, float chance)
    {
        // Shuffle directions for randomness
        Vector3Int[] dirs = (Vector3Int[])CardinalDirs.Clone();
        for (int i = dirs.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
        }

        foreach (var dir in dirs)
        {
            if (Random.value > chance) continue;

            Vector3Int target = cell + dir;
            if (!_activeFires.ContainsKey(target) && IsBurnable(target))
            {
                SpawnFireAtCell(target);
                break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Stage management
    // -------------------------------------------------------------------------

    private void SetStage(Vector3Int cell, FireCellData data, FireStage newStage)
    {
        FireStage oldStage = data.stage;
        data.stage         = newStage;
        PaintFireTile(cell, newStage);

        // Update smoke intensity for new stage
        SmokeManager.Instance?.UpdateSmoke(cell, newStage);

        // Notify population system when a fire becomes or leaves Large stage
        if (newStage == FireStage.Large && oldStage != FireStage.Large)
            CityPopulation.Instance?.OnFireBecameLarge(cell);
        else if (newStage != FireStage.Large && oldStage == FireStage.Large)
            CityPopulation.Instance?.OnFireNoLongerLarge(cell);
    }

    private void PaintFireTile(Vector3Int cell, FireStage stage)
    {
        // Small + Medium go on FireBackground (behind buildings)
        // Large goes on FireForeground (in front of buildings)
        if (stage == FireStage.Large)
        {
            fireBackground.SetTile(cell, null);
            fireForeground.SetTile(cell, largeFireTile);
        }
        else
        {
            fireForeground.SetTile(cell, null);
            fireBackground.SetTile(cell, stage == FireStage.Small
                                        ? smallFireTile : mediumFireTile);
        }
    }

    private void FullyExtinguish(Vector3Int cell, FireCellData data)
    {
        if (data.escalationCoroutine != null) StopCoroutine(data.escalationCoroutine);
        if (data.spreadCoroutine     != null) StopCoroutine(data.spreadCoroutine);

        fireBackground.SetTile(cell, null);
        fireForeground.SetTile(cell, null);
        SmokeManager.Instance?.RemoveSmoke(cell);

        _activeFires.Remove(cell);

        if (_activeFires.Count == 0)
            onAllFiresExtinguished?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Burnable check
    // -------------------------------------------------------------------------

    public bool IsBurnable(Vector3Int cell)
    {
        bool hasBuilding = buildingTilemap != null && buildingTilemap.HasTile(cell);
        bool hasTrees    = treesTilemap    != null && treesTilemap.HasTile(cell);

        // Trees and buildings are always burnable regardless of what's underneath —
        // this handles cases where tree tiles are painted over ground tiles.
        if (hasBuilding || hasTrees) return true;

        // Cells with only road or sidewalk tiles are fireproof
        return false;
    }

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
        if (_burnableCells.Count == 0) return;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3Int candidate = _burnableCells[Random.Range(0, _burnableCells.Count)];
            if (!_activeFires.ContainsKey(candidate))
            {
                SpawnFireAtCell(candidate);
                return;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private Vector3 GetCellCenter(Vector3Int cell)
    {
        // Use whichever burnable tilemap owns the cell
        if (buildingTilemap != null && buildingTilemap.HasTile(cell))
            return buildingTilemap.GetCellCenterWorld(cell);
        if (treesTilemap != null && treesTilemap.HasTile(cell))
            return treesTilemap.GetCellCenterWorld(cell);
        // Fallback — use fireBackground tilemap which shares the same Grid
        return fireBackground.GetCellCenterWorld(cell);
    }

    private void CacheBurnableCells()
    {
        _burnableCells.Clear();

        if (buildingTilemap != null)
            foreach (var pos in buildingTilemap.cellBounds.allPositionsWithin)
                if (IsBurnable(pos) && !_burnableCells.Contains(pos))
                    _burnableCells.Add(pos);

        if (treesTilemap != null)
            foreach (var pos in treesTilemap.cellBounds.allPositionsWithin)
                if (IsBurnable(pos) && !_burnableCells.Contains(pos))
                    _burnableCells.Add(pos);

        Debug.Log($"[FireManager] {_burnableCells.Count} burnable cells cached.");
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_activeFires == null || fireBackground == null) return;
        foreach (var kvp in _activeFires)
        {
            Gizmos.color = kvp.Value.stage switch
            {
                FireStage.Small  => new Color(1f, 1f, 0f, 0.4f),
                FireStage.Medium => new Color(1f, 0.5f, 0f, 0.4f),
                FireStage.Large  => new Color(1f, 0.1f, 0f, 0.5f),
                _                => Color.white
            };
            Gizmos.DrawWireCube(GetCellCenter(kvp.Key), Vector3.one);
        }
    }
#endif
}