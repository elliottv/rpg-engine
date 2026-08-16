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
            Properties: new[] { new LayerProperty("above_player", "bool", "true") });

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
            Properties: new[] { new LayerProperty("above_player", "bool", "false") });
        var nonBool = new TileLayerSpec(
            "non_bool",
            new uint[] { 0, 0, 0, 0 },
            Properties: new[] { new LayerProperty("above_player", "string", "true") });

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
            Properties: new[] { new LayerProperty("above_player", "bool", "true") });

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
