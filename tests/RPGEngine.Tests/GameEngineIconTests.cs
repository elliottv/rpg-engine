using RPGEngine.Sprites;
using RPGEngine.Tests.Sprites;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for the engine's icon-set API (story 70): <c>GameEngine.LoadIconSet</c> /
/// <c>LoadIconSetAsync</c> and <c>Character.IconIndex</c> rendering. The icon-set fixtures are
/// PNGs divided into 32×32 tiles, each tile filled with a unique color (see
/// <see cref="IconSetTestHelper"/>); the sprite fixtures are the seeded full sheets of
/// <see cref="CharacterTestHelper"/>.
/// </summary>
/// <remarks>
/// All rendering tests place a 48×48 sprite at a known feet anchor so the icon's expected pixel
/// position is exact: with the player at (2.5, 3.0) tiles and no map (48 px tiles), the feet are
/// at (120, 144), the sprite's top-left at (96, 96) and the 32×32 icon's top-left at (104, 64),
/// so the sprite center is (120, 120) and the icon center is (120, 80).
/// </remarks>
public class GameEngineIconTests
{
    private const double FrameDt = 1.0 / 60;
    private const int CellSize = 48;
    private const int StandingFrame = 1;
    private const int CanvasSize = 240;

    // The player's anchor and the derived pixel positions used by the rendering assertions.
    private static readonly Position PlayerPosition = new(2.5, 3.0);
    private const int PlayerSpriteCenterX = 120;
    private const int PlayerSpriteCenterY = 120;
    private const int PlayerIconCenterX = 120;
    private const int PlayerIconCenterY = 80;

    // ---------------------------------------------------------------------
    // Acceptance 8: each LoadIconSet overload stores the set (proven by
    // rendering); async via AsyncOnlyStream.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies the path, stream and async (AsyncOnlyStream) overloads each store the set, proven
    /// by rendering the loaded icon's color above the player's sprite.
    /// </summary>
    [Fact]
    public async Task LoadIconSet_ByPath_ByStream_Async_AllLoad()
    {
        var expected = IconSetTestHelper.IconColor(rows: 2, cols: 3, iconIndex: 0);

        // By path.
        var path = WriteTempPng(IconSetTestHelper.CreateIconSetPng(rows: 2, cols: 3));
        try
        {
            var byPath = new GameEngine();
            byPath.LoadIconSet(path);
            ConfigurePlayer(byPath);
            byPath.Player.Character.IconIndex = 0;
            AssertIconRendered(byPath, expected);
        }
        finally
        {
            File.Delete(path);
        }

        // By stream.
        var byStream = new GameEngine();
        using (var stream = IconSetTestHelper.CreateIconSetStream(rows: 2, cols: 3))
        {
            byStream.LoadIconSet(stream);
        }

        ConfigurePlayer(byStream);
        byStream.Player.Character.IconIndex = 0;
        AssertIconRendered(byStream, expected);

        // Async via an async-only stream (proves no synchronous read of the caller's stream).
        var byAsync = new GameEngine();
        using (var stream = new AsyncOnlyStream(IconSetTestHelper.CreateIconSetStream(rows: 2, cols: 3)))
        {
            await byAsync.LoadIconSetAsync(stream);
        }

        ConfigurePlayer(byAsync);
        byAsync.Player.Character.IconIndex = 0;
        AssertIconRendered(byAsync, expected);
    }

    // ---------------------------------------------------------------------
    // Acceptance 9: a subsequent load replaces the previous set.
    // ---------------------------------------------------------------------
    /// <summary>Verifies loading a second icon set replaces the first: rendering uses set B's colors.</summary>
    [Fact]
    public void LoadIconSet_ReplacesPreviousSet()
    {
        var engine = new GameEngine();

        // Set A: 96×64 (3 columns × 2 rows).
        using (var streamA = IconSetTestHelper.CreateIconSetStream(rows: 2, cols: 3))
        {
            engine.LoadIconSet(streamA);
        }

        ConfigurePlayer(engine);
        engine.Player.Character.IconIndex = 0;
        var colorA = IconSetTestHelper.IconColor(rows: 2, cols: 3, iconIndex: 0);
        AssertIconRendered(engine, colorA);

        // Set B: 64×96 (2 columns × 3 rows) — different dimensions, so the same icon index 0
        // maps to a different color.
        using (var streamB = IconSetTestHelper.CreateIconSetStream(rows: 3, cols: 2))
        {
            engine.LoadIconSet(streamB);
        }

        var colorB = IconSetTestHelper.IconColor(rows: 3, cols: 2, iconIndex: 0);
        Assert.NotEqual(colorA, colorB);
        AssertIconRendered(engine, colorB);
    }

    // ---------------------------------------------------------------------
    // Acceptance 10: invalid input throws.
    // ---------------------------------------------------------------------
    /// <summary>Verifies null path/stream throw ArgumentNullException and a non-32-grid PNG throws ArgumentException.</summary>
    [Fact]
    public async Task LoadIconSet_InvalidInput_Throws()
    {
        var engine = new GameEngine();

        // null path / null stream.
        Assert.Throws<ArgumentNullException>(() => engine.LoadIconSet((string)null!));
        Assert.Throws<ArgumentNullException>(() => engine.LoadIconSet((Stream)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => engine.LoadIconSetAsync(null!));

        // A PNG whose dimensions are not a 32×32 grid.
        using var bad = new MemoryStream(IconSetTestHelper.CreateIconSetPngBySize(width: 33, height: 32), writable: false);
        Assert.Throws<ArgumentException>(() => engine.LoadIconSet(bad));
    }

    // ---------------------------------------------------------------------
    // Acceptance 11: rendering pixel tests.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies a player with a full sheet and IconIndex = i renders the icon tile's unique color
    /// at the icon's center, above the sprite's color at the sprite center.
    /// </summary>
    [Fact]
    public void Render_CharacterWithIconIndex_DrawsIconAboveSprite()
    {
        var engine = new GameEngine();
        ConfigurePlayer(engine);
        engine.Player.Character.IconIndex = 0;
        using (var iconStream = IconSetTestHelper.CreateIconSetStream(rows: 2, cols: 3))
        {
            engine.LoadIconSet(iconStream);
        }

        using var bitmap = Render(engine, CanvasSize, CanvasSize);

        var expectedIcon = IconSetTestHelper.IconColor(rows: 2, cols: 3, iconIndex: 0);
        Assert.Equal(expectedIcon, bitmap.GetPixel(PlayerIconCenterX, PlayerIconCenterY));

        var expectedSprite = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, StandingFrame);
        Assert.Equal(expectedSprite, bitmap.GetPixel(PlayerSpriteCenterX, PlayerSpriteCenterY));
    }

    /// <summary>
    /// Regression: with IconIndex null the pixel above the sprite is not the icon color and the
    /// sprite still renders.
    /// </summary>
    [Fact]
    public void Render_CharacterWithoutIconIndex_DrawsNoIcon()
    {
        var engine = new GameEngine();
        ConfigurePlayer(engine);
        using (var iconStream = IconSetTestHelper.CreateIconSetStream(rows: 2, cols: 3))
        {
            engine.LoadIconSet(iconStream);
        }

        using var bitmap = Render(engine, CanvasSize, CanvasSize);

        var iconColor = IconSetTestHelper.IconColor(rows: 2, cols: 3, iconIndex: 0);
        Assert.NotEqual(iconColor, bitmap.GetPixel(PlayerIconCenterX, PlayerIconCenterY));

        var expectedSprite = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, StandingFrame);
        Assert.Equal(expectedSprite, bitmap.GetPixel(PlayerSpriteCenterX, PlayerSpriteCenterY));
    }

    /// <summary>Verifies two NPCs with different IconIndex values each render their own icon color above their own sprite.</summary>
    [Fact]
    public void Render_TwoCharacters_DifferentIcons()
    {
        var engine = new GameEngine();
        using (var iconStream = IconSetTestHelper.CreateIconSetStream(rows: 2, cols: 3))
        {
            engine.LoadIconSet(iconStream);
        }

        var npc1 = ConfigureNpc(engine, "npc1", seed: 2, characterIndex: 1, position: new Position(1.5, 2.0));
        npc1.IconIndex = 0;
        var npc2 = ConfigureNpc(engine, "npc2", seed: 3, characterIndex: 2, position: new Position(3.5, 2.0));
        npc2.IconIndex = 1;
        engine.Characters.Add(npc1);
        engine.Characters.Add(npc2);

        using var bitmap = Render(engine, CanvasSize, CanvasSize);

        // NPC1 at (1.5, 2.0): feet (72, 96), sprite top-left (48, 48), icon top-left (56, 16),
        // icon center (72, 32), sprite center (72, 72).
        var icon1 = IconSetTestHelper.IconColor(rows: 2, cols: 3, iconIndex: 0);
        Assert.Equal(icon1, bitmap.GetPixel(72, 32));
        Assert.Equal(CharacterTestHelper.SpriteColor(2, 1, Direction.Down, StandingFrame), bitmap.GetPixel(72, 72));

        // NPC2 at (3.5, 2.0): feet (168, 96), sprite top-left (144, 48), icon top-left (152, 16),
        // icon center (168, 32), sprite center (168, 72).
        var icon2 = IconSetTestHelper.IconColor(rows: 2, cols: 3, iconIndex: 1);
        Assert.NotEqual(icon1, icon2);
        Assert.Equal(icon2, bitmap.GetPixel(168, 32));
        Assert.Equal(CharacterTestHelper.SpriteColor(3, 2, Direction.Down, StandingFrame), bitmap.GetPixel(168, 72));
    }

    /// <summary>Verifies a non-null IconIndex with no icon set loaded throws InvalidOperationException at Render.</summary>
    [Fact]
    public void Render_CharacterWithIconIndex_NoIconSetLoaded_Throws()
    {
        var engine = new GameEngine();
        ConfigurePlayer(engine);
        engine.Player.Character.IconIndex = 0;

        using var bitmap = new SKBitmap(CanvasSize, CanvasSize);
        using var canvas = new SKCanvas(bitmap);
        Assert.Throws<InvalidOperationException>(() => engine.Render(canvas, FrameDt));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>Loads a seeded full sheet under the name "hero" and configures the player to use it.</summary>
    private static void ConfigurePlayer(GameEngine engine)
    {
        using var stream = CharacterTestHelper.CreateSheetStream(seed: 1);
        engine.LoadSpriteSheet("hero", stream);
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
        engine.Player.Position = PlayerPosition;
    }

    /// <summary>Loads a seeded full sheet under the given name and returns a new NPC character configured to use it.</summary>
    private static Character ConfigureNpc(
        GameEngine engine,
        string sheetName,
        int seed,
        int characterIndex,
        Position position)
    {
        using var stream = CharacterTestHelper.CreateSheetStream(seed);
        engine.LoadSpriteSheet(sheetName, stream);
        var npc = new Character { Position = position };
        npc.SpriteSheets.Add(new SpriteSheetRef(sheetName, CharacterIndex: characterIndex));
        return npc;
    }

    /// <summary>Renders the engine into a fresh transparent bitmap of the requested size.</summary>
    private static SKBitmap Render(GameEngine engine, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.Render(canvas, FrameDt);
        }

        return bitmap;
    }

    /// <summary>
    /// Renders the engine and asserts the player's icon center pixel is the expected icon color
    /// (proving the loaded set is in effect).
    /// </summary>
    private static void AssertIconRendered(GameEngine engine, SKColor expectedIcon)
    {
        using var bitmap = Render(engine, CanvasSize, CanvasSize);
        Assert.Equal(expectedIcon, bitmap.GetPixel(PlayerIconCenterX, PlayerIconCenterY));
    }

    /// <summary>Writes a PNG to a temporary file and returns its path.</summary>
    private static string WriteTempPng(byte[] png)
    {
        var path = Path.Combine(Path.GetTempPath(), "rpg-engine-iconset-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, png);
        return path;
    }
}
