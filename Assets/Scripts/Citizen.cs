using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(SpriteRenderer))]
public class Citizen : MonoBehaviour
{
    private static readonly Vector3Int[] CardinalDirections =
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private float decisionInterval = 0.8f;
    [SerializeField] private float destinationIdlePause = 0.45f;
    [SerializeField] private float waypointThreshold = 0.03f;
    [SerializeField] private float fireAvoidDistance = 2.2f;
    [SerializeField] private float minSafeFireDistanceTiles = 5f;
    [SerializeField] private int maxSearchDepth = 24;
    [SerializeField] private int minDestinationStepsFromCurrent = 4;

    private Tilemap _sidewalkTilemap;
    private Tilemap _crosswalkTilemap;
    private Tilemap _treesTilemap;
    private FireManager _fireManager;

    private Coroutine _wanderRoutine;
    private bool _isAlive = true;
    private bool _hasHomeWindow;
    private Vector3Int _homeWindowCell;
    private readonly List<Vector3Int> _currentPath = new List<Vector3Int>();
    private int _pathIndex;

    public bool IsAlive => _isAlive;
    public bool HasHomeWindow => _hasHomeWindow;
    public Vector3Int HomeWindowCell => _homeWindowCell;

    public void Initialize(
        Tilemap sidewalkTilemap,
        Tilemap crosswalkTilemap,
        Tilemap treesTilemap,
        FireManager fireManager,
        float citizenMoveSpeed,
        float citizenDecisionInterval,
        float citizenFireAvoidDistance)
    {
        _sidewalkTilemap = sidewalkTilemap;
        _crosswalkTilemap = crosswalkTilemap;
        _treesTilemap = treesTilemap;
        _fireManager = fireManager;
        moveSpeed = citizenMoveSpeed;
        decisionInterval = citizenDecisionInterval;
        fireAvoidDistance = citizenFireAvoidDistance;
    }

    public void SetHomeWindowCell(Vector3Int windowCell)
    {
        _homeWindowCell = windowCell;
        _hasHomeWindow = true;
    }

    private void OnEnable()
    {
        _wanderRoutine = StartCoroutine(WanderRoutine());
    }

    private void OnDisable()
    {
        if (_wanderRoutine != null)
        {
            StopCoroutine(_wanderRoutine);
            _wanderRoutine = null;
        }
    }

    public void Die()
    {
        if (!_isAlive)
        {
            return;
        }

        _isAlive = false;
        CityPopulation.Instance?.RegisterCitizenDeath();
        Destroy(gameObject);
    }

    private IEnumerator WanderRoutine()
    {
        while (_isAlive)
        {
            Vector3Int currentCell = GetCurrentCell();
            if (_fireManager != null && _fireManager.IsBurningAt(currentCell))
            {
                Die();
                yield break;
            }

            if (_hasHomeWindow && _fireManager != null && _fireManager.IsBurningAt(_homeWindowCell))
            {
                // Trapped while this citizen's assigned window is burning.
                yield return new WaitForSeconds(decisionInterval);
                continue;
            }

            if (!HasRemainingPath(currentCell))
            {
                BuildPathToRandomSafeDestination(currentCell);
            }

            if (HasRemainingPath(currentCell))
            {
                Vector3Int nextCell = _currentPath[_pathIndex];
                _pathIndex++;
                yield return MoveTo(nextCell);

                if (_pathIndex >= _currentPath.Count)
                {
                    // Pause only after completing a full destination path.
                    yield return new WaitForSeconds(destinationIdlePause);
                }
                continue;
            }

            // Only wait when there is nowhere valid to move.
            yield return new WaitForSeconds(decisionInterval);
        }
    }

    private IEnumerator MoveTo(Vector3Int targetCell)
    {
        Vector3 worldTarget = GetCellCenter(targetCell);
        while (_isAlive && Vector3.Distance(transform.position, worldTarget) > waypointThreshold)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                worldTarget,
                moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = worldTarget;
    }

    private bool HasRemainingPath(Vector3Int currentCell)
    {
        if (_currentPath.Count == 0 || _pathIndex >= _currentPath.Count)
        {
            return false;
        }

        if (_pathIndex == 0 && _currentPath[0] == currentCell)
        {
            _pathIndex = 1;
        }

        return _pathIndex < _currentPath.Count;
    }

    private void BuildPathToRandomSafeDestination(Vector3Int startCell)
    {
        _currentPath.Clear();
        _pathIndex = 0;

        Dictionary<Vector3Int, Vector3Int> parents = new Dictionary<Vector3Int, Vector3Int>();
        Dictionary<Vector3Int, int> depths = new Dictionary<Vector3Int, int>();
        List<Vector3Int> destinationCandidates = new List<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();

        queue.Enqueue(startCell);
        depths[startCell] = 0;

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int depth = depths[current];
            if (depth >= maxSearchDepth)
            {
                continue;
            }

            foreach (Vector3Int direction in CardinalDirections)
            {
                Vector3Int next = current + direction;
                if (depths.ContainsKey(next))
                {
                    continue;
                }

                if (!IsWalkableCitizenCell(next))
                {
                    continue;
                }

                if (_fireManager != null && _fireManager.IsBurningAt(next))
                {
                    continue;
                }

                if (!IsCellFireSafe(next))
                {
                    continue;
                }

                depths[next] = depth + 1;
                parents[next] = current;
                queue.Enqueue(next);

                if (depths[next] >= minDestinationStepsFromCurrent)
                {
                    destinationCandidates.Add(next);
                }
            }
        }

        if (destinationCandidates.Count == 0)
        {
            return;
        }

        Vector3Int destination = destinationCandidates[Random.Range(0, destinationCandidates.Count)];
        ReconstructPath(startCell, destination, parents, _currentPath);
        _pathIndex = _currentPath.Count > 1 ? 1 : 0;
    }

    private static void ReconstructPath(
        Vector3Int startCell,
        Vector3Int endCell,
        Dictionary<Vector3Int, Vector3Int> parents,
        List<Vector3Int> outPath)
    {
        outPath.Clear();
        Vector3Int current = endCell;
        outPath.Add(current);

        while (current != startCell && parents.TryGetValue(current, out Vector3Int parent))
        {
            current = parent;
            outPath.Add(current);
        }

        outPath.Reverse();
    }

    private bool IsCellFireSafe(Vector3Int cell)
    {
        if (_fireManager == null)
        {
            return true;
        }

        Vector2 cellCenter = GetCellCenter(cell);
        Vector2? nearest = _fireManager.GetNearestFirePosition(cellCenter, 9999f);
        if (!nearest.HasValue)
        {
            return true;
        }

        float distance = Vector2.Distance(cellCenter, nearest.Value);
        return distance >= minSafeFireDistanceTiles;
    }

    private Vector3Int GetCurrentCell()
    {
        if (_sidewalkTilemap != null)
        {
            return _sidewalkTilemap.WorldToCell(transform.position);
        }

        if (_crosswalkTilemap != null)
        {
            return _crosswalkTilemap.WorldToCell(transform.position);
        }

        return Vector3Int.RoundToInt(transform.position);
    }

    private Vector3 GetCellCenter(Vector3Int cell)
    {
        if (_sidewalkTilemap != null && _sidewalkTilemap.HasTile(cell))
        {
            return _sidewalkTilemap.GetCellCenterWorld(cell);
        }

        if (_crosswalkTilemap != null && _crosswalkTilemap.HasTile(cell))
        {
            return _crosswalkTilemap.GetCellCenterWorld(cell);
        }

        if (_sidewalkTilemap != null)
        {
            return _sidewalkTilemap.GetCellCenterWorld(cell);
        }

        if (_crosswalkTilemap != null)
        {
            return _crosswalkTilemap.GetCellCenterWorld(cell);
        }

        return cell;
    }

    private bool IsWalkableCitizenCell(Vector3Int cell)
    {
        bool hasSidewalk = _sidewalkTilemap != null && _sidewalkTilemap.HasTile(cell);
        bool hasCrosswalk = _crosswalkTilemap != null && _crosswalkTilemap.HasTile(cell);
        bool hasTrees = _treesTilemap != null && _treesTilemap.HasTile(cell);
        return (hasSidewalk || hasCrosswalk) && !hasTrees;
    }
}
