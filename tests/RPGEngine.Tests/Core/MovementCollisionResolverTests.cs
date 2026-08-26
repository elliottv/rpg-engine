using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Unit tests for <see cref="MovementCollisionResolver"/> (story 56): the internal per-axis
/// slide-to-boundary clamp that resolves a character's displacement against the map's solid
/// tiles. Each test drives the resolver directly (via <c>InternalsVisibleTo</c>) and asserts the
/// exact-boundary semantics for the four cardinal directions, large steps and the map edge.
/// </summary>
public class MovementCollisionResolverTests
{
    // The default 48x48 sprite with 48 px tiles has a lower-half footprint with hw = hh = 0.5
    // tiles (x in [pos.X - 0.5, pos.X + 0.5], y in [pos.Y - 0.5, pos.Y]).
    private const double HalfWidth = 0.5;
    private const double HalfHeight = 0.5;

    /// <summary>Verifies a downward step that would overshoot a solid row clamps the feet to exactly the row's top edge (y = r).</summary>
    [Fact]
    public void Resolve_DownStep_OvershootingSolidRow_ClampsFeetToExactTopEdge()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidRow(6, row: 2));
        using var map = TileMap.Load(fixture.MapPath);

        // The step from 1.9666... by 1/30 would land at 2.0000... (with float overshoot); the
        // clamp must land the feet on exactly y = 2.0, never one step short.
        var result = MovementCollisionResolver.Resolve(
            new Position(2.0, 1.9666666666666666), 0, 1.0 / 30, map, HalfWidth, HalfHeight);

        Assert.Equal(2.0, result.Y, precision: 9);
        Assert.Equal(2.0, result.X, precision: 9);
        Assert.True(result.Y <= 2.0 + 1e-9, "The footprint must never overlap the solid row.");
    }

    /// <summary>Verifies a rightward step into a solid column clamps the right edge to exactly the column's left edge (x = c - hw).</summary>
    [Fact]
    public void Resolve_RightStep_IntoSolidColumn_ClampsFeetToExactLeftEdge()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidColumn(6, column: 2));
        using var map = TileMap.Load(fixture.MapPath);

        // The player has walked 29 frame-steps from x = 0.5 (x = 1.4666...); the 30th step would
        // overshoot the boundary at x = 1.5 (float accumulation lands at 1.5000000000000013),
        // which the old revert-the-whole-step rule rejected, leaving the feet one step short.
        // The clamp must land them on exactly x = 1.5.
        var result = MovementCollisionResolver.Resolve(
            new Position(1.466666666666668, 2.0), 1.0 / 30, 0, map, HalfWidth, HalfHeight);

        Assert.Equal(1.5, result.X, precision: 9);
        Assert.Equal(2.0, result.Y, precision: 9);
    }

    /// <summary>Verifies a leftward step into a solid column clamps the left edge to exactly the column's right edge (x = c + 1 + hw).</summary>
    [Fact]
    public void Resolve_LeftStep_IntoSolidColumn_ClampsFeetToExactRightEdge()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidColumn(6, column: 2));
        using var map = TileMap.Load(fixture.MapPath);

        // The solid column occupies [2,3): approaching from the right, the left edge (x - 0.5)
        // stops at the column's right edge x = 3.0, so the feet clamp to x = 3.5.
        var result = MovementCollisionResolver.Resolve(
            new Position(3.5333333333333332, 2.0), -1.0 / 30, 0, map, HalfWidth, HalfHeight);

        Assert.Equal(3.5, result.X, precision: 9);
        Assert.Equal(2.0, result.Y, precision: 9);
    }

    /// <summary>Verifies an upward step into a solid row clamps the top edge to exactly the row's bottom edge (y = r + 1 + hh).</summary>
    [Fact]
    public void Resolve_UpStep_IntoSolidRow_ClampsFeetToExactBottomEdge()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidRow(6, row: 2));
        using var map = TileMap.Load(fixture.MapPath);

        // The solid row occupies [2,3): approaching from below, the top edge (y - 0.5) stops at
        // the row's bottom edge y = 3.0, so the feet clamp to y = 3.5.
        var result = MovementCollisionResolver.Resolve(
            new Position(2.0, 3.5333333333333332), 0, -1.0 / 30, map, HalfWidth, HalfHeight);

        Assert.Equal(3.5, result.Y, precision: 9);
        Assert.Equal(2.0, result.X, precision: 9);
    }

    /// <summary>Verifies a single large step toward a solid tile clamps to the exact boundary without tunneling through it.</summary>
    [Fact]
    public void Resolve_LargeStep_TowardSolidTile_ClampsToExactBoundaryWithoutTunneling()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidColumn(6, column: 3));
        using var map = TileMap.Load(fixture.MapPath);

        // A 4-tile step from x = 0.5 toward the wall at x = 3 stops the right edge exactly at the
        // wall's left edge: feet x = 2.5.
        var result = MovementCollisionResolver.Resolve(
            new Position(0.5, 1.0), 4.0, 0, map, HalfWidth, HalfHeight);

        Assert.Equal(2.5, result.X, precision: 9);
        Assert.Equal(1.0, result.Y, precision: 9);
    }

    /// <summary>Verifies a legal full displacement (no solid gained) is returned unchanged.</summary>
    [Fact]
    public void Resolve_FullDisplacementLegal_ReturnsRequestedPosition()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidColumn(6, column: 4));
        using var map = TileMap.Load(fixture.MapPath);

        var result = MovementCollisionResolver.Resolve(
            new Position(0.5, 0.5), 1.5, 0.25, map, HalfWidth, HalfHeight);

        Assert.Equal(2.0, result.X, precision: 9);
        Assert.Equal(0.75, result.Y, precision: 9);
    }

    /// <summary>Verifies the right map edge clamps the feet to exactly x = Width - hw.</summary>
    [Fact]
    public void Resolve_RightStep_PastMapEdge_ClampsFeetToExactMapEdge()
    {
        using var fixture = CollisionMapFixture(3, 3, new uint[9]);
        using var map = TileMap.Load(fixture.MapPath);

        var result = MovementCollisionResolver.Resolve(
            new Position(1.5, 0.5), 2.0, 0, map, HalfWidth, HalfHeight);

        Assert.Equal(2.5, result.X, precision: 9); // Width(3) - hw(0.5)
        Assert.Equal(0.5, result.Y, precision: 9);
    }

    /// <summary>Verifies the bottom map edge clamps the feet to exactly y = Height.</summary>
    [Fact]
    public void Resolve_DownStep_PastMapEdge_ClampsFeetToExactMapEdge()
    {
        using var fixture = CollisionMapFixture(3, 3, new uint[9]);
        using var map = TileMap.Load(fixture.MapPath);

        var result = MovementCollisionResolver.Resolve(
            new Position(0.5, 1.5), 0, 2.0, map, HalfWidth, HalfHeight);

        Assert.Equal(3.0, result.Y, precision: 9); // Height(3)
        Assert.Equal(0.5, result.X, precision: 9);
    }

    /// <summary>Verifies the top map edge clamps the feet to exactly y = hh.</summary>
    [Fact]
    public void Resolve_UpStep_PastMapEdge_ClampsFeetToExactMapEdge()
    {
        using var fixture = CollisionMapFixture(3, 3, new uint[9]);
        using var map = TileMap.Load(fixture.MapPath);

        var result = MovementCollisionResolver.Resolve(
            new Position(0.5, 0.5), 0, -2.0, map, HalfWidth, HalfHeight);

        Assert.Equal(0.5, result.Y, precision: 9); // hh(0.5)
        Assert.Equal(0.5, result.X, precision: 9);
    }

    /// <summary>
    /// Verifies the resolver refuses a move that would keep the footprint overlapping a solid
    /// tile when the starting position is already illegal (e.g. left embedded in a wall by the
    /// click auto-walk): moving deeper into the wall returns the starting position instead of
    /// tunnelling through it.
    /// </summary>
    [Fact]
    public void Resolve_FromIllegalStart_UpwardIntoWall_RefusesMove()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidRow(6, row: 2));
        using var map = TileMap.Load(fixture.MapPath);

        // An illegal start: feet at y = 3.49, so the lower-half footprint [2.99, 3.49] already
        // overlaps the solid row [2,3). Moving up would keep the overlap (the gained-range scan
        // only sees rows above the already-overlapped wall), so the move must be refused.
        var start = new Position(2.0, 3.49);
        Assert.True(map.IsAreaSolid(2.0 - 0.5, 3.49 - 0.5, 1.0, 0.5), "sanity: the start is illegal");

        var result = MovementCollisionResolver.Resolve(start, 0, -0.05, map, HalfWidth, HalfHeight);

        Assert.Equal(start, result);
    }

    /// <summary>
    /// Verifies the resolver allows a move that clears an illegal starting overlap (escaping away
    /// from the wall), so an embedded player is never permanently stuck.
    /// </summary>
    [Fact]
    public void Resolve_FromIllegalStart_DownwardAwayFromWall_AllowsEscape()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidRow(6, row: 2));
        using var map = TileMap.Load(fixture.MapPath);

        var start = new Position(2.0, 3.49);
        var result = MovementCollisionResolver.Resolve(start, 0, 0.1, map, HalfWidth, HalfHeight);

        Assert.True(result.Y > start.Y, $"The player should move down (escape): result.Y={result.Y}");
        Assert.False(map.IsAreaSolid(result.X - 0.5, result.Y - 0.5, 1.0, 0.5), "The escaped footprint must be legal.");
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>Builds a 6-row gid array with a single solid row.</summary>
    private static uint[] SolidRow(int width, int row)
    {
        var gids = new uint[width * 6];
        for (var x = 0; x < width; x++)
        {
            gids[(row * width) + x] = 1;
        }

        return gids;
    }

    /// <summary>Builds a 6-column gid array with a single solid column.</summary>
    private static uint[] SolidColumn(int height, int column)
    {
        var gids = new uint[6 * height];
        for (var y = 0; y < height; y++)
        {
            gids[(y * 6) + column] = 1;
        }

        return gids;
    }

    /// <summary>Creates a map fixture with a walkable ground layer and a "walls" collision layer.</summary>
    private static TiledTestFixture CollisionMapFixture(int width, int height, uint[] collisionGids)
        => new(
            width,
            height,
            new[]
            {
                new TileLayerSpec("ground", Enumerable.Repeat(1u, width * height).ToArray()),
                new TileLayerSpec(
                    "walls",
                    collisionGids,
                    Properties: new[] { new FixtureProperty("is_collision", "bool", "true") }),
            });
}
