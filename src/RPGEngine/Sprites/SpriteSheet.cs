using SkiaSharp;

namespace RPGEngine.Sprites;

/// <summary>
/// A single RPG Maker MZ spritesheet: an image whose cells form a normative 12-column × 8-row
/// grid. The cell size is derived from the image dimensions (<c>width / 12</c> ×
/// <c>height / 8</c>), so the standard 576×384 sheet (48×48 cells) and larger sheets such as a
/// 936×864 sheet (78×108 cells) are both supported. The sheet contains 8 characters laid out
/// as a 4×2 grid; each character occupies a 3×4 block of cells (3 animation frames ×
/// 4 directions).
/// </summary>
/// <remarks>
/// <para>
/// Both full sheets and part sheets use this same layout. The sheet is a plain data + slice
/// object; the composition of part sheets into a complete character belongs to the
/// <c>Character</c> renderer, not here.
/// </para>
/// <para>
/// Instances are created through <see cref="SpriteSheetManager"/> (the constructor is internal)
/// and own the single decoded <see cref="SKImage"/> that backs every sprite returned by
/// <see cref="GetSprite"/>. The decoded image is never mutated.
/// </para>
/// </remarks>
public sealed class SpriteSheet
{
    private const int CharactersPerRow = 4;
    private const int CharacterRowCount = 2;
    private const int FramesPerCharacter = 3;
    private const int DirectionsPerCharacter = 4;

    /// <summary>The normative grid width, in cells.</summary>
    public const int Columns = 12;

    /// <summary>The normative grid height, in cells.</summary>
    public const int Rows = 8;

    private readonly SKImage _source;

    /// <summary>Gets the unique name used to reference the sheet.</summary>
    public string Name { get; }

    /// <summary>Gets the kind of sheet (full or part).</summary>
    public SpriteSheetType Type { get; }

    /// <summary>
    /// Gets the character layer this part sheet provides, or <see langword="null"/> when
    /// <see cref="Type"/> is <see cref="SpriteSheetType.Full"/>.
    /// </summary>
    public CharacterPartType? PartType { get; }

    /// <summary>Gets the width of a single cell in pixels (<c>sheet width / Columns</c>).</summary>
    /// <remarks>
    /// Derived from the decoded image: 48 for a 576×384 sheet, 78 for a 936×864 sheet.
    /// </remarks>
    public int CellWidth { get; }

    /// <summary>Gets the height of a single cell in pixels (<c>sheet height / Rows</c>).</summary>
    /// <remarks>
    /// Derived from the decoded image: 48 for a 576×384 sheet, 108 for a 936×864 sheet.
    /// </remarks>
    public int CellHeight { get; }

    /// <summary>Gets the total sheet width in pixels (<see cref="Columns"/> × <see cref="CellWidth"/>).</summary>
    public int SheetWidth => Columns * CellWidth;

    /// <summary>Gets the total sheet height in pixels (<see cref="Rows"/> × <see cref="CellHeight"/>).</summary>
    public int SheetHeight => Rows * CellHeight;

    /// <summary>Gets the number of characters contained in the sheet (always 8).</summary>
    public int CharacterCount => CharactersPerRow * CharacterRowCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpriteSheet"/> class. Sheets are created
    /// through <see cref="SpriteSheetManager"/>.
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet.</param>
    /// <param name="type">The kind of sheet (full or part).</param>
    /// <param name="partType">The character layer for part sheets, or <see langword="null"/> for full sheets.</param>
    /// <param name="source">The decoded sheet image, already validated as a 12×8 grid.</param>
    /// <param name="cellWidth">The derived cell width (<c>source.Width / Columns</c>).</param>
    /// <param name="cellHeight">The derived cell height (<c>source.Height / Rows</c>).</param>
    internal SpriteSheet(
        string name,
        SpriteSheetType type,
        CharacterPartType? partType,
        SKImage source,
        int cellWidth,
        int cellHeight)
    {
        Name = name;
        Type = type;
        PartType = partType;
        _source = source ?? throw new ArgumentNullException(nameof(source));
        CellWidth = cellWidth;
        CellHeight = cellHeight;
    }

    /// <summary>
    /// Returns the <see cref="CellWidth"/>×<see cref="CellHeight"/> sprite at
    /// (<paramref name="direction"/>, <paramref name="frame"/>) for the character
    /// <paramref name="characterIndex"/> (1-based) within the sheet. For a standard 576×384
    /// sheet this is a 48×48 crop; for a 936×864 sheet it is 78×108.
    /// </summary>
    /// <param name="characterIndex">The 1-based index (1..8) of the character in the sheet.</param>
    /// <param name="direction">The direction the sprite faces. Cardinal directions map to
    /// their own sheet row; diagonal directions map to the side-view row of their horizontal
    /// component (see <see cref="DirectionExtensions.RowIndex"/>).</param>
    /// <param name="frame">The animation frame (0..2).</param>
    /// <returns>
    /// An <see cref="SKImage"/> cropped from the decoded source. The caller owns and disposes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="characterIndex"/> is outside 1..8 or <paramref name="frame"/> is outside 0..2.
    /// </exception>
    /// <remarks>
    /// Character <c>i</c> is located at <c>charCol = (i - 1) % 4</c>, <c>charRow = (i - 1) / 4</c>;
    /// its cell <c>(frame, direction)</c> is at column <c>charCol * 3 + frame</c> and row
    /// <c>charRow * 4 + direction.RowIndex()</c>.
    /// <para>
    /// The returned image is an independent <see cref="CellWidth"/>×<see cref="CellHeight"/>
    /// raster crop of the decoded source, produced with nearest-neighbour sampling (a 1:1 pixel
    /// copy, never a re-encode). We deliberately avoid <c>SKImage.Subset</c> here: on SkiaSharp
    /// 3.119.4, subsets of an image decoded from encoded data crash the native runtime once an
    /// earlier subset has been disposed (the same reasoning as <c>TileSet.GetTileImage</c>).
    /// </para>
    /// </remarks>
    public SKImage GetSprite(int characterIndex, Direction direction, int frame)
    {
        if (characterIndex < 1 || characterIndex > CharacterCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterIndex),
                characterIndex,
                $"Character index must be between 1 and {CharacterCount} for spritesheet '{Name}'.");
        }

        if (frame < 0 || frame >= FramesPerCharacter)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frame),
                frame,
                $"Animation frame must be between 0 and {FramesPerCharacter - 1} for spritesheet '{Name}'.");
        }

        var charCol = (characterIndex - 1) % CharactersPerRow;
        var charRow = (characterIndex - 1) / CharactersPerRow;
        var col = (charCol * FramesPerCharacter) + frame;
        var row = (charRow * DirectionsPerCharacter) + direction.RowIndex();

        var source = new SKRectI(
            col * CellWidth,
            row * CellHeight,
            (col + 1) * CellWidth,
            (row + 1) * CellHeight);

        // Raster crop of the decoded source with nearest-neighbour sampling (1:1 pixel copy, no
        // re-encode). SKImage.FromBitmap copies the pixels, so the returned image is independent
        // of _source and of the temporary bitmap disposed below.
        var spriteBitmap = new SKBitmap(CellWidth, CellHeight);
        try
        {
            using var canvas = new SKCanvas(spriteBitmap);
            canvas.Clear(SKColors.Transparent);

            var destination = new SKRect(0, 0, CellWidth, CellHeight);
            var sampling = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
            canvas.DrawImage(_source, source, destination, sampling);

            return SKImage.FromBitmap(spriteBitmap);
        }
        finally
        {
            spriteBitmap.Dispose();
        }
    }
}
