using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HelicopterOutline : MonoBehaviour
{
    [Header("Outline Settings")]
    public Color outlineColor = new Color(0f, 1f, 1f, 1f); // cyan
    [Tooltip("How much larger than the original sprite the outline is. 1.12 = 12% bigger.")]
    public float outlineScale = 1.2f;

    private SpriteRenderer _mainRenderer;
    private SpriteRenderer _outlineRenderer;
    private GameObject     _outlineObject;

    private void Awake()
    {
        _mainRenderer = GetComponent<SpriteRenderer>();
        CreateOutlineRenderer();
        HideOutline();
    }

    private void LateUpdate()
    {
        if (_outlineObject.activeSelf)
            _outlineRenderer.sprite = _mainRenderer.sprite;
    }

    public void ShowOutline()
    {
        _outlineRenderer.sprite = _mainRenderer.sprite;
        _outlineObject.SetActive(true);
    }

    public void HideOutline()
    {
        _outlineObject.SetActive(false);
    }

    private void CreateOutlineRenderer()
    {
        _outlineObject = new GameObject("OutlineRenderer");
        _outlineObject.transform.SetParent(transform, false);
        _outlineObject.transform.localPosition = Vector3.zero;
        _outlineObject.transform.localScale    = Vector3.one * outlineScale;

        _outlineRenderer = _outlineObject.AddComponent<SpriteRenderer>();

        _outlineRenderer.flipX = _mainRenderer.flipX;
        _outlineRenderer.flipY = _mainRenderer.flipY;

        _outlineRenderer.sortingLayerID   = _mainRenderer.sortingLayerID;
        _outlineRenderer.sortingOrder     = _mainRenderer.sortingOrder - 1;

        _outlineRenderer.color            = outlineColor;
        _outlineRenderer.maskInteraction  = _mainRenderer.maskInteraction;

        _outlineRenderer.sharedMaterial   = _mainRenderer.sharedMaterial;
    }
}
