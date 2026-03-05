using System.Collections;
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

    [Header("Death Chance")]
    [Tooltip("Seconds a large fire must burn before it can start claiming victims.")]
    public float largeFireGracePeriod = 15f;
    [Tooltip("Seconds between each death roll while a large fire is active.")]
    public float deathCheckInterval   = 8f;
    [Tooltip("Chance per check that a large fire claims a victim (0–1).")]
    [Range(0f, 1f)] public float deathChance = 0.3f;

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
        StartCoroutine(LargeFireDeathRoutine(cell));
    }

    public void OnFireNoLongerLarge(Vector3Int cell)
    {
    }

    private IEnumerator LargeFireDeathRoutine(Vector3Int cell)
    {
        yield return new WaitForSeconds(largeFireGracePeriod);

        while (true)
        {
            if (!FireManager.Instance.IsBurningAt(cell)) yield break;
            if (FireManager.Instance.GetStageAt(cell) != FireManager.FireStage.Large) yield break;

            if (Random.value < deathChance)
            {
                Deaths++;
                Deaths = Mathf.Min(Deaths, TotalPopulation);
                UpdateDisplay();
                Debug.Log($"[CityPopulation] A victim was claimed by fire at {cell}. Deaths: {Deaths}/{TotalPopulation}");
            }

            yield return new WaitForSeconds(deathCheckInterval);
        }
    }

    private void UpdateDisplay()
    {
        if (deathCounterText != null)
            deathCounterText.text = $"{Deaths}/{TotalPopulation}";
    }
}