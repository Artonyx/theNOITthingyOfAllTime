using UnityEngine;
using UnityEngine.UI;

public class WaterIndicator : MonoBehaviour
{
    [Header("UI")]
    public Slider waterSlider;

    [Header("Reference")]
    public Helicopter helicopter;

    [Header("Fill Color (optional)")]
    [Tooltip("The Fill image of the slider — for color changes based on water level.")]
    public Image fillImage;
    public Color fullColor     = new Color(0.2f, 0.6f, 1f, 1f);  // blue
    public Color criticalColor = new Color(1f,   0.2f, 0.2f, 1f); // red
    [Tooltip("Water fraction below which the bar turns red.")]
    [Range(0f, 1f)] public float criticalThreshold = 0.25f;

    // -------------------------------------------------------------------------

    private void Start()
    {
        if (waterSlider == null || helicopter == null) return;

        waterSlider.minValue = 0;
        waterSlider.maxValue = helicopter.maxWater;
        waterSlider.value    = helicopter.CurrentWater;
    }

    private void Update()
    {
        if (waterSlider == null || helicopter == null) return;

        waterSlider.value = helicopter.CurrentWater;

        if (fillImage != null)
        {
            float fraction = (float)helicopter.CurrentWater / helicopter.maxWater;
            fillImage.color = fraction <= criticalThreshold
                ? criticalColor
                : Color.Lerp(criticalColor, fullColor,
                    (fraction - criticalThreshold) / (1f - criticalThreshold));
        }
    }
}