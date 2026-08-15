using SkiaSharp;

namespace RPGEngine.Tests;

/// <summary>
/// Generates RPG Maker MZ spritesheet fixtures for the <see cref="CharacterTests"/>.
/// Each sheet is identified by a <em>seed</em> so that different sheets (different character
/// parts) get globally distinct cell colors; within a sheet, every cell (at the sheet's derived
/// size) is uniquely
/// colored by (row, column). This lets pixel-level composition tests tell which part ended up
/// on top.
/// </summary>
/// <remarks>
/// <para>
/// The color scheme guarantees global uniqueness across (seed, row, col): the red channel
/// encodes the seed (seeds 0..6 map to distinct red values), the green channel encodes the
/// column and the blue channel encodes the row-major cell index.
/// </para>
/// <para>
/// A sheet can also be generated fully transparent (the <c>transparent</c> flag of
/// <see cref="CreateSheetPng(int,bool)"/>). Transparent sheets are used for the head part in the
/// <c>$</c>-rule tests: they let the hair layer show through at the checked pixel while the
/// head is still present in the list (and its <c>$</c> rule still applies), so the test can
/// observe whether hair was drawn or skipped.
/// </para>
/// </remarks>
internal static class CharacterTestHelper
{
    public const int SheetWidth = 576;
    public const int SheetHeight = 384;
    public const int CellSize = 48;
    public const int Columns = 12;
    public const int Rows = 8;

    /// <summary>
    /// Returns the unique opaque color used for the cell at (row, col) of the sheet identified
    /// by <paramref name="seed"/>.
    /// </summary>
    public static SKColor CellColor(int seed, int row, int col)
    {
        // R encodes the seed, G the column, B the row-major cell index. Distinct (seed, row, col)
        // triples always map to distinct colors for the seeds used by the tests.
        var r = (byte)((seed * 37) % 256);
        var g = (byte)col;
        var b = (byte)((row * Columns) + col);
        return new SKColor(r, g, b, alpha: 255);
    }

    /// <summary>
    /// Encodes a 576×384 sheet as PNG. When <paramref name="transparent"/> is
    /// <see langword="true"/> every cell is fully transparent; otherwise each 48×48 cell is
    /// filled with the unique color from <see cref="CellColor(int, int, int)"/>.
    /// </summary>
    public static byte[] CreateSheetPng(int seed, bool transparent = false)
        => CreateSheetPng(seed, SheetWidth, SheetHeight, transparent);

    /// <summary>
    /// Encodes a sheet of the requested dimensions as PNG. The grid is always the normative
    /// 12×8 layout, so the cell size is derived from the image (e.g. 48×48 for 576×384,
    /// 78×108 for 936×864). When <paramref name="transparent"/> is <see langword="true"/>
    /// every cell is fully transparent; otherwise each cell is filled with the unique color from
    /// <see cref="CellColor(int, int, int)"/> at the derived cell size.
    /// </summary>
    public static byte[] CreateSheetPng(int seed, int width, int height, bool transparent = false)
    {
        var cellWidth = width / Columns;
        var cellHeight = height / Rows;

        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);

            if (!transparent)
            {
                for (var row = 0; row < Rows; row++)
                {
                    for (var col = 0; col < Columns; col++)
                    {
                        using var paint = new SKPaint { Color = CellColor(seed, row, col), IsAntialias = false };
                        canvas.DrawRect(
                            new SKRect(col * cellWidth, row * cellHeight, (col + 1) * cellWidth, (row + 1) * cellHeight),
                            paint);
                    }
                }
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Returns a read-only stream containing a 576×384 sheet encoded as PNG.</summary>
    public static MemoryStream CreateSheetStream(int seed, bool transparent = false)
        => CreateSheetStream(seed, SheetWidth, SheetHeight, transparent);

    /// <summary>Returns a read-only stream containing a sheet of the requested dimensions encoded as PNG.</summary>
    public static MemoryStream CreateSheetStream(int seed, int width, int height, bool transparent = false)
        => new(CreateSheetPng(seed, width, height, transparent), writable: false);

    /// <summary>
    /// Computes the sheet cell (row, col) that <c>SpriteSheet.GetSprite</c> crops for the given
    /// 1-based character index, direction and animation frame (mirrors the sheet's slicing
    /// contract so tests can predict the exact color).
    /// </summary>
    public static (int Row, int Col) CellFor(int characterIndex, Direction direction, int frame)
    {
        var charCol = (characterIndex - 1) % 4;
        var charRow = (characterIndex - 1) / 4;
        var col = (charCol * 3) + frame;
        var row = (charRow * 4) + (int)direction;
        return (row, col);
    }

    /// <summary>
    /// Returns the expected opaque color of the cell that a part or full sheet with the given
    /// <paramref name="seed"/> would produce for (characterIndex, direction, frame).
    /// </summary>
    public static SKColor SpriteColor(int seed, int characterIndex, Direction direction, int frame)
    {
        var (row, col) = CellFor(characterIndex, direction, frame);
        return CellColor(seed, row, col);
    }
}
