using DotTiled;
using DotTiled.Serialization;
using SkiaSharp;

namespace RPGEngine.Tiled;

/// <summary>
/// A single Tiled tileset: a grid of tiles cut from one image, together with the
/// global tile ID (<see cref="FirstGid"/>) at which the tileset starts within a map.
/// </summary>
/// <remarks>
/// <para>
/// Standalone tilesets are created through the static factories
/// <see cref="Load(string)"/> (local file system) and
/// <see cref="Load(Stream, Uri, TiledAssetFetcher)"/> (streams, e.g. fetched from a URL
/// in WebAssembly). The tilesets referenced by a map are created internally by
/// <see cref="TileMap.Load(string)"/> and <see cref="TileMap.Load(Stream, Uri, TiledAssetFetcher)"/>.
/// The constructor is internal; use one of those entry points.
/// </para>
/// <para>
/// The backing image is decoded exactly once with SkiaSharp and kept for the whole
/// lifetime of the tileset. <see cref="GetTileImage"/> returns a cropped raster copy of a
/// single tile; each call produces an independent image that the caller owns and may
/// dispose freely.
/// </para>
/// </remarks>
public sealed class TileSet
{
    private readonly SKBitmap _sourceImage;
    private readonly int _tileCount;
    private readonly int _columns;
    private readonly int _spacing;
    private readonly int _margin;

    /// <summary>Gets the name of the tileset.</summary>
    public string Name { get; }

    /// <summary>
    /// Gets the global tile ID of the first tile in this tileset (the GID that maps to
    /// local tile ID 0). For standalone tilesets loaded through
    /// <see cref="Load(string)"/> this is <c>0</c> because the tileset is not part of a map.
    /// </summary>
    public uint FirstGid { get; }

    /// <summary>Gets the width of a single tile in pixels.</summary>
    public int TileWidth { get; }

    /// <summary>Gets the height of a single tile in pixels.</summary>
    public int TileHeight { get; }

    internal TileSet(
        string name,
        uint firstGid,
        int tileWidth,
        int tileHeight,
        SKBitmap sourceImage,
        int tileCount,
        int columns,
        int spacing = 0,
        int margin = 0)
    {
        Name = name;
        FirstGid = firstGid;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        _sourceImage = sourceImage ?? throw new ArgumentNullException(nameof(sourceImage));
        _tileCount = tileCount;
        _columns = columns;
        _spacing = spacing;
        _margin = margin;
    }

    /// <summary>
    /// Loads the Tiled tileset (<c>.tsx</c>) at <paramref name="path"/> and decodes its
    /// image. The image <c>source</c> declared by the tileset is resolved relative to the
    /// directory containing the <c>.tsx</c> file.
    /// </summary>
    /// <param name="path">The path to a Tiled <c>.tsx</c> tileset file.</param>
    /// <returns>The loaded <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The tileset has no image or the image could not be decoded.</exception>
    /// <exception cref="FileNotFoundException">The tileset file or its image does not exist.</exception>
    public static TileSet Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var fullPath = Path.GetFullPath(path);
        var baseDirectory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        var dotTiledTileset = Loader.Default().LoadTileset(fullPath);

        return FromDotTiled(dotTiledTileset, dotTiledTileset.FirstGID.GetValueOr(0u), source =>
        {
            var imagePath = Path.IsPathRooted(source)
                ? source
                : Path.Combine(baseDirectory, source);

            return SKBitmap.Decode(imagePath)
                ?? throw new FileNotFoundException(
                    $"Unable to load tileset image for '{dotTiledTileset.Name}'.", imagePath);
        });
    }

    /// <summary>
    /// Loads a Tiled tileset (<c>.tsx</c>) from <paramref name="stream"/> and decodes its
    /// image. The image <c>source</c> declared by the tileset is resolved relative to
    /// <paramref name="baseUri"/> and fetched through <paramref name="fetcher"/>.
    /// </summary>
    /// <param name="stream">A stream containing the Tiled <c>.tsx</c> tileset content.</param>
    /// <param name="baseUri">
    /// The URI the tileset was fetched from (e.g. <c>https://example.com/tiles/tiles.tsx</c>).
    /// Relative image sources declared by the tileset are resolved against it.
    /// </param>
    /// <param name="fetcher">Fetches the raw bytes of a resolved asset URI.</param>
    /// <returns>The loaded <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The tileset has no image or the image could not be decoded.</exception>
    /// <remarks>
    /// This overload is the file-system-free entry point: it is used when the tileset
    /// content was obtained without a local file path, such as in a WebAssembly build
    /// running in a browser where the <c>.tsx</c> and its image are fetched over HTTP.
    /// The caller remains the owner of <paramref name="stream"/>.
    /// </remarks>
    public static TileSet Load(Stream stream, Uri baseUri, TiledAssetFetcher fetcher)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(fetcher);

        var content = ReadAllText(stream);
        var dotTiledTileset = ParseTilesetContent(content);

        return FromDotTiled(dotTiledTileset, dotTiledTileset.FirstGID.GetValueOr(0u), source =>
        {
            var imageUri = new Uri(baseUri, source);
            var bytes = fetcher(imageUri);
            using var imageStream = new MemoryStream(bytes, writable: false);
            return SKBitmap.Decode(imageStream)
                ?? throw new InvalidOperationException(
                    $"Unable to decode tileset image '{imageUri}' for '{dotTiledTileset.Name}'.");
        });
    }

    /// <summary>
    /// Asynchronously loads a Tiled tileset (<c>.tsx</c>) from <paramref name="stream"/> and
    /// decodes its image. The image <c>source</c> declared by the tileset is resolved relative to
    /// <paramref name="baseUri"/> and fetched through <paramref name="fetcher"/>.
    /// </summary>
    /// <param name="stream">A stream containing the Tiled <c>.tsx</c> tileset content.</param>
    /// <param name="baseUri">
    /// The URI the tileset was fetched from (e.g. <c>https://example.com/tiles/tiles.tsx</c>).
    /// Relative image sources declared by the tileset are resolved against it.
    /// </param>
    /// <param name="fetcher">Asynchronously fetches the raw bytes of a resolved asset URI.</param>
    /// <returns>A task that resolves to the loaded <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The tileset has no image or the image could not be decoded.</exception>
    /// <remarks>
    /// This is the asynchronous counterpart of <see cref="Load(Stream, Uri, TiledAssetFetcher)"/>
    /// for streams and asset fetchers that only support asynchronous I/O (e.g. certain
    /// network/browser streams). The TSX content is read with <c>StreamReader.ReadToEndAsync()</c>
    /// and the image is fetched with <c>await fetcher(...)</c>, so no synchronous read is
    /// performed on the caller's stream. The caller remains the owner of <paramref name="stream"/>.
    /// </remarks>
    public static async Task<TileSet> LoadAsync(Stream stream, Uri baseUri, TiledAssetFetcherAsync fetcher)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(fetcher);

        var content = await ReadAllTextAsync(stream).ConfigureAwait(false);
        var dotTiledTileset = ParseTilesetContent(content);

        return await FromDotTiledAsync(dotTiledTileset, dotTiledTileset.FirstGID.GetValueOr(0u), async source =>
        {
            var imageUri = new Uri(baseUri, source);
            var bytes = await fetcher(imageUri).ConfigureAwait(false);
            using var imageStream = new MemoryStream(bytes, writable: false);
            return SKBitmap.Decode(imageStream)
                ?? throw new InvalidOperationException(
                    $"Unable to decode tileset image '{imageUri}' for '{dotTiledTileset.Name}'.");
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the image of the tile with the given 0-based <paramref name="localTileId"/>,
    /// cropped from the tileset image at <see cref="TileWidth"/>&#215;<see cref="TileHeight"/>.
    /// </summary>
    /// <param name="localTileId">The 0-based local tile ID within this tileset.</param>
    /// <returns>An independent raster image containing the requested tile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="localTileId"/> is negative or greater than or equal to the number of
    /// tiles in the tileset.
    /// </exception>
    /// <remarks>
    /// The returned <see cref="SKImage"/> owns a copy of its pixels and is disposed by the
    /// caller when it is no longer needed.
    /// </remarks>
    public SKImage GetTileImage(int localTileId)
    {
        if (localTileId < 0 || localTileId >= _tileCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(localTileId),
                localTileId,
                $"Local tile ID must be between 0 and {_tileCount - 1} for tileset '{Name}'.");
        }

        var column = localTileId % _columns;
        var row = localTileId / _columns;
        var left = _margin + ((TileWidth + _spacing) * column);
        var top = _margin + ((TileHeight + _spacing) * row);
        var source = new SKRectI(left, top, left + TileWidth, top + TileHeight);

        // ExtractSubset copies the tile's pixels into a fresh bitmap, and SKImage.FromBitmap
        // copies them again into an image the caller fully owns. We deliberately avoid
        // SKImage.Subset here: on SkiaSharp 3.119.4, subsets of an image decoded from encoded
        // data crash the native runtime once an earlier subset has been disposed.
        var tileBitmap = new SKBitmap(TileWidth, TileHeight);
        try
        {
            if (!_sourceImage.ExtractSubset(tileBitmap, source))
            {
                throw new InvalidOperationException(
                    $"Failed to extract tile {localTileId} from tileset '{Name}'.");
            }

            return SKImage.FromBitmap(tileBitmap);
        }
        finally
        {
            tileBitmap.Dispose();
        }
    }

    /// <summary>
    /// Creates a <see cref="TileSet"/> from a DotTiled <see cref="Tileset"/>, decoding its
    /// image with <paramref name="imageDecoder"/>.
    /// </summary>
    /// <param name="tileset">The parsed DotTiled tileset.</param>
    /// <param name="firstGid">The first global tile ID of the tileset within its map.</param>
    /// <param name="imageDecoder">
    /// Decodes the image of the tileset given its raw <c>source</c> string as declared by the
    /// tileset. The caller resolves the source (e.g. relative to a directory or a URI) and
    /// returns the decoded bitmap; the resolution base is owned by the caller.
    /// </param>
    /// <returns>A fully decoded <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tileset"/> or <paramref name="imageDecoder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The tileset has no image, or the image could not be decoded.
    /// </exception>
    internal static TileSet FromDotTiled(Tileset tileset, uint firstGid, Func<string, SKBitmap> imageDecoder)
    {
        ArgumentNullException.ThrowIfNull(tileset);
        ArgumentNullException.ThrowIfNull(imageDecoder);

        if (!tileset.Image.HasValue)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name}' has no image; image-collection tilesets are not supported.");
        }

        var source = tileset.Image.Value.Source.GetValueOr(string.Empty);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException($"Tileset '{tileset.Name}' does not declare an image source.");
        }

        var sourceImage = imageDecoder(source)
            ?? throw new InvalidOperationException($"Unable to decode the image for tileset '{tileset.Name}'.");

        return new TileSet(
            tileset.Name,
            firstGid,
            tileset.TileWidth,
            tileset.TileHeight,
            sourceImage,
            tileset.TileCount,
            tileset.Columns,
            tileset.Spacing,
            tileset.Margin);
    }

    /// <summary>
    /// Asynchronously creates a <see cref="TileSet"/> from a DotTiled <see cref="Tileset"/>,
    /// decoding its image with <paramref name="imageDecoderAsync"/>. Shares the validation of
    /// <see cref="FromDotTiled"/>: a tileset with no image, an empty image source, or an image
    /// that cannot be decoded throws the same exceptions.
    /// </summary>
    /// <param name="tileset">The parsed DotTiled tileset.</param>
    /// <param name="firstGid">The first global tile ID of the tileset within its map.</param>
    /// <param name="imageDecoderAsync">
    /// Asynchronously decodes the image of the tileset given its raw <c>source</c> string as
    /// declared by the tileset. The caller resolves the source (e.g. relative to a URI) and
    /// returns the decoded bitmap; the resolution base is owned by the caller.
    /// </param>
    /// <returns>A task that resolves to a fully decoded <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tileset"/> or <paramref name="imageDecoderAsync"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The tileset has no image, or the image could not be decoded.
    /// </exception>
    internal static async Task<TileSet> FromDotTiledAsync(
        Tileset tileset,
        uint firstGid,
        Func<string, Task<SKBitmap>> imageDecoderAsync)
    {
        ArgumentNullException.ThrowIfNull(tileset);
        ArgumentNullException.ThrowIfNull(imageDecoderAsync);

        if (!tileset.Image.HasValue)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name}' has no image; image-collection tilesets are not supported.");
        }

        var source = tileset.Image.Value.Source.GetValueOr(string.Empty);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException($"Tileset '{tileset.Name}' does not declare an image source.");
        }

        var sourceImage = await imageDecoderAsync(source).ConfigureAwait(false);
        if (sourceImage is null)
        {
            throw new InvalidOperationException($"Unable to decode the image for tileset '{tileset.Name}'.");
        }

        return new TileSet(
            tileset.Name,
            firstGid,
            tileset.TileWidth,
            tileset.TileHeight,
            sourceImage,
            tileset.TileCount,
            tileset.Columns,
            tileset.Spacing,
            tileset.Margin);
    }

    private static Tileset ParseTilesetContent(string content)
    {
        using var reader = new TilesetReader(
            content,
            externalTilesetResolver: source => throw new NotSupportedException(
                $"External tileset '{source}' is not supported when loading a standalone tileset."),
            externalTemplateResolver: source => throw new NotSupportedException(
                $"External template '{source}' is not supported when loading a standalone tileset."),
            customTypeResolver: _ => Optional.Empty);
        return reader.ReadTileset();
    }

    private static string ReadAllText(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static async Task<string> ReadAllTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
