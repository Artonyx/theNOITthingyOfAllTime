using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The bottom-of-screen HUD panel that slides up when a firetruck is selected.
/// Shows three action buttons: [Q] Move  [W] Stop  [E] Extinguish
///
/// SETUP (Unity Canvas):
///  1. Create a Canvas (Screen Space – Overlay) if you don't have one.
///  2. Create an empty child GameObject: name it "TruckHUD". Attach this script.
///  3. Add a child Panel (the visual background). Assign to 'panelRoot'.
///     - Anchor: bottom-center  (min 0.5/0 max 0.5/0, pivot 0.5/0)
///     - Size: ~360 x 90
///  4. Inside the panel create three Button children. Assign to moveButton, stopButton, extinguishButton.
///  5. Each button should have:
///       - A TextMeshProUGUI child showing e.g. "[Q] Move"
///       - A second small TextMeshProUGUI for the key badge (assigned to moveKeyLabel etc.)
///  6. Assign the three ButtonHighlight Image references for the active-state glow.
/// </summary>
public class TruckHUD : MonoBehaviour
{
    public static TruckHUD Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector wiring
    // -------------------------------------------------------------------------

    [Header("Panel Root (slides up/down)")]
    public GameObject panelRoot;

    [Header("Buttons")]
    public Button moveButton;
    public Button stopButton;
    public Button extinguishButton;

    [Header("Button Highlight Images (shown when active/selected state)")]
    public Image moveButtonHighlight;
    public Image stopButtonHighlight;
    public Image extinguishButtonHighlight;

    [Header("Button Labels (TextMeshPro)")]
    public TextMeshProUGUI moveLabel;
    public TextMeshProUGUI stopLabel;
    public TextMeshProUGUI extinguishLabel;

    [Header("Truck Name Label")]
    [Tooltip("Optional label showing which truck is selected e.g. 'Truck 1'")]
    public TextMeshProUGUI truckNameLabel;

    [Header("Animation")]
    [Tooltip("How far below screen the panel sits when hidden (pixels).")]
    public float hiddenOffsetY = -120f;
    [Tooltip("Y position when fully visible (pixels from bottom, usually 0).")]
    public float visibleOffsetY = 0f;
    [Tooltip("Slide animation duration in seconds.")]
    public float slideDuration = 0.2f;

    // -------------------------------------------------------------------------
    // Private runtime
    // -------------------------------------------------------------------------

    private bool      _visible = false;
    private Coroutine _slideCoroutine;

    // Which button is currently 'active' (lit up)
    private enum ActiveButton { None, Move, Stop, Extinguish }
    private ActiveButton _activeButton = ActiveButton.None;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Start hidden below screen
        /*if (panelRoot != null)
        {
            Vector2 pos = panelRoot.anchoredPosition;
            panelRoot.anchoredPosition = new Vector2(pos.x, hiddenOffsetY);
        }*/

        // Wire button clicks
        moveButton?.onClick.AddListener(OnMoveClicked);
        stopButton?.onClick.AddListener(OnStopClicked);
        extinguishButton?.onClick.AddListener(OnExtinguishClicked);

        //SetLabels();
        //SetHighlight(ActiveButton.None);
    }

    private void Update()
    {
        if (!_visible) return;

        // Keyboard shortcuts — only active when HUD is shown
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

        //SetHighlight(ActiveButton.None);
        RefreshExtinguishInteractable(truck);
        //SlideIn();
    }

    public void Hide()
    {
        //SlideOut();
        panelRoot.SetActive(false);
    }

    /// <summary>Call after truck arrives to re-check if Extinguish should be enabled.</summary>
    public void RefreshExtinguishInteractable(FireTruck truck)
    {
        if (extinguishButton == null) return;

        bool nearFire = FireManager.Instance != null &&
                        FireManager.Instance.HasNearbyFire(
                            new Vector2(truck.transform.position.x, truck.transform.position.y),
                            truck.extinguishRadius);

        extinguishButton.interactable = nearFire;

        // Dim the label when not interactable
        if (extinguishLabel != null)
            extinguishLabel.alpha = nearFire ? 1f : 0.4f;
    }

    // -------------------------------------------------------------------------
    // Button handlers
    // -------------------------------------------------------------------------

    private void OnMoveClicked()
    {
        //SetHighlight(ActiveButton.Move);
        TruckSelectionManager.Instance?.BeginMoveTargeting();
    }

    private void OnStopClicked()
    {
        //SetHighlight(ActiveButton.Stop);
        TruckSelectionManager.Instance?.CommandStop();
    }

    private void OnExtinguishClicked()
    {
        if (extinguishButton != null && !extinguishButton.interactable) return;
        //SetHighlight(ActiveButton.Extinguish);
        TruckSelectionManager.Instance?.CommandExtinguish();
    }

    // -------------------------------------------------------------------------
    // Visual state
    // -------------------------------------------------------------------------
    /*
    private void SetHighlight(ActiveButton active)
    {
        _activeButton = active;
        SetImageActive(moveButtonHighlight,       active == ActiveButton.Move);
        SetImageActive(stopButtonHighlight,       active == ActiveButton.Stop);
        SetImageActive(extinguishButtonHighlight, active == ActiveButton.Extinguish);
    }

    private void SetImageActive(Image img, bool on)
    {
        if (img != null) img.enabled = on;
    }

    private void SetLabels()
    {
        if (moveLabel       != null) moveLabel.text       = "[Q]  Move";
        if (stopLabel       != null) stopLabel.text       = "[W]  Stop";
        if (extinguishLabel != null) extinguishLabel.text = "[E]  Extinguish";
    }

    // -------------------------------------------------------------------------
    // Slide animation
    // -------------------------------------------------------------------------

    private void SlideIn()
    {
        _visible = true;
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(SlideTo(visibleOffsetY));
    }

    private void SlideOut()
    {
        _visible = false;
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(SlideTo(hiddenOffsetY));
    }

    private IEnumerator SlideTo(float targetY)
    {
        if (panelRoot == null) yield break;

        float startY  = panelRoot.anchoredPosition.y;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            Vector2 pos = panelRoot.anchoredPosition;
            panelRoot.anchoredPosition = new Vector2(pos.x, Mathf.Lerp(startY, targetY, t));
            yield return null;
        }

        Vector2 finalPos = panelRoot.anchoredPosition;
        panelRoot.anchoredPosition = new Vector2(finalPos.x, targetY);
    }*/
}