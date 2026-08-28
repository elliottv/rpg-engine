using SkiaSharp;

namespace RPGEngine.Sprites;

/// <summary>
/// A single icon set: an image divided into a 32×32 tile grid from which characters can display
/// a small icon above their sprite (e.g. a quest marker or a status balloon). The number of rows
/// and columns is deduced from the decoded image dimensions (<see cref="RowCount"/> =
/// <c>height / <see cref="TileSize"/></c>, <see cref="ColumnCount"/> = <c>width /
/// <see cref="TileSize"/></c>), so a 96×64 image is a 3-column × 2-row set of 6 icons and a
/// 32×32 image is a 1×1 set of a single icon.
/// </summary>
/// <remarks>
/// <para>
/// This is the icon-set counterpart of <see cref="SpriteSheet"/>: a plain data + slice object
/// that owns the single decoded <see cref="SKImage"/> backing every icon returned by
/// <see cref="GetIcon"/>. The decoded image is never mutated. Instances are created through the
/// <see cref="Load(string)"/>, <see cref="Load(Stream)"/> and <see cref="LoadAsync(Stream)"/>
/// factories (the constructor is internal) and are not <see cref="System.IDisposable"/>; the
/// engine owns the single loaded instance for its lifetime.
/// </para>
/// <para>
/// Icons are addressed with a zero-based index using the <em>row-major</em> formula
/// <c>row = iconIndex / ColumnCount</c>, <c>col = iconIndex % ColumnCount</c> (integer division,
/// i.e. floor). Consecutive indices walk <em>left-to-right across a row first</em>, then wrap to
/// the next row: index 0 is the top-left tile, index 1 is immediately to its <em>right</em>, and
/// a full row is filled before moving down. On a 96×64 set (3 columns × 2 rows), index 4 is the
/// tile at <c>(row = 1, col = 1)</c>. This row-major ordering is the only one that is valid for
/// arbitrary (non-square) sets and matches the engine's existing character-index convention
/// (<c>SpriteSheet.GetSprite</c>: <c>charCol = (index − 1) % 4</c>, <c>charRow = (index − 1) / 4</c>).
/// </para>
/// </remarks>
public sealed class IconSet
{
    /// <summary>The normative icon tile size in pixels.</summary>
    public const int TileSize = 32;

    private readonly SKImage _source;

    /// <summary>
    /// Gets the number of icon rows in the set (<c>source.Height / <see cref="TileSize"/></c>),
    /// i.e. the vertical count of 32×32 tiles.
    /// </summary>
    public int RowCount { get; }

    /// <summary>
    /// Gets the number of icon columns in the set (<c>source.Width / <see cref="TileSize"/></c>),
    /// i.e. the horizontal count of 32×32 tiles.
    /// </summary>
    public int ColumnCount { get; }

    /// <summary>Gets the total number of icons in the set (<see cref="RowCount"/> × <see cref="ColumnCount"/>).</summary>
    public int Count => RowCount * ColumnCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="IconSet"/> class. Sets are created through
    /// the <see cref="Load(string)"/>, <see cref="Load(Stream)"/> and <see cref="LoadAsync(Stream)"/>
    /// factories.
    /// </summary>
    /// <param name="source">The decoded set image, already validated as a 32×32 tile grid.</param>
    /// <param name="rowCount">The derived row count (<c>source.Height / <see cref="TileSize"/></c>).</param>
    /// <param name="columnCount">The derived column count (<c>source.Width / <see cref="TileSize"/></c>).</param>
    internal IconSet(SKImage source, int rowCount, int columnCount)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        RowCount = rowCount;
        ColumnCount = columnCount;
    }

    /// <summary>
    /// Loads the icon set at <paramref name="path"/>: the image is decoded and its dimensions are
    /// validated as a 32×32 tile grid.
    /// </summary>
    /// <param name="path">The path to an image file (PNG or other SkiaSharp-supported format).</param>
    /// <returns>The loaded <see cref="IconSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions do not form a valid 32×32 grid (positive width and height, each a multiple of
    /// <see cref="TileSize"/>).
    /// </exception>
    public static IconSet Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Trim().Length == 0)
        {
            throw new ArgumentException("Icon set path must not be empty after trimming.", nameof(path));
        }

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>
    /// Loads the icon set from <paramref name="stream"/>: the image is decoded and its dimensions
    /// are validated as a 32×32 tile grid. This is the file-system-free entry point (e.g.
    /// WebAssembly builds where assets are fetched over HTTP).
    /// </summary>
    /// <param name="stream">A stream containing the encoded image (PNG or other SkiaSharp-supported format).</param>
    /// <returns>The loaded <see cref="IconSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The image cannot be decoded, or its dimensions do not form a valid 32×32 grid (positive
    /// width and height, each a multiple of <see cref="TileSize"/>).
    /// </exception>
    /// <remarks>
    /// The caller remains the owner of <paramref name="stream"/>; it is not disposed here.
    /// </remarks>
    public static IconSet Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var image = Decode(stream, "<stream>");
        return new IconSet(image, image.Height / TileSize, image.Width / TileSize);
    }

    /// <summary>
    /// Asynchronously loads the icon set from <paramref name="stream"/>. This is the asynchronous
    /// counterpart of <see cref="Load(Stream)"/> for streams that only support asynchronous reads
    /// (e.g. certain network/browser streams).
    /// </summary>
    /// <param name="stream">A stream containing the encoded image (PNG or other SkiaSharp-supported format).</param>
    /// <returns>A task that resolves to the loaded <see cref="IconSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The image cannot be decoded, or its dimensions do not form a valid 32×32 grid (positive
    /// width and height, each a multiple of <see cref="TileSize"/>).
    /// </exception>
    /// <remarks>
    /// The stream is copied into an in-memory buffer asynchronously and decoded from that seekable
    /// buffer with the same validation as the synchronous overload, so no synchronous read is
    /// performed on the caller's stream. The caller remains the owner of <paramref name="stream"/>;
    /// it is not disposed here.
    /// </remarks>
    public static async Task<IconSet> LoadAsync(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(false);
        buffer.Position = 0;

        return Load(buffer);
    }

    /// <summary>
    /// Returns the 32×32 icon at <paramref name="iconIndex"/> within the set.
    /// </summary>
    /// <param name="iconIndex">The zero-based icon index, in <c>0..<see cref="Count"/> - 1</c>.</param>
    /// <returns>
    /// An independent 32×32 <see cref="SKImage"/> cropped from the decoded source. The caller owns
    /// and disposes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="iconIndex"/> is outside <c>0..<see cref="Count"/> - 1</c>.
    /// </exception>
    /// <remarks>
    /// The icon is selected with the <em>row-major</em> formula
    /// <c>row = iconIndex / ColumnCount</c>, <c>col = iconIndex % ColumnCount</c>: consecutive
    /// indices walk left-to-right across a row first, then wrap to the next row (index 1 is to the
    /// right of index 0). On a 96×64 set (3 columns × 2 rows), index 4 is the tile at
    /// <c>(row = 1, col = 1)</c>.
    /// <para>
    /// The returned image is an independent <see cref="TileSize"/>×<see cref="TileSize"/> raster
    /// crop of the decoded source, produced with nearest-neighbour sampling (a 1:1 pixel copy,
    /// never a re-encode). We deliberately avoid <c>SKImage.Subset</c> here: on SkiaSharp 3.119.4,
    /// subsets of an image decoded from encoded data crash the native runtime once an earlier
    /// subset has been disposed (the same reasoning as <see cref="SpriteSheet.GetSprite"/>).
    /// </para>
    /// </remarks>
    public SKImage GetIcon(int iconIndex)
    {
        if (iconIndex < 0 || iconIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iconIndex),
                iconIndex,
                $"Icon index must be between 0 and {Count - 1} for an icon set with {Count} icons.");
        }

        // Row-major slicing: row = iconIndex / ColumnCount, col = iconIndex % ColumnCount.
        var row = iconIndex / ColumnCount;
        var col = iconIndex % ColumnCount;

        var source = new SKRectI(
            col * TileSize,
            row * TileSize,
            (col + 1) * TileSize,
            (row + 1) * TileSize);

        // Raster crop of the decoded source with nearest-neighbour sampling (1:1 pixel copy, no
        // re-encode). SKImage.FromBitmap copies the pixels, so the returned image is independent
        // of _source and of the temporary bitmap disposed below.
        var iconBitmap = new SKBitmap(TileSize, TileSize);
        try
        {
            using var canvas = new SKCanvas(iconBitmap);
            canvas.Clear(SKColors.Transparent);

            var destination = new SKRect(0, 0, TileSize, TileSize);
            var sampling = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
            canvas.DrawImage(_source, source, destination, sampling);

            return SKImage.FromBitmap(iconBitmap);
        }
        finally
        {
            iconBitmap.Dispose();
        }
    }

    /// <summary>
    /// Decodes the image from <paramref name="stream"/> and validates it as a 32×32 tile grid
    /// (mirrors <c>SpriteSheetManager.Decode</c>).
    /// </summary>
    private static SKImage Decode(Stream stream, string sourceDescription)
    {
        var image = SKImage.FromEncodedData(stream)
            ?? throw new ArgumentException($"The image '{sourceDescription}' could not be decoded as an icon set.");

        // Capture the dimensions before any disposal: the decoded image must stay alive while its
        // native properties are read (accessing it inside the exception message after Dispose()
        // would be a native use-after-free).
        var width = image.Width;
        var height = image.Height;
        if (width <= 0 || height <= 0 || width % TileSize != 0 || height % TileSize != 0)
        {
            image.Dispose();
            throw new ArgumentException(
                $"An icon set must be an image divided into {TileSize}×{TileSize} tiles " +
                $"(positive width and height, each a multiple of {TileSize}), but '{sourceDescription}' " +
                $"was {width}×{height}.");
        }

        return image;
    }
}
