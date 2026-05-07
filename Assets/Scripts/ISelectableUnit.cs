using UnityEngine;

public interface ISelectableUnit
{
    float ExtinguishRadius { get; }
    Transform Transform { get; }

    void OnSelected();
    void OnDeselected();

    void MoveTo(Vector3 worldDestination);
    void StopUnit();

    void StartExtinguishing();
    void StopExtinguishing();
}

public interface IWaterRechargeable
{
    void RechargeWater();
    bool HasWater { get; }
}
