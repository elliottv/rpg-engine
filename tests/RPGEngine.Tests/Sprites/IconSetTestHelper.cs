using System.IO.Compression;
using System.Text;
using SkiaSharp;

namespace RPGEngine.Tests.Sprites;

/// <summary>
/// Generates icon-set fixtures: PNG images divided into 32×32 tiles, each tile filled with a
/// unique opaque color, so pixel-level tests can tell exactly which icon was drawn. The color
/// scheme mirrors <c>CharacterTestHelper.CellColor</c>: the <c>(rows, cols, iconIndex)</c>
/// triple is encoded into RGB.
/// </summary>
/// <remarks>
/// <para>
/// The color scheme guarantees global uniqueness across <c>(rows, cols, iconIndex)</c>: the red
/// channel uniquely identifies the <c>(rows, cols)</c> pair, the green channel uniquely
/// identifies the icon index within a set (so two different icons of the same set never share a
/// color, and two sets with different dimensions never share an icon color either).
/// </para>
/// <para>
/// <see cref="CreateIconSetPng(int,int)"/> always produces a valid <c>rows × cols</c> grid; the
/// <c>(width, height)</c> overload additionally supports arbitrary dimensions (including zero) so
/// the loader's <see cref="ArgumentException"/> validation path can be exercised.
/// </para>
/// </remarks>
internal static class IconSetTestHelper
{
    /// <summary>The normative icon tile size in pixels (matches <c>IconSet.TileSize</c>).</summary>
    public const int TileSize = 32;

    /// <summary>
    /// Returns the unique opaque color used for the tile at <paramref name="iconIndex"/> of a
    /// <c>rows × cols</c> icon set.
    /// </summary>
    public static SKColor IconColor(int rows, int cols, int iconIndex)
    {
        // R uniquely identifies the (rows, cols) pair, G the icon within the set, B a secondary
        // mix. Distinct (rows, cols, iconIndex) triples always map to distinct colors for the
        // set sizes used by the tests.
        var r = (byte)((rows * 31 + cols * 17 + 1) % 256);
        var g = (byte)((iconIndex * 41) % 256);
        var b = (byte)((rows * 5 + cols * 11 + iconIndex * 23) % 256);
        return new SKColor(r, g, b, alpha: 255);
    }

    /// <summary>
    /// Encodes a <c>rows × cols</c> icon set (a <c>(cols * 32) × (rows * 32)</c> image) as PNG,
    /// where every 32×32 tile is filled with the unique color from
    /// <see cref="IconColor(int,int,int)"/>.
    /// </summary>
    public static byte[] CreateIconSetPng(int rows, int cols)
        => CreateIconSetPngBySize(cols * TileSize, rows * TileSize);

    /// <summary>
    /// Encodes a PNG of the requested dimensions. When the dimensions form a valid 32×32 grid
    /// (positive width and height, each a multiple of <see cref="TileSize"/>), every tile is
    /// filled with the unique per-icon color from <see cref="IconColor(int,int,int)"/> at the
    /// 32×32 tile size; any other size is left transparent (only its dimensions matter for
    /// validation tests).
    /// </summary>
    public static byte[] CreateIconSetPngBySize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            // SkiaSharp cannot encode a zero-dimension image, so hand-build raw PNG bytes that
            // represent those dimensions. They are not decodable (PNG requires positive
            // dimensions), but they let the loader's ArgumentException path be exercised.
            return CreateRawPng(width, height);
        }

        var validGrid = width % TileSize == 0 && height % TileSize == 0;
        var rows = height / TileSize;
        var cols = width / TileSize;

        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);

            if (validGrid)
            {
                for (var row = 0; row < rows; row++)
                {
                    for (var col = 0; col < cols; col++)
                    {
                        var iconIndex = (row * cols) + col;
                        using var paint = new SKPaint { Color = IconColor(rows, cols, iconIndex), IsAntialias = false };
                        canvas.DrawRect(
                            new SKRect(col * TileSize, row * TileSize, (col + 1) * TileSize, (row + 1) * TileSize),
                            paint);
                    }
                }
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Returns a read-only stream containing a <c>rows × cols</c> icon set encoded as PNG.</summary>
    public static MemoryStream CreateIconSetStream(int rows, int cols)
        => new(CreateIconSetPng(rows, cols), writable: false);

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
