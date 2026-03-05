using UnityEngine;

public class TruckSelectionManager : MonoBehaviour
{
    public static TruckSelectionManager Instance { get; private set; }

    public FireTruck SelectedTruck { get; private set; }

    private bool   _waitingForMoveTarget = false;
    private Camera _mainCamera;


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

    public void SelectTruck(FireTruck truck)
    {
        if (SelectedTruck == truck) return;

        SelectedTruck?.OnDeselected();
        SelectedTruck = truck;
        SelectedTruck.OnSelected();

        _waitingForMoveTarget = false;
        TruckHUD.Instance?.ShowForTruck(truck);
    }

    public void Deselect()
    {
        SelectedTruck?.OnDeselected();
        SelectedTruck         = null;
        _waitingForMoveTarget = false;
        TruckHUD.Instance?.Hide();
    }

    public void BeginMoveTargeting()
    {
        if (SelectedTruck == null) return;
        _waitingForMoveTarget = true;
        TruckHUD.Instance?.SetAwaitingTarget(true);
        UIPanel.Instance?.SetAwaitingTarget(true);
    }

    public void CommandStop()
    {
        SelectedTruck?.StopTruck();
    }

    public void CommandExtinguish()
    {
        SelectedTruck?.StartExtinguishing();
    }

    private void HandleMapClick()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;

        SelectedTruck?.MoveTo(worldPos);

        _waitingForMoveTarget = false;
        TruckHUD.Instance?.SetAwaitingTarget(false);
        UIPanel.Instance?.SetAwaitingTarget(false);
    }

    private void CancelMoveTarget()
    {
        _waitingForMoveTarget = false;
        TruckHUD.Instance?.SetAwaitingTarget(false);
        UIPanel.Instance?.SetAwaitingTarget(false);
    }
}