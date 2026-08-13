using SkiaSharp;

namespace RPGEngine.Tests.Sprites;

/// <summary>
/// Generates RPG Maker MZ spritesheet fixtures: 576×384 images whose 48×48 cells are uniquely
/// colored by (row, column), so tests can assert exact crops without shipping binary assets.
/// </summary>
internal static class SpriteSheetTestHelper
{
    public const int SheetWidth = 576;
    public const int SheetHeight = 384;
    public const int CellSize = 48;
    public const int Columns = 12;
    public const int Rows = 8;

    /// <summary>Returns the unique opaque color used for the cell at (row, col).</summary>
    public static SKColor CellColor(int row, int col)
        => new((byte)col, (byte)row, (byte)((row * Columns) + col), alpha: 255);

    /// <summary>
    /// Encodes a PNG of the requested dimensions. A 576×384 sheet gets the unique per-cell
    /// colors; any other size is left transparent (only its dimensions matter for validation).
    /// </summary>
    public static byte[] CreateSheetPng(int width = SheetWidth, int height = SheetHeight)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);

            if (width == SheetWidth && height == SheetHeight)
            {
                for (var row = 0; row < Rows; row++)
                {
                    for (var col = 0; col < Columns; col++)
                    {
                        using var paint = new SKPaint { Color = CellColor(row, col), IsAntialias = false };
                        canvas.DrawRect(
                            new SKRect(col * CellSize, row * CellSize, (col + 1) * CellSize, (row + 1) * CellSize),
                            paint);
                    }
                }
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Returns a read-only stream containing a standard 576×384 sheet encoded as PNG.</summary>
    public static MemoryStream CreateSheetStream() => new(CreateSheetPng(), writable: false);
}
