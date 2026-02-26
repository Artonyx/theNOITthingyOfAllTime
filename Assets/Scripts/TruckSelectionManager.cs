using UnityEngine;

/// <summary>
/// Singleton that tracks which firetruck is currently selected
/// and routes map-click and keyboard commands to it.
///
/// Attach to a persistent GameObject (e.g. "GameManager").
/// </summary>
public class TruckSelectionManager : MonoBehaviour
{
    public static TruckSelectionManager Instance { get; private set; }

    public FireTruck SelectedTruck { get; private set; }

    private bool   _waitingForMoveTarget = false;
    private Camera _mainCamera;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance    = this;
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!_waitingForMoveTarget) return;

        if (Input.GetMouseButtonDown(0))
            HandleMapClick();

        if (Input.GetMouseButtonDown(1))
            CancelMoveTarget();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Select a truck — deselects the previous one and shows the HUD.</summary>
    public void SelectTruck(FireTruck truck)
    {
        if (SelectedTruck == truck) return;

        SelectedTruck?.OnDeselected();
        SelectedTruck = truck;
        SelectedTruck.OnSelected();

        _waitingForMoveTarget = false;
        TruckHUD.Instance?.ShowForTruck(truck);
    }

    /// <summary>Deselect all trucks and hide the HUD.</summary>
    public void Deselect()
    {
        SelectedTruck?.OnDeselected();
        SelectedTruck         = null;
        _waitingForMoveTarget = false;
        TruckHUD.Instance?.Hide();
    }

    /// <summary>[Q] — enter move-targeting mode; next map click sends the truck there.</summary>
    public void BeginMoveTargeting()
    {
        if (SelectedTruck == null) return;
        _waitingForMoveTarget = true;
        // Optionally change cursor here
    }

    /// <summary>[W] — stop the truck immediately wherever it is.</summary>
    public void CommandStop()
    {
        SelectedTruck?.StopTruck();
    }

    /// <summary>[E] — begin extinguishing fires in range.</summary>
    public void CommandExtinguish()
    {
        SelectedTruck?.StartExtinguishing();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void HandleMapClick()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;

        SelectedTruck?.MoveTo(worldPos);

        _waitingForMoveTarget = false;
    }

    private void CancelMoveTarget()
    {
        _waitingForMoveTarget = false;
    }
}