namespace RPGEngine;

/// <summary>
/// Tile-based A* pathfinding over a caller-supplied walkability predicate.
/// </summary>
/// <remarks>
/// The engine moves tile-by-tile, never pixel-by-pixel, so pathfinding operates purely on
/// integer tile coordinates. The class is <c>internal</c> and has no dependencies on
/// <c>TileMap</c> or SkiaSharp: the click-to-move story wires the <c>isWalkable</c> predicate to
/// <c>TileMap</c> solidity. Movement is 8-direction (cardinal cost 1, diagonal cost √2) with
/// corner cutting prevented, and the octile heuristic is consistent, so returned paths are
/// optimal.
/// </remarks>
internal static class AStarPathfinder
{
    /// <summary>Cost of a cardinal (orthogonal) step.</summary>
    private const double CardinalCost = 1.0;

    /// <summary>Cost of a diagonal step (√2).</summary>
    private static readonly double DiagonalCost = Math.Sqrt(2);

    /// <summary>
    /// The eight neighbor deltas in a fixed order (cardinal then diagonal) so that expansion is
    /// deterministic and results are stable across runs.
    /// </summary>
    private static readonly (int DX, int DY)[] NeighborDeltas =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (1, -1), (-1, 1), (-1, -1),
    };

    /// <summary>
    /// Finds an optimal 8-direction path from <paramref name="start"/> (exclusive) to
    /// <paramref name="goal"/> (inclusive) on a <paramref name="width"/> ×
    /// <paramref name="height"/> tile grid.
    /// </summary>
    /// <param name="start">The starting tile (not included in the result).</param>
    /// <param name="goal">The goal tile (included in the result).</param>
    /// <param name="isWalkable">
    /// Predicate deciding whether a tile is passable; out-of-bounds tiles are always treated as
    /// not walkable regardless of the predicate.
    /// </param>
    /// <param name="width">Grid width in tiles.</param>
    /// <param name="height">Grid height in tiles.</param>
    /// <returns>
    /// The ordered list of tiles from <paramref name="start"/> (exclusive) to <paramref name="goal"/>
    /// (inclusive), or an empty list when no movement is needed, the start or goal is not walkable
    /// (including out of bounds), or no path exists.
    /// </returns>
    internal static IReadOnlyList<(int X, int Y)> FindPath(
        (int X, int Y) start,
        (int X, int Y) goal,
        Func<int, int, bool> isWalkable,
        int width,
        int height)
    {
        // No movement is needed when start and goal coincide.
        if (start == goal)
        {
            return Array.Empty<(int X, int Y)>();
        }

        // A path can neither begin nor end on a tile that cannot be stood on (out-of-bounds tiles
        // are never walkable).
        if (!IsWalkable(goal.X, goal.Y, isWalkable, width, height) ||
            !IsWalkable(start.X, start.Y, isWalkable, width, height))
        {
            return Array.Empty<(int X, int Y)>();
        }

        // gScore[tile] is the best known cost from start to tile.
        var gScore = new Dictionary<(int X, int Y), double> { [start] = 0.0 };

        // cameFrom[tile] is the predecessor on the best known path; used to reconstruct the result.
        var cameFrom = new Dictionary<(int X, int Y), (int X, int Y)>();

        // Open set ordered by f = g + h (octile). A tile may be enqueued more than once: it is only
        // re-enqueued when a strictly better g is found. Because the heuristic is consistent, the
        // first time a tile is dequeued its g is optimal, so the closed set below simply skips stale
        // duplicate entries that carry an equal-or-worse g.
        var open = new PriorityQueue<(int X, int Y), double>();
        var closed = new HashSet<(int X, int Y)>();
        open.Enqueue(start, Heuristic(start, goal));

        while (open.Count > 0)
        {
            var current = open.Dequeue();

            if (current == goal)
            {
                return ReconstructPath(cameFrom, start, goal);
            }

            if (!closed.Add(current))
            {
                // Stale duplicate entry; this tile was already expanded with its optimal g.
                continue;
            }

            double currentG = gScore[current];
            foreach ((int DX, int DY) in NeighborDeltas)
            {
                (int X, int Y) neighbor = (current.X + DX, current.Y + DY);

                // Blocked and out-of-bounds tiles are not walkable.
                if (!IsWalkable(neighbor.X, neighbor.Y, isWalkable, width, height))
                {
                    continue;
                }

                // Prevent corner cutting: a diagonal step may only pass through the corner when both
                // orthogonally adjacent cells are walkable.
                if (DX != 0 && DY != 0 &&
                    (!IsWalkable(current.X + DX, current.Y, isWalkable, width, height) ||
                     !IsWalkable(current.X, current.Y + DY, isWalkable, width, height)))
                {
                    continue;
                }

                double stepCost = DX != 0 && DY != 0 ? DiagonalCost : CardinalCost;
                double tentativeG = currentG + stepCost;

                // Re-open a tile only when a strictly better g is found.
                if (gScore.TryGetValue(neighbor, out double knownG) && tentativeG >= knownG)
                {
                    continue;
                }

                gScore[neighbor] = tentativeG;
                cameFrom[neighbor] = current;
                open.Enqueue(neighbor, tentativeG + Heuristic(neighbor, goal));
            }
        }

        // The open set was exhausted without reaching the goal: no path exists.
        return Array.Empty<(int X, int Y)>();
    }

    /// <summary>
    /// Reconstructs the path by walking parents from <paramref name="goal"/> back to
    /// <paramref name="start"/> (exclusive), then reversing so the result is
    /// start-exclusive → goal-inclusive.
    /// </summary>
    private static IReadOnlyList<(int X, int Y)> ReconstructPath(
        Dictionary<(int X, int Y), (int X, int Y)> cameFrom,
        (int X, int Y) start,
        (int X, int Y) goal)
    {
        var path = new List<(int X, int Y)>();
        var current = goal;
        while (current != start)
        {
            path.Add(current);
            current = cameFrom[current];
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Returns whether the tile is passable according to <paramref name="isWalkable"/>;
    /// out-of-bounds tiles are never walkable.
    /// </summary>
    private static bool IsWalkable(int x, int y, Func<int, int, bool> isWalkable, int width, int height)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return false;
        }

        return isWalkable(x, y);
    }

    /// <summary>
    /// Octile distance: <c>max(|dx|, |dy|) + (√2 − 1) · min(|dx|, |dy|)</c>. This is the exact
    /// movement cost on an unobstructed 8-direction grid and is consistent, so A* returns optimal
    /// paths.
    /// </summary>
    private static double Heuristic((int X, int Y) from, (int X, int Y) to)
    {
        int dx = Math.Abs(from.X - to.X);
        int dy = Math.Abs(from.Y - to.Y);
        return Math.Max(dx, dy) + (DiagonalCost - CardinalCost) * Math.Min(dx, dy);
    }
}
