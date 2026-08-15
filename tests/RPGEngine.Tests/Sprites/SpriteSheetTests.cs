using RPGEngine.Sprites;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests.Sprites;

/// <summary>
/// Acceptance tests for <see cref="SpriteSheet"/> and <see cref="SpriteSheetManager"/>
/// (story 10: RPG Maker MZ spritesheets — full sheets &amp; part sheets).
/// </summary>
public class SpriteSheetTests
{
    // ---------------------------------------------------------------------
    // Acceptance 1: a test sheet whose cells are uniquely colored by (row, col)
    // slices correctly; GetSprite(1, Down, 0) is the exact expected crop and
    // GetSprite(8, Up, 2) spot-checks the last character.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a full sheet slices to the exact expected crop for the first and last characters.</summary>
    [Fact]
    public void GetSprite_ReturnsExactCrop_ForFirstAndLastCharacter()
    {
        var manager = new SpriteSheetManager();
        using var stream = SpriteSheetTestHelper.CreateSheetStream();
        var sheet = manager.Load("hero", stream);

        // Normative full-sheet metadata.
        Assert.Equal(SpriteSheetType.Full, sheet.Type);
        Assert.Null(sheet.PartType);
        Assert.Equal(48, sheet.CellWidth);
        Assert.Equal(48, sheet.CellHeight);
        Assert.Equal(8, sheet.CharacterCount);

        // Character 1, down, frame 0 → cell (row 0, col 0).
        using (var sprite = sheet.GetSprite(1, Direction.Down, 0))
        {
            AssertCell(sprite, row: 0, col: 0);
        }

        // Character 8, up, frame 2 → charCol = 3, charRow = 1 → cell (row 7, col 11).
        using (var sprite = sheet.GetSprite(8, Direction.Up, 2))
        {
            AssertCell(sprite, row: 7, col: 11);
        }
    }

    /// <summary>Verifies the four direction rows map to rows 0..3 within a character block.</summary>
    [Theory]
    [InlineData(Direction.Down, 0)]
    [InlineData(Direction.Left, 1)]
    [InlineData(Direction.Right, 2)]
    [InlineData(Direction.Up, 3)]
    public void GetSprite_MapsDirectionsToRows(Direction direction, int expectedRow)
    {
        var manager = new SpriteSheetManager();
        using var stream = SpriteSheetTestHelper.CreateSheetStream();
        var sheet = manager.Load("hero", stream);

        using var sprite = sheet.GetSprite(1, direction, frame: 0);
        AssertCell(sprite, expectedRow, col: 0);
    }

    // ---------------------------------------------------------------------
    // Acceptance 2 & 7: a part sheet is also 576×384 with CharacterCount == 8,
    // reports Type == Part and round-trips PartType; GetSprite(3, ...) works
    // (the index selects the character within the part sheet).
    // ---------------------------------------------------------------------
    /// <summary>Verifies a part sheet reports part metadata and slices by character index.</summary>
    [Fact]
    public void LoadPart_ReportsPartMetadata_AndSlicesByCharacterIndex()
    {
        var manager = new SpriteSheetManager();
        using var stream = SpriteSheetTestHelper.CreateSheetStream();
        var sheet = manager.LoadPart("armour", stream, CharacterPartType.Armour);

        Assert.Equal(SpriteSheetType.Part, sheet.Type);
        Assert.Equal(CharacterPartType.Armour, sheet.PartType);
        Assert.Equal(8, sheet.CharacterCount);

        // Character 3, down, frame 0 → charCol = 2, charRow = 0 → cell (row 0, col 6).
        using var sprite = sheet.GetSprite(3, Direction.Down, frame: 0);
        AssertCell(sprite, row: 0, col: 6);
    }

    /// <summary>Verifies every character part type round-trips through LoadPart and PartType.</summary>
    [Theory]
    [InlineData(CharacterPartType.Body)]
    [InlineData(CharacterPartType.Armour)]
    [InlineData(CharacterPartType.Face)]
    [InlineData(CharacterPartType.FaceHair)]
    [InlineData(CharacterPartType.Hair1)]
    [InlineData(CharacterPartType.Hair2)]
    [InlineData(CharacterPartType.Head)]
    public void LoadPart_RoundTripsPartType(CharacterPartType partType)
    {
        var manager = new SpriteSheetManager();
        using var stream = SpriteSheetTestHelper.CreateSheetStream();
        var sheet = manager.LoadPart("part", stream, partType);

        Assert.Equal(SpriteSheetType.Part, sheet.Type);
        Assert.Equal(partType, sheet.PartType);
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: invalid dimensions throw ArgumentException. The 144×192
    // single-character '$' sheet variant is deliberately not supported.
    // ---------------------------------------------------------------------
    /// <summary>Verifies loading an image whose dimensions are not 576×384 throws ArgumentException.</summary>
    [Theory]
    [InlineData(144, 192)] // the single-character '$' sheet variant (out of scope)
    [InlineData(100, 100)]
    public void Load_ThrowsArgumentException_ForInvalidDimensions(int width, int height)
    {
        var manager = new SpriteSheetManager();
        using var stream = new MemoryStream(
            SpriteSheetTestHelper.CreateSheetPng(width, height),
            writable: false);

        Assert.Throws<ArgumentException>(() => manager.Load("bad", stream));
    }

    // ---------------------------------------------------------------------
    // Acceptance 4: GetSprite rejects characterIndex outside 1..8 and frames
    // outside 0..2 with ArgumentOutOfRangeException.
    // ---------------------------------------------------------------------
    /// <summary>Verifies GetSprite throws for character indices outside the 1..8 range.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(9)]
    public void GetSprite_ThrowsArgumentOutOfRange_ForInvalidCharacterIndex(int characterIndex)
    {
        var manager = new SpriteSheetManager();
        using var stream = SpriteSheetTestHelper.CreateSheetStream();
        var sheet = manager.Load("hero", stream);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => sheet.GetSprite(characterIndex, Direction.Down, frame: 0));
    }

    /// <summary>Verifies GetSprite throws for animation frames outside the 0..2 range.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void GetSprite_ThrowsArgumentOutOfRange_ForInvalidFrame(int frame)
    {
        var manager = new SpriteSheetManager();
        using var stream = SpriteSheetTestHelper.CreateSheetStream();
        var sheet = manager.Load("hero", stream);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => sheet.GetSprite(1, Direction.Down, frame));
    }

    // ---------------------------------------------------------------------
    // Acceptance 5: registering a duplicate name throws InvalidOperationException.
    // ---------------------------------------------------------------------
    /// <summary>Verifies loading a second sheet under an already-registered name throws InvalidOperationException.</summary>
    [Fact]
    public void Load_DuplicateName_ThrowsInvalidOperationException()
    {
        var manager = new SpriteSheetManager();
        using (var stream = SpriteSheetTestHelper.CreateSheetStream())
        {
            manager.Load("hero", stream);
        }

        using var duplicate = SpriteSheetTestHelper.CreateSheetStream();
        Assert.Throws<InvalidOperationException>(() => manager.Load("hero", duplicate));
    }

    // ---------------------------------------------------------------------
    // Acceptance 6: Get on an unknown name throws KeyNotFoundException.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Get throws KeyNotFoundException for a name that was never loaded.</summary>
    [Fact]
    public void Get_UnknownName_ThrowsKeyNotFoundException()
    {
        var manager = new SpriteSheetManager();

        Assert.Throws<KeyNotFoundException>(() => manager.Get("missing"));
    }

    // ---------------------------------------------------------------------
    // Additional manager behaviour: the path overload, Contains, and that
    // names are trimmed but case-sensitive.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the path overload loads a sheet and Contains reports it.</summary>
    [Fact]
    public void Load_FromPath_AndContains_Work()
    {
        var manager = new SpriteSheetManager();
        var path = WriteTempPng(SpriteSheetTestHelper.CreateSheetPng());

        try
        {
            var sheet = manager.Load("hero", path);

            Assert.Equal("hero", sheet.Name);
            Assert.True(manager.Contains("hero"));
            Assert.False(manager.Contains("nope"));
            Assert.Same(sheet, manager.Get("hero"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies names are trimmed on registration but still case-sensitive.</summary>
    [Fact]
    public void Load_TrimsNames_ButIsCaseSensitive()
    {
        var manager = new SpriteSheetManager();
        using (var stream = SpriteSheetTestHelper.CreateSheetStream())
        {
            var sheet = manager.Load("  Hero  ", stream);
            Assert.Equal("Hero", sheet.Name);
        }

        Assert.True(manager.Contains("Hero"));
        Assert.False(manager.Contains("hero")); // case-sensitive lookup

        // "Hero" is now registered, so a trimmed variant of the same name collides.
        using var duplicate = SpriteSheetTestHelper.CreateSheetStream();
        Assert.Throws<InvalidOperationException>(() => manager.Load(" Hero ", duplicate));
    }

    // ---------------------------------------------------------------------
    // Async loading (story 22): LoadAsync / LoadPartAsync must never perform a
    // synchronous read of the caller's stream, so they are exercised against a
    // non-seekable, read-async-only stream.
    // ---------------------------------------------------------------------
    /// <summary>Verifies LoadAsync loads a full sheet from a non-seekable, read-async-only stream (proves no synchronous read of the caller's stream).</summary>
    [Fact]
    public async Task LoadAsync_SucceedsFromAsyncOnlyStream()
    {
        var manager = new SpriteSheetManager();
        using var stream = new AsyncOnlyStream(SpriteSheetTestHelper.CreateSheetStream());

        var sheet = await manager.LoadAsync("hero", stream);

        Assert.Equal("hero", sheet.Name);
        Assert.Equal(SpriteSheetType.Full, sheet.Type);
        Assert.Null(sheet.PartType);

        using var sprite = sheet.GetSprite(1, Direction.Down, 0);
        AssertCell(sprite, row: 0, col: 0);
    }

    /// <summary>Verifies LoadPartAsync round-trips every character part type.</summary>
    [Theory]
    [InlineData(CharacterPartType.Body)]
    [InlineData(CharacterPartType.Armour)]
    [InlineData(CharacterPartType.Face)]
    [InlineData(CharacterPartType.FaceHair)]
    [InlineData(CharacterPartType.Hair1)]
    [InlineData(CharacterPartType.Hair2)]
    [InlineData(CharacterPartType.Head)]
    public async Task LoadPartAsync_RoundTripsPartType(CharacterPartType partType)
    {
        var manager = new SpriteSheetManager();
        using var stream = new AsyncOnlyStream(SpriteSheetTestHelper.CreateSheetStream());

        var sheet = await manager.LoadPartAsync("part", stream, partType);

        Assert.Equal(SpriteSheetType.Part, sheet.Type);
        Assert.Equal(partType, sheet.PartType);
    }

    /// <summary>Verifies the async path keeps the duplicate-name failure mode (InvalidOperationException) and fails without touching the stream.</summary>
    [Fact]
    public async Task LoadAsync_DuplicateName_ThrowsInvalidOperationException()
    {
        var manager = new SpriteSheetManager();
        using (var stream = new AsyncOnlyStream(SpriteSheetTestHelper.CreateSheetStream()))
        {
            await manager.LoadAsync("hero", stream);
        }

        using var duplicate = new AsyncOnlyStream(SpriteSheetTestHelper.CreateSheetStream());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.LoadAsync("hero", duplicate));
    }

    /// <summary>Verifies the async path keeps the invalid-dimensions failure mode (ArgumentException).</summary>
    [Theory]
    [InlineData(144, 192)] // the single-character '$' sheet variant (out of scope)
    [InlineData(100, 100)]
    public async Task LoadAsync_ThrowsArgumentException_ForInvalidDimensions(int width, int height)
    {
        var manager = new SpriteSheetManager();
        using var stream = new AsyncOnlyStream(new MemoryStream(
            SpriteSheetTestHelper.CreateSheetPng(width, height),
            writable: false));

        await Assert.ThrowsAsync<ArgumentException>(() => manager.LoadAsync("bad", stream));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>Asserts the sprite is a 48×48 crop whose every pixel has the expected cell color.</summary>
    private static void AssertCell(SKImage sprite, int row, int col)
    {
        Assert.Equal(48, sprite.Width);
        Assert.Equal(48, sprite.Height);

        var expected = SpriteSheetTestHelper.CellColor(row, col);
        using var bitmap = new SKBitmap(48, 48);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(sprite, new SKPoint(0, 0));
        }

        for (var y = 0; y < 48; y++)
        {
            for (var x = 0; x < 48; x++)
            {
                Assert.Equal(expected, bitmap.GetPixel(x, y));
            }
        }
    }

    /// <summary>Writes a PNG to a temporary file and returns its path.</summary>
    private static string WriteTempPng(byte[] png)
    {
        var path = Path.Combine(Path.GetTempPath(), "rpg-engine-sprite-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, png);
        return path;
    }
}
