using UnityEngine;
using TMPro;

public class CityPopulation : MonoBehaviour
{
    public static CityPopulation Instance { get; private set; }

    [Header("Population")]
    [Tooltip("Minimum total population of the city.")]
    public int minPopulation = 30;
    [Tooltip("Maximum total population of the city.")]
    public int maxPopulation = 80;

    [Header("UI")]
    public TextMeshProUGUI deathCounterText;

    public int TotalPopulation { get; private set; }
    public int Deaths          { get; private set; } = 0;
    public bool HasDeaths      => Deaths > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        TotalPopulation = Random.Range(minPopulation, maxPopulation + 1);
        UpdateDisplay();
    }

    public void OnFireBecameLarge(Vector3Int cell)
    {
        // Reserved for future per-building evacuation logic.
    }

    public void OnFireNoLongerLarge(Vector3Int cell)
    {
    }

    public void RegisterCitizenDeath()
    {
        Deaths++;
        Deaths = Mathf.Min(Deaths, TotalPopulation);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (deathCounterText != null)
            deathCounterText.text = $"{Deaths}/{TotalPopulation}";
    }
}