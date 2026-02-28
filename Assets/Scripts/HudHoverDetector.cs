using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this to the hudPanel GameObject.
/// It forwards pointer enter/exit events up to TruckHUD.
/// </summary>
public class HUDHoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
        => TruckHUD.Instance?.OnPointerEnter(eventData);

    public void OnPointerExit(PointerEventData eventData)
        => TruckHUD.Instance?.OnPointerExit(eventData);
}