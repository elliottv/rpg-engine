using RPGEngine.Sprites;
using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Partial <see cref="GameEngineTests"/> containing the minimap rendering tests. Splitting the large engine
/// test class into one file per functional area keeps the test files manageable; the shared
/// helpers (map fixtures, sprite configuration, rendering) live in the core
/// <c>GameEngineTests</c> partial file and are reused here.
/// </summary>
public partial class GameEngineTests
{
    // ---------------------------------------------------------------------
    // Story 36: minimap rendering. RenderMinimap draws the map's prerendered
    // tile layers (both below- and above-player layers, in file order), a
    // green dot for the player and a yellow dot for each NPC, onto a canvas
    // separate from the main game canvas. zoomLevel 1.0 fits the whole map
    // centered with the aspect preserved; > 1 zooms in around the player's
    // dot with the same edge clamp as the main camera; when a map is set the
    // canvas is cleared to black first, so the unused margins are black. The
    // method is pure (it never mutates engine state), a null map is a no-op,
    // and zoomLevel <= 0 throws ArgumentOutOfRangeException.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies the default zoom fits the whole (non-square) map into the canvas, centered with
    /// the aspect ratio preserved, leaves the unused margins black (the minimap clears its
    /// canvas to black when a map is set), and shows every distinct tile color of a two-color
    /// map plus both dots at their scaled positions.
    /// </summary>
    [Fact]
    public void RenderMinimap_DefaultZoom_FitsWholeMapCenteredWithBlankMargins()
    {
        // A 4x2 map (192x96 px): red tiles on the left half, blue on the right half, so the map
        // is non-square and the two tile colors are distinguishable.
        using var fixture = new TiledTestFixture(
            4, 2,
            new[] { new TileLayerSpec("ground", new uint[]
            {
                1, 1, 2, 2,
                1, 1, 2, 2,
            }) },
            tileColors: new[] { SKColors.Red, SKColors.Blue });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // Player near the top-left, NPC near the bottom-right, both away from the sampled tiles.
        engine.Player.Position = new Position(0.5, 0.5);
        engine.Characters.Add(new Character { Position = new Position(3.5, 1.5) });

        const int canvasWidth = 400;
        const int canvasHeight = 300;
        using var bitmap = RenderMinimap(engine, canvasWidth, canvasHeight, zoomLevel: 1.0);

        // Aspect-preserving fit: baseFit = min(400/192, 300/96) = 25/12, so the map is scaled to
        // exactly 400x200 and centered with 50 px margins top and bottom (it fills the width).
        const double scale = 25.0 / 12;
        Assert.Equal(192 * scale, 400, precision: 6);
        Assert.Equal(96 * scale, 200, precision: 6);

        // Both distinct tile colors are visible at their scaled centers, away from the dots.
        // Red tile (1,0) center (72,24) px -> screen (150,100); blue tile (2,0) center (120,24)
        // px -> screen (250,100).
        Assert.Equal(SKColors.Red, bitmap.GetPixel(150, 100));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(250, 100));

        // The dots are drawn at the scaled world positions: player (0.5,0.5) -> map px (24,24)
        // -> screen (50,100) green; NPC (3.5,1.5) -> map px (168,72) -> screen (350,200) yellow.
        Assert.Equal(SKColors.Green, bitmap.GetPixel(50, 100));
        Assert.Equal(SKColors.Yellow, bitmap.GetPixel(350, 200));

        // The map is centered vertically with ~50 px margins above and below (it fills the 400 px
        // width), so it is not stretched to fill the 300 px canvas. Sample well inside each
        // region to avoid the rasterizer's sub-pixel edge rows.
        Assert.Equal(SKColors.Black, bitmap.GetPixel(200, 20));   // top margin black
        Assert.Equal(SKColors.Black, bitmap.GetPixel(200, 40));   // top margin black
        Assert.NotEqual(0, bitmap.GetPixel(200, 60).Alpha);  // map interior (top half)
        Assert.NotEqual(0, bitmap.GetPixel(200, 240).Alpha); // map interior (bottom half)
        Assert.Equal(SKColors.Black, bitmap.GetPixel(200, 260));  // bottom margin black
        Assert.Equal(SKColors.Black, bitmap.GetPixel(200, 280));  // bottom margin black
    }

    /// <summary>
    /// Verifies the player dot (green) and NPC dots (yellow) are drawn as small filled circles
    /// at the scaled world positions, and that with no NPCs only the green dot is drawn.
    /// </summary>
    [Fact]
    public void RenderMinimap_Dots_AtScaledPositions()
    {
        using var fixture = CreateFilledMapFixture(2, 2); // 96x96 red map
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // A 200x200 canvas: baseFit = min(200/96, 200/96) = 25/12, so the 96x96 map is scaled to
        // exactly 200x200 and fills the canvas (origin 0,0).
        engine.Player.Position = new Position(0.5, 0.5);   // map px (24,24) -> screen (50,50)
        var npc = new Character { Position = new Position(1.5, 1.5) }; // map px (72,72) -> screen (150,150)
        engine.Characters.Add(npc);

        using var bitmap = RenderMinimap(engine, 200, 200, zoomLevel: 1.0);
        Assert.Equal(SKColors.Green, bitmap.GetPixel(50, 50));
        Assert.Equal(SKColors.Yellow, bitmap.GetPixel(150, 150));

        // With no NPCs only the green dot is drawn.
        engine.Characters.Clear();
        using var withoutNpc = RenderMinimap(engine, 200, 200, zoomLevel: 1.0);
        Assert.Equal(SKColors.Green, withoutNpc.GetPixel(50, 50));
        Assert.Equal(0, CountColor(withoutNpc, SKColors.Yellow));
    }

    /// <summary>
    /// Verifies zoom-in shows only the sub-region around the player, and that moving the player
    /// to the top-left / bottom-right corners clamps the view so the map edge is shown (never
    /// blank space) — the same clamping behavior as the main camera.
    /// </summary>
    [Fact]
    public void RenderMinimap_ZoomIn_ShowsSubRegionAndClampsToMapEdges()
    {
        // A 10x10 (480x480) map: red everywhere, with blue corner tiles at (0,0) and (9,9) so the
        // visible sub-region can be told apart from the rest of the map.
        var gids = Enumerable.Repeat(1u, 100).ToArray();
        gids[0] = 2;   // tile (0,0) is blue
        gids[99] = 2;  // tile (9,9) is blue
        using var fixture = new TiledTestFixture(
            10, 10,
            new[] { new TileLayerSpec("ground", gids) },
            tileColors: new[] { SKColors.Red, SKColors.Blue });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        const int canvasSize = 240; // baseFit 0.5; zoom 4 -> scale 2, visible region 120x120 map px

        // Center: player at (5,5) tiles = map px (240,240); the visible region clamps to
        // (180,180)-(300,300). The blue corner tiles lie outside it, so no blue is visible and a
        // tile inside the region (e.g. tile (4,4) at screen (72,72)) is shown.
        engine.Player.Position = new Position(5, 5);
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(0, CountColor(bitmap, SKColors.Blue));     // corner tiles are outside the region
            Assert.Equal(SKColors.Red, bitmap.GetPixel(72, 72));    // a tile inside the region is visible
            Assert.Equal(SKColors.Green, bitmap.GetPixel(120, 120)); // the player dot is centered
        }

        // Top-left corner: player at (0,0); the visible region clamps to (0,0)-(120,120) so the
        // map edge is shown at the canvas edge instead of blank space — the blue corner tile (0,0)
        // is visible at the top-left of the canvas (a few pixels away from the green dot at 0,0).
        engine.Player.Position = new Position(0, 0);
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 10));
        }

        // Bottom-right corner: player at (9,9); the visible region clamps to (360,360)-(480,480)
        // so the blue corner tile (9,9) is visible at the bottom-right of the canvas.
        engine.Player.Position = new Position(9, 9);
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(239, 239));
        }
    }

    /// <summary>
    /// Verifies a dot outside the visible region is not drawn (no yellow pixels), while a dot
    /// inside the region is drawn at its scaled position.
    /// </summary>
    [Fact]
    public void RenderMinimap_DotOutsideVisibleRegion_IsNotDrawn()
    {
        using var fixture = CreateFilledMapFixture(10, 10); // 480x480 red map
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        const int canvasSize = 240; // zoom 4 -> scale 2, visible region 120x120 map px
        engine.Player.Position = new Position(5, 5); // visible region (180,180)-(300,300)

        // An NPC inside the visible region is drawn at its scaled position.
        engine.Characters.Add(new Character { Position = new Position(6, 5) }); // map px (288,240) -> screen (216,120)
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(SKColors.Yellow, bitmap.GetPixel(216, 120));
        }

        // An NPC outside the visible region is skipped entirely.
        engine.Characters.Clear();
        engine.Characters.Add(new Character { Position = new Position(1, 5) }); // map px (48,240), left of the region
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(0, CountColor(bitmap, SKColors.Yellow));
        }
    }

    /// <summary>
    /// Verifies that with no map RenderMinimap is a no-op: the canvas is left untouched.
    /// </summary>
    [Fact]
    public void RenderMinimap_NoMap_LeavesCanvasUntouched()
    {
        var engine = new GameEngine(); // no map

        using var bitmap = new SKBitmap(120, 90);
        using (var canvas = new SKCanvas(bitmap))
        {
            // Pre-fill the canvas with an arbitrary backdrop the minimap must not overwrite.
            canvas.Clear(SKColors.Orange);
            engine.RenderMinimap(canvas, zoomLevel: 1.0);
        }

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                Assert.Equal(SKColors.Orange, bitmap.GetPixel(x, y));
            }
        }
    }

    /// <summary>Verifies a zoom level of zero or negative throws ArgumentOutOfRangeException.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-0.5)]
    public void RenderMinimap_NonPositiveZoom_ThrowsArgumentOutOfRangeException(double zoomLevel)
    {
        using var fixture = CreateFilledMapFixture(2, 2);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        using var bitmap = new SKBitmap(96, 96);
        using var canvas = new SKCanvas(bitmap);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.RenderMinimap(canvas, zoomLevel));
    }

    /// <summary>Verifies a small positive zoom (zoom out) is accepted and still draws the map.</summary>
    [Fact]
    public void RenderMinimap_SmallPositiveZoom_IsAccepted()
    {
        using var fixture = CreateFilledMapFixture(2, 2);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // zoom 0.5 on a 200x200 canvas: scale = 25/24, the 96x96 map is scaled to 100x100 and
        // centered with 50 px margins; the map is still drawn (e.g. its center is a map pixel).
        using var bitmap = RenderMinimap(engine, 200, 200, zoomLevel: 0.5);
        Assert.NotEqual(0, bitmap.GetPixel(100, 100).Alpha);
        Assert.Equal(SKColors.Black, bitmap.GetPixel(0, 0)); // the margin is black
    }

    /// <summary>
    /// Verifies RenderMinimap is pure: rendering a minimap on a separate surface does not change
    /// the output of the main Render nor the engine state (regression guard for the minimap work).
    /// </summary>
    [Fact]
    public void RenderMinimap_DoesNotMutateEngineState_MainRenderUnchanged()
    {
        using var fixture = CreateFilledMapFixture(4, 4);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(1.5, 1.5);
        engine.Characters.Add(new Character { Position = new Position(2.5, 2.5) });

        var before = Render(engine, 240, 240);

        // Render a minimap on a separate surface; it must not touch the main render path or state.
        using (var minimap = RenderMinimap(engine, 120, 120, zoomLevel: 1.0))
        {
            Assert.NotEqual(0, minimap.GetPixel(60, 60).Alpha); // the minimap did draw something
        }

        var after = Render(engine, 240, 240);
        AssertBitmapsEqual(before, after);

        Assert.Equal(new Position(1.5, 1.5), engine.Player.Position);
        Assert.Single(engine.Characters);
    }
}
