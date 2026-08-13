using System.Text;
using RPGEngine.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests.Tiled;

/// <summary>
/// Acceptance tests for <see cref="TileMap"/>, <see cref="TileSet"/> and
/// <see cref="TileSetManager"/> (story 7: Tiled TMX/TSX loading and rendering).
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
    // Acceptance 4: IsSolid returns false for all tiles (current contract).
    // ---------------------------------------------------------------------
    /// <summary>Verifies IsSolid returns false for every tile under the current (collision-less) contract.</summary>
    [Fact]
    public void IsSolid_ReturnsFalseForAllTiles()
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
    // Acceptance 6: TileSetManager unique-name registration, duplicate names
    // throw, and unknown lookups throw.
    // ---------------------------------------------------------------------
    /// <summary>Verifies TileSetManager registers tilesets under unique names, throws on duplicate registration and throws for unknown lookups.</summary>
    [Fact]
    public void TileSetManager_RegistersUniqueNames_AndRejectsDuplicates()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var manager = new TileSetManager();

        var tileSet = manager.Load("tiles", fixture.TilesetPath);

        Assert.True(manager.Contains("tiles"));
        Assert.Same(tileSet, manager.Get("tiles"));
        Assert.False(manager.Contains("missing"));

        // Duplicate registration throws.
        Assert.Throws<InvalidOperationException>(() => manager.Load("tiles", fixture.TilesetPath));

        // Unknown lookup throws.
        Assert.Throws<KeyNotFoundException>(() => manager.Get("missing"));
    }

    /// <summary>Verifies TileSetManager.Load(name, Stream) parses a TSX from a stream and registers it under the given name.</summary>
    [Fact]
    public void TileSetManager_LoadFromStream_RegistersTileset()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var manager = new TileSetManager();

        // Rewrite the TSX so the image source is absolute (a stream has no
        // file-system location to resolve a relative path against).
        var tsx = File.ReadAllText(fixture.TilesetPath).Replace(
            "source=\"tiles.png\"",
            $"source=\"{fixture.ImagePath}\"");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(tsx));

        var tileSet = manager.Load("stream_tiles", stream);

        Assert.True(manager.Contains("stream_tiles"));
        Assert.Same(tileSet, manager.Get("stream_tiles"));

        // A duplicate registration throws (using a fresh stream: the first call
        // consumed its stream).
        using var duplicateStream = new MemoryStream(Encoding.UTF8.GetBytes(tsx));
        Assert.Throws<InvalidOperationException>(() => manager.Load("stream_tiles", duplicateStream));
    }

    // ---------------------------------------------------------------------
    // TileSet.GetTileImage: crops the tile from the decoded tileset image.
    // ---------------------------------------------------------------------
    /// <summary>Verifies TileSet.GetTileImage returns a cropped tile image from the decoded tileset image.</summary>
    [Fact]
    public void GetTileImage_ReturnsCroppedTile()
    {
        using var fixture = TiledTestFixture.Create2x2(new[] { Ground });
        var manager = new TileSetManager();
        var tileSet = manager.Load("tiles", fixture.TilesetPath);

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

    private static SKBitmap RenderMap(TileMap map)
    {
        var size = Math.Max(map.PixelWidth, map.PixelHeight);
        var bitmap = new SKBitmap(size, size);
        var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        map.Draw(canvas, new SKRect(0, 0, size, size));
        canvas.Dispose();
        return bitmap;
    }
}
