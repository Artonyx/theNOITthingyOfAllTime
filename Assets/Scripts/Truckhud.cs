using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class TruckHUD : MonoBehaviour
{
    public static TruckHUD Instance { get; private set; }

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

    private CanvasGroup _canvasGroup;
    private Coroutine   _stopFlashCoroutine;
    private Coroutine   _fadeCoroutine;
    private bool        _isHovered         = false;
    private bool        _isAwaitingTarget  = false;
    private ISelectableUnit _currentUnit;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

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

        if (Input.GetKeyDown(KeyCode.R) && _currentUnit is IWaterRechargeable recharge)
            recharge.RechargeWater();

        if (_isAwaitingTarget) return;

        if (Input.GetKeyDown(KeyCode.Q)) OnMoveClicked();
        if (Input.GetKeyDown(KeyCode.W)) OnStopClicked();
        if (Input.GetKeyDown(KeyCode.E)) OnExtinguishClicked();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        if (!_isAwaitingTarget)
            FadeTo(hoverAlpha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        if (!_isAwaitingTarget)
            FadeTo(normalAlpha);
    }

    public void ShowForTruck(FireTruck truck) => ShowForUnit(truck);

    public void ShowForUnit(ISelectableUnit unit)
    {
        _currentUnit = unit;

        if (truckNameLabel != null && unit != null)
            truckNameLabel.text = unit.Transform.gameObject.name;

        ClearAllHighlights();
        RefreshExtinguishInteractable(unit);
        SetAwaitingTarget(false);
        hudPanel?.SetActive(true);

        if (_canvasGroup != null) _canvasGroup.alpha = normalAlpha;
    }

    public void Hide()
    {
        _currentUnit = null;
        ClearAllHighlights();
        SetAwaitingTarget(false);
        hudPanel?.SetActive(false);
    }

    public void SetAwaitingTarget(bool awaiting)
    {
        _isAwaitingTarget = awaiting;

        if (_canvasGroup != null)
        {
            _canvasGroup.interactable   = !awaiting;
            _canvasGroup.blocksRaycasts = !awaiting;
        }

        FadeTo(awaiting ? hoverAlpha : (_isHovered ? hoverAlpha : normalAlpha));
    }


    public void OnTruckMoving() => OnUnitMoving();
    public void OnUnitMoving()
    {
        SetAwaitingTarget(false);
        UIPanel.Instance?.SetAwaitingTarget(false);
        SetButtonColor(moveButton,       true);
        SetButtonColor(stopButton,       false);
        SetButtonColor(extinguishButton, false);
    }

    public void OnTruckArrived(FireTruck truck) => OnUnitArrived(truck);
    public void OnUnitArrived(ISelectableUnit unit)
    {
        SetButtonColor(moveButton, false);
        RefreshExtinguishInteractable(unit);
    }

    public void OnTruckStopped(FireTruck truck) => OnUnitStopped(truck);
    public void OnUnitStopped(ISelectableUnit unit)
    {
        SetButtonColor(moveButton, false);
        if (_stopFlashCoroutine != null) StopCoroutine(_stopFlashCoroutine);
        _stopFlashCoroutine = StartCoroutine(StopFlashRoutine());
        RefreshExtinguishInteractable(unit);
    }

    public void OnExtinguishStarted()
    {
        SetButtonColor(extinguishButton, true);
    }

    public void OnExtinguishFinished(FireTruck truck) => OnExtinguishFinished((ISelectableUnit)truck);
    public void OnExtinguishFinished(ISelectableUnit unit)
    {
        SetButtonColor(extinguishButton, false);
        RefreshExtinguishInteractable(unit);
    }

    public void RefreshExtinguishInteractable(ISelectableUnit unit)
    {
        if (extinguishButton == null) return;
        if (unit == null)
        {
            extinguishButton.interactable = false;
            if (extinguishLabel != null) extinguishLabel.alpha = 0.4f;
            return;
        }

        bool nearFire = FireManager.Instance != null &&
                        FireManager.Instance.HasNearbyFire(
                            new Vector2(unit.Transform.position.x, unit.Transform.position.y),
                            unit.ExtinguishRadius);

        if (unit is IWaterRechargeable waterUnit && !waterUnit.HasWater)
            nearFire = false;

        extinguishButton.interactable = nearFire;

        if (extinguishLabel != null)
            extinguishLabel.alpha = nearFire ? 1f : 0.4f;
    }

    private void OnCancelClicked()
    {
        TruckSelectionManager.Instance?.Deselect();
        Hide();
    }

    private void OnMoveClicked()
    {
        SetButtonColor(moveButton, true);
        SetAwaitingTarget(true);
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