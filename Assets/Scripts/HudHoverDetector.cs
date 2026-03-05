using UnityEngine;
using UnityEngine.EventSystems;

public class HUDHoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
        => TruckHUD.Instance?.OnPointerEnter(eventData);

    public void OnPointerExit(PointerEventData eventData)
        => TruckHUD.Instance?.OnPointerExit(eventData);
}