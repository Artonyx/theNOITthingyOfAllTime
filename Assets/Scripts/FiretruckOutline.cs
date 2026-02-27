using UnityEngine;

/// <summary>
/// Draws a selection outline by placing a second SpriteRenderer on a child
/// GameObject, scaled slightly larger and rendered behind the main sprite
/// in a solid outline color.
///
/// No shader or material setup required — works out of the box.
///
/// SETUP:
///  1. Attach this script to your firetruck GameObject.
///  2. Optionally tweak OutlineColor and OutlineScale in the Inspector.
///  That's it — the outline child is created entirely in code at runtime.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FiretruckOutline : MonoBehaviour
{
    [Header("Outline Settings")]
    public Color outlineColor = new Color(0f, 1f, 1f, 1f); // orange
    [Tooltip("How much larger than the original sprite the outline is. 1.12 = 12% bigger.")]
    public float outlineScale = 1.2f;

    private SpriteRenderer _mainRenderer;
    private SpriteRenderer _outlineRenderer;
    private GameObject     _outlineObject;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _mainRenderer = GetComponent<SpriteRenderer>();
        CreateOutlineRenderer();
        HideOutline();
    }

    // Keep the outline sprite in sync with the main sprite every frame
    // (necessary because the Animator swaps sprites per frame).
    private void LateUpdate()
    {
        if (_outlineObject.activeSelf)
            _outlineRenderer.sprite = _mainRenderer.sprite;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void ShowOutline()
    {
        _outlineRenderer.sprite = _mainRenderer.sprite;
        _outlineObject.SetActive(true);
    }

    public void HideOutline()
    {
        _outlineObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private void CreateOutlineRenderer()
    {
        _outlineObject = new GameObject("OutlineRenderer");
        _outlineObject.transform.SetParent(transform, false);
        _outlineObject.transform.localPosition = Vector3.zero;
        _outlineObject.transform.localScale    = Vector3.one * outlineScale;

        _outlineRenderer = _outlineObject.AddComponent<SpriteRenderer>();

        // Copy the flip state and sorting settings from the main renderer
        _outlineRenderer.flipX = _mainRenderer.flipX;
        _outlineRenderer.flipY = _mainRenderer.flipY;

        // Render behind the main sprite on the same sorting layer
        _outlineRenderer.sortingLayerID   = _mainRenderer.sortingLayerID;
        _outlineRenderer.sortingOrder     = _mainRenderer.sortingOrder - 1;

        // Solid color — no texture sampling needed
        _outlineRenderer.color            = outlineColor;
        _outlineRenderer.maskInteraction  = _mainRenderer.maskInteraction;

        // Use the same material as the main sprite so Unity's sprite batching
        // handles the draw call; we just tint it with the outline color above.
        _outlineRenderer.sharedMaterial   = _mainRenderer.sharedMaterial;
    }
}