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
    [SerializeField] private float waypointThreshold = 0.03f;
    [SerializeField] private float fireAvoidDistance = 2.2f;

    private Tilemap _sidewalkTilemap;
    private Tilemap _crosswalkTilemap;
    private FireManager _fireManager;

    private Coroutine _wanderRoutine;
    private bool _isAlive = true;
    private bool _hasHomeWindow;
    private Vector3Int _homeWindowCell;

    public bool IsAlive => _isAlive;
    public bool HasHomeWindow => _hasHomeWindow;
    public Vector3Int HomeWindowCell => _homeWindowCell;

    public void Initialize(
        Tilemap sidewalkTilemap,
        Tilemap crosswalkTilemap,
        FireManager fireManager,
        float citizenMoveSpeed,
        float citizenDecisionInterval,
        float citizenFireAvoidDistance)
    {
        _sidewalkTilemap = sidewalkTilemap;
        _crosswalkTilemap = crosswalkTilemap;
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

            Vector3Int nextCell = ChooseNextCell(currentCell);
            if (nextCell != currentCell)
            {
                yield return MoveTo(nextCell);
            }

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

    private Vector3Int ChooseNextCell(Vector3Int currentCell)
    {
        Vector3Int bestCandidate = currentCell;
        float bestScore = float.MinValue;
        float currentFireDistance = DistanceToNearestFire(currentCell);

        foreach (Vector3Int direction in CardinalDirections)
        {
            Vector3Int candidate = currentCell + direction;
            if (!IsWalkableCitizenCell(candidate))
            {
                continue;
            }

            if (_fireManager != null && _fireManager.IsBurningAt(candidate))
            {
                continue;
            }

            float candidateFireDistance = DistanceToNearestFire(candidate);
            if (candidateFireDistance <= fireAvoidDistance * 0.6f)
            {
                // Avoid stepping too close to active fire when possible.
                continue;
            }

            float moveAwayBonus = candidateFireDistance - currentFireDistance;
            float randomJitter = Random.Range(-0.05f, 0.05f);
            float score = candidateFireDistance * 2f + moveAwayBonus * 3f + randomJitter;

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate != currentCell)
        {
            return bestCandidate;
        }

        return currentCell;
    }

    private float DistanceToNearestFire(Vector3Int cell)
    {
        if (_fireManager == null)
        {
            return float.MaxValue;
        }

        Vector2 cellCenter = GetCellCenter(cell);
        Vector2? nearest = _fireManager.GetNearestFirePosition(cellCenter, fireAvoidDistance);
        if (!nearest.HasValue)
        {
            return float.MaxValue;
        }

        return Vector2.Distance(cellCenter, nearest.Value);
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
        return hasSidewalk || hasCrosswalk;
    }
}
