using UnityEngine;

public class TruckSelectionManager : MonoBehaviour
{
    public static TruckSelectionManager Instance { get; private set; }

    public ISelectableUnit SelectedUnit { get; private set; }

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

    public void SelectTruck(FireTruck truck) => SelectUnit(truck);

    public void SelectUnit(ISelectableUnit unit)
    {
        if (SelectedUnit == unit) return;

        SelectedUnit?.OnDeselected();
        SelectedUnit = unit;
        SelectedUnit?.OnSelected();

        _waitingForMoveTarget = false;
        TruckHUD.Instance?.ShowForUnit(unit);
    }

    public void Deselect()
    {
        SelectedUnit?.OnDeselected();
        SelectedUnit          = null;
        _waitingForMoveTarget = false;
        TruckHUD.Instance?.Hide();
    }

    public void BeginMoveTargeting()
    {
        if (SelectedUnit == null) return;
        _waitingForMoveTarget = true;
        TruckHUD.Instance?.SetAwaitingTarget(true);
        UIPanel.Instance?.SetAwaitingTarget(true);
    }

    public void CommandStop()
    {
        SelectedUnit?.StopUnit();
    }

    public void CommandExtinguish()
    {
        SelectedUnit?.StartExtinguishing();
    }

    private void HandleMapClick()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;

        SelectedUnit?.MoveTo(worldPos);

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