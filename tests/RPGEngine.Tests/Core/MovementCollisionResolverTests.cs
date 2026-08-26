using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Unit tests for <see cref="MovementCollisionResolver"/> (story 56): the internal per-axis
/// slide-to-boundary clamp that resolves a character's displacement against the map's solid
/// tiles. Each test drives the resolver directly (via <c>InternalsVisibleTo</c>) and asserts the
/// exact-boundary semantics for the four cardinal directions, large steps, the map edge and the
/// fixed 1×1 tile lower-body box.
/// </summary>
public class MovementCollisionResolverTests
{
    // The player's collision footprint is the fixed 1x1 tile lower-body box (48x48 px): 1 tile
    // wide (hw = 0.5) and extending 1 tile above the feet (heightAboveFeet = 1.0), so the box is
    // x in [pos.X - 0.5, pos.X + 0.5], y in [pos.Y - 1.0, pos.Y] - independent of the rendered
    // sprite size (a 1-tile corridor always fits, see Resolve_OneTileWideCorridor_...).
    private const double HalfWidth = 0.5;
    private const double HeightAboveFeet = 1.0;

    /// <summary>Verifies a downward step that would overshoot a solid row clamps the feet to exactly the row's top edge (y = r).</summary>
    [Fact]
    public void Resolve_DownStep_OvershootingSolidRow_ClampsFeetToExactTopEdge()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidRow(6, row: 2));
        using var map = TileMap.Load(fixture.MapPath);

        // The step from 1.9666... by 1/30 would land at 2.0000... (with float overshoot); the
        // clamp must land the feet on exactly y = 2.0, never one step short.
        var result = MovementCollisionResolver.Resolve(
            new Position(2.0, 1.9666666666666666), 0, 1.0 / 30, map, HalfWidth, HeightAboveFeet);

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
            new Position(1.466666666666668, 2.0), 1.0 / 30, 0, map, HalfWidth, HeightAboveFeet);

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
            new Position(3.5333333333333332, 2.0), -1.0 / 30, 0, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(3.5, result.X, precision: 9);
        Assert.Equal(2.0, result.Y, precision: 9);
    }

    /// <summary>Verifies an upward step into a solid row clamps the top edge to exactly the row's bottom edge (y = r + 1 + heightAboveFeet).</summary>
    [Fact]
    public void Resolve_UpStep_IntoSolidRow_ClampsFeetToExactBottomEdge()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidRow(6, row: 2));
        using var map = TileMap.Load(fixture.MapPath);

        // The solid row occupies [2,3): approaching from below, the top edge (y - 1.0) stops at
        // the row's bottom edge y = 3.0, so the feet clamp to y = 2 + 1 + 1.0 = 4.0 (the whole
        // body stops below the ceiling, no head overlap).
        var result = MovementCollisionResolver.Resolve(
            new Position(2.0, 4.033333333333333), 0, -1.0 / 30, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(4.0, result.Y, precision: 9);
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
            new Position(0.5, 1.0), 4.0, 0, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(2.5, result.X, precision: 9);
        Assert.Equal(1.0, result.Y, precision: 9);
    }

    /// <summary>Verifies a legal full displacement (no solid gained) is returned unchanged.</summary>
    [Fact]
    public void Resolve_FullDisplacementLegal_ReturnsRequestedPosition()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidColumn(6, column: 4));
        using var map = TileMap.Load(fixture.MapPath);

        // Start in row 1 so the 1x1 box is fully inside the map; the destination (2.0, 1.75)
        // does not gain any solid column/row, so the full displacement is returned.
        var result = MovementCollisionResolver.Resolve(
            new Position(0.5, 1.5), 1.5, 0.25, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(2.0, result.X, precision: 9);
        Assert.Equal(1.75, result.Y, precision: 9);
    }

    /// <summary>Verifies the right map edge clamps the feet to exactly x = Width - hw.</summary>
    [Fact]
    public void Resolve_RightStep_PastMapEdge_ClampsFeetToExactMapEdge()
    {
        using var fixture = CollisionMapFixture(3, 3, new uint[9]);
        using var map = TileMap.Load(fixture.MapPath);

        var result = MovementCollisionResolver.Resolve(
            new Position(1.5, 1.5), 2.0, 0, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(2.5, result.X, precision: 9); // Width(3) - hw(0.5)
        Assert.Equal(1.5, result.Y, precision: 9);
    }

    /// <summary>Verifies the bottom map edge clamps the feet to exactly y = Height.</summary>
    [Fact]
    public void Resolve_DownStep_PastMapEdge_ClampsFeetToExactMapEdge()
    {
        using var fixture = CollisionMapFixture(3, 3, new uint[9]);
        using var map = TileMap.Load(fixture.MapPath);

        var result = MovementCollisionResolver.Resolve(
            new Position(0.5, 1.5), 0, 2.0, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(3.0, result.Y, precision: 9); // Height(3)
        Assert.Equal(0.5, result.X, precision: 9);
    }

    /// <summary>Verifies the top map edge clamps the feet to exactly y = heightAboveFeet.</summary>
    [Fact]
    public void Resolve_UpStep_PastMapEdge_ClampsFeetToExactMapEdge()
    {
        using var fixture = CollisionMapFixture(3, 3, new uint[9]);
        using var map = TileMap.Load(fixture.MapPath);

        var result = MovementCollisionResolver.Resolve(
            new Position(0.5, 1.5), 0, -2.0, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(1.0, result.Y, precision: 9); // heightAboveFeet(1.0)
        Assert.Equal(0.5, result.X, precision: 9);
    }

    /// <summary>
    /// Verifies the resolver refuses a move that would keep the footprint overlapping a solid
    /// tile when the starting position is already illegal (e.g. left embedded in a wall by an
    /// external teleport): moving deeper into the wall returns the starting position instead of
    /// tunnelling through it.
    /// </summary>
    [Fact]
    public void Resolve_FromIllegalStart_UpwardIntoWall_RefusesMove()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidRow(6, row: 2));
        using var map = TileMap.Load(fixture.MapPath);

        // An illegal start: feet at y = 3.49, so the 1x1 box [2.49, 3.49] already overlaps the
        // solid row [2,3). Moving up would keep the overlap (the gained-range scan only sees rows
        // above the already-overlapped wall), so the move must be refused.
        var start = new Position(2.0, 3.49);
        Assert.True(map.IsAreaSolid(2.0 - 0.5, 3.49 - 1.0, 1.0, 1.0), "sanity: the start is illegal");

        var result = MovementCollisionResolver.Resolve(start, 0, -0.05, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(start, result);
    }

    /// <summary>
    /// Verifies the resolver allows a move that clears an illegal starting overlap (escaping away
    /// from the wall), so an embedded player is never permanently stuck. With the 1x1 box the
    /// escape displacement must clear the whole overlap in one step: the box at y = 3.49 overlaps
    /// the solid row [2,3) by 0.49 tiles, so a 0.6-tile downward step (not a small 0.1 one) is
    /// required.
    /// </summary>
    [Fact]
    public void Resolve_FromIllegalStart_DownwardAwayFromWall_AllowsEscape()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidRow(6, row: 2));
        using var map = TileMap.Load(fixture.MapPath);

        var start = new Position(2.0, 3.49);
        var result = MovementCollisionResolver.Resolve(start, 0, 0.6, map, HalfWidth, HeightAboveFeet);

        Assert.True(result.Y > start.Y, $"The player should move down (escape): result.Y={result.Y}");
        Assert.False(map.IsAreaSolid(result.X - 0.5, result.Y - 1.0, 1.0, 1.0), "The escaped footprint must be legal.");
    }

    /// <summary>
    /// Verifies the fixed 1x1 tile box fits through a 1-tile-wide corridor: with solid side walls
    /// one tile apart, a centred downward move is returned unchanged because the box spans exactly
    /// the corridor column and never touches the walls (the previous sprite-derived footprint,
    /// wider than 1 tile for larger sprites, could not enter such a corridor).
    /// </summary>
    [Fact]
    public void Resolve_OneTileWideCorridor_CenteredMoveIsNotBlocked()
    {
        // 3x4 map: solid columns 0 and 2 for rows 1..3, open column 1 (the 1-tile corridor).
        var gids = new uint[3 * 4];
        for (var row = 1; row <= 3; row++)
        {
            gids[(row * 3) + 0] = 1;
            gids[(row * 3) + 2] = 1;
        }

        using var fixture = CollisionMapFixture(3, 4, gids);
        using var map = TileMap.Load(fixture.MapPath);

        // The player is centred in the corridor (feet x = 1.5); moving down a full tile is legal
        // because the 1x1 box spans exactly [1.0, 2.0] (column 1), never touching the walls.
        var result = MovementCollisionResolver.Resolve(
            new Position(1.5, 0.5), 0, 1.0, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(1.5, result.X, precision: 9);
        Assert.Equal(1.5, result.Y, precision: 9);
    }

    /// <summary>
    /// Verifies a diagonal move where one axis is blocked is fully refused: the starting position
    /// is returned (no sliding along the free axis) - a diagonal into a wall where only one axis
    /// is free stops the character entirely.
    /// </summary>
    [Fact]
    public void ResolveDiagonal_OneAxisBlocked_ReturnsStart()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidColumn(6, column: 3));
        using var map = TileMap.Load(fixture.MapPath);

        // Start flush against the wall's left edge (feet x = 2.5, box right edge at x = 3.0);
        // moving up-right, the X displacement is blocked while Y is free. All-or-nothing: the
        // move is refused entirely rather than sliding up along the wall.
        var start = new Position(2.5, 4.0);
        var result = MovementCollisionResolver.ResolveDiagonal(
            start, 1.0 / 30, -1.0 / 30, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(start, result);
    }

    /// <summary>
    /// Verifies a diagonal move whose full displacement is clear on both axes applies the full
    /// displacement (the destination is returned unchanged).
    /// </summary>
    [Fact]
    public void ResolveDiagonal_BothAxesFree_ReturnsDestination()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidColumn(6, column: 4));
        using var map = TileMap.Load(fixture.MapPath);

        var result = MovementCollisionResolver.ResolveDiagonal(
            new Position(1.5, 3.0), 0.5, -0.5, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(2.0, result.X, precision: 9);
        Assert.Equal(2.5, result.Y, precision: 9);
    }

    /// <summary>
    /// Verifies a large diagonal step toward a solid column is refused entirely (the starting
    /// position is returned): the gained-range scan detects the solid column along the whole
    /// displacement, so the step neither tunnels through the wall nor partially slides along the
    /// free axis.
    /// </summary>
    [Fact]
    public void ResolveDiagonal_LargeStep_TowardSolidColumn_RefusedWithoutSliding()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidColumn(6, column: 4));
        using var map = TileMap.Load(fixture.MapPath);

        // A 2.5-tile right / 2.0-tile up step from (1.5, 3.0): the right edge would enter the
        // solid column at x = 4, so the whole diagonal move is refused - the free Y axis does
        // not move either.
        var start = new Position(1.5, 3.0);
        var result = MovementCollisionResolver.ResolveDiagonal(
            start, 2.5, -2.0, map, HalfWidth, HeightAboveFeet);

        Assert.Equal(start, result);
    }

    /// <summary>
    /// Verifies a diagonal move from an already-illegal start that clears the overlap (escaping
    /// away from the wall) is allowed, so an embedded player is never permanently stuck.
    /// </summary>
    [Fact]
    public void ResolveDiagonal_FromIllegalStart_EscapeAwayFromWall_Allowed()
    {
        using var fixture = CollisionMapFixture(6, 6, SolidColumn(6, column: 2));
        using var map = TileMap.Load(fixture.MapPath);

        // An illegal start: the box [1.5, 2.5] already overlaps the solid column [2,3) at row 2.
        var start = new Position(2.0, 2.5);
        Assert.True(map.IsAreaSolid(2.0 - 0.5, 2.5 - 1.0, 1.0, 1.0), "sanity: the start is illegal");

        // Moving down-right far enough clears the overlap in one step (the box's left edge must
        // pass the column's right edge at x = 3.0, so the rightward displacement must be at least
        // 1.5 tiles; a smaller step would keep the overlap and be refused, never moving deeper).
        var result = MovementCollisionResolver.ResolveDiagonal(
            start, 1.6, 0.6, map, HalfWidth, HeightAboveFeet);

        Assert.False(map.IsAreaSolid(result.X - 0.5, result.Y - 1.0, 1.0, 1.0), "The escaped footprint must be legal.");
        Assert.True(result.X > start.X && result.Y > start.Y, "The player should move down-right (escape).");
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
