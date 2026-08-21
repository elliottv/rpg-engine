using System.Xml.Linq;
using DotTiled;
using DotTiled.Serialization;
using SkiaSharp;

namespace RPGEngine.Tiled;

/// <summary>
/// A tile map loaded from a Tiled <c>.tmx</c> file (and its referenced <c>.tsx</c> tilesets).
/// </summary>
/// <remarks>
/// <para>
/// A map owns the <see cref="TileSet"/>s that its layers reference; these are created during
/// <see cref="Load(string)"/> and are not registered anywhere globally. Loading a map never
/// touches the engine's tilesets; standalone tilesets are loaded explicitly through the
/// <see cref="TileSet.Load(string)"/> / <see cref="TileSet.Load(Stream, Uri, TiledAssetFetcher)"/>
/// factories instead.
/// </para>
/// <para>
/// Tile coordinates are 0-based. Layer data is stored row-major. Only the orthogonal map
/// layout is supported by this story; collision, pathfinding and <c>.tmj</c> are out of scope.
/// </para>
/// <para>
/// Rendering is a per-layer image blit. When the map is loaded, every visible, non-empty tile
/// layer is prerendered once into its own <see cref="SKImage"/> of the map's pixel size: the
/// tiles are drawn at their world pixel positions with the flip transforms from
/// <see cref="TileFlags"/> applied, and the layer's <see cref="TileMapLayer.Opacity"/> is baked
/// into the layer alpha. <see cref="Draw"/> and <see cref="DrawAbovePlayer"/> then only blit the
/// cached layer images that intersect the viewport, so drawing a frame no longer touches the
/// per-tile data (tiles are drawn exactly once, at load time).
/// </para>
/// <para>
/// A <see cref="TileMap"/> is <see cref="IDisposable"/>: disposing it releases the prerendered
/// layer images (and any surfaces kept during their creation). The engine disposes the previous
/// map when <c>GameEngine.Map</c> is replaced and when the engine itself is disposed; hosts that
/// load maps directly are responsible for disposing them when they are replaced or no longer
/// needed.
/// </para>
/// <para>
/// Rendering happens in two passes. <see cref="Draw"/> draws the layers below the player
/// (every layer whose <see cref="TileMapLayer.AbovePlayer"/> is <see langword="false"/>), and
/// <see cref="DrawAbovePlayer"/> draws the layers above the player (those marked with the
/// Tiled <c>above_player</c> custom property set to <see langword="true"/>). The engine
/// renders the below-player pass, then the characters, then the above-player pass so those
/// tiles appear in front of the player.
/// </para>
/// </remarks>
public sealed class TileMap : IDisposable
{
    private readonly IReadOnlyList<TileSet> _tileSets;
    private readonly Dictionary<string, TileMapLayer> _layersByName;

    /// <summary>
    /// The prerendered layer images, one slot per <see cref="Layers"/> entry in file order.
    /// A slot is <see langword="null"/> when the layer was not prerendered (invisible or empty).
    /// </summary>
    private readonly SKImage?[] _prerenderedImages;

    private bool _disposed;

    private TileMap(
        int width,
        int height,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<TileSet> tileSets,
        IReadOnlyList<TileMapLayer> layers,
        IReadOnlyList<MapProperty> properties,
        IReadOnlyList<TileMapObjectLayer> objectLayers)
    {
        Width = width;
        Height = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        _tileSets = tileSets.OrderBy(t => t.FirstGid).ToArray();
        Layers = layers;
        Properties = properties;
        ObjectLayers = objectLayers;

        _layersByName = new Dictionary<string, TileMapLayer>(StringComparer.Ordinal);
        foreach (var layer in layers)
        {
            _layersByName.TryAdd(layer.Name, layer);
        }

        // Every visible, non-empty tile layer is rasterized once into an SKImage at load time.
        // The prerendered images are the source of every later render call.
        _prerenderedImages = PrerenderLayers();
    }

    /// <summary>Gets the width of the map in tiles.</summary>
    public int Width { get; }

    /// <summary>Gets the height of the map in tiles.</summary>
    public int Height { get; }

    /// <summary>Gets the width of a single tile in pixels.</summary>
    public int TileWidth { get; }

    /// <summary>Gets the height of a single tile in pixels.</summary>
    public int TileHeight { get; }

    /// <summary>Gets the total width of the map in pixels (<see cref="Width"/> × <see cref="TileWidth"/>).</summary>
    public int PixelWidth => Width * TileWidth;

    /// <summary>Gets the total height of the map in pixels (<see cref="Height"/> × <see cref="TileHeight"/>).</summary>
    public int PixelHeight => Height * TileHeight;

    /// <summary>
    /// Gets the tile layers of the map, in the order they appear in the file (bottom → top).
    /// Only tile layers are represented here; object layers are exposed through
    /// <see cref="ObjectLayers"/>, and image and group layers are ignored for now.
    /// </summary>
    public IReadOnlyList<TileMapLayer> Layers { get; }

    /// <summary>
    /// Gets the map's custom properties (from the map's <c>&lt;properties&gt;</c> block), in file
    /// order. Map properties are typically used for per-map configuration such as ambient light
    /// or difficulty settings.
    /// </summary>
    public IReadOnlyList<MapProperty> Properties { get; }

    /// <summary>
    /// Gets the object layers of the map, in the order they appear in the file. Only object
    /// layers are represented here; tile layers are exposed through <see cref="Layers"/> and
    /// image/group layers are ignored for now.
    /// </summary>
    public IReadOnlyList<TileMapObjectLayer> ObjectLayers { get; }

    /// <summary>
    /// Gets the prerendered layer images, one slot per <see cref="Layers"/> entry in file order.
    /// Each non-<see langword="null"/> image is the full pixel-size raster of its layer with the
    /// flip transforms and layer opacity baked in; a <see langword="null"/> slot means the layer
    /// was not prerendered (it is invisible or empty).
    /// </summary>
    /// <remarks>
    /// Internal so the test project can assert the prerender contract (acceptance criterion 5).
    /// The caller must not dispose these images; <see cref="Dispose"/> owns them.
    /// </remarks>
    internal IReadOnlyList<SKImage?> PrerenderedLayerImages => _prerenderedImages;

    /// <summary>Gets whether this map has been disposed. Internal for tests.</summary>
    internal bool IsDisposed => _disposed;

    /// <summary>
    /// Returns the map property named <paramref name="name"/> using a case-sensitive comparison,
    /// or <see langword="null"/> when no property with that exact name exists.
    /// </summary>
    /// <param name="name">The exact property name to look up.</param>
    /// <returns>The matching <see cref="MapProperty"/>, or <see langword="null"/> when absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public MapProperty? GetProperty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        foreach (var property in Properties)
        {
            if (property.Name == name)
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads the Tiled map at <paramref name="path"/>, parsing the referenced external tilesets
    /// and decoding their images from the local file system.
    /// </summary>
    /// <param name="path">The path to a Tiled <c>.tmx</c> map file.</param>
    /// <returns>The loaded <see cref="TileMap"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public static TileMap Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var fullPath = Path.GetFullPath(path);
        var mapDirectory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        var map = Loader.Default().LoadMap(fullPath);

        return BuildMap(map, dotTiledTileset => CreateFileSystemTileSet(dotTiledTileset, mapDirectory));
    }

    /// <summary>
    /// Loads the Tiled map content from <paramref name="stream"/>, parsing the referenced
    /// external tilesets and decoding their images. External resources declared by the map
    /// (external <c>.tsx</c> tilesets and their images) are resolved against
    /// <paramref name="baseUri"/> and fetched through <paramref name="fetcher"/>.
    /// </summary>
    /// <param name="stream">A stream containing the Tiled <c>.tmx</c> map content.</param>
    /// <param name="baseUri">
    /// The URI the map was fetched from (e.g. <c>https://example.com/maps/map.tmx</c>).
    /// Relative references declared by the map and its tilesets are resolved against it.
    /// </param>
    /// <param name="fetcher">Fetches the raw bytes of a resolved asset URI.</param>
    /// <returns>The loaded <see cref="TileMap"/>.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A referenced tileset has no image or an image could not be decoded.</exception>
    /// <remarks>
    /// This overload is the file-system-free entry point: it is used when the map content was
    /// obtained without a local file path, such as in a WebAssembly build running in a browser
    /// where the <c>.tmx</c>, the <c>.tsx</c> tilesets and the tile images are fetched over
    /// HTTP. The caller remains the owner of <paramref name="stream"/>.
    /// </remarks>
    public static TileMap Load(Stream stream, Uri baseUri, TiledAssetFetcher fetcher)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(fetcher);

        var content = ReadAllText(stream);
        var map = ParseMapContent(content, baseUri, fetcher);

        return BuildMap(map, dotTiledTileset => CreateUriTileSet(dotTiledTileset, baseUri, fetcher));
    }

    /// <summary>
    /// Asynchronously loads the Tiled map content from <paramref name="stream"/>, parsing the
    /// referenced external tilesets and decoding their images.
    /// </summary>
    /// <param name="stream">A stream containing the Tiled <c>.tmx</c> map content.</param>
    /// <param name="baseUri">
    /// The URI the map was fetched from (e.g. <c>https://example.com/maps/map.tmx</c>).
    /// Relative references declared by the map and its tilesets are resolved against it.
    /// </param>
    /// <param name="fetcher">Asynchronously fetches the raw bytes of a resolved asset URI.</param>
    /// <returns>A task that resolves to the loaded <see cref="TileMap"/>.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A referenced tileset has no image or an image could not be decoded.</exception>
    /// <remarks>
    /// <para>
    /// This is the asynchronous counterpart of <see cref="Load(Stream, Uri, TiledAssetFetcher)"/>
    /// for streams and asset fetchers that only support asynchronous I/O (e.g. certain
    /// network/browser streams). No synchronous read is performed on the caller's stream: the
    /// TMX content is read with <c>StreamReader.ReadToEndAsync()</c> and every external asset is
    /// fetched with <c>await fetcher(...)</c>.
    /// </para>
    /// <para>
    /// DotTiled 1.0.0 only exposes synchronous external-tileset resolvers (the parser takes
    /// <c>Func&lt;string, Tileset&gt;</c>), so the external TSX and its image cannot be awaited
    /// from inside the parser. The async path therefore <em>pre-fetches the external asset
    /// graph asynchronously</em> and then reuses the existing synchronous DotTiled
    /// parsing/decoding against an in-memory cache: the map's external <c>.tsx</c> tilesets and
    /// the images they reference (plus any embedded <c>&lt;tileset&gt;&lt;image&gt;</c> images)
    /// are fetched and stored in a <see cref="Dictionary{TKey,TValue}"/> cache keyed by resolved
    /// URI, and the synchronous helpers then resolve only from that memory cache, so no blocking
    /// I/O remains. This is a pragmatic bridge until DotTiled exposes asynchronous resolvers; the
    /// TMX/TSX format remains the source of truth.
    /// </para>
    /// </remarks>
    public static async Task<TileMap> LoadAsync(Stream stream, Uri baseUri, TiledAssetFetcherAsync fetcher)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(fetcher);

        var content = await ReadAllTextAsync(stream).ConfigureAwait(false);
        var cache = await PrefetchAssetsAsync(content, baseUri, fetcher).ConfigureAwait(false);

        var map = ParseMapContent(content, baseUri, uri => cache[uri]);
        return BuildMap(map, dotTiledTileset => CreateUriTileSet(dotTiledTileset, baseUri, uri => cache[uri]));
    }

    /// <summary>
    /// Returns the global tile ID of the tile at (<paramref name="x"/>, <paramref name="y"/>) in
    /// the layer named <paramref name="layerName"/>, with all flip bits masked off. Returns 0 for
    /// an empty cell.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No tile layer named <paramref name="layerName"/> exists.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The coordinates are outside the layer bounds.</exception>
    public uint GetTileId(string layerName, int x, int y) => GetLayer(layerName).GetTileId(x, y);

    /// <summary>
    /// Returns the <see cref="TileFlags"/> of the tile at (<paramref name="x"/>, <paramref name="y"/>)
    /// in the layer named <paramref name="layerName"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No tile layer named <paramref name="layerName"/> exists.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The coordinates are outside the layer bounds.</exception>
    public TileFlags GetTileFlags(string layerName, int x, int y) => GetLayer(layerName).GetTileFlags(x, y);

    /// <summary>
    /// Returns whether the tile at (<paramref name="tileX"/>, <paramref name="tileY"/>) blocks
    /// movement. The current contract always returns <see langword="false"/>; collision data will
    /// be added by a later story, at which point this method will consult the map's collision
    /// information instead.
    /// </summary>
    /// <param name="tileX">The 0-based tile X coordinate.</param>
    /// <param name="tileY">The 0-based tile Y coordinate.</param>
    /// <returns><see langword="false"/> for every tile under the current contract.</returns>
    public bool IsSolid(int tileX, int tileY) => false;

    /// <summary>
    /// Draws the visible part of the map to <paramref name="canvas"/>, rendering only the
    /// layers that belong <em>below</em> the player (those whose
    /// <see cref="TileMapLayer.AbovePlayer"/> is <see langword="false"/>). Only the region of
    /// each prerendered layer image that intersects <paramref name="viewport"/> is blitted; the
    /// viewport is in the same (world) coordinate space that tiles are drawn into, so callers
    /// that apply a camera transform must pass the corresponding viewport rectangle.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <param name="viewport">The visible world-space rectangle used to cull layers.</param>
    /// <exception cref="ObjectDisposedException">The map has been disposed.</exception>
    /// <remarks>
    /// This is the first render pass: the engine calls it before drawing the characters, then
    /// calls <see cref="DrawAbovePlayer"/> afterwards so <c>above_player</c> layers appear on
    /// top of the player. Each layer is a prerendered image blit; no per-tile work happens here.
    /// </remarks>
    internal void Draw(SKCanvas canvas, SKRect viewport)
        => DrawLayerImages(canvas, viewport, abovePlayerOnly: false);

    /// <summary>
    /// Draws the visible part of the map to <paramref name="canvas"/>, rendering only the
    /// layers that belong <em>above</em> the player (those whose
    /// <see cref="TileMapLayer.AbovePlayer"/> is <see langword="true"/>). The culling and image
    /// blitting are identical to <see cref="Draw"/>; only the layer selection differs.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <param name="viewport">The visible world-space rectangle used to cull layers.</param>
    /// <exception cref="ObjectDisposedException">The map has been disposed.</exception>
    /// <remarks>
    /// This is the second render pass: the engine calls it after the NPCs and the player have
    /// been drawn, so the tiles of <c>above_player</c> layers appear in front of the player.
    /// </remarks>
    internal void DrawAbovePlayer(SKCanvas canvas, SKRect viewport)
        => DrawLayerImages(canvas, viewport, abovePlayerOnly: true);

    /// <summary>
    /// Releases the prerendered layer images owned by this map. This method is idempotent:
    /// calling it more than once, or after the engine has already replaced/disposed the map, is
    /// a no-op.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var image in _prerenderedImages)
        {
            image?.Dispose();
        }
    }

    /// <summary>
    /// Shared implementation of the two map render passes. When
    /// <paramref name="abovePlayerOnly"/> is <see langword="false"/> only layers below the
    /// player are drawn (see <see cref="Draw"/>); when <see langword="true"/> only layers
    /// above the player are drawn (see <see cref="DrawAbovePlayer"/>). In both cases the
    /// prerendered layer images are iterated in file order and each image's intersection with
    /// <paramref name="viewport"/> is blitted with a plain, non-antialiased image draw. Layers
    /// that were not prerendered (invisible or empty) have a <see langword="null"/> slot and are
    /// skipped. Opacity and flip transforms were baked in at load time, so the draw path is a
    /// pure image blit.
    /// </summary>
    private void DrawLayerImages(SKCanvas canvas, SKRect viewport, bool abovePlayerOnly)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Cull: intersect the viewport with the map's pixel bounds (each prerendered layer image
        // spans exactly (0, 0, PixelWidth, PixelHeight)). When the viewport does not overlap the
        // map nothing is drawn, so pixels outside the viewport are never touched.
        var left = Math.Max(viewport.Left, 0f);
        var top = Math.Max(viewport.Top, 0f);
        var right = Math.Min(viewport.Right, (float)PixelWidth);
        var bottom = Math.Min(viewport.Bottom, (float)PixelHeight);
        if (left >= right || top >= bottom)
        {
            return;
        }

        var visible = new SKRect(left, top, right, bottom);

        using var paint = new SKPaint { IsAntialias = false };

        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            if (Layers[layerIndex].AbovePlayer != abovePlayerOnly)
            {
                continue;
            }

            var image = _prerenderedImages[layerIndex];
            if (image is null)
            {
                continue;
            }

            // The source and destination rects are the same world-pixel rect: the visible part
            // of the prerendered image is blitted in place, which preserves viewport culling.
            canvas.DrawImage(image, visible, visible, paint);
        }
    }

    /// <summary>
    /// Prerenders every visible, non-empty tile layer into an <see cref="SKImage"/> of the map's
    /// pixel size. Invisible layers and empty layers (no non-zero GID) produce a
    /// <see langword="null"/> slot. Each returned image is owned by this map and released by
    /// <see cref="Dispose"/>.
    /// </summary>
    private SKImage?[] PrerenderLayers()
    {
        var images = new SKImage?[Layers.Count];

        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            var layer = Layers[layerIndex];
            if (!layer.Visible || !HasAnyTile(layer))
            {
                continue;
            }

            images[layerIndex] = PrerenderLayer(layer);
        }

        return images;
    }

    /// <summary>
    /// Rasterizes one tile layer into a full map-size <see cref="SKImage"/>. The tiles are drawn
    /// at their world pixel positions with the flip transforms from <see cref="DrawTile"/>
    /// applied; when the layer has an opacity below 1 the whole layer is faded afterwards, so the
    /// opacity is baked into the stored image and the draw path stays a plain image blit.
    /// </summary>
    private SKImage PrerenderLayer(TileMapLayer layer)
    {
        using var surface = SKSurface.Create(new SKImageInfo(PixelWidth, PixelHeight))
            ?? throw new InvalidOperationException("Failed to create the raster surface used to prerender the map layers.");

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var tilePaint = new SKPaint { IsAntialias = false };

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var gid = layer.GetTileId(x, y);
                if (gid == 0)
                {
                    continue;
                }

                var tileSet = ResolveTileSet(gid);
                if (tileSet is null)
                {
                    // A GID that no tileset covers; ignore defensively (malformed map).
                    continue;
                }

                var flags = layer.GetTileFlags(x, y);
                var localTileId = (int)(gid - tileSet.FirstGid);
                using var tileImage = tileSet.GetTileImage(localTileId);

                var dest = new SKRect(
                    x * TileWidth,
                    y * TileHeight,
                    (x + 1) * TileWidth,
                    (y + 1) * TileHeight);

                DrawTile(canvas, tileImage, dest, flags, tilePaint);
            }
        }

        if (layer.Opacity < 1f)
        {
            // Bake the layer opacity into the whole layer: the tiles are composited at full
            // alpha first and the resulting layer is then faded, matching the previous
            // SaveLayer-based behavior. Drawing the full-alpha snapshot back onto the surface
            // with an alpha-modulated paint produces exactly that faded layer.
            using var fullAlphaLayer = surface.Snapshot();
            canvas.Clear(SKColors.Transparent);

            using var opacityPaint = new SKPaint
            {
                Color = SKColors.White.WithAlpha((byte)Math.Round(layer.Opacity * 255f)),
                IsAntialias = false,
            };
            canvas.DrawImage(fullAlphaLayer, 0, 0, opacityPaint);
        }

        // The snapshot keeps the surface's pixels alive after the surface is disposed, so the
        // surface can be released as soon as the image has been taken.
        return surface.Snapshot();
    }

    /// <summary>Returns whether the layer contains at least one non-zero (non-empty) tile GID.</summary>
    private static bool HasAnyTile(TileMapLayer layer)
    {
        foreach (var tileId in layer.TileIds)
        {
            if (tileId != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawTile(SKCanvas canvas, SKImage image, SKRect dest, TileFlags flags, SKPaint paint)
    {
        var horizontallyFlipped = (flags & TileFlags.FlippedHorizontally) != 0;
        var verticallyFlipped = (flags & TileFlags.FlippedVertically) != 0;
        var diagonallyFlipped = (flags & TileFlags.FlippedDiagonally) != 0;

        var source = new SKRect(0, 0, image.Width, image.Height);

        if (!horizontallyFlipped && !verticallyFlipped && !diagonallyFlipped)
        {
            canvas.DrawImage(image, source, dest, paint);
            return;
        }

        // Apply the flip/rotation transforms. Tiled applies the diagonal flip first and then
        // the horizontal/vertical flips (see the Tiled docs on global tile IDs). The
        // transformation matches Tiled's own renderer: rotate 90° clockwise, swap the effective
        // H/V flags, then scale by -1 where a flip applies. All transforms are centred so the
        // tile stays inside its cell.
        canvas.Save();
        try
        {
            if (diagonallyFlipped)
            {
                // Compensate for the swapped tile dimensions when tiles are not square.
                var halfDiff = (dest.Height - dest.Width) / 2f;
                canvas.Translate(dest.MidX + halfDiff, dest.MidY + halfDiff);
                canvas.RotateDegrees(90f);
                (horizontallyFlipped, verticallyFlipped) = (verticallyFlipped, !horizontallyFlipped);
            }
            else
            {
                canvas.Translate(dest.MidX, dest.MidY);
            }

            canvas.Scale(horizontallyFlipped ? -1f : 1f, verticallyFlipped ? -1f : 1f);

            var target = new SKRect(
                -dest.Width / 2f,
                -dest.Height / 2f,
                dest.Width / 2f,
                dest.Height / 2f);

            canvas.DrawImage(image, source, target, paint);
        }
        finally
        {
            canvas.Restore();
        }
    }

    private static TileMap BuildMap(Map map, Func<Tileset, TileSet> tileSetFactory)
    {
        var tileSets = new List<TileSet>();
        foreach (var dotTiledTileset in map.Tilesets)
        {
            tileSets.Add(tileSetFactory(dotTiledTileset));
        }

        var layers = new List<TileMapLayer>();
        var objectLayers = new List<TileMapObjectLayer>();
        foreach (var layer in map.Layers)
        {
            if (layer is TileLayer tileLayer)
            {
                layers.Add(TileMapLayer.FromDotTiled(tileLayer));
            }
            else if (layer is ObjectLayer objectLayer)
            {
                objectLayers.Add(TileMapObjectLayer.FromDotTiled(objectLayer));
            }
        }

        return new TileMap(
            map.Width,
            map.Height,
            map.TileWidth,
            map.TileHeight,
            tileSets,
            layers,
            map.Properties.Select(MapProperty.Create).ToArray(),
            objectLayers);
    }

    private static TileSet CreateFileSystemTileSet(Tileset dotTiledTileset, string mapDirectory)
    {
        // The image source inside an external TSX is relative to that TSX file, while the
        // image source inside an embedded <tileset> is relative to the map file.
        var imageBaseDirectory = mapDirectory;
        if (dotTiledTileset.Source.HasValue && !string.IsNullOrWhiteSpace(dotTiledTileset.Source.Value))
        {
            var tsxPath = Path.GetFullPath(Path.Combine(mapDirectory, dotTiledTileset.Source.Value));
            imageBaseDirectory = Path.GetDirectoryName(tsxPath) ?? mapDirectory;
        }

        var firstGid = dotTiledTileset.FirstGID.GetValueOr(0u);
        return TileSet.FromDotTiled(dotTiledTileset, firstGid, source =>
        {
            var imagePath = Path.IsPathRooted(source)
                ? source
                : Path.Combine(imageBaseDirectory, source);

            return SKBitmap.Decode(imagePath)
                ?? throw new FileNotFoundException(
                    $"Unable to load tileset image for '{dotTiledTileset.Name}'.", imagePath);
        });
    }

    private static TileSet CreateUriTileSet(Tileset dotTiledTileset, Uri baseUri, TiledAssetFetcher fetcher)
    {
        // An external TSX's image is relative to the TSX URI, while an embedded <tileset>'s
        // image is relative to the map URI.
        var tilesetUri = baseUri;
        if (dotTiledTileset.Source.HasValue && !string.IsNullOrWhiteSpace(dotTiledTileset.Source.Value))
        {
            tilesetUri = new Uri(baseUri, dotTiledTileset.Source.Value);
        }

        var firstGid = dotTiledTileset.FirstGID.GetValueOr(0u);
        return TileSet.FromDotTiled(dotTiledTileset, firstGid, source =>
        {
            var imageUri = new Uri(tilesetUri, source);
            var bytes = fetcher(imageUri);
            using var imageStream = new MemoryStream(bytes, writable: false);
            return SKBitmap.Decode(imageStream)
                ?? throw new InvalidOperationException(
                    $"Unable to decode tileset image '{imageUri}' for '{dotTiledTileset.Name}'.");
        });
    }

    private static Map ParseMapContent(string content, Uri baseUri, TiledAssetFetcher fetcher)
    {
        using var reader = new MapReader(
            content,
            externalTilesetResolver: source => LoadExternalTileset(new Uri(baseUri, source), fetcher),
            externalTemplateResolver: source => throw new NotSupportedException(
                $"External template '{source}' is not supported when loading a map from a stream."),
            customTypeResolver: _ => Optional.Empty);
        return reader.ReadMap();
    }

    private static Tileset LoadExternalTileset(Uri tilesetUri, TiledAssetFetcher fetcher)
    {
        var bytes = fetcher(tilesetUri);
        var content = TextFromBytes(bytes);

        using var reader = new TilesetReader(
            content,
            externalTilesetResolver: source => LoadExternalTileset(new Uri(tilesetUri, source), fetcher),
            externalTemplateResolver: source => throw new NotSupportedException(
                $"External template '{source}' is not supported when loading a map from a stream."),
            customTypeResolver: _ => Optional.Empty);
        return reader.ReadTileset();
    }

    private static string ReadAllText(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Pre-fetches the external asset graph referenced by <paramref name="mapContent"/> into an
    /// in-memory cache keyed by resolved URI, so the synchronous DotTiled parse that follows can
    /// resolve every asset (external TSX and image) without performing blocking I/O.
    /// </summary>
    /// <param name="mapContent">The raw TMX content of the map.</param>
    /// <param name="baseUri">The URI the map was fetched from; relative references are resolved against it.</param>
    /// <param name="fetcher">Asynchronously fetches the raw bytes of a resolved asset URI.</param>
    /// <returns>A task that resolves to a cache of resolved asset URIs to their raw bytes.</returns>
    private static async Task<Dictionary<Uri, byte[]>> PrefetchAssetsAsync(
        string mapContent,
        Uri baseUri,
        TiledAssetFetcherAsync fetcher)
    {
        var cache = new Dictionary<Uri, byte[]>();

        var document = XDocument.Parse(mapContent, LoadOptions.PreserveWhitespace);
        foreach (var tilesetElement in document.Root?.Elements("tileset") ?? Enumerable.Empty<XElement>())
        {
            var sourceAttribute = tilesetElement.Attribute("source");
            if (sourceAttribute is not null && !string.IsNullOrWhiteSpace(sourceAttribute.Value))
            {
                // External tileset: fetch the TSX, then parse it to find the image it declares
                // (relative to the TSX URI) and fetch that too.
                var tilesetUri = new Uri(baseUri, sourceAttribute.Value);
                var tsxBytes = await fetcher(tilesetUri).ConfigureAwait(false);
                cache[tilesetUri] = tsxBytes;

                var tsxContent = TextFromBytes(tsxBytes);
                var tsxDocument = XDocument.Parse(tsxContent, LoadOptions.PreserveWhitespace);
                var imageSource = tsxDocument.Root?.Element("image")?.Attribute("source")?.Value;
                if (!string.IsNullOrWhiteSpace(imageSource))
                {
                    var imageUri = new Uri(tilesetUri, imageSource);
                    cache[imageUri] = await fetcher(imageUri).ConfigureAwait(false);
                }
            }
            else
            {
                // Embedded <tileset> with an image: the image source is relative to the map URI.
                var imageSource = tilesetElement.Element("image")?.Attribute("source")?.Value;
                if (!string.IsNullOrWhiteSpace(imageSource))
                {
                    var imageUri = new Uri(baseUri, imageSource);
                    cache[imageUri] = await fetcher(imageUri).ConfigureAwait(false);
                }
            }
        }

        return cache;
    }

    private static async Task<string> ReadAllTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static string TextFromBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private TileMapLayer GetLayer(string layerName)
    {
        ArgumentNullException.ThrowIfNull(layerName);

        if (!_layersByName.TryGetValue(layerName, out var layer))
        {
            throw new KeyNotFoundException($"No tile layer named '{layerName}' exists in the map.");
        }

        return layer;
    }

    private TileSet? ResolveTileSet(uint gid)
    {
        // The tilesets are sorted by FirstGid; the owning tileset is the last one whose
        // FirstGid is <= gid (standard Tiled resolution).
        TileSet? match = null;
        foreach (var tileSet in _tileSets)
        {
            if (tileSet.FirstGid <= gid)
            {
                match = tileSet;
            }
            else
            {
                break;
            }
        }

        return match;
    }
}
