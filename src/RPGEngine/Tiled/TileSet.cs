using DotTiled;
using SkiaSharp;

namespace RPGEngine.Tiled;

/// <summary>
/// A single Tiled tileset: a grid of tiles cut from one image, together with the
/// global tile ID (<see cref="FirstGid"/>) at which the tileset starts within a map.
/// </summary>
/// <remarks>
/// <para>
/// Instances are created by <see cref="TileSetManager"/> (for standalone tilesets
/// loaded from <c>.tsx</c> files) or by <see cref="TileMap.Load"/> (for the tilesets
/// referenced by a map). The constructor is internal; use one of those entry points.
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
    /// local tile ID 0). For standalone tilesets registered through
    /// <see cref="TileSetManager"/> this is <c>0</c> because the tileset is not part of a map.
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
    /// Returns the image of the tile with the given 0-based <paramref name="localTileId"/>,
    /// cropped from the tileset image at <see cref="TileWidth"/>×<see cref="TileHeight"/>.
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
    /// Creates a <see cref="TileSet"/> from a DotTiled <see cref="Tileset"/> whose image is
    /// resolved relative to <paramref name="imageBaseDirectory"/> and decoded with SkiaSharp.
    /// </summary>
    /// <param name="tileset">The parsed DotTiled tileset.</param>
    /// <param name="firstGid">The first global tile ID of the tileset within its map.</param>
    /// <param name="imageBaseDirectory">
    /// The directory used to resolve the tileset image <c>source</c> when it is relative.
    /// </param>
    /// <returns>A fully decoded <see cref="TileSet"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// The tileset has no image, or the image could not be decoded.
    /// </exception>
    /// <exception cref="FileNotFoundException">The tileset image file does not exist.</exception>
    internal static TileSet FromDotTiled(Tileset tileset, uint firstGid, string imageBaseDirectory)
    {
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

        var imagePath = Path.IsPathRooted(source)
            ? source
            : Path.Combine(imageBaseDirectory, source);

        var sourceImage = SKBitmap.Decode(imagePath)
            ?? throw new FileNotFoundException(
                $"Unable to load tileset image for '{tileset.Name}'.", imagePath);

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
}
