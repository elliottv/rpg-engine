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
/// </remarks>
public sealed class TileMap
{
    private readonly IReadOnlyList<TileSet> _tileSets;
    private readonly Dictionary<string, TileMapLayer> _layersByName;

    private TileMap(
        int width,
        int height,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<TileSet> tileSets,
        IReadOnlyList<TileMapLayer> layers)
    {
        Width = width;
        Height = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        _tileSets = tileSets.OrderBy(t => t.FirstGid).ToArray();
        Layers = layers;

        _layersByName = new Dictionary<string, TileMapLayer>(StringComparer.Ordinal);
        foreach (var layer in layers)
        {
            _layersByName.TryAdd(layer.Name, layer);
        }
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
    /// Only tile layers are represented; object, image and group layers are ignored for now.
    /// </summary>
    public IReadOnlyList<TileMapLayer> Layers { get; }

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
    /// Draws the visible part of the map to <paramref name="canvas"/>. Only tiles intersecting
    /// <paramref name="viewport"/> are drawn; the viewport is in the same (world) coordinate space
    /// that tiles are drawn into, so callers that apply a camera transform must pass the
    /// corresponding viewport rectangle.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <param name="viewport">The visible world-space rectangle used to cull tiles.</param>
    internal void Draw(SKCanvas canvas, SKRect viewport)
    {
        var startX = Math.Max(0, (int)MathF.Floor(viewport.Left / TileWidth));
        var endX = Math.Min(Width - 1, (int)MathF.Ceiling(viewport.Right / TileWidth) - 1);
        var startY = Math.Max(0, (int)MathF.Floor(viewport.Top / TileHeight));
        var endY = Math.Min(Height - 1, (int)MathF.Ceiling(viewport.Bottom / TileHeight) - 1);

        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            var layer = Layers[layerIndex];
            if (!layer.Visible)
            {
                continue;
            }

            using var tilePaint = new SKPaint { IsAntialias = false };
            var applyOpacity = layer.Opacity < 1f;
            if (applyOpacity)
            {
                // Apply the layer opacity by drawing the whole layer into a temporary layer
                // whose alpha is controlled by a dedicated paint. The tile paint keeps its
                // full alpha so the opacity is not applied twice.
                using var layerPaint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha((byte)Math.Round(layer.Opacity * 255f)),
                };
                canvas.SaveLayer(layerPaint);
                try
                {
                    DrawLayer(canvas, layer, startX, endX, startY, endY, tilePaint);
                }
                finally
                {
                    canvas.Restore();
                }
            }
            else
            {
                DrawLayer(canvas, layer, startX, endX, startY, endY, tilePaint);
            }
        }
    }

    private void DrawLayer(SKCanvas canvas, TileMapLayer layer, int startX, int endX, int startY, int endY, SKPaint paint)
    {
        for (var y = startY; y <= endY; y++)
        {
            for (var x = startX; x <= endX; x++)
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

                DrawTile(canvas, tileImage, dest, flags, paint);
            }
        }
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
        foreach (var layer in map.Layers)
        {
            if (layer is TileLayer tileLayer)
            {
                layers.Add(TileMapLayer.FromDotTiled(tileLayer));
            }
        }

        return new TileMap(map.Width, map.Height, map.TileWidth, map.TileHeight, tileSets, layers);
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
