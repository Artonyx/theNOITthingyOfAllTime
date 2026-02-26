using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A* Pathfinder tailored for a 2D tilemap grid.
/// Attach this to a GridManager GameObject alongside your Tilemaps.
/// </summary>
public class AStarPathfinder : MonoBehaviour
{
    [Header("Tilemaps")]
    [Tooltip("The street/ground tilemap — tiles here are potentially walkable")]
    public Tilemap groundTilemap;

    [Tooltip("The buildings tilemap — tiles here are always blocked")]
    public Tilemap buildingTilemap;

    [Tooltip("Sidewalk tilemap — walkable by citizens, but blocked for trucks")]
    public Tilemap sidewalkTilemap;

    [Header("Settings")]
    [Tooltip("Allow diagonal movement between tiles")]
    public bool allowDiagonals = false;

    // ─────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Find a path from startWorld to endWorld in world-space coordinates.
    /// Returns a list of world-space Vector3 waypoints, or an empty list if no path exists.
    /// </summary>
    public List<Vector3> FindPath(Vector3 startWorld, Vector3 endWorld)
    {
        Vector3Int startCell = groundTilemap.WorldToCell(startWorld);
        Vector3Int endCell   = groundTilemap.WorldToCell(endWorld);
        return FindPathCells(startCell, endCell);
    }

    /// <summary>
    /// Find a path between two cell positions.
    /// Returns world-space waypoints centered on each tile.
    /// </summary>
    public List<Vector3> FindPathCells(Vector3Int startCell, Vector3Int endCell)
    {
        // Snap end to nearest walkable cell if the exact target is blocked
        if (!IsWalkable(endCell))
            endCell = FindNearestWalkable(endCell, 5);

        if (!IsWalkable(startCell) || !IsWalkable(endCell))
        {
            Debug.LogWarning($"[AStar] Start or end cell is not walkable. Start:{startCell} End:{endCell}");
            return new List<Vector3>();
        }

        if (startCell == endCell)
            return new List<Vector3> { CellToWorld(startCell) };

        // ── A* setup ──────────────────────────────
        var openSet   = new SortedList<float, Node>(new DuplicateKeyComparer());
        var allNodes  = new Dictionary<Vector3Int, Node>();
        var closedSet = new HashSet<Vector3Int>();

        Node startNode = new Node(startCell, null, 0f, Heuristic(startCell, endCell));
        openSet.Add(startNode.F, startNode);
        allNodes[startCell] = startNode;

        int maxIterations = 10000; // safety cap
        int iterations    = 0;

        while (openSet.Count > 0 && iterations++ < maxIterations)
        {
            // Pop the node with lowest F
            Node current = openSet.Values[0];
            openSet.RemoveAt(0);

            if (current.Position == endCell)
                return ReconstructPath(current);

            closedSet.Add(current.Position);

            foreach (Vector3Int neighborPos in GetNeighbors(current.Position))
            {
                if (closedSet.Contains(neighborPos) || !IsWalkable(neighborPos))
                    continue;

                float moveCost = current.G + MovementCost(current.Position, neighborPos);

                if (!allNodes.TryGetValue(neighborPos, out Node neighborNode))
                {
                    // New node — add to open set
                    neighborNode = new Node(neighborPos, current, moveCost, Heuristic(neighborPos, endCell));
                    allNodes[neighborPos] = neighborNode;
                    openSet.Add(neighborNode.F, neighborNode);
                }
                else if (moveCost < neighborNode.G)
                {
                    // Found a cheaper route — update the node
                    openSet.Remove(neighborNode.F);   // remove old entry
                    neighborNode.G      = moveCost;
                    neighborNode.Parent = current;
                    openSet.Add(neighborNode.F, neighborNode); // re-insert with new F
                }
            }
        }

        Debug.LogWarning($"[AStar] No path found from {startCell} to {endCell}.");
        return new List<Vector3>(); // no path
    }

    // ─────────────────────────────────────────────
    //  Walkability
    // ─────────────────────────────────────────────

    /// <summary>
    /// A tile is walkable for a firetruck if:
    ///   - The ground tilemap has a tile there (it's a street)
    ///   - The building tilemap does NOT have a tile there
    ///   - The sidewalk tilemap does NOT have a tile there (trucks stay on roads)
    /// Note: the park shares the sidewalk tilemap, so it is blocked implicitly.
    /// </summary>
    public bool IsWalkable(Vector3Int cell)
    {
        bool hasGround   = groundTilemap.HasTile(cell);
        bool hasBuilding = buildingTilemap != null && buildingTilemap.HasTile(cell);
        bool hasSidewalk = sidewalkTilemap != null && sidewalkTilemap.HasTile(cell);

        // Trucks can only travel on road tiles — buildings and sidewalks (incl. park) are blocked
        return hasGround && !hasBuilding && !hasSidewalk;
    }

    /// <summary>
    /// Convert a cell position to the world-space CENTER of that tile.
    /// </summary>
    public Vector3 CellToWorld(Vector3Int cell)
    {
        return groundTilemap.GetCellCenterWorld(cell);
    }

    /// <summary>
    /// Convert a world-space position to a cell coordinate.
    /// </summary>
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        return groundTilemap.WorldToCell(worldPos);
    }

    // ─────────────────────────────────────────────
    //  Internals
    // ─────────────────────────────────────────────

    private List<Vector3Int> GetNeighbors(Vector3Int cell)
    {
        var neighbors = new List<Vector3Int>
        {
            cell + Vector3Int.up,
            cell + Vector3Int.down,
            cell + Vector3Int.left,
            cell + Vector3Int.right
        };

        if (allowDiagonals)
        {
            neighbors.Add(cell + new Vector3Int( 1,  1, 0));
            neighbors.Add(cell + new Vector3Int(-1,  1, 0));
            neighbors.Add(cell + new Vector3Int( 1, -1, 0));
            neighbors.Add(cell + new Vector3Int(-1, -1, 0));
        }

        return neighbors;
    }

    /// <summary>
    /// Manhattan heuristic — accurate for 4-directional grids.
    /// Switch to Euclidean if you enable diagonals.
    /// </summary>
    private float Heuristic(Vector3Int a, Vector3Int b)
    {
        if (!allowDiagonals)
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan
        else
            return Vector3Int.Distance(a, b); // Euclidean for diagonals
    }

    private float MovementCost(Vector3Int from, Vector3Int to)
    {
        // Diagonal costs √2, straight costs 1
        return (Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y) > 1) ? 1.414f : 1f;
    }

    private List<Vector3> ReconstructPath(Node endNode)
    {
        var path = new List<Vector3>();
        Node current = endNode;

        while (current != null)
        {
            path.Add(CellToWorld(current.Position));
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// If the clicked tile is blocked (e.g. a building edge), find the nearest walkable tile.
    /// </summary>
    private Vector3Int FindNearestWalkable(Vector3Int origin, int searchRadius)
    {
        for (int r = 1; r <= searchRadius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue; // only check ring edge
                    var candidate = origin + new Vector3Int(x, y, 0);
                    if (IsWalkable(candidate)) return candidate;
                }
            }
        }
        return origin; // fallback — caller will handle the failure
    }

    // ─────────────────────────────────────────────
    //  Debug Gizmos
    // ─────────────────────────────────────────────
#if UNITY_EDITOR
    [Header("Debug")]
    public bool showWalkableGizmos = false;
    public Vector3 gizmoScanCenter = Vector3.zero;
    public int gizmoScanRadius = 10;

    private void OnDrawGizmosSelected()
    {
        if (!showWalkableGizmos || groundTilemap == null) return;

        Vector3Int center = groundTilemap.WorldToCell(gizmoScanCenter);

        for (int x = -gizmoScanRadius; x <= gizmoScanRadius; x++)
        {
            for (int y = -gizmoScanRadius; y <= gizmoScanRadius; y++)
            {
                Vector3Int cell = center + new Vector3Int(x, y, 0);
                Gizmos.color = IsWalkable(cell) ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);
                Gizmos.DrawCube(CellToWorld(cell), Vector3.one * 0.9f);
            }
        }
    }
#endif

    // ─────────────────────────────────────────────
    //  Helper Classes
    // ─────────────────────────────────────────────

    private class Node
    {
        public Vector3Int Position;
        public Node      Parent;
        public float     G;   // cost from start
        public float     H;   // heuristic to end
        public float     F => G + H;

        public Node(Vector3Int pos, Node parent, float g, float h)
        {
            Position = pos;
            Parent   = parent;
            G        = g;
            H        = h;
        }
    }

    /// <summary>
    /// Allows duplicate float keys in SortedList (F values can be equal).
    /// </summary>
    private class DuplicateKeyComparer : IComparer<float>
    {
        public int Compare(float x, float y)
        {
            int result = x.CompareTo(y);
            return result == 0 ? 1 : result; // treat equal as "greater" to allow duplicates
        }
    }
}