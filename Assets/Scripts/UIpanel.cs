using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public static UIPanel Instance { get; private set; }

    [Header("Transparency")]
    [Range(0f, 1f)] public float normalAlpha   = 0.4f;
    [Range(0f, 1f)] public float hoverAlpha    = 1f;
    [Range(0f, 1f)] public float targetingAlpha = 0.4f;
    public float fadeSpeed = 8f;

    private CanvasGroup _canvasGroup;
    private Coroutine   _fadeCoroutine;
    private bool        _isHovered        = false;
    private bool        _isAwaitingTarget = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _canvasGroup       = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = normalAlpha;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        RefreshState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        RefreshState();
    }

    public void SetAwaitingTarget(bool awaiting)
    {
        Debug.Log($"[UIPanel] SetAwaitingTarget({awaiting})");
        _isAwaitingTarget = awaiting;
        RefreshState();
    }

    private void RefreshState()
    {
        if (_isAwaitingTarget)
        {
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
            FadeTo(targetingAlpha);
        }
        else if (_isHovered)
        {
            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;
            FadeTo(hoverAlpha);
        }
        else
        {
            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;
            FadeTo(normalAlpha);
        }
    }

    private void FadeTo(float target)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(target));
    }

    private IEnumerator FadeRoutine(float target)
    {
        while (!Mathf.Approximately(_canvasGroup.alpha, target))
        {
            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }
        _canvasGroup.alpha = target;
    }
}