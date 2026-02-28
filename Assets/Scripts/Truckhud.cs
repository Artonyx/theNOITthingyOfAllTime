using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// SETUP ADDITION: Add a CanvasGroup component to your hudPanel GameObject.
/// Everything else stays the same.
/// </summary>
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

    [Header("Hover Transparency")]
    [Tooltip("Panel alpha when not hovered (fully opaque = 1).")]
    [Range(0f, 1f)] public float normalAlpha = 1f;
    [Tooltip("Panel alpha when hovered over.")]
    [Range(0f, 1f)] public float hoverAlpha  = 0.4f;
    [Tooltip("Speed of the alpha fade transition.")]
    public float fadeSspeed = 8f;

    [Header("Labels (TextMeshPro — optional)")]
    public TextMeshProUGUI moveLabel;
    public TextMeshProUGUI stopLabel;
    public TextMeshProUGUI extinguishLabel;
    public TextMeshProUGUI truckNameLabel;

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private CanvasGroup _canvasGroup;
    private Coroutine   _stopFlashCoroutine;
    private Coroutine   _fadeCoroutine;
    private bool        _isHovered         = false;
    private bool        _isAwaitingTarget  = false; // true while waiting for map click

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Get or add CanvasGroup on the panel — controls alpha + interactability
        if (hudPanel != null)
        {
            _canvasGroup = hudPanel.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = hudPanel.AddComponent<CanvasGroup>();
        }

        cancelButton?.onClick.AddListener(OnCancelClicked);
        moveButton?.onClick.AddListener(OnMoveClicked);
        stopButton?.onClick.AddListener(OnStopClicked);
        extinguishButton?.onClick.AddListener(OnExtinguishClicked);

        SetLabels();
        ClearAllHighlights();
        hudPanel?.SetActive(false);
    }

    private void Update()
    {
        if (hudPanel == null || !hudPanel.activeSelf) return;
        if (_isAwaitingTarget) return; // block keyboard shortcuts during targeting too

        if (Input.GetKeyDown(KeyCode.Q)) OnMoveClicked();
        if (Input.GetKeyDown(KeyCode.W)) OnStopClicked();
        if (Input.GetKeyDown(KeyCode.E)) OnExtinguishClicked();
    }

    // -------------------------------------------------------------------------
    // Pointer hover — IPointerEnterHandler / IPointerExitHandler
    // Note: your hudPanel needs a Graphic (Image) component to receive raycasts.
    // -------------------------------------------------------------------------

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        if (!_isAwaitingTarget) // don't override targeting fade
            FadeTo(hoverAlpha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        if (!_isAwaitingTarget) // don't restore if locked out
            FadeTo(normalAlpha);
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
        SetAwaitingTarget(false);
        hudPanel?.SetActive(true);

        // Start fully opaque
        if (_canvasGroup != null) _canvasGroup.alpha = normalAlpha;
    }

    public void Hide()
    {
        ClearAllHighlights();
        SetAwaitingTarget(false);
        hudPanel?.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Awaiting move target — lock out the HUD while player clicks the map
    // -------------------------------------------------------------------------

    public void SetAwaitingTarget(bool awaiting)
    {
        _isAwaitingTarget = awaiting;

        if (_canvasGroup != null)
        {
            // Non-interactable and blocks no raycasts while awaiting target —
            // clicks pass through to the map underneath
            _canvasGroup.interactable   = !awaiting;
            _canvasGroup.blocksRaycasts = !awaiting;
        }

        // Fade to semi-transparent while locked, restore when done
        FadeTo(awaiting ? hoverAlpha : (_isHovered ? hoverAlpha : normalAlpha));
    }

    // -------------------------------------------------------------------------
    // State transitions called by FireTruck
    // -------------------------------------------------------------------------

    public void OnTruckMoving()
    {
        SetAwaitingTarget(false); // targeting done — truck is now moving
        UIPanel.Instance?.SetAwaitingTarget(false);
        SetButtonColor(moveButton,       true);
        SetButtonColor(stopButton,       false);
        SetButtonColor(extinguishButton, false);
    }

    public void OnTruckArrived(FireTruck truck)
    {
        SetButtonColor(moveButton, false);
        RefreshExtinguishInteractable(truck);
    }

    public void OnTruckStopped(FireTruck truck)
    {
        SetButtonColor(moveButton, false);
        if (_stopFlashCoroutine != null) StopCoroutine(_stopFlashCoroutine);
        _stopFlashCoroutine = StartCoroutine(StopFlashRoutine());
        RefreshExtinguishInteractable(truck);
    }

    public void OnExtinguishStarted()
    {
        SetButtonColor(extinguishButton, true);
    }

    public void OnExtinguishFinished(FireTruck truck)
    {
        SetButtonColor(extinguishButton, false);
        RefreshExtinguishInteractable(truck);
    }

    // -------------------------------------------------------------------------
    // Extinguish interactability
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
    // Button handlers
    // -------------------------------------------------------------------------

    private void OnCancelClicked()
    {
        TruckSelectionManager.Instance?.Deselect();
        Hide();
    }

    private void OnMoveClicked()
    {
        SetButtonColor(moveButton, true);
        SetAwaitingTarget(true);  // lock HUD while player picks a spot
        TruckSelectionManager.Instance?.BeginMoveTargeting();
    }

    private void OnStopClicked()
    {
        TruckSelectionManager.Instance?.CommandStop();
    }

    private void OnExtinguishClicked()
    {
        if (extinguishButton != null && !extinguishButton.interactable) return;
        TruckSelectionManager.Instance?.CommandExtinguish();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private IEnumerator StopFlashRoutine()
    {
        SetButtonColor(stopButton, true);
        yield return new WaitForSecondsRealtime(stopFlashDuration);
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

    private void FadeTo(float targetAlpha)
    {
        if (_canvasGroup == null) return;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        while (!Mathf.Approximately(_canvasGroup.alpha, targetAlpha))
        {
            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha, targetAlpha, fadeSspeed * Time.unscaledDeltaTime);
            yield return null;
        }
        _canvasGroup.alpha = targetAlpha;
    }

    private void SetLabels()
    {
        if (moveLabel       != null) moveLabel.text       = "[Q]  Move";
        if (stopLabel       != null) stopLabel.text       = "[W]  Stop";
        if (extinguishLabel != null) extinguishLabel.text = "[E]  Extinguish";
    }
}