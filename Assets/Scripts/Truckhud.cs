using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TruckHUD : MonoBehaviour
{
    public static TruckHUD Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Panel")]
    public GameObject hudPanel;

    [Header("Buttons")]
    public Button cancelButton;
    public Button moveButton;
    public Button stopButton;
    public Button extinguishButton;

    [Header("Button Colors")]
    public Color normalColor = Color.white;
    public Color activeColor = new Color(1f, 0.55f, 0f, 1f);

    [Tooltip("How long the Stop button stays highlighted after being pressed.")]
    public float stopFlashDuration = 0.4f;

    [Header("Labels (TextMeshPro — optional)")]
    public TextMeshProUGUI moveLabel;
    public TextMeshProUGUI stopLabel;
    public TextMeshProUGUI extinguishLabel;
    public TextMeshProUGUI truckNameLabel;

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private Coroutine _stopFlashCoroutine;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        cancelButton?.onClick.AddListener(OnCancelClicked);
        moveButton?.onClick.AddListener(OnMoveClicked);
        stopButton?.onClick.AddListener(OnStopClicked);
        extinguishButton?.onClick.AddListener(OnExtinguishClicked);
        
        ClearAllHighlights();
        hudPanel?.SetActive(false);
    }

    private void Update()
    {
        if (hudPanel == null || !hudPanel.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.Q)) OnMoveClicked();
        if (Input.GetKeyDown(KeyCode.W)) OnStopClicked();
        if (Input.GetKeyDown(KeyCode.E)) OnExtinguishClicked();
    }

    // -------------------------------------------------------------------------
    // Show / Hide
    // -------------------------------------------------------------------------

    public void ShowForTruck(FireTruck truck)
    {
        if (truckNameLabel != null)
            truckNameLabel.text = truck.gameObject.name;

        ClearAllHighlights();
        RefreshExtinguishInteractable(truck);
        hudPanel?.SetActive(true);
    }

    public void Hide()
    {
        ClearAllHighlights();
        hudPanel?.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Called by FireTruck at each state transition
    // -------------------------------------------------------------------------

    /// <summary>Q pressed and map click received — truck is now moving.</summary>
    public void OnTruckMoving()
    {
        // Move highlight stays on for the entire journey
        SetButtonColor(moveButton, true);
        SetButtonColor(stopButton, false);
        SetButtonColor(extinguishButton, false);
    }

    /// <summary>Truck finished pathing to destination on its own.</summary>
    public void OnTruckArrived(FireTruck truck)
    {
        SetButtonColor(moveButton, false);
        RefreshExtinguishInteractable(truck);
    }

    /// <summary>W pressed — truck was force-stopped.</summary>
    public void OnTruckStopped(FireTruck truck)
    {
        SetButtonColor(moveButton, false);

        // Flash stop highlight briefly then clear it
        if (_stopFlashCoroutine != null) StopCoroutine(_stopFlashCoroutine);
        _stopFlashCoroutine = StartCoroutine(StopFlashRoutine());

        RefreshExtinguishInteractable(truck);
    }

    /// <summary>E pressed — extinguishing has begun.</summary>
    public void OnExtinguishStarted()
    {
        SetButtonColor(extinguishButton, true);
    }

    /// <summary>Extinguishing finished (no fires left in range or manually stopped).</summary>
    public void OnExtinguishFinished(FireTruck truck)
    {
        SetButtonColor(extinguishButton, false);
        RefreshExtinguishInteractable(truck);
    }

    // -------------------------------------------------------------------------
    // Extinguish button interactability
    // -------------------------------------------------------------------------

    public void RefreshExtinguishInteractable(FireTruck truck)
    {
        if (extinguishButton == null) return;

        bool nearFire = FireManager.Instance != null &&
                        FireManager.Instance.HasNearbyFire(
                            new Vector2(truck.transform.position.x, truck.transform.position.y),
                            truck.extinguishRadius);

        extinguishButton.interactable = nearFire;

        if (extinguishLabel != null)
            extinguishLabel.alpha = nearFire ? 1f : 0.4f;
    }

    // -------------------------------------------------------------------------
    // Button click handlers
    // -------------------------------------------------------------------------

    private void OnCancelClicked()
    {
        TruckSelectionManager.Instance?.Deselect();
        Hide();
    }

    private void OnMoveClicked()
    {
        // Highlight goes on immediately when Q/Move is pressed —
        // it stays on through spot selection and the entire move.
        // FireTruck.MoveTo() → TruckHUD.OnTruckMoving() keeps it alive.
        // It turns off in OnTruckArrived() or OnTruckStopped().
        SetButtonColor(moveButton, true);
        TruckSelectionManager.Instance?.BeginMoveTargeting();
    }

    private void OnStopClicked()
    {
        TruckSelectionManager.Instance?.CommandStop();
        // Highlight is handled inside OnTruckStopped() which FireTruck calls
    }

    private void OnExtinguishClicked()
    {
        if (extinguishButton != null && !extinguishButton.interactable) return;
        TruckSelectionManager.Instance?.CommandExtinguish();
        // Highlight is handled inside OnExtinguishStarted() which FireTruck calls
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private IEnumerator StopFlashRoutine()
    {
        SetButtonColor(stopButton, true);
        yield return new WaitForSecondsRealtime(stopFlashDuration); // unscaled — works while paused
        SetButtonColor(stopButton, false);
    }

    private void ClearAllHighlights()
    {
        if (_stopFlashCoroutine != null) StopCoroutine(_stopFlashCoroutine);
        SetButtonColor(moveButton,       false);
        SetButtonColor(stopButton,       false);
        SetButtonColor(extinguishButton, false);
    }

    private void SetButtonColor(Button btn, bool isActive)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = isActive ? activeColor : normalColor;
    }
}