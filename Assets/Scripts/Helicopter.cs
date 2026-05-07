using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Helicopter : MonoBehaviour, ISelectableUnit, IWaterRechargeable
{
    public enum HelicopterState { Idle, Selected, Moving, Arrived, Extinguishing }

    [Header("State (read-only)")]
    [SerializeField] private HelicopterState _state = HelicopterState.Idle;
    public HelicopterState State => _state;

    [Header("Flying Movement")]
    public float flySpeed = 6f;
    public float waypointThreshold = 0.05f;

    [Header("Extinguishing")]
    [Tooltip("Helicopter can only extinguish within a very small radius (2 tiles).")]
    public float extinguishRadius = 2f;
    public float extinguishInterval = 0.9f;
    public float ExtinguishRadius => extinguishRadius;
    public Transform Transform => transform;

    [Header("Water")]
    public int maxWater = 8;
    [SerializeField] private int _currentWater = 8;
    public bool HasWater => _currentWater > 0;

    [Header("Input")]
    public KeyCode rechargeKey = KeyCode.R;

    [Header("Visuals")]
    public SpriteRenderer selectionIndicator;
    public ParticleSystem waterSprayVFX;

    private HelicopterOutline _outline;
    private Coroutine _moveCoroutine;
    private Coroutine _extinguishCoroutine;

    private void Awake()
    {
        _outline = GetComponent<HelicopterOutline>();
        SetSelectionIndicator(false);

        if (maxWater <= 0) maxWater = 1;
        _currentWater = Mathf.Clamp(_currentWater, 0, maxWater);
    }

    private void Update()
    {
        if (Input.GetKeyDown(rechargeKey))
            RechargeWater();
    }

    private void OnMouseDown()
    {
        TruckSelectionManager.Instance?.SelectUnit(this);
    }

    public void OnSelected()
    {
        _state = HelicopterState.Selected;
        SetSelectionIndicator(true);
        _outline?.ShowOutline();
    }

    public void OnDeselected()
    {
        if (_state == HelicopterState.Selected)
            _state = HelicopterState.Idle;
        SetSelectionIndicator(false);
        _outline?.HideOutline();
    }

    public void MoveTo(Vector3 worldDestination)
    {
        StopAllCoroutinesAndVfx();
        _state = HelicopterState.Moving;
        TruckHUD.Instance?.OnUnitMoving();
        _moveCoroutine = StartCoroutine(FlyTo(worldDestination));
    }

    public void StopUnit()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
        SetWaterVFX(false);
        _state = HelicopterState.Arrived;
        TruckHUD.Instance?.OnUnitStopped(this);
    }

    public void StartExtinguishing()
    {
        if (_state == HelicopterState.Moving) return;

        StopAllCoroutinesAndVfx();
        _state = HelicopterState.Extinguishing;
        TruckHUD.Instance?.OnExtinguishStarted();
        _extinguishCoroutine = StartCoroutine(ExtinguishRoutine());
    }

    public void StopExtinguishing()
    {
        if (_extinguishCoroutine != null) StopCoroutine(_extinguishCoroutine);
        SetWaterVFX(false);
        _state = HelicopterState.Idle;
    }

    public void RechargeWater()
    {
        _currentWater = maxWater;
    }

    private IEnumerator FlyTo(Vector3 worldDestination)
    {
        worldDestination.z = transform.position.z;

        Vector2 dir = (new Vector2(worldDestination.x, worldDestination.y)
                     - new Vector2(transform.position.x, transform.position.y)).normalized;
        if (dir != Vector2.zero) _lastMoveDir = dir;

        while (Vector3.Distance(transform.position, worldDestination) > waypointThreshold)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, worldDestination, flySpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = worldDestination;
        _state = HelicopterState.Arrived;
        TruckHUD.Instance?.OnUnitArrived(this);
    }

    private IEnumerator ExtinguishRoutine()
    {
        SetWaterVFX(true);

        while (true)
        {
            if (_currentWater <= 0)
            {
                Debug.Log("[Helicopter] Out of water — press R to recharge.");
                break;
            }

            bool hit = FireManager.Instance != null &&
                       FireManager.Instance.ExtinguishNearest(
                           new Vector2(transform.position.x, transform.position.y),
                           extinguishRadius);

            if (!hit)
            {
                Debug.Log("[Helicopter] No fires in range — stopping.");
                break;
            }

            _currentWater = Mathf.Max(0, _currentWater - 1);
            yield return new WaitForSeconds(extinguishInterval);
        }

        SetWaterVFX(false);
        _state = HelicopterState.Idle;
        TruckHUD.Instance?.OnExtinguishFinished(this);
    }

    private void StopAllCoroutinesAndVfx()
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        if (_extinguishCoroutine != null) StopCoroutine(_extinguishCoroutine);
        _moveCoroutine = null;
        _extinguishCoroutine = null;
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
            waterSprayVFX.Play();
        else
            waterSprayVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
