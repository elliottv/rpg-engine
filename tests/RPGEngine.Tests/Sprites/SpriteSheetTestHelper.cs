using System.IO.Compression;
using System.Text;
using SkiaSharp;

namespace RPGEngine.Tests.Sprites;

/// <summary>
/// Generates RPG Maker MZ spritesheet fixtures: images whose cells form a 12×8 grid and are
/// uniquely colored by (row, column), so tests can assert exact crops without shipping binary
/// assets. The standard 576×384 sheet yields 48×48 cells; arbitrary valid sizes such as
/// 936×864 yield 78×108 cells.
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
    /// Encodes a PNG of the requested dimensions. When the dimensions form a valid 12×8 grid
    /// (positive width divisible by <see cref="Columns"/> and positive height divisible by
    /// <see cref="Rows"/>), every cell is filled with the unique per-cell color from
    /// <see cref="CellColor(int,int)"/> at the derived cell size (e.g. 48×48 for 576×384,
    /// 78×108 for 936×864); any other size is left transparent (only its dimensions matter
    /// for validation tests).
    /// </summary>
    public static byte[] CreateSheetPng(int width = SheetWidth, int height = SheetHeight)
    {
        if (width <= 0 || height <= 0)
        {
            // SkiaSharp cannot encode a zero-dimension image, so hand-build raw PNG bytes that
            // represent those dimensions. They are not decodable (PNG requires positive
            // dimensions), but they let the loader's ArgumentException path be exercised.
            return CreateRawPng(width, height);
        }

        var cellWidth = width / Columns;
        var cellHeight = height / Rows;
        var validGrid = width % Columns == 0 && height % Rows == 0;

        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);

            if (validGrid)
            {
                for (var row = 0; row < Rows; row++)
                {
                    for (var col = 0; col < Columns; col++)
                    {
                        using var paint = new SKPaint { Color = CellColor(row, col), IsAntialias = false };
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

    /// <summary>Returns a read-only stream containing a standard 576×384 sheet encoded as PNG.</summary>
    public static MemoryStream CreateSheetStream() => new(CreateSheetPng(), writable: false);

    /// <summary>
    /// Hand-builds raw PNG bytes with the requested (possibly zero) dimensions without going
    /// through the SkiaSharp encoder, using 8-bit RGBA (color type 6) and valid chunk CRCs.
    /// </summary>
    private static byte[] CreateRawPng(int width, int height)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, width);
        WriteBigEndian(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        ms.Write(Chunk("IHDR", ihdr), 0, 4 + 4 + 13 + 4);

        // One scanline per row: a filter byte (0 = none) plus the row's pixel bytes. With a zero
        // width the scanlines carry only the filter bytes; with a zero height IDAT is empty.
        var raw = new List<byte>();
        var rowBytes = checked(width * 4);
        for (var y = 0; y < height; y++)
        {
            raw.Add(0);
            for (var x = 0; x < rowBytes; x++)
            {
                raw.Add(0);
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(raw.ToArray(), 0, raw.Count);
        }

        var idat = Chunk("IDAT", compressed.ToArray());
        ms.Write(idat, 0, idat.Length);

        var iend = Chunk("IEND", Array.Empty<byte>());
        ms.Write(iend, 0, iend.Length);
        return ms.ToArray();
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        using var ms = new MemoryStream();
        WriteBigEndian(ms, data.Length);
        ms.Write(typeBytes, 0, 4);
        ms.Write(data, 0, data.Length);
        WriteBigEndian(ms, (int)Crc32(typeBytes.Concat(data).ToArray()));
        return ms.ToArray();
    }

    private static void WriteBigEndian(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
            }
        }
        return crc ^ 0xFFFFFFFF;
    }
}
