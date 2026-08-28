using RPGEngine.Sprites;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests.Sprites;

/// <summary>
/// Acceptance tests for <see cref="IconSet"/> (story 70: icon sets loaded into the engine and
/// displayed above character sprites). The fixtures are PNGs divided into 32×32 tiles, each
/// tile filled with a unique color encoding <c>(rows, cols, iconIndex)</c> (see
/// <see cref="IconSetTestHelper"/>), so pixel-level assertions can tell exactly which icon was
/// sliced.
/// </summary>
public class IconSetTests
{
    // ---------------------------------------------------------------------
    // Acceptance 1: a 96-wide × 64-tall PNG derives 3 columns × 2 rows (rows
    // are the vertical count = height/32, columns the horizontal count =
    // width/32).
    // ---------------------------------------------------------------------
    /// <summary>Verifies a 96×64 PNG derives ColumnCount == 3, RowCount == 2 and Count == 6.</summary>
    [Fact]
    public void Load_96x64_DerivesRowsAndColumns()
    {
        using var stream = new MemoryStream(IconSetTestHelper.CreateIconSetPng(rows: 2, cols: 3), writable: false);
        var set = IconSet.Load(stream);

        Assert.Equal(3, set.ColumnCount); // width / 32 = 96 / 32
        Assert.Equal(2, set.RowCount);    // height / 32 = 64 / 32
        Assert.Equal(6, set.Count);       // RowCount * ColumnCount
    }

    // ---------------------------------------------------------------------
    // Acceptance 2: GetIcon uses the corrected row-major formula
    // row = iconIndex / ColumnCount, col = iconIndex % ColumnCount. For the
    // 3-column × 2-row set, index i returns the tile at (row = i / 3, col = i % 3).
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies GetIcon slices the correct 32×32 tile for every index 0..5 of a 3-column × 2-row
    /// set, proving index 1 is to the right of index 0 (across a row) and indices wrap to the
    /// next row at index 3.
    /// </summary>
    [Fact]
    public void GetIcon_UsesRowMajorFormula()
    {
        const int rows = 2;
        const int cols = 3;

        using var stream = new MemoryStream(IconSetTestHelper.CreateIconSetPng(rows, cols), writable: false);
        var set = IconSet.Load(stream);

        for (var iconIndex = 0; iconIndex < set.Count; iconIndex++)
        {
            using var icon = set.GetIcon(iconIndex);

            // The tile is 32×32 and every pixel carries the unique color of tile
            // (row = iconIndex / cols, col = iconIndex % cols). If the implementation used the
            // (incorrect) column-major formula the colors would not match: index 1 must be the
            // second tile of the top row (to the right of index 0), not the tile below index 0.
            var expectedRow = iconIndex / cols;
            var expectedCol = iconIndex % cols;
            var expected = IconSetTestHelper.IconColor(rows, cols, iconIndex);

            Assert.Equal(IconSetTestHelper.TileSize, icon.Width);
            Assert.Equal(IconSetTestHelper.TileSize, icon.Height);

            using var bitmap = new SKBitmap(IconSetTestHelper.TileSize, IconSetTestHelper.TileSize);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawImage(icon, new SKPoint(0, 0));
            }

            for (var y = 0; y < IconSetTestHelper.TileSize; y++)
            {
                for (var x = 0; x < IconSetTestHelper.TileSize; x++)
                {
                    Assert.Equal(expected, bitmap.GetPixel(x, y));
                }
            }

            // Sanity: the same index on a column-major layout would land in a different cell, so
            // the expected color itself proves the row-major ordering (the fixture fills tile
            // (row, col) with IconColor(rows, cols, row * cols + col)).
            _ = expectedRow;
            _ = expectedCol;
        }
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: GetIcon rejects indices outside 0..Count-1.
    // ---------------------------------------------------------------------
    /// <summary>Verifies GetIcon throws ArgumentOutOfRangeException for -1 and Count.</summary>
    [Fact]
    public void GetIcon_OutOfRange_Throws()
    {
        using var stream = new MemoryStream(IconSetTestHelper.CreateIconSetPng(rows: 2, cols: 3), writable: false);
        var set = IconSet.Load(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.GetIcon(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.GetIcon(set.Count));
    }

    // ---------------------------------------------------------------------
    // Acceptance 4: dimensions that are not a positive multiple of 32 throw.
    // ---------------------------------------------------------------------
    /// <summary>Verifies loading an image whose dimensions are not a 32×32 grid throws ArgumentException.</summary>
    [Theory]
    [InlineData(33, 32)]  // width not a multiple of 32
    [InlineData(64, 31)]  // height not a multiple of 32
    [InlineData(0, 32)]   // zero width
    public void Load_NonMultipleOf32_Throws(int width, int height)
    {
        using var stream = new MemoryStream(IconSetTestHelper.CreateIconSetPngBySize(width, height), writable: false);
        Assert.Throws<ArgumentException>(() => IconSet.Load(stream));
    }

    // ---------------------------------------------------------------------
    // Acceptance 5: undecodable bytes throw.
    // ---------------------------------------------------------------------
    /// <summary>Verifies loading garbage bytes throws ArgumentException.</summary>
    [Fact]
    public void Load_Undecodable_Throws()
    {
        using var stream = new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, writable: false);
        Assert.Throws<ArgumentException>(() => IconSet.Load(stream));
    }

    // ---------------------------------------------------------------------
    // Acceptance 6: LoadAsync reads an async-only stream (no synchronous read
    // of the caller's stream) and derives the same rows/columns.
    // ---------------------------------------------------------------------
    /// <summary>Verifies LoadAsync loads a 96×64 set from a non-seekable, read-async-only stream.</summary>
    [Fact]
    public async Task LoadAsync_ReadsAsyncOnlyStream()
    {
        using var stream = new AsyncOnlyStream(
            new MemoryStream(IconSetTestHelper.CreateIconSetPng(rows: 2, cols: 3), writable: false));

        var set = await IconSet.LoadAsync(stream);

        Assert.Equal(3, set.ColumnCount);
        Assert.Equal(2, set.RowCount);
        Assert.Equal(6, set.Count);

        using var icon = set.GetIcon(0);
        Assert.Equal(IconSetTestHelper.TileSize, icon.Width);
        Assert.Equal(IconSetTestHelper.TileSize, icon.Height);
    }

    // ---------------------------------------------------------------------
    // Acceptance 7: GetIcon returns an independent crop that the caller owns;
    // disposing an earlier returned icon does not affect a later one.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies two calls to GetIcon return independent 32×32 images: disposing the first has no
    /// effect on the second (the raster-crop copy, never a shared subset).
    /// </summary>
    [Fact]
    public void GetIcon_ReturnsIndependentCrop()
    {
        using var stream = new MemoryStream(IconSetTestHelper.CreateIconSetPng(rows: 2, cols: 3), writable: false);
        var set = IconSet.Load(stream);

        var first = set.GetIcon(0);
        var second = set.GetIcon(1);

        try
        {
            first.Dispose(); // disposing an earlier crop must not affect later ones

            Assert.Equal(IconSetTestHelper.TileSize, second.Width);
            Assert.Equal(IconSetTestHelper.TileSize, second.Height);

            var expected = IconSetTestHelper.IconColor(rows: 2, cols: 3, iconIndex: 1);
            using var bitmap = new SKBitmap(IconSetTestHelper.TileSize, IconSetTestHelper.TileSize);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawImage(second, new SKPoint(0, 0));
            }

            Assert.Equal(expected, bitmap.GetPixel(0, 0));
        }
        finally
        {
            second.Dispose();
        }
    }
}
