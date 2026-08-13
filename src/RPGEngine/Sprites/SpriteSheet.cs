using SkiaSharp;

namespace RPGEngine.Sprites;

/// <summary>
/// A single RPG Maker MZ spritesheet: a 576×384 image made of 48×48 cells arranged in a
/// 12-column × 8-row grid. It contains 8 characters laid out as a 4×2 grid; each character
/// occupies a 3×4 block of cells (3 animation frames × 4 directions).
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
    private const int CellsPerRow = 12;
    private const int CellRows = 8;
    private const int CharactersPerRow = 4;
    private const int CharacterRowCount = 2;
    private const int FramesPerCharacter = 3;
    private const int DirectionsPerCharacter = 4;

    /// <summary>The width and height of a single character cell in pixels (normative RPG Maker MZ value).</summary>
    public const int CellSize = 48;

    /// <summary>The width of a full or part spritesheet in pixels (normative RPG Maker MZ value: 12 cells).</summary>
    public const int SheetWidth = CellsPerRow * CellSize; // 576

    /// <summary>The height of a full or part spritesheet in pixels (normative RPG Maker MZ value: 8 cells).</summary>
    public const int SheetHeight = CellRows * CellSize; // 384

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

    /// <summary>Gets the width of a single cell in pixels (always 48).</summary>
    public int CellWidth => CellSize;

    /// <summary>Gets the height of a single cell in pixels (always 48).</summary>
    public int CellHeight => CellSize;

    /// <summary>Gets the number of characters contained in the sheet (always 8).</summary>
    public int CharacterCount => CharactersPerRow * CharacterRowCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpriteSheet"/> class. Sheets are created
    /// through <see cref="SpriteSheetManager"/>.
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet.</param>
    /// <param name="type">The kind of sheet (full or part).</param>
    /// <param name="partType">The character layer for part sheets, or <see langword="null"/> for full sheets.</param>
    /// <param name="source">The decoded sheet image, already validated as 576×384.</param>
    internal SpriteSheet(string name, SpriteSheetType type, CharacterPartType? partType, SKImage source)
    {
        Name = name;
        Type = type;
        PartType = partType;
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Returns the 48×48 sprite at (<paramref name="direction"/>, <paramref name="frame"/>) for
    /// the character <paramref name="characterIndex"/> (1-based) within the sheet.
    /// </summary>
    /// <param name="characterIndex">The 1-based index (1..8) of the character in the sheet.</param>
    /// <param name="direction">The direction (down/left/right/up) the sprite faces.</param>
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
    /// <c>charRow * 4 + (int)direction</c>.
    /// <para>
    /// The returned image is an independent 48×48 raster crop of the decoded source, produced
    /// with nearest-neighbour sampling (a 1:1 pixel copy, never a re-encode). We deliberately
    /// avoid <c>SKImage.Subset</c> here: on SkiaSharp 3.119.4, subsets of an image decoded from
    /// encoded data crash the native runtime once an earlier subset has been disposed (the same
    /// reasoning as <c>TileSet.GetTileImage</c>).
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
        var row = (charRow * DirectionsPerCharacter) + (int)direction;

        var source = new SKRectI(
            col * CellSize,
            row * CellSize,
            (col + 1) * CellSize,
            (row + 1) * CellSize);

        // Raster crop of the decoded source with nearest-neighbour sampling (1:1 pixel copy, no
        // re-encode). SKImage.FromBitmap copies the pixels, so the returned image is independent
        // of _source and of the temporary bitmap disposed below.
        var spriteBitmap = new SKBitmap(CellSize, CellSize);
        try
        {
            using var canvas = new SKCanvas(spriteBitmap);
            canvas.Clear(SKColors.Transparent);

            var destination = new SKRect(0, 0, CellSize, CellSize);
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
