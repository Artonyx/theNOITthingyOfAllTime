using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Camera))]
public class CameraPanZoomController : MonoBehaviour
{
    private static bool _sceneHookRegistered;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 1f;
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 5f;

    [Header("Edge Pan")]
    [SerializeField] private float panSpeed = 10f;
    [SerializeField] private float edgeThicknessPixels = 24f;
    [SerializeField] private float boundsPadding = 0.05f;

    private Camera _camera;
    private Bounds _levelBounds;
    private bool _hasLevelBounds;
    private float _initialZoom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (_sceneHookRegistered) return;
        SceneManager.sceneLoaded += OnAnySceneLoaded;
        _sceneHookRegistered = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureControllerOnInitialScene()
    {
        EnsureControllerOnMainCamera();
    }

    private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureControllerOnMainCamera();
    }

    private static void EnsureControllerOnMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;
        if (mainCamera.GetComponent<CameraPanZoomController>() == null)
            mainCamera.gameObject.AddComponent<CameraPanZoomController>();
    }

    private void Awake()
    {
        InitializeForCurrentScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeForCurrentScene();
    }

    private void InitializeForCurrentScene()
    {
        _camera = GetComponent<Camera>();
        _camera.orthographic = true;
        _initialZoom = _camera.orthographicSize;

        if (maxZoom < minZoom)
        {
            maxZoom = minZoom;
        }

        ResolveLevelBounds();
        ClampCameraToBounds();
    }

    private void Update()
    {
        if (_camera == null)
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                return;
            }
        }

        HandleZoom();
        HandleEdgePan();
    }

    private void HandleZoom()
    {
        if (IsZoomBlocked())
        {
            return;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < Mathf.Epsilon)
        {
            return;
        }

        // Zoom around the cursor position like common RTS/top-down games.
        Vector3 beforeZoomWorld = GetMouseWorldPositionOnCameraPlane();
        float zoom = _camera.orthographicSize - scroll * zoomSpeed * Time.unscaledDeltaTime * 60f;
        _camera.orthographicSize = Mathf.Clamp(zoom, minZoom, maxZoom);

        Vector3 afterZoomWorld = GetMouseWorldPositionOnCameraPlane();
        Vector3 offset = beforeZoomWorld - afterZoomWorld;
        transform.position += new Vector3(offset.x, offset.y, 0f);
        ClampCameraToBounds();
    }

    private void HandleEdgePan()
    {
        // Pan only when zoomed in from default size.
        if (_camera.orthographicSize >= _initialZoom - 0.01f)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector3 panDirection = Vector3.zero;
        Vector3 mouse = Input.mousePosition;

        if (mouse.x <= edgeThicknessPixels)
        {
            panDirection.x = -1f;
        }
        else if (mouse.x >= Screen.width - edgeThicknessPixels)
        {
            panDirection.x = 1f;
        }

        if (mouse.y <= edgeThicknessPixels)
        {
            panDirection.y = -1f;
        }
        else if (mouse.y >= Screen.height - edgeThicknessPixels)
        {
            panDirection.y = 1f;
        }

        if (panDirection == Vector3.zero)
        {
            return;
        }

        panDirection.Normalize();
        transform.position += panDirection * (panSpeed * Time.unscaledDeltaTime);
        ClampCameraToBounds();
    }

    private void ClampCameraToBounds()
    {
        if (!_hasLevelBounds)
        {
            return;
        }

        float halfHeight = _camera.orthographicSize;
        float halfWidth = halfHeight * _camera.aspect;

        float minX = _levelBounds.min.x + halfWidth + boundsPadding;
        float maxX = _levelBounds.max.x - halfWidth - boundsPadding;
        float minY = _levelBounds.min.y + halfHeight + boundsPadding;
        float maxY = _levelBounds.max.y - halfHeight - boundsPadding;

        Vector3 pos = transform.position;

        if (minX > maxX)
        {
            pos.x = (_levelBounds.min.x + _levelBounds.max.x) * 0.5f;
        }
        else
        {
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
        }

        if (minY > maxY)
        {
            pos.y = (_levelBounds.min.y + _levelBounds.max.y) * 0.5f;
        }
        else
        {
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
        }

        pos.z = transform.position.z;
        transform.position = pos;
    }

    private void ResolveLevelBounds()
    {
        List<Tilemap> tilemaps = new List<Tilemap>();
        FireManager fireManager = FindFirstObjectByType<FireManager>();
        if (fireManager != null)
        {
            AddIfNotNull(tilemaps, fireManager.groundTilemap);
            AddIfNotNull(tilemaps, fireManager.sidewalkTilemap);
            AddIfNotNull(tilemaps, fireManager.buildingTilemap);
            AddIfNotNull(tilemaps, fireManager.treesTilemap);
            AddIfNotNull(tilemaps, fireManager.windowsTilemap);
        }

        if (tilemaps.Count == 0)
        {
            foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                AddIfNotNull(tilemaps, tilemap);
            }
        }

        bool hasBounds = false;
        Bounds aggregate = default;

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null || tilemap.cellBounds.size == Vector3Int.zero)
            {
                continue;
            }

            if (!TryGetOccupiedWorldBounds(tilemap, out Bounds worldBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                aggregate = worldBounds;
                hasBounds = true;
            }
            else
            {
                aggregate.Encapsulate(worldBounds.min);
                aggregate.Encapsulate(worldBounds.max);
            }
        }

        _hasLevelBounds = hasBounds;
        _levelBounds = aggregate;
    }

    private bool TryGetOccupiedWorldBounds(Tilemap tilemap, out Bounds worldBounds)
    {
        worldBounds = default;
        bool hasAnyTile = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell))
            {
                continue;
            }

            Vector3 localMin = tilemap.layoutGrid.CellToLocalInterpolated((Vector3Int)cell);
            Vector3 localMax = tilemap.layoutGrid.CellToLocalInterpolated((Vector3Int)(cell + Vector3Int.one));

            Vector3 worldMin = tilemap.transform.TransformPoint(localMin);
            Vector3 worldMax = tilemap.transform.TransformPoint(localMax);

            if (!hasAnyTile)
            {
                min = Vector3.Min(worldMin, worldMax);
                max = Vector3.Max(worldMin, worldMax);
                hasAnyTile = true;
            }
            else
            {
                min = Vector3.Min(min, Vector3.Min(worldMin, worldMax));
                max = Vector3.Max(max, Vector3.Max(worldMin, worldMax));
            }
        }

        if (!hasAnyTile)
        {
            return false;
        }

        worldBounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }

    private Vector3 GetMouseWorldPositionOnCameraPlane()
    {
        Vector3 mouse = Input.mousePosition;
        mouse.z = -_camera.transform.position.z;
        Vector3 world = _camera.ScreenToWorldPoint(mouse);
        world.z = transform.position.z;
        return world;
    }

    private static bool IsZoomBlocked()
    {
        return pause.gameIsPaused;
    }

    private static void AddIfNotNull(List<Tilemap> list, Tilemap tilemap)
    {
        if (tilemap != null && !list.Contains(tilemap))
        {
            list.Add(tilemap);
        }
    }
}
