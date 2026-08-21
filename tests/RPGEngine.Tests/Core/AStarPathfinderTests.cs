using RPGEngine;
using Xunit;

namespace RPGEngine.Tests.Core;

/// <summary>
/// Acceptance tests for <see cref="AStarPathfinder"/> (story 40: A* tile pathfinding, internal).
/// </summary>
public class AStarPathfinderTests
{
    // ---------------------------------------------------------------------
    // Acceptance 1: straight line on an open grid.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies a straight horizontal run on an open 10×10 grid returns the expected tiles,
    /// excluding the start and including the goal.
    /// </summary>
    [Fact]
    public void FindPath_OpenGrid_StraightLine_ReturnsExpectedPath()
    {
        var path = AStarPathfinder.FindPath((0, 0), (3, 0), AlwaysWalkable, 10, 10);

        Assert.Equal(new[] { (1, 0), (2, 0), (3, 0) }, path);
    }

    // ---------------------------------------------------------------------
    // Acceptance 2: an L-shaped wall forces a valid detour.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies an L-shaped wall blocks the direct route and the returned path is valid (every
    /// step walkable, adjacent by the 8-neighborhood, no corner cutting) and ends at the goal.
    /// </summary>
    [Fact]
    public void FindPath_LShapedWall_DetoursAroundWall()
    {
        // Wall cells: (2,0), (3,0), (2,1), (2,2) — an L that blocks the direct row-0 route.
        string[] map =
        {
            "..##.",
            "..#..",
            "..#..",
            ".....",
            ".....",
        };
        var start = (0, 0);
        var goal = (4, 0);
        var isWalkable = MapWalkability(map);

        var path = AStarPathfinder.FindPath(start, goal, isWalkable, width: 5, height: 5);

        AssertValidPath(path, start, goal, isWalkable, 5, 5);
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: a U-shaped (fully enclosing) obstacle makes the goal unreachable.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies a wall ring with no opening enclosing the goal yields an empty path.
    /// </summary>
    [Fact]
    public void FindPath_EnclosedGoal_ReturnsEmpty()
    {
        // The goal (3,3) sits inside a closed wall ring; there is no opening to reach it.
        string[] map =
        {
            ".......",
            ".#####.",
            ".#...#.",
            ".#...#.",
            ".#...#.",
            ".#####.",
            ".......",
        };
        var start = (0, 0);
        var goal = (3, 3);

        var path = AStarPathfinder.FindPath(start, goal, MapWalkability(map), width: 7, height: 7);

        Assert.Empty(path);
    }

    // ---------------------------------------------------------------------
    // Acceptance 4: start == goal needs no movement.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a zero-length request returns an empty path.</summary>
    [Fact]
    public void FindPath_StartEqualsGoal_ReturnsEmpty()
    {
        var path = AStarPathfinder.FindPath((2, 2), (2, 2), AlwaysWalkable, 10, 10);

        Assert.Empty(path);
    }

    // ---------------------------------------------------------------------
    // Acceptance 5: a solid (or out-of-bounds) goal has no path.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a blocked goal tile yields an empty path.</summary>
    [Fact]
    public void FindPath_GoalBlocked_ReturnsEmpty()
    {
        // (2,1) is a wall tile and is the requested goal.
        string[] map =
        {
            ".....",
            "..#..",
            ".....",
            ".....",
            ".....",
        };

        var path = AStarPathfinder.FindPath((0, 0), (2, 1), MapWalkability(map), width: 5, height: 5);

        Assert.Empty(path);
    }

    /// <summary>Verifies an out-of-bounds goal yields an empty path (never throws).</summary>
    [Theory]
    [InlineData(10, 0)]
    [InlineData(0, 10)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void FindPath_GoalOutOfBounds_ReturnsEmpty(int goalX, int goalY)
    {
        var path = AStarPathfinder.FindPath((0, 0), (goalX, goalY), AlwaysWalkable, 10, 10);

        Assert.Empty(path);
    }

    // ---------------------------------------------------------------------
    // Acceptance 6: a direct diagonal on an open grid.
    // ---------------------------------------------------------------------
    /// <summary>Verifies (0,0) → (2,2) on an open grid walks straight diagonally.</summary>
    [Fact]
    public void FindPath_OpenGrid_Diagonal_ReturnsDirectDiagonalPath()
    {
        var path = AStarPathfinder.FindPath((0, 0), (2, 2), AlwaysWalkable, 10, 10);

        Assert.Equal(new[] { (1, 1), (2, 2) }, path);
    }

    // ---------------------------------------------------------------------
    // Acceptance 7: corner cutting around a blocked tile is prevented.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies that with (1,1) blocked the path from (0,0) to (2,2) detours orthogonally instead
    /// of cutting the corner — no diagonal step is possible without passing the blocked cell.
    /// </summary>
    [Fact]
    public void FindPath_BlockedCorner_PreventsCornerCutting()
    {
        string[] map =
        {
            "...",
            ".#.",
            "...",
        };
        var start = (0, 0);
        var goal = (2, 2);
        var isWalkable = MapWalkability(map);

        var path = AStarPathfinder.FindPath(start, goal, isWalkable, width: 3, height: 3);

        AssertValidPath(path, start, goal, isWalkable, 3, 3);
        Assert.DoesNotContain((1, 1), path);
        AssertNoDiagonalSteps(path, start);
    }

    // ---------------------------------------------------------------------
    // Acceptance 8: determinism and optimality on a grid with two equal-length routes.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies that with (1,1) blocked there are two equal-length routes of cost 4, that running
    /// the search twice returns the exact same list, and that the returned path is minimal.
    /// </summary>
    [Fact]
    public void FindPath_TwoEqualLengthRoutes_IsDeterministicAndOptimal()
    {
        string[] map =
        {
            "...",
            ".#.",
            "...",
        };
        var start = (0, 0);
        var goal = (2, 2);
        var isWalkable = MapWalkability(map);

        var first = AStarPathfinder.FindPath(start, goal, isWalkable, width: 3, height: 3);
        var second = AStarPathfinder.FindPath(start, goal, isWalkable, width: 3, height: 3);

        // Deterministic: both runs return the exact same list.
        Assert.Equal(first, second);
        AssertValidPath(first, start, goal, isWalkable, 3, 3);

        // Optimal: every route around the blocked corner costs exactly 4 (all cardinal steps).
        Assert.Equal(4.0, PathCost(first, start), precision: 6);
    }

    // ---------------------------------------------------------------------
    // Acceptance 9: out-of-bounds start/goal handling never throws.
    // ---------------------------------------------------------------------
    /// <summary>Verifies an out-of-bounds start yields an empty path (never throws).</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(10, 0)]
    [InlineData(0, 10)]
    public void FindPath_StartOutOfBounds_ReturnsEmpty(int startX, int startY)
    {
        var path = AStarPathfinder.FindPath((startX, startY), (5, 5), AlwaysWalkable, 10, 10);

        Assert.Empty(path);
    }

    /// <summary>Verifies wildly out-of-bounds start and goal return empty without throwing.</summary>
    [Fact]
    public void FindPath_OutOfBoundsStartAndGoal_ReturnsEmptyWithoutThrowing()
    {
        var path = AStarPathfinder.FindPath((-5, -5), (99, 99), AlwaysWalkable, 10, 10);

        Assert.Empty(path);
    }

    /// <summary>Verifies an out-of-bounds start equal to an out-of-bounds goal returns empty.</summary>
    [Fact]
    public void FindPath_StartEqualsGoalOutOfBounds_ReturnsEmptyWithoutThrowing()
    {
        var path = AStarPathfinder.FindPath((99, 99), (99, 99), AlwaysWalkable, 10, 10);

        Assert.Empty(path);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>A walkability predicate over a fully open grid.</summary>
    private static bool AlwaysWalkable(int x, int y) => true;

    /// <summary>
    /// Builds a walkability predicate from a string map where <c>'.'</c> is walkable and any other
    /// character is blocked. Row 0 is y = 0, so <c>map[y][x]</c> is the tile at (x, y).
    /// </summary>
    private static Func<int, int, bool> MapWalkability(string[] map) =>
        (x, y) => map[y][x] == '.';

    /// <summary>
    /// Asserts <paramref name="path"/> is a valid start-exclusive → goal-inclusive route: every
    /// tile is walkable and in bounds, each step is an 8-neighbor move, and no diagonal step cuts a
    /// corner (both orthogonally adjacent cells are walkable).
    /// </summary>
    private static void AssertValidPath(
        IReadOnlyList<(int X, int Y)> path,
        (int X, int Y) start,
        (int X, int Y) goal,
        Func<int, int, bool> isWalkable,
        int width,
        int height)
    {
        Assert.NotEmpty(path);
        Assert.Equal(goal, path[^1]);

        var previous = start;
        foreach (var tile in path)
        {
            Assert.True(IsWalkable(tile, isWalkable, width, height), $"tile ({tile.X}, {tile.Y}) must be walkable");

            int dx = tile.X - previous.X;
            int dy = tile.Y - previous.Y;
            int adx = Math.Abs(dx);
            int ady = Math.Abs(dy);

            // Each step must be a single 8-neighbor move (never a jump or a self-loop).
            Assert.True(
                adx <= 1 && ady <= 1 && adx + ady >= 1,
                $"step from ({previous.X}, {previous.Y}) to ({tile.X}, {tile.Y}) is not an 8-neighbor move");

            // Diagonal steps must not cut corners.
            if (adx == 1 && ady == 1)
            {
                Assert.True(
                    IsWalkable((previous.X + dx, previous.Y), isWalkable, width, height) &&
                    IsWalkable((previous.X, previous.Y + dy), isWalkable, width, height),
                    $"diagonal step to ({tile.X}, {tile.Y}) cuts a corner");
            }

            previous = tile;
        }
    }

    /// <summary>Asserts the path uses only cardinal steps (no diagonals) from the given start.</summary>
    private static void AssertNoDiagonalSteps(IReadOnlyList<(int X, int Y)> path, (int X, int Y) start)
    {
        var previous = start;
        foreach (var tile in path)
        {
            int dx = Math.Abs(tile.X - previous.X);
            int dy = Math.Abs(tile.Y - previous.Y);
            Assert.True(
                dx + dy == 1,
                $"path uses a diagonal step to ({tile.X}, {tile.Y}) instead of detouring orthogonally");
            previous = tile;
        }
    }

    /// <summary>Computes the sum of step costs (1 cardinal, √2 diagonal) from start through path.</summary>
    private static double PathCost(IReadOnlyList<(int X, int Y)> path, (int X, int Y) start)
    {
        double cost = 0;
        var previous = start;
        foreach (var tile in path)
        {
            int dx = Math.Abs(tile.X - previous.X);
            int dy = Math.Abs(tile.Y - previous.Y);
            cost += dx == 1 && dy == 1 ? Math.Sqrt(2) : 1.0;
            previous = tile;
        }

        return cost;
    }

    /// <summary>Returns whether the tile is in bounds and walkable.</summary>
    private static bool IsWalkable((int X, int Y) tile, Func<int, int, bool> isWalkable, int width, int height)
    {
        if (tile.X < 0 || tile.Y < 0 || tile.X >= width || tile.Y >= height)
        {
            return false;
        }

        return isWalkable(tile.X, tile.Y);
    }
}
