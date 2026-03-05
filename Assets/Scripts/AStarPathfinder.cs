using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    public List<Vector3> FindPath(Vector3 startWorld, Vector3 endWorld)
    {
        Vector3Int startCell = groundTilemap.WorldToCell(startWorld);
        Vector3Int endCell   = groundTilemap.WorldToCell(endWorld);
        return FindPathCells(startCell, endCell);
    }
    
    public List<Vector3> FindPathCells(Vector3Int startCell, Vector3Int endCell)
    {
        if (!IsWalkable(endCell))
            endCell = FindNearestWalkable(endCell, 5);

        if (!IsWalkable(startCell) || !IsWalkable(endCell))
        {
            Debug.LogWarning($"[AStar] Start or end cell is not walkable. Start:{startCell} End:{endCell}");
            return new List<Vector3>();
        }

        if (startCell == endCell)
            return new List<Vector3> { CellToWorld(startCell) };
        var openSet   = new SortedList<float, Node>(new DuplicateKeyComparer());
        var allNodes  = new Dictionary<Vector3Int, Node>();
        var closedSet = new HashSet<Vector3Int>();

        Node startNode = new Node(startCell, null, 0f, Heuristic(startCell, endCell));
        openSet.Add(startNode.F, startNode);
        allNodes[startCell] = startNode;

        int maxIterations = 10000;
        int iterations    = 0;

        while (openSet.Count > 0 && iterations++ < maxIterations)
        {
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
                    neighborNode = new Node(neighborPos, current, moveCost, Heuristic(neighborPos, endCell));
                    allNodes[neighborPos] = neighborNode;
                    openSet.Add(neighborNode.F, neighborNode);
                }
                else if (moveCost < neighborNode.G)
                {
                    openSet.Remove(neighborNode.F);
                    neighborNode.G      = moveCost;
                    neighborNode.Parent = current;
                    openSet.Add(neighborNode.F, neighborNode);
                }
            }
        }

        Debug.LogWarning($"[AStar] No path found from {startCell} to {endCell}.");
        return new List<Vector3>();
    }

    public bool IsWalkable(Vector3Int cell)
    {
        bool hasGround   = groundTilemap.HasTile(cell);
        bool hasBuilding = buildingTilemap != null && buildingTilemap.HasTile(cell);
        bool hasSidewalk = sidewalkTilemap != null && sidewalkTilemap.HasTile(cell);
        
        return hasGround && !hasBuilding && !hasSidewalk;
    }

    public Vector3 CellToWorld(Vector3Int cell)
    {
        return groundTilemap.GetCellCenterWorld(cell);
    }

    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        return groundTilemap.WorldToCell(worldPos);
    }

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

    private float Heuristic(Vector3Int a, Vector3Int b)
    {
        if (!allowDiagonals)
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        else
            return Vector3Int.Distance(a, b);
    }

    private float MovementCost(Vector3Int from, Vector3Int to)
    {
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
    
    private Vector3Int FindNearestWalkable(Vector3Int origin, int searchRadius)
    {
        for (int r = 1; r <= searchRadius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;
                    var candidate = origin + new Vector3Int(x, y, 0);
                    if (IsWalkable(candidate)) return candidate;
                }
            }
        }
        return origin;
    }


    private class Node
    {
        public Vector3Int Position;
        public Node      Parent;
        public float     G;
        public float     H;
        public float     F => G + H;

        public Node(Vector3Int pos, Node parent, float g, float h)
        {
            Position = pos;
            Parent   = parent;
            G        = g;
            H        = h;
        }
    }
    
    private class DuplicateKeyComparer : IComparer<float>
    {
        public int Compare(float x, float y)
        {
            int result = x.CompareTo(y);
            return result == 0 ? 1 : result;
        }
    }
}