using RPGEngine.Sprites;
using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Partial <see cref="GameEngineTests"/> containing the map collision and player-clamping tests. Splitting the large engine
/// test class into one file per functional area keeps the test files manageable; the shared
/// helpers (map fixtures, sprite configuration, rendering) live in the core
/// <c>GameEngineTests</c> partial file and are reused here.
/// </summary>
public partial class GameEngineTests
{
    // ---------------------------------------------------------------------
    // Acceptance (story 23, updated by story 56): the player is clamped inside
    // the map using the fixed 1×1 tile lower-body collision box. The box (48×48
    // px, the lower body of the sprite) is independent of the rendered sprite
    // size, so clamping is identical for the default 48×48 sprite and a larger
    // 78×108 sprite: feet x ∈ [0.5, Map.Width - 0.5], y ∈ [1.0, Map.Height].
    // ---------------------------------------------------------------------
    /// <summary>Verifies ClampPlayerToMap clamps the fixed 1×1 tile lower-body box regardless of the rendered sprite size (a 78×108 sheet clamps exactly like the default sprite).</summary>
    [Fact]
    public void ClampPlayerToMap_UsesFixedOneTileBox_RegardlessOfSpriteSize()
    {
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // A larger spritesheet (78×108 cells) must not widen or raise the collision box: the
        // clamp still uses the fixed 1×1 tile lower-body box (the head sticks out visually but
        // never participates in collision/clamping).
        using (var stream = CharacterTestHelper.CreateSheetStream(seed: 1, width: 936, height: 864))
        {
            engine.LoadSpriteSheet("hero", stream);
        }
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        // Beyond the bottom-right corner: the 1×1 box clamps so its right edge (feet X + 0.5)
        // stays at the map's right edge and its bottom edge (the feet) at the bottom edge:
        // x = 10 - 0.5 = 9.5, y = 10.
        engine.Player.Position = new Position(1000, 1000);
        engine.Update(FrameDt);
        Assert.Equal(10 - 0.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(10, engine.Player.Position.Y, precision: 6);

        // Negative position clamps the feet to the top-left: the box's left edge (feet X - 0.5)
        // at the left map edge (x = 0.5) and its top edge (feet Y - 1.0) at the top map edge
        // (y = 1.0).
        engine.Player.Position = new Position(-100, -100);
        engine.Update(FrameDt);
        Assert.Equal(0.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(1.0, engine.Player.Position.Y, precision: 6);
    }

    /// <summary>Verifies the fixed 1×1 box clamp for a 78×108 sprite through ComputeCameraOrigin and rendered output.</summary>
    [Fact]
    public void ClampPlayerToMap_LargeSheet_VerifiedViaCameraAndRender()
    {
        const int canvasSize = 240;
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        using (var stream = CharacterTestHelper.CreateSheetStream(seed: 1, width: 936, height: 864))
        {
            engine.LoadSpriteSheet("hero", stream);
        }
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        engine.Player.Position = new Position(1000, 1000);
        engine.Update(FrameDt); // clamps the fixed 1×1 box to (9.5, 10)

        // The camera origin clamps to the map: maxX = maxY = 10 - 240/48 = 5 tiles.
        Assert.Equal(new Position(5, 5), engine.ComputeCameraOrigin(canvasSize, canvasSize));

        // The 78×108 sprite is anchored at its middle-bottom (feet): with the player's feet at
        // (9.5, 10) tiles the screen feet are ((9.5-5)*48, (10-5)*48) = (216, 240) and the
        // sprite top-left is (216 - 39, 240 - 108) = (177, 132); its centre is (216, 186).
        using var bitmap = Render(engine, canvasSize, canvasSize);
        var expected = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, StandingFrame);
        Assert.Equal(expected, bitmap.GetPixel(177 + 39, 132 + 54));
    }

    /// <summary>Verifies ClampPlayerToMap keeps the default 48×48 sprite's fixed 1×1 tile lower-body box in the map (feet x ∈ [0.5, 9.5], y ∈ [1.0, 10]).</summary>
    [Fact]
    public void ClampPlayerToMap_DefaultSprite_ClampsFeetFootprint()
    {
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Beyond the bottom-right corner: the 1×1 box's half-width is 0.5 tiles, so the feet
        // clamp to x = 10 - 0.5 = 9.5 and y = 10 (the box's bottom edge, the feet, at the bottom).
        engine.Player.Position = new Position(1000, 1000);
        engine.Update(FrameDt);
        Assert.Equal(9.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(10, engine.Player.Position.Y, precision: 6);

        // Negative position clamps the feet to the top-left: x = 0.5 (left edge at 0), and
        // y = 1.0 (the box's top edge at the top map edge y = 0).
        engine.Player.Position = new Position(-100, -100);
        engine.Update(FrameDt);
        Assert.Equal(0.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(1.0, engine.Player.Position.Y, precision: 6);
    }

    // ---------------------------------------------------------------------
    // Story 35: map collisions. A tile layer declaring the Tiled is_collision
    // bool property contains solid tiles that block the player; the engine
    // resolves the player's displacement with axis-separated movement so the
    // player stops at solid boundaries (never overlapping them) and slides
    // along walls on the free axis, while the map edge is solid (characters
    // cannot leave the map) and non-collision layers never block.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a player walking right into a solid tile stops at its exact tile-unit boundary and never overlaps it.</summary>
    [Fact]
    public void Update_PlayerWalksIntoSolidTile_StopsAtBoundary()
    {
        // 4x4 map: a "walls" collision layer with a solid column at x=2 for every row.
        using var fixture = CreateCollisionMapFixture(4, 4, new uint[]
        {
            0, 0, 1, 0,
            0, 0, 1, 0,
            0, 0, 1, 0,
            0, 0, 1, 0,
        });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // The default 48x48 sprite collides with a fixed 1x1 tile lower-body box (1 tile wide,
        // 1 tile tall, feet at the bottom-centre). Start at (0.5, 1.0) and hold D.
        engine.Player.Position = new Position(0.5, 1.0);
        engine.Input(Key.D, true);

        // Move right by exactly 1 tile (dt = 0.5 at 2 tiles/s): the feet reach X = 1.5, where
        // the box's right edge [1.0, 2.0] just touches the solid tile at x = 2.0.
        engine.Update(dt: 0.5);
        Assert.Equal(1.5, engine.Player.Position.X, precision: 6);

        // The next step would push the box into the solid tile and is clamped back.
        engine.Update(dt: 0.5);
        Assert.Equal(1.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(1.0, engine.Player.Position.Y, precision: 6); // straight line, Y unchanged
        Assert.True(engine.Player.Position.X + 0.5 <= 2.0 + 1e-9, "The footprint must never overlap the solid tile.");
        Assert.Equal(Direction.Right, engine.Player.Direction);
    }

    /// <summary>Verifies a player walking down into a solid tile stops at its exact tile-unit boundary and never overlaps it.</summary>
    [Fact]
    public void Update_PlayerWalksDownIntoSolidTile_StopsAtBoundary()
    {
        // 4x4 map: a "walls" collision layer with a solid row at y=2 for every column.
        using var fixture = CreateCollisionMapFixture(4, 4, new uint[]
        {
            0, 0, 0, 0,
            0, 0, 0, 0,
            1, 1, 1, 1,
            1, 1, 1, 1,
        });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        engine.Player.Position = new Position(1.0, 1.5);
        engine.Input(Key.S, true);

        // Move down by exactly 0.5 tiles (dt = 0.25 at 2 tiles/s): the feet reach Y = 2.0, where
        // the fixed 1x1 box's bottom edge (the feet) just touches the solid row at y = 2.0.
        engine.Update(dt: 0.25);
        Assert.Equal(2.0, engine.Player.Position.Y, precision: 6);

        // The next step would push the box into the solid row and is clamped back.
        engine.Update(dt: 0.1);
        Assert.Equal(1.0, engine.Player.Position.X, precision: 6);
        Assert.Equal(2.0, engine.Player.Position.Y, precision: 6);
        Assert.True(engine.Player.Position.Y <= 2.0 + 1e-9, "The footprint must never overlap the solid tile.");
        Assert.Equal(Direction.Down, engine.Player.Direction);
    }

    /// <summary>Verifies a player moving diagonally into a vertical wall stops entirely: diagonal movement is all-or-nothing, so the blocked X axis stops the player instead of sliding up along the wall on the free Y axis.</summary>
    [Fact]
    public void Update_PlayerMovesDiagonallyIntoWall_StopsEntirely()
    {
        // 5x5 map: a "walls" collision layer with a solid column at x=3 for every row.
        var gids = new uint[25];
        for (var y = 0; y < 5; y++)
        {
            gids[(y * 5) + 3] = 1;
        }

        using var fixture = CreateCollisionMapFixture(5, 5, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Start flush against the left edge of the wall column (x=3): the fixed 1x1 box spans
        // [2,3) horizontally at feet x=2.5. Moving UpRight, the X displacement is blocked by the
        // wall while Y is free; diagonal movement is all-or-nothing, so the player stays put
        // instead of sliding straight up along the wall.
        engine.Player.Position = new Position(2.5, 4.0);
        engine.Input(Key.W, true);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player never moved: X stays at the wall's left edge (2.5) and Y stays at 4.0 (no
        // sliding along the free axis).
        Assert.Equal(2.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(4.0, engine.Player.Position.Y, precision: 6);
    }

    /// <summary>Verifies tiles drawn from a normal (non-collision) layer never block: the player walks across them freely.</summary>
    [Fact]
    public void Update_NonCollisionLayer_NeverBlocksMovement()
    {
        // A 4x4 map whose ground layer draws a tile in every cell but has no collision layer.
        using var fixture = CreateFilledMapFixture(4, 4);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Feet in row 1 so the fixed 1x1 tile box (y in [0.5, 1.5]) is fully inside the map.
        engine.Player.Position = new Position(0.5, 1.5);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // One second at 2 tiles/s: moved ~2 tiles right, Y unchanged (the drawn tiles never block).
        Assert.Equal(2.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(1.5, engine.Player.Position.Y, precision: 6);
    }

    /// <summary>
    /// Verifies a solid tile anywhere within the fixed 1×1 tile lower-body box blocks the
    /// player: the box covers the whole body of the default 48×48 sprite, so a tile at "head"
    /// level (row 0 while the feet are in row 1) is inside the box and stops the walk at the
    /// tile's near edge, exactly like a tile at feet level.
    /// </summary>
    [Fact]
    public void Update_SolidTileInBodyBox_BlocksWalkingPast()
    {
        // 4x4 map with a single solid tile at (2, 0) — inside the 1×1 box when the player's
        // feet are in row 1 (the box spans y ∈ [0.5, 1.5], which includes row 0).
        var gids = new uint[16];
        gids[(0 * 4) + 2] = 1;
        using var fixture = CreateCollisionMapFixture(4, 4, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Feet in row 1: the 1×1 box spans y ∈ [0.5, 1.5], so the solid tile at (2, 0) is
        // inside the box and must block the walk at the box's right edge (x = 2.0).
        engine.Player.Position = new Position(0.5, 1.5);
        engine.Input(Key.D, true);

        // One second at 2 tiles/s (dt = 1.0): the right edge stops exactly at the tile's left
        // edge, so the feet clamp to X = 1.5 (never 2.5, the tile is inside the box).
        engine.Update(dt: 1.0);
        Assert.Equal(1.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(1.5, engine.Player.Position.Y, precision: 6);
    }

    /// <summary>Verifies a solid tile in the lower-body region blocks the player at the feet boundary (feet X = 1.5).</summary>
    [Fact]
    public void Update_SolidTileInLowerBody_BlocksAtFeetBoundary()
    {
        // 4x4 map with a single solid tile at (2, 1) — inside the 1×1 box when the player's
        // feet are in row 1 (the box spans y ∈ [0.5, 1.5], which includes row 1).
        var gids = new uint[16];
        gids[(1 * 4) + 2] = 1;
        using var fixture = CreateCollisionMapFixture(4, 4, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        engine.Player.Position = new Position(0.5, 1.5);
        engine.Input(Key.D, true);

        // Move right by exactly 1 tile: the feet reach X = 1.5, where the 1×1 box's right edge
        // [1.0, 2.0] just touches the solid tile at x = 2.0.
        engine.Update(dt: 0.5);
        Assert.Equal(1.5, engine.Player.Position.X, precision: 6);

        // The next step would push the box into the solid tile and is clamped back.
        engine.Update(dt: 0.5);
        Assert.Equal(1.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(1.5, engine.Player.Position.Y, precision: 6);
        Assert.True(engine.Player.Position.X + 0.5 <= 2.0 + 1e-9);
    }

    /// <summary>
    /// Verifies the map edge is solid: with no collision layer the player cannot walk out of the
    /// map, and the displacement slides to the <em>exact</em> map edge of the fixed 1×1 tile
    /// lower-body box. Walking right clamps the feet to exactly x = 1.5 (the box's right edge at
    /// x = 2), walking down clamps them to exactly y = 2.0 (the box's bottom edge, the feet, at
    /// the bottom map edge), and walking up clamps them to exactly y = 1.0 (the box's top edge
    /// at y = 0). The default 48x48 sprite with 48 px tiles has a 1x1 box: hw = 0.5 and the box
    /// extends 1 tile above the feet.
    /// </summary>
    [Fact]
    public void Update_MapEdgeIsSolid_PlayerCannotLeaveMap()
    {
        // A 2x2 map with a ground layer only (no collision layer).
        using var fixture = CreateFilledMapFixture(2, 2);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Start in row 1 so the fixed 1x1 box is fully inside the map, then walk right: the box
        // stops when its right edge (feet X + 0.5) reaches the map edge at x=2, so the player's
        // feet stop at exactly x = 1.5.
        engine.Player.Position = new Position(0.5, 1.5);
        engine.Input(Key.D, true);
        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(1.5, engine.Player.Position.X, precision: 9);
        Assert.Equal(1.5, engine.Player.Position.Y, precision: 9);
        Assert.True(engine.Player.Position.X + 0.5 <= 2.0 + 1e-9);

        // Then walk down: the same rule clamps the feet to exactly y = 2.0 (the box's bottom
        // edge, which is the feet, at the map edge y=2).
        engine.Input(Key.D, false);
        engine.Input(Key.S, true);
        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(1.5, engine.Player.Position.X, precision: 9);
        Assert.Equal(2.0, engine.Player.Position.Y, precision: 9);
        Assert.True(engine.Player.Position.Y <= 2.0 + 1e-9);

        // Finally walk up: the box's top edge (feet Y - 1.0) clamps at the top map edge y = 0,
        // so the feet stop at exactly y = 1.0.
        engine.Input(Key.S, false);
        engine.Input(Key.W, true);
        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(1.5, engine.Player.Position.X, precision: 9);
        Assert.Equal(1.0, engine.Player.Position.Y, precision: 9);
        Assert.True(engine.Player.Position.Y - 1.0 >= 0.0 - 1e-9);
    }

    // ---------------------------------------------------------------------
    // Story 56: move-by-key collisions clamp to the exact tile boundary. The
    // axis-separated resolution slides each blocked axis to the exact boundary
    // of the first solid tile (or the map edge) instead of reverting the whole
    // step, so the feet stop exactly at the solid tile's edge - matching
    // click-to-move - with no one-frame-step gap and no floating-point
    // overshoot accumulation. The collision footprint is the fixed 1×1 tile
    // lower-body box (see the engine class remarks), so the box (and therefore
    // the whole body for the default 48×48 sprite) stops exactly at the edge in
    // every direction: down at the row's top edge, up at the row's bottom edge,
    // right/left at the column's left/right edge.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies holding S (down) at 60 fps stops the feet at exactly y = 2.0 when a solid row
    /// starts at y = 2, instead of one frame-step short (~1.9667) as with the old
    /// revert-the-whole-step rule.
    /// </summary>
    [Fact]
    public void Update_KeyMovementDown_ClampsFeetToExactSolidRowBoundary()
    {
        // 6x6 map: a "walls" collision layer with a solid row at y=2 for every column.
        var gids = new uint[36];
        for (var x = 0; x < 6; x++)
        {
            gids[(2 * 6) + x] = 1;
        }

        using var fixture = CreateCollisionMapFixture(6, 6, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        engine.Player.Position = new Position(2.0, 1.5);
        engine.Input(Key.S, true);

        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        // The feet stop at exactly the solid row's top edge (y = 2.0), never one step short.
        Assert.Equal(2.0, engine.Player.Position.Y, precision: 9);
        Assert.Equal(2.0, engine.Player.Position.X, precision: 9);
        Assert.True(engine.Player.Position.Y <= 2.0 + 1e-9, "The footprint must never overlap the solid row.");
    }

    /// <summary>
    /// Verifies holding D (right) stops the feet at exactly x = 1.5 when a solid column starts
    /// at x = 2 (the right edge of the fixed 1×1 tile box touches the column's left edge).
    /// </summary>
    [Fact]
    public void Update_KeyMovementRight_ClampsFeetToExactSolidColumnBoundary()
    {
        // 6x6 map: a "walls" collision layer with a solid column at x=2 for every row.
        var gids = new uint[36];
        for (var y = 0; y < 6; y++)
        {
            gids[(y * 6) + 2] = 1;
        }

        using var fixture = CreateCollisionMapFixture(6, 6, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        engine.Player.Position = new Position(0.5, 2.0);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(1.5, engine.Player.Position.X, precision: 9);
        Assert.Equal(2.0, engine.Player.Position.Y, precision: 9);
        Assert.True(engine.Player.Position.X + 0.5 <= 2.0 + 1e-9, "The footprint must never overlap the solid column.");
    }

    /// <summary>
    /// Verifies holding A (left) approaching the solid column [2,3) from the right stops the
    /// feet at exactly x = 3.5: the left edge of the fixed 1×1 tile box touches the column's
    /// right edge.
    /// </summary>
    [Fact]
    public void Update_KeyMovementLeft_ClampsFeetToExactSolidColumnBoundary()
    {
        // 6x6 map: a "walls" collision layer with a solid column at x=2 for every row.
        var gids = new uint[36];
        for (var y = 0; y < 6; y++)
        {
            gids[(y * 6) + 2] = 1;
        }

        using var fixture = CreateCollisionMapFixture(6, 6, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        engine.Player.Position = new Position(5.5, 2.0);
        engine.Input(Key.A, true);

        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(3.5, engine.Player.Position.X, precision: 9);
        Assert.Equal(2.0, engine.Player.Position.Y, precision: 9);
        Assert.True(engine.Player.Position.X - 0.5 >= 3.0 - 1e-9, "The footprint must never overlap the solid column.");
    }

    /// <summary>
    /// Verifies holding W (up) approaching the solid row [2,3) from below stops the feet at
    /// exactly y = 4.0: the top edge of the fixed 1×1 tile box touches the row's bottom edge
    /// (y = 3.0), so the whole body stops below the ceiling with no head overlap.
    /// </summary>
    [Fact]
    public void Update_KeyMovementUp_ClampsFeetToExactSolidRowBoundary()
    {
        // 6x6 map: a "walls" collision layer with a solid row at y=2 for every column.
        var gids = new uint[36];
        for (var x = 0; x < 6; x++)
        {
            gids[(2 * 6) + x] = 1;
        }

        using var fixture = CreateCollisionMapFixture(6, 6, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        engine.Player.Position = new Position(2.0, 5.5);
        engine.Input(Key.W, true);

        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(4.0, engine.Player.Position.Y, precision: 9);
        Assert.Equal(2.0, engine.Player.Position.X, precision: 9);
        Assert.True(engine.Player.Position.Y - 1.0 >= 3.0 - 1e-9, "The footprint must never overlap the solid row.");
    }

    /// <summary>
    /// Verifies a single large step (a displacement of several tiles toward a solid tile) stops
    /// the feet exactly at the boundary: the clamp (not the revert) applies, so there is no
    /// tunneling through the wall and no one-step shortfall.
    /// </summary>
    [Fact]
    public void Update_KeyMovementLargeStep_ClampsFeetToExactBoundaryWithoutTunneling()
    {
        // 6x6 map: a "walls" collision layer with a solid column at x=3 for every row.
        var gids = new uint[36];
        for (var y = 0; y < 6; y++)
        {
            gids[(y * 6) + 3] = 1;
        }

        using var fixture = CreateCollisionMapFixture(6, 6, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // A single step of 4 tiles (dt = 2.0 at 2 tiles/s) toward the wall at x = 3: the feet
        // clamp to exactly x = 2.5 (the right edge of the fixed 1×1 tile box at the wall's left
        // edge).
        engine.Player.Position = new Position(0.5, 1.0);
        engine.Input(Key.D, true);
        engine.Update(dt: 2.0);

        Assert.Equal(2.5, engine.Player.Position.X, precision: 9);
        Assert.Equal(1.0, engine.Player.Position.Y, precision: 9);
        Assert.True(engine.Player.Position.X + 0.5 <= 3.0 + 1e-9, "The footprint must never overlap the solid column.");
    }

    /// <summary>
    /// Verifies key movement walks all the way down a 1-tile-wide corridor: the fixed 1×1 tile
    /// lower-body box is exactly 1 tile wide, so a player centred in the corridor (feet x = 1.5,
    /// box x in [1.0, 2.0]) never touches the side walls and reaches the far end. This is the
    /// scenario the fixed box fixes: a footprint wider than 1 tile (derived from a larger
    /// spritesheet) was stopped before the entrance.
    /// </summary>
    [Fact]
    public void Update_KeyMovementDownThroughOneTileCorridor_IsNotBlocked()
    {
        // 3x4 map: solid columns 0 and 2 for rows 1..3 (the corridor walls), open column 1.
        var gids = new uint[3 * 4];
        for (var row = 1; row <= 3; row++)
        {
            gids[(row * 3) + 0] = 1;
            gids[(row * 3) + 2] = 1;
        }

        using var fixture = CreateCollisionMapFixture(3, 4, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Centred in the corridor entrance (feet x = 1.5, row 0 open): the 1x1 box spans exactly
        // column 1, so holding S walks straight down to the bottom map edge (y = 4.0).
        engine.Player.Position = new Position(1.5, 0.5);
        engine.Input(Key.S, true);
        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(1.5, engine.Player.Position.X, precision: 9);
        Assert.Equal(4.0, engine.Player.Position.Y, precision: 9);
    }

    /// <summary>
    /// Verifies a player with a <em>larger</em> rendered sprite (78×108 cells) can still walk
    /// through a 1-tile-wide corridor: the collision box is the fixed 1×1 tile lower-body box,
    /// independent of the spritesheet's cell size. Before this fix the footprint was derived from
    /// the sprite width (1.625 tiles for 78 px), so the player was stopped before the entrance.
    /// </summary>
    [Fact]
    public void Update_KeyMovementDownThroughOneTileCorridor_LargeSprite_IsNotBlocked()
    {
        // 3x4 map: solid columns 0 and 2 for rows 1..3, open column 1.
        var gids = new uint[3 * 4];
        for (var row = 1; row <= 3; row++)
        {
            gids[(row * 3) + 0] = 1;
            gids[(row * 3) + 2] = 1;
        }

        using var fixture = CreateCollisionMapFixture(3, 4, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        using (var stream = CharacterTestHelper.CreateSheetStream(seed: 1, width: 936, height: 864))
        {
            engine.LoadSpriteSheet("hero", stream);
        }
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        engine.Player.Position = new Position(1.5, 0.5);
        engine.Input(Key.S, true);
        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        // The 78x108 sprite renders larger but collides with the fixed 1x1 box, so the corridor
        // still fits: the feet reach the bottom map edge at exactly y = 4.0.
        Assert.Equal(1.5, engine.Player.Position.X, precision: 9);
        Assert.Equal(4.0, engine.Player.Position.Y, precision: 9);
    }

    /// <summary>
    /// Verifies the auto-walk never crosses a solid corner when the player is not tile-centred:
    /// from a key-movement boundary position beside a wall, clicking a tile whose direct path
    /// would cross the wall's corner cancels the walk instead of moving the player through (or
    /// into) the solid tile, so the player is never left at an illegal footprint position.
    /// </summary>
    [Fact]
    public void Click_FromWallBoundary_NearSolidCorner_CancelsInsteadOfCrossingWall()
    {
        // 7x5 map: a "walls" collision layer with a solid column at x=3 for rows 0..2 (wall),
        // leaving a gap at the bottom (rows 3-4) so a path to the right exists but must detour.
        var gids = new uint[35];
        gids[(0 * 7) + 3] = 1;
        gids[(1 * 7) + 3] = 1;
        gids[(2 * 7) + 3] = 1;

        using var fixture = CreateCollisionMapFixture(7, 5, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // The player stands at the wall's left boundary, below the wall's bottom corner:
        // (2.5, 3.0). This is a legal position reachable by keys (walk right into the wall, then
        // down). From here the direct displacement toward waypoint (3,3) centre (3.5, 3.5) would
        // cross the solid corner at (3,2), so the auto-walk must cancel rather than cross it.
        engine.Player.Position = new Position(2.5, 3.0);

        const int canvas = 336; // 7 tiles x 48 px
        ClickOnTile(engine, 5, 3, canvas, canvas);
        Assert.NotEmpty(engine.AutoWalkPath);

        var before = engine.Player.Position;
        var illegalFrames = 0;
        for (var frame = 0; frame < 600; frame++)
        {
            engine.Update(FrameDt);
            var p = engine.Player.Position;
            if (engine.Map!.IsAreaSolid(p.X - 0.5, p.Y - 1.0, 1.0, 1.0))
            {
                illegalFrames++;
            }

            if (engine.AutoWalkPath.Count == 0)
            {
                break;
            }
        }

        // The auto-walk must never place the player on an illegal footprint, and because the
        // direct path is blocked the walk is cancelled without moving the player.
        Assert.Equal(0, illegalFrames);
        Assert.Equal(before, engine.Player.Position);
        Assert.Empty(engine.AutoWalkPath);
    }

    /// <summary>
    /// Verifies key movement from a position whose footprint already overlaps a solid tile (e.g.
    /// left there by an external teleport/click) never moves the player through the wall: the
    /// displacement is refused instead of tunnelling to the other side.
    /// </summary>
    [Fact]
    public void Update_KeyMovementFromIllegalPosition_DoesNotMoveThroughWall()
    {
        // 6x6 map: a "walls" collision layer with a solid row at y=2 for every column.
        var gids = new uint[36];
        for (var x = 0; x < 6; x++)
        {
            gids[(2 * 6) + x] = 1;
        }

        using var fixture = CreateCollisionMapFixture(6, 6, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Place the player at an illegal position: feet at y=3.49, so the fixed 1x1 box
        // [2.49, 3.49] already overlaps the solid row at y=2 (its top edge is inside the wall).
        engine.Player.Position = new Position(2.0, 3.49);
        Assert.True(engine.Map!.IsAreaSolid(2.0 - 0.5, 3.49 - 1.0, 1.0, 1.0), "sanity: the start is illegal");

        // Hold W (up, deeper into the wall): the move must be refused, never tunnelling through.
        engine.Input(Key.W, true);
        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player must stay at (or be pushed back to) a position whose top edge never passes
        // above the wall's bottom edge (y=3.0).
        Assert.True(engine.Player.Position.Y >= 3.0 - 1e-9, $"The player tunnelled through the wall: Y={engine.Player.Position.Y}");
    }

    /// <summary>
    /// Verifies key movement from an illegal position can escape <em>away</em> from the wall
    /// (moving down clears the overlapping footprint), so an embedded player is never stuck. The
    /// escape displacement must clear the whole overlap in one step: the fixed 1×1 box at
    /// y = 3.49 overlaps the solid row [2,3) by 0.49 tiles, so a single large step down (rather
    /// than many small ones, which stay illegal and are refused) is required.
    /// </summary>
    [Fact]
    public void Update_KeyMovementFromIllegalPosition_CanEscapeAwayFromWall()
    {
        // 6x6 map: a "walls" collision layer with a solid row at y=2 for every column.
        var gids = new uint[36];
        for (var x = 0; x < 6; x++)
        {
            gids[(2 * 6) + x] = 1;
        }

        using var fixture = CreateCollisionMapFixture(6, 6, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        engine.Player.Position = new Position(2.0, 3.49);
        engine.Input(Key.S, true); // move down, away from the solid row above

        // A single 0.6 s step (1.2 tiles) fully clears the 0.49-tile overlap in one displacement;
        // the resolver then returns a legal position.
        engine.Update(dt: 0.6);

        Assert.True(engine.Player.Position.Y > 3.49, $"The player did not escape: Y={engine.Player.Position.Y}");
        Assert.False(
            engine.Map!.IsAreaSolid(engine.Player.Position.X - 0.5, engine.Player.Position.Y - 1.0, 1.0, 1.0),
            "The footprint must be legal after escaping.");
    }
}
