using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a single firetruck: selection, A* pathfinding movement, stopping, and fire extinguishing.
///
/// SETUP:
///  1. Attach to your firetruck GameObject.
///  2. Add a Collider2D — required for OnMouseDown click detection.
///  3. Assign selectionIndicator and waterSprayVFX in the Inspector (optional).
///  4. AStarPathfinder and FireManager must exist in the scene.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FireTruck : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // State machine
    // -------------------------------------------------------------------------

    public enum TruckState { Idle, Selected, Moving, Arrived, Extinguishing }

    [Header("State (read-only)")]
    [SerializeField] private TruckState _state = TruckState.Idle;
    public TruckState State => _state;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float waypointThreshold = 0.05f;

    [Header("Extinguishing")]
    public float extinguishRadius   = 1.5f;
    public float extinguishInterval = 0.8f;

    [Header("Visuals")]
    public SpriteRenderer selectionIndicator;
    public ParticleSystem waterSprayVFX;

    // -------------------------------------------------------------------------
    // Private runtime
    // -------------------------------------------------------------------------

    private AStarPathfinder   _pathfinder;
    private FiretruckOutline  _outline;
    private FiretruckAnimation _anim;
    private List<Vector3>   _currentPath = new List<Vector3>();
    private Coroutine       _moveCoroutine;
    private Coroutine       _extinguishCoroutine;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _pathfinder = FindObjectOfType<AStarPathfinder>();
        _outline    = GetComponent<FiretruckOutline>();
        _anim       = GetComponent<FiretruckAnimation>();
        if (_pathfinder == null)
            Debug.LogError("[FireTruck] No AStarPathfinder found in scene!");

        SetSelectionIndicator(false);
    }

    private void OnMouseDown()
    {
        TruckSelectionManager.Instance?.SelectTruck(this);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void OnSelected()
    {
        _state = TruckState.Selected;
        SetSelectionIndicator(true);
        _outline?.ShowOutline();
    }

    public void OnDeselected()
    {
        if (_state == TruckState.Selected)
            _state = TruckState.Idle;
        SetSelectionIndicator(false);
        _outline?.HideOutline();
    }

    /// <summary>Pathfind to a world-space destination. Called after [Q] + map click.</summary>
    public void MoveTo(Vector3 worldDestination)
    {
        if (_pathfinder == null) return;

        StopAllTruckCoroutines();

        List<Vector3> path = _pathfinder.FindPath(transform.position, worldDestination);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("[FireTruck] No path found to destination.");
            return;
        }

        _currentPath   = path;
        _state         = TruckState.Moving;
        TruckHUD.Instance?.OnTruckMoving();
        _moveCoroutine = StartCoroutine(FollowPath());
    }

    /// <summary>Stop moving immediately. Called by [W].</summary>
    public void StopTruck()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
        SetWaterVFX(false);
        _currentPath.Clear();
        _state = TruckState.Arrived;
        TruckHUD.Instance?.OnTruckStopped(this);
    }

    /// <summary>Begin extinguishing fires in range. Called by [E].</summary>
    public void StartExtinguishing()
    {
        if (_state == TruckState.Moving) return;

        StopAllTruckCoroutines();
        _state               = TruckState.Extinguishing;
        TruckHUD.Instance?.OnExtinguishStarted();
        _extinguishCoroutine = StartCoroutine(ExtinguishRoutine());
    }

    public void StopExtinguishing()
    {
        if (_extinguishCoroutine != null) StopCoroutine(_extinguishCoroutine);
        SetWaterVFX(false);
        _state = TruckState.Idle;
    }

    // -------------------------------------------------------------------------
    // Movement coroutine
    // -------------------------------------------------------------------------

    private IEnumerator FollowPath()
    {
        foreach (Vector3 waypoint in _currentPath)
        {
            // Tell the animator the exact direction to this waypoint up front —
            // no per-frame delta needed, no flicker.
            Vector2 dir = (new Vector2(waypoint.x, waypoint.y)
                         - new Vector2(transform.position.x, transform.position.y)).normalized;
            _anim?.SetMovementDirection(dir);

            while (Vector3.Distance(transform.position, waypoint) > waypointThreshold)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, waypoint, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = waypoint;
        }
        _state = TruckState.Arrived;
        TruckHUD.Instance?.OnTruckArrived(this);
    }

    // -------------------------------------------------------------------------
    // Extinguish coroutine
    // -------------------------------------------------------------------------

    private IEnumerator ExtinguishRoutine()
    {
        SetWaterVFX(true);

        while (true)
        {
            FireTile hit = FireManager.Instance?.ExtinguishNearest(
                new Vector2(transform.position.x, transform.position.y),
                extinguishRadius);

            if (hit == null)
            {
                Debug.Log("[FireTruck] No fires in range — stopping.");
                break;
            }

            yield return new WaitForSeconds(extinguishInterval);
        }

        SetWaterVFX(false);
        _state = TruckState.Idle;
        TruckHUD.Instance?.OnExtinguishFinished(this);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void StopAllTruckCoroutines()
    {
        if (_moveCoroutine       != null) StopCoroutine(_moveCoroutine);
        if (_extinguishCoroutine != null) StopCoroutine(_extinguishCoroutine);
        SetWaterVFX(false);
    }

    private void SetSelectionIndicator(bool active)
    {
        if (selectionIndicator != null)
            selectionIndicator.enabled = active;
    }

    private void SetWaterVFX(bool active)
    {
        if (waterSprayVFX == null) return;
        if (active) waterSprayVFX.Play();
        else        waterSprayVFX.Stop();
    }
}