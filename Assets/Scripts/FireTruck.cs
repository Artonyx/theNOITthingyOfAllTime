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
    [Tooltip("How wide the water spray fans out. 0 = straight line, 0.5 = wide cone.")]
    [Range(0f, 1f)] public float coneSpread = 0.25f;

    // -------------------------------------------------------------------------
    // Private runtime
    // -------------------------------------------------------------------------

    private AStarPathfinder   _pathfinder;
    private FiretruckOutline  _outline;
    private FiretruckAnimation _anim;
    private Vector2            _lastMoveDir = Vector2.up; // direction truck last travelled
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
            _lastMoveDir = dir; // remember for VFX orientation

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
            // Re-aim spray toward current nearest fire each tick
            PointVFXTowardNearestFire();

            bool hit = FireManager.Instance != null &&
                       FireManager.Instance.ExtinguishNearest(
                           new Vector2(transform.position.x, transform.position.y),
                           extinguishRadius);

            if (!hit)
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

        if (active)
        {
            PointVFXTowardNearestFire();
            waterSprayVFX.Play();
        }
        else
        {
            // Stop emitting new particles immediately and clear all existing
            // ones so they don't drift in the last set velocity direction.
            waterSprayVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Reset velocity over lifetime to zero so there's no residual direction
            var vel   = waterSprayVFX.velocityOverLifetime;
            vel.x     = new ParticleSystem.MinMaxCurve(0f);
            vel.y     = new ParticleSystem.MinMaxCurve(0f);
            vel.z     = new ParticleSystem.MinMaxCurve(0f);
        }
    }

    private void PointVFXTowardNearestFire()
    {
        if (FireManager.Instance == null) return;

        Vector2 truckPos = new Vector2(transform.position.x, transform.position.y);
        Vector2? firePos = FireManager.Instance.GetNearestFirePosition(truckPos, extinguishRadius);

        Vector2 direction = firePos.HasValue
            ? (firePos.Value - truckPos).normalized
            : _lastMoveDir;

        if (direction == Vector2.zero) return;

        var main  = waterSprayVFX.main;
        float speed = main.startSpeed.constant;

        // Compute a perpendicular axis to the spray direction so we can
        // spread particles sideways, simulating a cone without the Shape module.
        Vector2 perp = new Vector2(-direction.y, direction.x); // 90 degree rotation

        // coneSpread controls how wide the spray fans out.
        // At 0 it's a straight line; at 1 it's very wide.
        float spread = speed * coneSpread;

        var vel   = waterSprayVFX.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;

        // Each particle gets a random velocity between the two edges of the cone.
        // TwoConstants mode picks a random value between min and max per particle.
        vel.x = new ParticleSystem.MinMaxCurve(
            direction.x * speed - perp.x * spread,
            direction.x * speed + perp.x * spread)
            { mode = ParticleSystemCurveMode.TwoConstants };
        vel.y = new ParticleSystem.MinMaxCurve(
            direction.y * speed - perp.y * spread,
            direction.y * speed + perp.y * spread)
            { mode = ParticleSystemCurveMode.TwoConstants };
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f)
            { mode = ParticleSystemCurveMode.TwoConstants };

        var shape = waterSprayVFX.shape;
        shape.enabled = false;
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, extinguishRadius);

        if (_currentPath == null || _currentPath.Count == 0) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < _currentPath.Count - 1; i++)
            Gizmos.DrawLine(_currentPath[i], _currentPath[i + 1]);
        Gizmos.color = Color.yellow;
        foreach (var pt in _currentPath)
            Gizmos.DrawSphere(pt, 0.12f);
    }
#endif
}