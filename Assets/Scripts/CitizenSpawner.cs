using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class CitizenSpawner : MonoBehaviour
{
    public static CitizenSpawner Instance { get; private set; }

    [Header("Citizen Pool (prefabs)")]
    [SerializeField] private List<GameObject> citizenPrefabs = new List<GameObject>();

    [Header("Tilemaps")]
    [SerializeField] private Tilemap sidewalkTilemap;
    [SerializeField] private Tilemap crosswalkTilemap;

    [Header("Optional Explicit Slots")]
    [SerializeField] private Transform topLeftSlot;
    [SerializeField] private Transform bottomCenterSlot;
    [SerializeField] private Transform centerCenterSlot;

    [Header("Spawn Settings")]
    [SerializeField] private float citizenMoveSpeed = 1.8f;
    [SerializeField] private float citizenDecisionInterval = 0.8f;
    [SerializeField] private float citizenFireAvoidDistance = 3.5f;
    [SerializeField] private int nearestCellSearchRadius = 8;

    private readonly List<Citizen> _spawnedCitizens = new List<Citizen>();
    private Camera _mainCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _mainCamera = Camera.main;
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "level1")
        {
            return;
        }

        SpawnLevelOneCitizens();
    }

    public void HandleLargeFire(Vector3Int fireCell)
    {
        Citizen targetCitizen = FindCitizenByWindowCell(fireCell);
        if (targetCitizen == null)
        {
            targetCitizen = FindClosestAliveCitizen(fireCell);
        }
        targetCitizen?.Die();
    }

    private void SpawnLevelOneCitizens()
    {
        if (citizenPrefabs.Count == 0)
        {
            Debug.LogWarning("[CitizenSpawner] No citizen prefabs assigned.");
            return;
        }

        _spawnedCitizens.Clear();
        List<GameObject> availablePool = new List<GameObject>(citizenPrefabs);
        List<Vector3Int> availableWindowCells = CollectWindowCells();

        List<Vector3> anchorWorldPoints = new List<Vector3>
        {
            ResolveSlotWorldPosition(topLeftSlot, new Vector2(0.15f, 0.85f)),
            ResolveSlotWorldPosition(bottomCenterSlot, new Vector2(0.50f, 0.15f)),
            ResolveSlotWorldPosition(centerCenterSlot, new Vector2(0.50f, 0.50f))
        };

        foreach (Vector3 anchorWorldPoint in anchorWorldPoints)
        {
            Vector3Int spawnCell = FindNearestCitizenCell(anchorWorldPoint);
            if (spawnCell == Vector3Int.one * int.MinValue)
            {
                continue;
            }

            GameObject prefab = DrawRandomPrefab(availablePool, citizenPrefabs);
            if (prefab == null)
            {
                continue;
            }
            Vector3 spawnWorld = GetCellCenter(spawnCell);
            GameObject instance = Instantiate(prefab, spawnWorld, Quaternion.identity);

            Citizen citizen = instance.GetComponent<Citizen>();
            if (citizen == null)
            {
                citizen = instance.AddComponent<Citizen>();
            }

            citizen.Initialize(
                sidewalkTilemap,
                crosswalkTilemap,
                FireManager.Instance,
                citizenMoveSpeed,
                citizenDecisionInterval,
                citizenFireAvoidDistance);
            Vector3Int assignedWindow = TryAssignNearestWindow(anchorWorldPoint, availableWindowCells);
            if (assignedWindow != Vector3Int.one * int.MinValue)
            {
                citizen.SetHomeWindowCell(assignedWindow);
            }

            _spawnedCitizens.Add(citizen);
        }
    }

    private Citizen FindCitizenByWindowCell(Vector3Int windowCell)
    {
        foreach (Citizen citizen in _spawnedCitizens)
        {
            if (citizen == null || !citizen.IsAlive || !citizen.HasHomeWindow)
            {
                continue;
            }

            if (citizen.HomeWindowCell == windowCell)
            {
                return citizen;
            }
        }

        return null;
    }

    private Citizen FindClosestAliveCitizen(Vector3Int fireCell)
    {
        Citizen best = null;
        float bestDistance = float.MaxValue;
        Vector3 fireWorld = GetCellCenter(fireCell);

        foreach (Citizen citizen in _spawnedCitizens)
        {
            if (citizen == null || !citizen.IsAlive)
            {
                continue;
            }

            float distance = Vector3.Distance(citizen.transform.position, fireWorld);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = citizen;
            }
        }

        return best;
    }

    private Vector3 ResolveSlotWorldPosition(Transform slotTransform, Vector2 viewportFallback)
    {
        if (slotTransform != null)
        {
            return slotTransform.position;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
        {
            return Vector3.zero;
        }

        Vector3 viewportPoint = new Vector3(viewportFallback.x, viewportFallback.y, Mathf.Abs(_mainCamera.transform.position.z));
        return _mainCamera.ViewportToWorldPoint(viewportPoint);
    }

    private Vector3Int FindNearestCitizenCell(Vector3 worldPosition)
    {
        if (sidewalkTilemap == null && crosswalkTilemap == null)
        {
            return Vector3Int.one * int.MinValue;
        }

        Vector3Int origin = sidewalkTilemap != null
            ? sidewalkTilemap.WorldToCell(worldPosition)
            : crosswalkTilemap.WorldToCell(worldPosition);

        if (IsCitizenCell(origin))
        {
            return origin;
        }

        for (int radius = 1; radius <= nearestCellSearchRadius; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                    {
                        continue;
                    }

                    Vector3Int candidate = origin + new Vector3Int(x, y, 0);
                    if (IsCitizenCell(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return Vector3Int.one * int.MinValue;
    }

    private Vector3 GetCellCenter(Vector3Int cell)
    {
        if (sidewalkTilemap != null && sidewalkTilemap.HasTile(cell))
        {
            return sidewalkTilemap.GetCellCenterWorld(cell);
        }

        if (crosswalkTilemap != null && crosswalkTilemap.HasTile(cell))
        {
            return crosswalkTilemap.GetCellCenterWorld(cell);
        }

        if (sidewalkTilemap != null)
        {
            return sidewalkTilemap.GetCellCenterWorld(cell);
        }

        if (crosswalkTilemap != null)
        {
            return crosswalkTilemap.GetCellCenterWorld(cell);
        }

        return cell;
    }

    private bool IsCitizenCell(Vector3Int cell)
    {
        bool hasSidewalk = sidewalkTilemap != null && sidewalkTilemap.HasTile(cell);
        bool hasCrosswalk = crosswalkTilemap != null && crosswalkTilemap.HasTile(cell);
        return hasSidewalk || hasCrosswalk;
    }

    private static GameObject DrawRandomPrefab(List<GameObject> availablePool, List<GameObject> fallbackPool)
    {
        if (availablePool.Count == 0 && fallbackPool.Count == 0)
        {
            return null;
        }

        if (availablePool.Count > 0)
        {
            int availableIndex = Random.Range(0, availablePool.Count);
            GameObject selected = availablePool[availableIndex];
            availablePool.RemoveAt(availableIndex);
            return selected;
        }

        return fallbackPool[Random.Range(0, fallbackPool.Count)];
    }

    private List<Vector3Int> CollectWindowCells()
    {
        List<Vector3Int> windowCells = new List<Vector3Int>();
        Tilemap windowsTilemap = FireManager.Instance != null ? FireManager.Instance.windowsTilemap : null;
        if (windowsTilemap == null)
        {
            return windowCells;
        }

        foreach (Vector3Int cell in windowsTilemap.cellBounds.allPositionsWithin)
        {
            if (windowsTilemap.HasTile(cell))
            {
                windowCells.Add(cell);
            }
        }

        return windowCells;
    }

    private Vector3Int TryAssignNearestWindow(Vector3 anchorWorldPoint, List<Vector3Int> availableWindowCells)
    {
        Tilemap windowsTilemap = FireManager.Instance != null ? FireManager.Instance.windowsTilemap : null;
        if (windowsTilemap == null || availableWindowCells.Count == 0)
        {
            return Vector3Int.one * int.MinValue;
        }

        int bestIndex = -1;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < availableWindowCells.Count; i++)
        {
            Vector3 world = windowsTilemap.GetCellCenterWorld(availableWindowCells[i]);
            float distance = Vector3.Distance(anchorWorldPoint, world);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return Vector3Int.one * int.MinValue;
        }

        Vector3Int chosen = availableWindowCells[bestIndex];
        availableWindowCells.RemoveAt(bestIndex);
        return chosen;
    }
}
