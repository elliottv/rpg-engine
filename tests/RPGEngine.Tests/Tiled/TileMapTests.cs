using RPGEngine.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests.Tiled;

/// <summary>
/// Acceptance tests for <see cref="TileMap"/> and <see cref="TileSet"/>
/// (story 7: Tiled TMX/TSX loading and rendering).
/// </summary>
public class TileMapTests
{
    /// <summary>The 2×2 ground layer used by the standard fixture.</summary>
    /// <remarks>
    /// Row-major: (0,0)=GID 1, (1,0)=empty, (0,1)=GID 1 with the horizontal flip bit set,
    /// (1,1)=empty.
    /// </remarks>
    private static TileLayerSpec Ground =>
        new("ground", new uint[] { 1, 0, 0x80000001u, 0 });

    private static TileLayerSpec Decor =>
        new("decor", new uint[4]);

    // ---------------------------------------------------------------------
    // Acceptance 1: a hand-written 2×2 TMX with one tile layer and one
    // external 1-tile TSX (48×48 PNG generated in test) loads without error.
    // ---------------------------------------------------------------------
    /// <summary>Verifies that a hand-written 2×2 TMX with one tile layer and one external 1-tile TSX (48×48 PNG generated in test) loads without error.</summary>
    [Fact]
    public void Load_LoadsTmxWithExternalTsx_WithoutError()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });

        var map = TileMap.Load(fixture.MapPath);

        Assert.NotNull(map);
        Assert.Single(map.Layers);
    }

    // ---------------------------------------------------------------------
    // Acceptance 2: Width/Height/TileWidth/TileHeight reflect the TMX and
    // Layers order is preserved.
    // ---------------------------------------------------------------------
    /// <summary>Verifies that Width/Height/TileWidth/TileHeight reflect the TMX and that Layers preserves the file order.</summary>
    [Fact]
    public void Load_ExposesDimensions_AndPreservesLayerOrder()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground, Decor });

        var map = TileMap.Load(fixture.MapPath);

        Assert.Equal(2, map.Width);
        Assert.Equal(2, map.Height);
        Assert.Equal(48, map.TileWidth);
        Assert.Equal(48, map.TileHeight);
        Assert.Equal(96, map.PixelWidth);
        Assert.Equal(96, map.PixelHeight);

        Assert.Equal(2, map.Layers.Count);
        Assert.Equal("ground", map.Layers[0].Name);
        Assert.Equal("decor", map.Layers[1].Name);
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: GetTileId returns the expected GID; a GID with flip bits
    // set is returned with the bits masked and the flags reported.
    // ---------------------------------------------------------------------
    /// <summary>Verifies GetTileId returns the expected GID, that a GID with flip bits set is returned with the bits masked off, and that GetTileFlags reports the flip flags.</summary>
    [Fact]
    public void GetTileId_MasksFlipBits_AndGetTileFlagsReportsThem()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });

        var map = TileMap.Load(fixture.MapPath);

        // Plain tile: GID 1, no flags.
        Assert.Equal(1u, map.GetTileId("ground", 0, 0));
        Assert.Equal(TileFlags.None, map.GetTileFlags("ground", 0, 0));

        // Tile with the horizontal flip bit set in the raw GID: the returned
        // GID is masked back to 1 and the flag is reported separately.
        Assert.Equal(1u, map.GetTileId("ground", 0, 1));
        Assert.Equal(TileFlags.FlippedHorizontally, map.GetTileFlags("ground", 0, 1));

        // Empty cells are reported as GID 0.
        Assert.Equal(0u, map.GetTileId("ground", 1, 0));
        Assert.Equal(TileFlags.None, map.GetTileFlags("ground", 1, 0));
    }

    // ---------------------------------------------------------------------
    // Story 35: collision layers (is_collision) and solid-tile queries. A layer
    // declaring the Tiled <property name="is_collision" type="bool" value="true"/>
    // custom property is reported by TileMapLayer.IsCollision; TileMap.IsSolid
    // consults those layers (a non-empty tile is solid and the map edge is solid),
    // and the internal IsAreaSolid helper tests the tiles overlapped by a tile-unit
    // rectangle (the engine's footprint check).
    // ---------------------------------------------------------------------
    /// <summary>Verifies IsSolid returns false for every in-bounds cell when the map has no collision layer (no is_collision property).</summary>
    [Fact]
    public void IsSolid_NoCollisionLayer_ReturnsFalseForAllInBoundsCells()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });

        var map = TileMap.Load(fixture.MapPath);

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Assert.False(map.IsSolid(x, y));
            }
        }
    }

    /// <summary>Verifies TileMapLayer.IsCollision is true for a layer declaring the is_collision bool property and false for a layer without it.</summary>
    [Fact]
    public void IsCollision_ReadsCustomProperty_FromTmx()
    {
        var ground = new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1 });
        var walls = new TileLayerSpec(
            "walls",
            new uint[] { 0, 1, 0, 1 },
            Properties: new[] { new FixtureProperty("is_collision", "bool", "true") });

        using var fixture = TiledTestFixture.Create2x2(new[] { ground, walls });
        var map = TileMap.Load(fixture.MapPath);

        Assert.False(map.Layers[0].IsCollision);
        Assert.True(map.Layers[1].IsCollision);
    }

    /// <summary>Verifies a layer without the is_collision property (or with it set to false, or with a non-bool property of that name) reports IsCollision == false.</summary>
    [Fact]
    public void IsCollision_AbsentFalseOrNonBool_ReportsFalse()
    {
        var absent = new TileLayerSpec("absent", new uint[] { 1, 1, 1, 1 });
        var falseValue = new TileLayerSpec(
            "false_value",
            new uint[] { 0, 0, 0, 0 },
            Properties: new[] { new FixtureProperty("is_collision", "bool", "false") });
        var nonBool = new TileLayerSpec(
            "non_bool",
            new uint[] { 0, 0, 0, 0 },
            Properties: new[] { new FixtureProperty("is_collision", "string", "true") });

        using var fixture = TiledTestFixture.Create2x2(new[] { absent, falseValue, nonBool });
        var map = TileMap.Load(fixture.MapPath);

        Assert.False(map.Layers[0].IsCollision);
        Assert.False(map.Layers[1].IsCollision);
        Assert.False(map.Layers[2].IsCollision);
    }

    /// <summary>Verifies the new is_collision parsing does not affect the existing above_player parsing (regression).</summary>
    [Fact]
    public void IsCollision_AndAbovePlayer_ParseIndependently()
    {
        var above = new TileLayerSpec(
            "above",
            new uint[] { 0, 0, 0, 0 },
            Properties: new[]
            {
                new FixtureProperty("above_player", "bool", "true"),
                new FixtureProperty("is_collision", "bool", "false"),
            });
        var walls = new TileLayerSpec(
            "walls",
            new uint[] { 0, 1, 0, 0 },
            Properties: new[] { new FixtureProperty("is_collision", "bool", "true") });

        using var fixture = TiledTestFixture.Create2x2(new[] { above, walls });
        var map = TileMap.Load(fixture.MapPath);

        Assert.True(map.Layers[0].AbovePlayer);
        Assert.False(map.Layers[0].IsCollision);
        Assert.False(map.Layers[1].AbovePlayer);
        Assert.True(map.Layers[1].IsCollision);
    }

    /// <summary>Verifies IsSolid returns true exactly where a collision layer has a non-empty tile, and false on empty cells.</summary>
    [Fact]
    public void IsSolid_CollisionLayerTile_IsSolid_AndEmptyCellsAreWalkable()
    {
        var ground = new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1 }); // walkable
        var walls = new TileLayerSpec(
            "walls",
            new uint[] { 0, 1, 0, 1 },
            Properties: new[] { new FixtureProperty("is_collision", "bool", "true") });

        using var fixture = TiledTestFixture.Create2x2(new[] { ground, walls });
        var map = TileMap.Load(fixture.MapPath);

        // (0,0) is empty in the collision layer -> walkable even though ground draws a tile there.
        Assert.False(map.IsSolid(0, 0));
        // (1,0) has a tile in the collision layer -> solid.
        Assert.True(map.IsSolid(1, 0));
        // (0,1) is empty -> walkable.
        Assert.False(map.IsSolid(0, 1));
        // (1,1) has a tile -> solid.
        Assert.True(map.IsSolid(1, 1));
    }

    /// <summary>Verifies IsSolid ignores tiles on non-collision layers: a tile drawn from a normal layer is walkable.</summary>
    [Fact]
    public void IsSolid_NonCollisionLayer_NeverBlocks()
    {
        // All four cells have a tile on the ground layer, but none is a collision layer.
        using var fixture = TiledTestFixture.Create2x2(new[] { new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1 }) });
        var map = TileMap.Load(fixture.MapPath);

        Assert.False(map.IsSolid(0, 0));
        Assert.False(map.IsSolid(1, 0));
        Assert.False(map.IsSolid(0, 1));
        Assert.False(map.IsSolid(1, 1));
    }

    /// <summary>Verifies IsSolid returns true for out-of-bounds coordinates (negative and >= map size): the map edge is solid.</summary>
    [Fact]
    public void IsSolid_OutOfBounds_ReturnsTrue()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var map = TileMap.Load(fixture.MapPath);

        Assert.True(map.IsSolid(-1, 0));
        Assert.True(map.IsSolid(0, -1));
        Assert.True(map.IsSolid(2, 0));
        Assert.True(map.IsSolid(0, 2));
        Assert.True(map.IsSolid(-1, -1));
        Assert.True(map.IsSolid(2, 2));
    }

    /// <summary>Verifies IsAreaSolid reports solid when the rectangle overlaps a collision-layer tile, using the floored bounds.</summary>
    [Fact]
    public void IsAreaSolid_OverlappingSolidTile_ReturnsTrue()
    {
        // Collision layer with a single solid tile at (1,1).
        var walls = new TileLayerSpec(
            "walls",
            new uint[] { 0, 0, 0, 1 },
            Properties: new[] { new FixtureProperty("is_collision", "bool", "true") });

        using var fixture = TiledTestFixture.Create2x2(new[] { walls });
        var map = TileMap.Load(fixture.MapPath);

        // A rectangle inside the solid cell is solid.
        Assert.True(map.IsAreaSolid(1.2, 1.2, 0.5, 0.5));

        // A rectangle fully inside a walkable cell is not.
        Assert.False(map.IsAreaSolid(0.2, 0.2, 0.5, 0.5));

        // A rectangle that ends exactly on the boundary of the solid cell does not include it;
        // once it spills past the boundary (even slightly) it becomes solid.
        Assert.False(map.IsAreaSolid(0.0, 1.0, 1.0, 0.5));
        Assert.True(map.IsAreaSolid(0.0, 1.0, 1.0001, 0.5));
    }

    /// <summary>Verifies IsAreaSolid returns false for an in-bounds rectangle when the map has no collision layers.</summary>
    [Fact]
    public void IsAreaSolid_NoCollisionLayer_ReturnsFalse()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var map = TileMap.Load(fixture.MapPath);

        Assert.False(map.IsAreaSolid(0, 0, 2, 2));
        Assert.False(map.IsAreaSolid(0.25, 0.25, 1.5, 1.5));
    }

    /// <summary>Verifies IsAreaSolid treats a rectangle that extends outside the map as solid (the map-edge rule).</summary>
    [Fact]
    public void IsAreaSolid_OutsideMap_ReturnsTrue()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var map = TileMap.Load(fixture.MapPath);

        Assert.True(map.IsAreaSolid(-1, 0, 1, 1));  // starts left of the map
        Assert.True(map.IsAreaSolid(1.5, 0, 1, 1)); // extends past the right edge
        Assert.True(map.IsAreaSolid(0, 1.5, 1, 1)); // extends past the bottom edge
        Assert.True(map.IsAreaSolid(1.5, 1.5, 1, 1)); // extends past both edges
    }

    // ---------------------------------------------------------------------
    // Acceptance 5: rendering produces non-transparent pixels in the expected
    // region and transparent pixels outside the map bounds.
    // ---------------------------------------------------------------------
    /// <summary>Verifies rendering to an offscreen bitmap produces non-transparent pixels in the expected regions and transparent pixels outside the map bounds.</summary>
    [Fact]
    public void Draw_RendersTilePixels_AndLeavesOutsideMapTransparent()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var map = TileMap.Load(fixture.MapPath);

        const int renderSize = 144; // 3×3 tiles: larger than the 96×96 map.
        using var bitmap = new SKBitmap(renderSize, renderSize);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            map.Draw(canvas, new SKRect(0, 0, renderSize, renderSize));
        }

        // The tile at (0,0) is solid red → non-transparent inside its cell.
        Assert.NotEqual(0, bitmap.GetPixel(24, 24).Alpha);

        // The tile at (0,1) is also red (flipped, but the solid tile is symmetric).
        Assert.NotEqual(0, bitmap.GetPixel(24, 72).Alpha);

        // The cell at (1,0) is empty → transparent.
        Assert.Equal(0, bitmap.GetPixel(72, 24).Alpha);

        // Pixels beyond the 96×96 map bounds are transparent.
        Assert.Equal(0, bitmap.GetPixel(120, 24).Alpha);
        Assert.Equal(0, bitmap.GetPixel(24, 120).Alpha);
        Assert.Equal(0, bitmap.GetPixel(120, 120).Alpha);
    }

    // ---------------------------------------------------------------------
    // Flip rendering correctness (spec: "Implement flip rendering for
    // correctness, it is cheap"). Uses the asymmetric marker tile.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the horizontal flip transform is applied when rendering a tile whose GID carries the horizontal flip flag.</summary>
    [Fact]
    public void Draw_AppliesHorizontalFlip()
    {
        // Marker tile (marker at source 36..39, 12..15) flipped horizontally.
        using var fixture = new TiledTestFixture(1, 1, new[] { new TileLayerSpec("ground", new uint[] { 0x80000001u }) }, TilePattern.Marker);
        var map = TileMap.Load(fixture.MapPath);

        using var bitmap = RenderMap(map);

        // The marker moves to the mirrored x position (8..11) at the same y (12..15).
        Assert.NotEqual(0, bitmap.GetPixel(9, 13).Alpha);
        Assert.Equal(0, bitmap.GetPixel(37, 13).Alpha);
    }

    /// <summary>Verifies the vertical flip transform is applied when rendering a tile whose GID carries the vertical flip flag.</summary>
    [Fact]
    public void Draw_AppliesVerticalFlip()
    {
        using var fixture = new TiledTestFixture(1, 1, new[] { new TileLayerSpec("ground", new uint[] { 0x40000001u }) }, TilePattern.Marker);
        var map = TileMap.Load(fixture.MapPath);

        using var bitmap = RenderMap(map);

        // The marker moves to the mirrored y position (32..35) at the same x (36..39).
        Assert.NotEqual(0, bitmap.GetPixel(37, 33).Alpha);
        Assert.Equal(0, bitmap.GetPixel(37, 13).Alpha);
    }

    /// <summary>Verifies the diagonal flip transform is applied when rendering a tile whose GID carries the diagonal flip flag.</summary>
    [Fact]
    public void Draw_AppliesDiagonalFlip()
    {
        // Diagonal flip is an x/y axis swap, so the marker at (36..39, 12..15)
        // lands at (12..15, 36..39).
        using var fixture = new TiledTestFixture(1, 1, new[] { new TileLayerSpec("ground", new uint[] { 0x20000001u }) }, TilePattern.Marker);
        var map = TileMap.Load(fixture.MapPath);

        using var bitmap = RenderMap(map);

        Assert.NotEqual(0, bitmap.GetPixel(13, 37).Alpha);
        Assert.Equal(0, bitmap.GetPixel(37, 13).Alpha);
    }

    // ---------------------------------------------------------------------
    // Above-player layers (story 24): a layer declaring the Tiled
    // <property name="above_player" type="bool" value="true"/> custom property is
    // reported by TileMapLayer.AbovePlayer and rendered by DrawAbovePlayer (after the
    // player); every other layer is rendered by Draw (below the player).
    // ---------------------------------------------------------------------
    /// <summary>Verifies TileMapLayer.AbovePlayer is true for a layer declaring the above_player bool property and false for a layer without it.</summary>
    [Fact]
    public void AbovePlayer_ReadsCustomProperty_FromTmx()
    {
        var ground = new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1 });
        var above = new TileLayerSpec(
            "above",
            new uint[] { 0, 0, 0, 0 },
            Properties: new[] { new FixtureProperty("above_player", "bool", "true") });

        using var fixture = TiledTestFixture.Create2x2(new[] { ground, above });
        var map = TileMap.Load(fixture.MapPath);

        Assert.False(map.Layers[0].AbovePlayer);
        Assert.True(map.Layers[1].AbovePlayer);
    }

    /// <summary>Verifies a layer without the property (or with it set to false, or with a non-bool property of that name) reports AbovePlayer == false.</summary>
    [Fact]
    public void AbovePlayer_AbsentFalseOrNonBool_ReportsFalse()
    {
        var absent = new TileLayerSpec("absent", new uint[] { 1, 1, 1, 1 });
        var falseValue = new TileLayerSpec(
            "false_value",
            new uint[] { 0, 0, 0, 0 },
            Properties: new[] { new FixtureProperty("above_player", "bool", "false") });
        var nonBool = new TileLayerSpec(
            "non_bool",
            new uint[] { 0, 0, 0, 0 },
            Properties: new[] { new FixtureProperty("above_player", "string", "true") });

        using var fixture = TiledTestFixture.Create2x2(new[] { absent, falseValue, nonBool });
        var map = TileMap.Load(fixture.MapPath);

        Assert.False(map.Layers[0].AbovePlayer);
        Assert.False(map.Layers[1].AbovePlayer);
        Assert.False(map.Layers[2].AbovePlayer);
    }

    /// <summary>Verifies Draw renders only the below-player layer and DrawAbovePlayer only the above-player layer, at pixel level with two distinct tile colors.</summary>
    [Fact]
    public void Draw_And_DrawAbovePlayer_RenderSeparatePasses()
    {
        var ground = new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1 }); // all red
        var above = new TileLayerSpec(
            "above",
            new uint[] { 2, 2, 2, 2 }, // all green
            Properties: new[] { new FixtureProperty("above_player", "bool", "true") });

        using var fixture = new TiledTestFixture(
            2,
            2,
            new[] { ground, above },
            tileColors: new[] { SKColors.Red, SKColors.Green });
        var map = TileMap.Load(fixture.MapPath);

        Assert.False(map.Layers[0].AbovePlayer);
        Assert.True(map.Layers[1].AbovePlayer);

        // The below-player pass draws only the ground (red) layer.
        using (var bitmap = RenderMap(map, draw: map.Draw))
        {
            Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(24, 24));
            Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(72, 72));
        }

        // The above-player pass draws only the above (green) layer.
        using (var bitmap = RenderMap(map, draw: map.DrawAbovePlayer))
        {
            Assert.Equal(new SKColor(0, 128, 0, 255), bitmap.GetPixel(24, 24));
            Assert.Equal(new SKColor(0, 128, 0, 255), bitmap.GetPixel(72, 72));
        }
    }

    // ---------------------------------------------------------------------
    // Map custom properties, object layers and layer properties (story 25).
    // ---------------------------------------------------------------------
    /// <summary>Verifies a map's custom properties are exposed with their name, type and typed value, and that GetProperty performs a case-sensitive lookup.</summary>
    [Fact]
    public void MapProperties_ExposeTypedValues_AndGetPropertyLooksUpCaseSensitively()
    {
        var mapProperties = new[]
        {
            new FixtureProperty("flag", "bool", "true"),
            new FixtureProperty("count", "int", "42"),
            new FixtureProperty("ratio", "float", "1.5"),
            new FixtureProperty("name", "string", "hello"),
            new FixtureProperty("tint", "color", "#ff0000"),
            new FixtureProperty("data", "file", "data.txt"),
        };

        using var fixture = TiledTestFixture.Create2x2(new[] { Ground }, mapProperties: mapProperties);
        var map = TileMap.Load(fixture.MapPath);

        // Properties are exposed in file order with the correct type and typed value.
        Assert.Equal(6, map.Properties.Count);
        AssertMapProperty(map.Properties[0], "flag", MapPropertyType.Bool, true);
        AssertMapProperty(map.Properties[1], "count", MapPropertyType.Int, 42);
        AssertMapProperty(map.Properties[2], "ratio", MapPropertyType.Float, 1.5f);
        AssertMapProperty(map.Properties[3], "name", MapPropertyType.String, "hello");
        Assert.Equal(MapPropertyType.Color, map.Properties[4].Type);
        Assert.Equal(new SKColor(255, 0, 0, 255), Assert.IsType<SKColor>(map.Properties[4].Value));
        AssertMapProperty(map.Properties[5], "data", MapPropertyType.File, "data.txt");

        // GetProperty returns the value by exact (case-sensitive) name.
        Assert.Equal(true, map.GetProperty("flag")!.Value);
        Assert.Equal(42, map.GetProperty("count")!.Value);
        Assert.Equal("hello", map.GetProperty("name")!.Value);
        Assert.Null(map.GetProperty("Flag"));
        Assert.Null(map.GetProperty("missing"));
    }

    /// <summary>Verifies an object layer exposes its objects (identity, geometry, shape) and the custom properties of both the layer and each object.</summary>
    [Fact]
    public void ObjectLayers_ExposeObjects_AndTheirProperties()
    {
        var objects = new[]
        {
            new ObjectSpec(
                Id: 1,
                Name: "player",
                Type: "hero",
                X: 10,
                Y: 20,
                Width: 32,
                Height: 48,
                Shape: FixtureObjectShape.Rectangle,
                Properties: new[]
                {
                    new FixtureProperty("hp", "int", "100"),
                    new FixtureProperty("alive", "bool", "true"),
                    new FixtureProperty("label", "string", "player"),
                }),
            new ObjectSpec(
                Id: 2,
                Name: "door",
                Type: "prop",
                X: 100,
                Y: 200,
                Width: 48,
                Height: 48,
                Shape: FixtureObjectShape.Point),
        };
        var objectLayer = new ObjectLayerSpec(
            "objects",
            objects,
            Visible: true,
            Opacity: 0.75f,
            Properties: new[] { new FixtureProperty("owner", "string", "level1") });

        using var fixture = TiledTestFixture.Create2x2(new[] { Ground }, objectLayers: new[] { objectLayer });
        var map = TileMap.Load(fixture.MapPath);

        Assert.Single(map.ObjectLayers);
        var layer = map.ObjectLayers[0];
        Assert.Equal("objects", layer.Name);
        Assert.True(layer.Visible);
        Assert.Equal(0.75f, layer.Opacity);
        Assert.Equal(2, layer.Objects.Count);

        // The object layer's own custom properties are exposed.
        Assert.Equal("owner", layer.Properties[0].Name);
        Assert.Equal("level1", layer.Properties[0].Value);

        // The first object exposes identity, geometry, shape and custom properties.
        var player = layer.Objects[0];
        Assert.Equal(1u, player.Id);
        Assert.Equal("player", player.Name);
        Assert.Equal("hero", player.Type);
        Assert.Equal(new Position(10, 20), player.Position);
        Assert.Equal(32f, player.Width);
        Assert.Equal(48f, player.Height);
        Assert.Equal(TileMapObjectShape.Rectangle, player.Shape);
        Assert.Equal(3, player.Properties.Count);
        Assert.Equal(100, player.Properties.Single(p => p.Name == "hp").Value);
        Assert.Equal(true, player.Properties.Single(p => p.Name == "alive").Value);
        Assert.Equal("player", player.Properties.Single(p => p.Name == "label").Value);

        // The second object exposes its own identity and geometry.
        var door = layer.Objects[1];
        Assert.Equal(2u, door.Id);
        Assert.Equal("door", door.Name);
        Assert.Equal(new Position(100, 200), door.Position);
        Assert.Equal(TileMapObjectShape.Point, door.Shape);
        Assert.Empty(door.Properties);
    }

    /// <summary>Verifies every object shape is detected from the Tiled object subtype (a plain object is a rectangle; markers/gid/text produce the others).</summary>
    [Fact]
    public void ObjectLayers_DetectObjectShapes()
    {
        var objects = new[]
        {
            new ObjectSpec(1, "rect", "x", 0, 0, 8, 9, FixtureObjectShape.Rectangle),
            new ObjectSpec(2, "ellipse", "x", 0, 0, 6, 7, FixtureObjectShape.Ellipse),
            new ObjectSpec(3, "point", "x", 0, 0, 0, 0, FixtureObjectShape.Point),
            new ObjectSpec(4, "polygon", "x", 0, 0, 0, 0, FixtureObjectShape.Polygon),
            new ObjectSpec(5, "polyline", "x", 0, 0, 0, 0, FixtureObjectShape.Polyline),
            new ObjectSpec(6, "tile", "x", 0, 0, 0, 0, FixtureObjectShape.Tile),
            new ObjectSpec(7, "text", "x", 0, 0, 50, 20, FixtureObjectShape.Text),
        };

        using var fixture = TiledTestFixture.Create2x2(
            new[] { Ground },
            objectLayers: new[] { new ObjectLayerSpec("objects", objects) });
        var map = TileMap.Load(fixture.MapPath);

        Assert.Equal(
            new[]
            {
                TileMapObjectShape.Rectangle,
                TileMapObjectShape.Ellipse,
                TileMapObjectShape.Point,
                TileMapObjectShape.Polygon,
                TileMapObjectShape.Polyline,
                TileMapObjectShape.Tile,
                TileMapObjectShape.Text,
            },
            map.ObjectLayers[0].Objects.Select(o => o.Shape));
    }

    /// <summary>Verifies Layers still contains only tile layers when the file also declares object layers (regression).</summary>
    [Fact]
    public void Layers_ContainsOnlyTileLayers_NotObjectLayers()
    {
        var objectLayer = new ObjectLayerSpec(
            "objects",
            new[] { new ObjectSpec(1, "o", "x", 0, 0, 10, 10, FixtureObjectShape.Point) });

        using var fixture = TiledTestFixture.Create2x2(new[] { Ground, Decor }, objectLayers: new[] { objectLayer });
        var map = TileMap.Load(fixture.MapPath);

        // Object layers must never be mixed into Layers; they are exposed via ObjectLayers.
        Assert.Equal(2, map.Layers.Count);
        Assert.Equal(new[] { "ground", "decor" }, map.Layers.Select(l => l.Name));
        Assert.Single(map.ObjectLayers);
        Assert.Equal("objects", map.ObjectLayers[0].Name);
    }

    /// <summary>Verifies maps without custom properties or object layers load with empty lists (regression).</summary>
    [Fact]
    public void Maps_WithoutPropertiesOrObjectLayers_LoadWithEmptyLists()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var map = TileMap.Load(fixture.MapPath);

        Assert.Empty(map.Properties);
        Assert.Empty(map.ObjectLayers);
        Assert.Null(map.GetProperty("anything"));
    }

    /// <summary>Verifies a tile layer exposes its custom properties, including the above_player flag that also drives the dedicated AbovePlayer member.</summary>
    [Fact]
    public void TileMapLayer_ExposesCustomProperties()
    {
        var ground = new TileLayerSpec(
            "ground",
            new uint[] { 1, 1, 1, 1 },
            Properties: new[]
            {
                new FixtureProperty("above_player", "bool", "true"),
                new FixtureProperty("owner", "string", "me"),
            });

        using var fixture = TiledTestFixture.Create2x2(new[] { ground });
        var map = TileMap.Load(fixture.MapPath);

        var layer = map.Layers[0];
        Assert.Equal(2, layer.Properties.Count);
        Assert.True(layer.AbovePlayer); // the dedicated flag still comes from the property
        AssertMapProperty(layer.Properties[0], "above_player", MapPropertyType.Bool, true);
        AssertMapProperty(layer.Properties[1], "owner", MapPropertyType.String, "me");
    }

    /// <summary>Asserts a <see cref="MapProperty"/> matches the expected name, type and boxed value.</summary>
    private static void AssertMapProperty(MapProperty property, string name, MapPropertyType type, object? value)
    {
        Assert.Equal(name, property.Name);
        Assert.Equal(type, property.Type);
        Assert.Equal(value, property.Value);
    }

    // ---------------------------------------------------------------------
    // Story 39: prerendered layer images + viewport culling + IDisposable.
    // Every visible, non-empty tile layer is rasterized once into a full
    // pixel-size SKImage at load time; Draw/DrawAbovePlayer blit those images
    // (culled to the viewport) and a disposed map is guarded.
    // ---------------------------------------------------------------------
    /// <summary>Verifies every visible non-empty layer is prerendered into a full pixel-size image, and that invisible and empty layers get a null slot.</summary>
    [Fact]
    public void Prerender_VisibleNonEmptyLayers_ProducePixelSizedImages()
    {
        var ground = new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1 });
        var decor = new TileLayerSpec("decor", new uint[4]); // empty layer
        var hidden = new TileLayerSpec("hidden", new uint[] { 1, 0, 0, 0 }, Visible: false);

        using var fixture = TiledTestFixture.Create2x2(new[] { ground, decor, hidden });
        var map = TileMap.Load(fixture.MapPath);

        Assert.Equal(3, map.PrerenderedLayerImages.Count);

        // ground is visible and non-empty -> a full map-size image.
        var groundImage = map.PrerenderedLayerImages[0];
        Assert.NotNull(groundImage);
        Assert.Equal(map.PixelWidth, groundImage.Width);
        Assert.Equal(map.PixelHeight, groundImage.Height);

        // decor is empty -> not prerendered.
        Assert.Null(map.PrerenderedLayerImages[1]);

        // hidden is invisible -> not prerendered.
        Assert.Null(map.PrerenderedLayerImages[2]);

        map.Dispose();
    }

    /// <summary>Verifies a viewport covering only a sub-region of the map draws only that region; pixels outside the viewport stay untouched.</summary>
    [Fact]
    public void Draw_WithSubregionViewport_CullsToViewport()
    {
        using var fixture = new TiledTestFixture(
            2,
            2,
            new[] { new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1 }) });
        var map = TileMap.Load(fixture.MapPath);

        // The viewport covers only the top-left tile (0,0,tile,tile) of the 96×96 map.
        using var bitmap = new SKBitmap(map.PixelWidth, map.PixelHeight);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            map.Draw(canvas, new SKRect(0, 0, map.TileWidth, map.TileHeight));
        }

        // Inside the viewport: the top-left tile is drawn (opaque).
        Assert.NotEqual(0, bitmap.GetPixel(map.TileWidth / 2, map.TileHeight / 2).Alpha);

        // Outside the viewport (the other three tiles): untouched (transparent).
        Assert.Equal(0, bitmap.GetPixel(map.TileWidth + (map.TileWidth / 2), map.TileHeight / 2).Alpha);
        Assert.Equal(0, bitmap.GetPixel(map.TileWidth / 2, map.TileHeight + (map.TileHeight / 2)).Alpha);
        Assert.Equal(0, bitmap.GetPixel(map.TileWidth + (map.TileWidth / 2), map.TileHeight + (map.TileHeight / 2)).Alpha);

        map.Dispose();
    }

    /// <summary>Verifies Dispose is idempotent and that Draw/DrawAbovePlayer are guarded (throw ObjectDisposedException) after disposal.</summary>
    [Fact]
    public void Dispose_IsIdempotent_AndRenderingIsGuarded()
    {
        using var fixture = new TiledTestFixture(
            1,
            1,
            new[] { new TileLayerSpec("ground", new uint[] { 1 }) });
        var map = TileMap.Load(fixture.MapPath);

        map.Dispose();
        map.Dispose(); // idempotent: the second call must not throw.

        Assert.True(map.IsDisposed);

        using var bitmap = new SKBitmap(map.PixelWidth, map.PixelHeight);
        using var canvas = new SKCanvas(bitmap);
        var viewport = new SKRect(0, 0, map.PixelWidth, map.PixelHeight);
        Assert.Throws<ObjectDisposedException>(() => map.Draw(canvas, viewport));
        Assert.Throws<ObjectDisposedException>(() => map.DrawAbovePlayer(canvas, viewport));
    }

    // ---------------------------------------------------------------------
    // TileSet factories (replacing the removed TileSetManager): standalone
    // tilesets are loaded directly, from the file system or from a stream
    // resolved over HTTP (the WebAssembly-compatible path).
    // ---------------------------------------------------------------------
    /// <summary>Verifies TileSet.Load(path) loads a standalone TSX from the file system and resolves its image relative to the TSX directory.</summary>
    [Fact]
    public void TileSet_LoadFromPath_LoadsStandaloneTileset()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });

        var tileSet = TileSet.Load(fixture.TilesetPath);

        Assert.Equal("test_tiles", tileSet.Name);
        Assert.Equal(0u, tileSet.FirstGid); // standalone tilesets have no map GID base
        Assert.Equal(48, tileSet.TileWidth);
        Assert.Equal(48, tileSet.TileHeight);

        using var tileImage = tileSet.GetTileImage(0);
        Assert.Equal(48, tileImage.Width);
        Assert.Equal(48, tileImage.Height);
    }

    /// <summary>
    /// Verifies TileSet.Load(stream, baseUri, fetcher) parses a TSX from a stream and
    /// resolves its image through the fetcher, i.e. without touching the local file system.
    /// </summary>
    [Fact]
    public void TileSet_LoadFromStreamWithFetcher_ResolvesImageUri()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var baseUri = new Uri("https://example.com/maps/tiles.tsx");
        using var stream = new MemoryStream(File.ReadAllBytes(fixture.TilesetPath));

        var tileSet = TileSet.Load(stream, baseUri, CreateFetcher(fixture));

        Assert.Equal("test_tiles", tileSet.Name);
        Assert.Equal(48, tileSet.TileWidth);
        Assert.Equal(48, tileSet.TileHeight);

        // The image was fetched from https://example.com/maps/tiles.png.
        using var tileImage = tileSet.GetTileImage(0);
        Assert.Equal(48, tileImage.Width);
        Assert.Equal(48, tileImage.Height);
    }

    // ---------------------------------------------------------------------
    // WebAssembly-compatible map loading: TileMap.Load(stream, baseUri,
    // fetcher) parses a TMX from a stream and resolves the external TSX and
    // its image exclusively through the fetcher (no file system access).
    // ---------------------------------------------------------------------
    /// <summary>Verifies TileMap.Load(stream, baseUri, fetcher) loads a TMX whose external TSX and image are fetched through the fetcher.</summary>
    [Fact]
    public void TileMap_LoadFromStreamWithFetcher_LoadsExternalTileset()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var baseUri = new Uri("https://example.com/maps/map.tmx");
        using var stream = new MemoryStream(File.ReadAllBytes(fixture.MapPath));

        var map = TileMap.Load(stream, baseUri, CreateFetcher(fixture));

        Assert.Equal(2, map.Width);
        Assert.Equal(2, map.Height);
        Assert.Equal(48, map.TileWidth);
        Assert.Equal(48, map.TileHeight);

        Assert.Equal(1u, map.GetTileId("ground", 0, 0));
        Assert.Equal(TileFlags.FlippedHorizontally, map.GetTileFlags("ground", 0, 1));
    }

    /// <summary>Verifies a map loaded through the fetcher renders its tiles correctly.</summary>
    [Fact]
    public void TileMap_LoadFromStreamWithFetcher_Renders()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var baseUri = new Uri("https://example.com/maps/map.tmx");
        using var stream = new MemoryStream(File.ReadAllBytes(fixture.MapPath));

        var map = TileMap.Load(stream, baseUri, CreateFetcher(fixture));

        using var bitmap = RenderMap(map);

        // Tile at (0,0) is solid red and opaque.
        Assert.NotEqual(0, bitmap.GetPixel(24, 24).Alpha);
        // Empty cell at (1,0) stays transparent.
        Assert.Equal(0, bitmap.GetPixel(72, 24).Alpha);
    }

    /// <summary>
    /// Verifies the fetcher is genuinely used: a map whose external tileset references an
    /// image the fetcher cannot provide fails with an informative error instead of reading
    /// from disk.
    /// </summary>
    [Fact]
    public void TileMap_LoadFromStreamWithFetcher_ThrowsWhenAssetMissing()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var baseUri = new Uri("https://example.com/maps/map.tmx");
        using var stream = new MemoryStream(File.ReadAllBytes(fixture.MapPath));

        // A fetcher that only serves the map and TSX but not the PNG image.
        TiledAssetFetcher fetcher = uri => uri.Segments[^1] == "tiles.png"
            ? throw new KeyNotFoundException($"Asset not found: {uri}")
            : CreateFetcher(fixture)(uri);

        Assert.Throws<KeyNotFoundException>(() => TileMap.Load(stream, baseUri, fetcher));
    }

    // ---------------------------------------------------------------------
    // Async loading (story 22): TileSet.LoadAsync / TileMap.LoadAsync must
    // never perform a synchronous read of the caller's stream, so they are
    // exercised against a non-seekable, read-async-only stream and an async
    // fetcher that is genuinely awaited.
    // ---------------------------------------------------------------------
    /// <summary>Verifies TileSet.LoadAsync parses a TSX from a non-seekable stream and resolves the image through the async fetcher (the fetched image is used).</summary>
    [Fact]
    public async Task TileSet_LoadAsyncFromStreamWithFetcher_ResolvesImageUri()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var baseUri = new Uri("https://example.com/maps/tiles.tsx");
        using var stream = new AsyncOnlyStream(new MemoryStream(File.ReadAllBytes(fixture.TilesetPath), writable: false));

        var tileSet = await TileSet.LoadAsync(stream, baseUri, CreateAsyncFetcher(fixture));

        Assert.Equal("test_tiles", tileSet.Name);
        Assert.Equal(48, tileSet.TileWidth);
        Assert.Equal(48, tileSet.TileHeight);

        // The image was fetched asynchronously from https://example.com/maps/tiles.png and the
        // fetched bytes are the ones actually decoded: the cropped tile is the fixture's solid
        // red, so its centre pixel is opaque.
        using var tileImage = tileSet.GetTileImage(0);
        Assert.Equal(48, tileImage.Width);
        Assert.Equal(48, tileImage.Height);

        using var bitmap = new SKBitmap(tileImage.Width, tileImage.Height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(tileImage, new SKPoint(0, 0));
        }

        Assert.NotEqual(0, bitmap.GetPixel(24, 24).Alpha);
    }

    /// <summary>Verifies TileSet.LoadAsync propagates an exception thrown by the async fetcher for a missing asset.</summary>
    [Fact]
    public async Task TileSet_LoadAsync_PropagatesFetcherException_ForMissingAsset()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var baseUri = new Uri("https://example.com/maps/tiles.tsx");
        using var stream = new AsyncOnlyStream(new MemoryStream(File.ReadAllBytes(fixture.TilesetPath), writable: false));

        // A fetcher that throws for the image (the only asset a standalone tileset fetches).
        TiledAssetFetcherAsync fetcher = async uri => uri.Segments[^1] == "tiles.png"
            ? throw new KeyNotFoundException($"Asset not found: {uri}")
            : await CreateAsyncFetcher(fixture)(uri);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => TileSet.LoadAsync(stream, baseUri, fetcher));
    }

    /// <summary>Verifies TileMap.LoadAsync loads a TMX from a non-seekable stream, resolving the external TSX and its image exclusively through the async fetcher.</summary>
    [Fact]
    public async Task TileMap_LoadAsyncFromStreamWithFetcher_LoadsExternalTileset()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var baseUri = new Uri("https://example.com/maps/map.tmx");
        using var stream = new AsyncOnlyStream(new MemoryStream(File.ReadAllBytes(fixture.MapPath), writable: false));

        var map = await TileMap.LoadAsync(stream, baseUri, CreateAsyncFetcher(fixture));

        Assert.Equal(2, map.Width);
        Assert.Equal(2, map.Height);
        Assert.Equal(48, map.TileWidth);
        Assert.Equal(48, map.TileHeight);

        Assert.Equal(1u, map.GetTileId("ground", 0, 0));
        Assert.Equal(TileFlags.FlippedHorizontally, map.GetTileFlags("ground", 0, 1));
    }

    /// <summary>Verifies a map loaded through the async fetcher renders its tiles correctly.</summary>
    [Fact]
    public async Task TileMap_LoadAsyncFromStreamWithFetcher_Renders()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var baseUri = new Uri("https://example.com/maps/map.tmx");
        using var stream = new AsyncOnlyStream(new MemoryStream(File.ReadAllBytes(fixture.MapPath), writable: false));

        var map = await TileMap.LoadAsync(stream, baseUri, CreateAsyncFetcher(fixture));

        using var bitmap = RenderMap(map);

        // Tile at (0,0) is solid red and opaque.
        Assert.NotEqual(0, bitmap.GetPixel(24, 24).Alpha);
        // Empty cell at (1,0) stays transparent.
        Assert.Equal(0, bitmap.GetPixel(72, 24).Alpha);
    }

    /// <summary>Verifies the async fetcher is genuinely awaited and that a fetcher throwing for a missing asset propagates the exception.</summary>
    [Fact]
    public async Task TileMap_LoadAsync_PropagatesFetcherException_ForMissingAsset()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var baseUri = new Uri("https://example.com/maps/map.tmx");
        using var stream = new AsyncOnlyStream(new MemoryStream(File.ReadAllBytes(fixture.MapPath), writable: false));

        // The fetcher only serves the map and TSX but throws for the PNG image. The original
        // exception type must propagate unwrapped, which proves the loader awaited the returned
        // task instead of blocking on it.
        TiledAssetFetcherAsync fetcher = async uri => uri.Segments[^1] == "tiles.png"
            ? throw new KeyNotFoundException($"Asset not found: {uri}")
            : await CreateAsyncFetcher(fixture)(uri);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => TileMap.LoadAsync(stream, baseUri, fetcher));
    }

    // ---------------------------------------------------------------------
    // TileSet.GetTileImage: crops the tile from the decoded tileset image.
    // ---------------------------------------------------------------------
    /// <summary>Verifies TileSet.GetTileImage returns a cropped tile image from the decoded tileset image.</summary>
    [Fact]
    public void GetTileImage_ReturnsCroppedTile()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var tileSet = TileSet.Load(fixture.TilesetPath);

        using var tileImage = tileSet.GetTileImage(0);

        Assert.Equal(48, tileImage.Width);
        Assert.Equal(48, tileImage.Height);

        // The fixture image is solid red, so the cropped tile is opaque.
        using var bitmap = new SKBitmap(tileImage.Width, tileImage.Height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(tileImage, new SKPoint(0, 0));
        }

        Assert.NotEqual(0, bitmap.GetPixel(24, 24).Alpha);
    }

    /// <summary>Verifies that invisible layers are skipped during rendering.</summary>
    [Fact]
    public void Draw_SkipsInvisibleLayers()
    {
        using var fixture = new TiledTestFixture(
            1,
            1,
            new[] { new TileLayerSpec("ground", new uint[] { 1 }, Visible: false) });
        var map = TileMap.Load(fixture.MapPath);

        using var bitmap = RenderMap(map);

        Assert.Equal(0, bitmap.GetPixel(24, 24).Alpha);
    }

    /// <summary>Verifies that the layer opacity is applied once during rendering.</summary>
    [Fact]
    public void Draw_AppliesLayerOpacity()
    {
        using var fixture = new TiledTestFixture(
            1,
            1,
            new[] { new TileLayerSpec("ground", new uint[] { 1 }, Opacity: 0.5f) });
        var map = TileMap.Load(fixture.MapPath);

        using var bitmap = RenderMap(map);

        // A fully opaque red tile drawn at 50% opacity yields roughly half-alpha pixels.
        var alpha = bitmap.GetPixel(24, 24).Alpha;
        Assert.InRange(alpha, 96, 160);
    }

    /// <summary>
    /// Builds a fetcher that serves the fixture's map, tileset and image bytes under a
    /// fake HTTP base URI, simulating the WebAssembly/HTTP asset loading scenario.
    /// </summary>
    private static TiledAssetFetcher CreateFetcher(TiledTestFixture fixture)
    {
        var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["map.tmx"] = File.ReadAllBytes(fixture.MapPath),
            ["tiles.tsx"] = File.ReadAllBytes(fixture.TilesetPath),
            ["tiles.png"] = File.ReadAllBytes(fixture.ImagePath),
        };

        return uri => assets.TryGetValue(uri.Segments[^1], out var bytes)
            ? bytes
            : throw new KeyNotFoundException($"Unexpected asset URI: {uri}");
    }

    /// <summary>
    /// Builds an async fetcher that serves the fixture's map, tileset and image bytes under a
    /// fake HTTP base URI, with a short await before returning so the async path is genuinely
    /// exercised (the loader must await the returned task).
    /// </summary>
    private static TiledAssetFetcherAsync CreateAsyncFetcher(TiledTestFixture fixture)
    {
        var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["map.tmx"] = File.ReadAllBytes(fixture.MapPath),
            ["tiles.tsx"] = File.ReadAllBytes(fixture.TilesetPath),
            ["tiles.png"] = File.ReadAllBytes(fixture.ImagePath),
        };

        return async uri =>
        {
            await Task.Delay(1); // prove the fetcher is genuinely awaited (a real async hop).
            return assets.TryGetValue(uri.Segments[^1], out var bytes)
                ? bytes
                : throw new KeyNotFoundException($"Unexpected asset URI: {uri}");
        };
    }

    private static SKBitmap RenderMap(TileMap map, Action<SKCanvas, SKRect>? draw = null)
    {
        var size = Math.Max(map.PixelWidth, map.PixelHeight);
        var bitmap = new SKBitmap(size, size);
        var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        (draw ?? map.Draw)(canvas, new SKRect(0, 0, size, size));
        canvas.Dispose();
        return bitmap;
    }
}
