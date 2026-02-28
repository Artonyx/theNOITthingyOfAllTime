using UnityEngine;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// Countdown timer displayed as MM:SS.
/// Attach to any GameObject and assign your TMP text object.
/// </summary>
public class CountdownTimer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Total countdown time in seconds. 60 = 1 minute.")]
    public float totalTime = 60f;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("Events")]
    [Tooltip("Fired when timer hits 0 AND no deaths AND no active fires — win condition.")]
    public UnityEvent onTimerWin;
    [Tooltip("Fired when timer hits 0 AND there are deaths or active fires — loss condition.")]
    public UnityEvent onTimerLoss;

    private float _timeRemaining;
    private bool  _running = true;

    // -------------------------------------------------------------------------

    private void Start()
    {
        _timeRemaining = totalTime;
        UpdateDisplay();
    }

    private void Update()
    {
        if (!_running) return;

        // Respect pause/freeze — timeScale 0 stops deltaTime
        _timeRemaining -= Time.deltaTime;

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            _running       = false;
            UpdateDisplay();

            bool hasDeaths   = CityPopulation.Instance != null && CityPopulation.Instance.HasDeaths;
            bool hasFires    = FireManager.Instance    != null && FireManager.Instance.ActiveFireCount > 0;

            if (!hasDeaths && !hasFires)
                onTimerWin?.Invoke();
            else
                onTimerLoss?.Invoke();

            return;
        }

        UpdateDisplay();
    }

    // -------------------------------------------------------------------------

    private void UpdateDisplay()
    {
        int minutes = Mathf.FloorToInt(_timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(_timeRemaining % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    // -------------------------------------------------------------------------
    // Public API — call these from other scripts if needed

    public void StopTimer()  => _running = false;
    public void StartTimer() => _running = true;
    public void ResetTimer() { _timeRemaining = totalTime; _running = true; }
}