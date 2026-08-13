using System.Text;
using RPGEngine.Sprites;
using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for <see cref="GameEngine"/> (story 3): the root object that owns the
/// player, NPCs, map, configuration and asset registries, and exposes the
/// <c>Update</c>/<c>Render</c>/<c>Input</c>/<c>LoadSpriteSheet</c>/<c>LoadTileSet</c> API used
/// by the host game loop.
/// </summary>
public class GameEngineTests
{
    private const double FrameDt = 1.0 / 60;
    private const int CellSize = 48;
    private const int StandingFrame = 1; // the frame a fresh/stopped character renders at

    // ---------------------------------------------------------------------
    // Acceptance 1: Input → movement. Pressing W and calling Update(1/60)
    // for 60 frames with BaseSpeed = 96 moves the player up ~96 px; releasing
    // the key stops the movement.
    // ---------------------------------------------------------------------
    /// <summary>Verifies holding W for one second at 96 px/s moves the player up ~96 px, and releasing the key stops the movement.</summary>
    [Fact]
    public void Input_UpKeyForOneSecond_MovesPlayerUpByBaseSpeed()
    {
        var engine = new GameEngine();
        engine.Player.Character.BaseSpeed = 96;
        engine.Player.Position = new Position(200, 200);

        engine.Input(Key.W, true);
        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // Moved straight up for one second: -96 px on Y, X unchanged.
        Assert.Equal(200, engine.Player.Position.X, precision: 6);
        Assert.Equal(200 - 96, engine.Player.Position.Y, precision: 6);
        Assert.Equal(Direction.Up, engine.Player.Direction);

        // Releasing the key stops the movement on the next update.
        var yAfterMove = engine.Player.Position.Y;
        engine.Input(Key.W, false);
        engine.Update(FrameDt);
        Assert.Equal(yAfterMove, engine.Player.Position.Y, precision: 6);
    }

    // ---------------------------------------------------------------------
    // Acceptance 2: the engine reads the configuration at input time, so
    // rebinding UpKey to Z makes Z move up and W do nothing.
    // ---------------------------------------------------------------------
    /// <summary>Verifies that rebinding UpKey to Z makes Z move the player up while W no longer moves it.</summary>
    [Fact]
    public void Config_RebindUpKeyToZ_UsesZAndIgnoresW()
    {
        var engine = new GameEngine();
        engine.Config.UpKey = Key.Z;
        engine.Player.Position = new Position(100, 100);

        // Z is now the up key: it moves the player up.
        engine.Input(Key.Z, true);
        engine.Update(FrameDt);
        Assert.True(engine.Player.Position.Y < 100);
        Assert.Equal(100, engine.Player.Position.X, precision: 6);

        // W is no longer bound to any direction: pressing it does not move.
        engine.Input(Key.Z, false);
        var yAfterRelease = engine.Player.Position.Y;
        engine.Input(Key.W, true);
        engine.Update(FrameDt);
        Assert.Equal(yAfterRelease, engine.Player.Position.Y, precision: 6);
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: the camera follows the player and clamps inside the map.
    // With a 10×10 (48px) map and a 240×240 canvas the origin is (0,0) at the
    // top-left corner, (PixelWidth - 240, PixelHeight - 240) at the bottom-right
    // corner, and centers the player in the middle. Verified via rendering.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the camera origin clamps to the map bounds and centers the player, asserted through rendered output.</summary>
    [Fact]
    public void Camera_ClampsToMapBounds_AndCentersPlayer()
    {
        const int canvasSize = 240;
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 pixels
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Top-left corner: origin (0,0), the player sprite is at screen (0,0).
        engine.Player.Position = new Position(0, 0);
        Assert.Equal(new Position(0, 0), engine.ComputeCameraOrigin(canvasSize, canvasSize));
        using (var bitmap = Render(engine, canvasSize, canvasSize))
        {
            AssertSpriteColor(bitmap, topLeftX: 0, topLeftY: 0, seed: 1, characterIndex: 1);
            // A map region with no sprite over it is still drawn (solid red tile).
            Assert.NotEqual(0, bitmap.GetPixel(200, 200).Alpha);
        }

        // Bottom-right corner: origin clamped to (PixelWidth - 240, PixelHeight - 240).
        engine.Player.Position = new Position(480 - CellSize, 480 - CellSize);
        Assert.Equal(new Position(240, 240), engine.ComputeCameraOrigin(canvasSize, canvasSize));
        using (var bitmap = Render(engine, canvasSize, canvasSize))
        {
            AssertSpriteColor(bitmap, topLeftX: 192, topLeftY: 192, seed: 1, characterIndex: 1);
        }

        // Middle: origin = player - canvas/2 (no clamping), so the player is centered.
        engine.Player.Position = new Position(216, 216);
        Assert.Equal(new Position(96, 96), engine.ComputeCameraOrigin(canvasSize, canvasSize));
        using (var bitmap = Render(engine, canvasSize, canvasSize))
        {
            AssertSpriteColor(bitmap, topLeftX: 120, topLeftY: 120, seed: 1, characterIndex: 1);
        }
    }

    // ---------------------------------------------------------------------
    // Acceptance 4: the render pipeline draws the map, each NPC and the player
    // on top; replacing the map changes the output; adding/removing characters
    // is reflected immediately.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the full render pipeline (map + NPC + player), that replacing the map changes the output, and that adding/removing characters is reflected.</summary>
    [Fact]
    public void Render_MapPlayerAndNpc_AllLayersPresent()
    {
        const int canvasSize = 240;
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 red tiles
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0, 0);

        var npc = new Character { Position = new Position(96, 96) };
        using (var npcStream = CharacterTestHelper.CreateSheetStream(2))
        {
            engine.LoadSpriteSheet("npc", npcStream);
        }
        npc.SpriteSheets.Add(new SpriteSheetRef("npc", CharacterIndex: 2));
        engine.Characters.Add(npc);

        using var bitmap = Render(engine, canvasSize, canvasSize);

        // Map pixels present in a region no sprite covers.
        Assert.NotEqual(0, bitmap.GetPixel(200, 200).Alpha);
        // Player drawn at screen (0,0).
        Assert.Equal(CharacterTestHelper.SpriteColor(1, 1, Direction.Down, StandingFrame), bitmap.GetPixel(24, 24));
        // NPC drawn at screen (96,96).
        Assert.Equal(CharacterTestHelper.SpriteColor(2, 2, Direction.Down, StandingFrame), bitmap.GetPixel(120, 120));

        // Replacing the map changes the output: a 2×2 (96×96) map leaves (200,200) transparent.
        using (var smallFixture = CreateFilledMapFixture(2, 2))
        {
            engine.Map = TileMap.Load(smallFixture.MapPath);
            using var replaced = Render(engine, canvasSize, canvasSize);
            Assert.Equal(0, replaced.GetPixel(200, 200).Alpha);
            Assert.NotEqual(0, bitmap.GetPixel(200, 200).Alpha);
        }

        // Removing the NPC removes its pixels; re-adding restores them.
        engine.Characters.Remove(npc);
        using (var withoutNpc = Render(engine, canvasSize, canvasSize))
        {
            Assert.NotEqual(CharacterTestHelper.SpriteColor(2, 2, Direction.Down, StandingFrame), withoutNpc.GetPixel(120, 120));
        }

        engine.Characters.Add(npc);
        using (var withNpc = Render(engine, canvasSize, canvasSize))
        {
            Assert.Equal(CharacterTestHelper.SpriteColor(2, 2, Direction.Down, StandingFrame), withNpc.GetPixel(120, 120));
        }
    }

    // ---------------------------------------------------------------------
    // Acceptance 5: the player is never in the Characters list.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the player's character is never present in the Characters list, even after NPCs are added.</summary>
    [Fact]
    public void Characters_NeverContainsPlayerCharacter()
    {
        var engine = new GameEngine();

        Assert.DoesNotContain(engine.Player.Character, engine.Characters);

        engine.Characters.Add(new Character());
        engine.Characters.Add(new Character());

        Assert.Equal(2, engine.Characters.Count);
        Assert.DoesNotContain(engine.Player.Character, engine.Characters);
    }

    // ---------------------------------------------------------------------
    // Acceptance 6a: LoadSpriteSheet registers by unique name; a duplicate
    // throws InvalidOperationException.
    // ---------------------------------------------------------------------
    /// <summary>Verifies LoadSpriteSheet rejects a duplicate name with InvalidOperationException.</summary>
    [Fact]
    public void LoadSpriteSheet_DuplicateName_ThrowsInvalidOperationException()
    {
        var engine = new GameEngine();
        using (var stream = CharacterTestHelper.CreateSheetStream(0))
        {
            engine.LoadSpriteSheet("hero", stream);
        }

        using var duplicate = CharacterTestHelper.CreateSheetStream(1);
        Assert.Throws<InvalidOperationException>(() => engine.LoadSpriteSheet("hero", duplicate));
    }

    // ---------------------------------------------------------------------
    // Acceptance 6b: LoadTileSet registers by unique name (path overload); a
    // duplicate throws InvalidOperationException.
    // ---------------------------------------------------------------------
    /// <summary>Verifies LoadTileSet (path overload) rejects a duplicate name with InvalidOperationException.</summary>
    [Fact]
    public void LoadTileSet_DuplicateName_ThrowsInvalidOperationException()
    {
        using var fixture = new TiledTestFixture(2, 2, new[] { FilledLayer(2, 2) });
        var engine = new GameEngine();

        engine.LoadTileSet("tiles", fixture.TilesetPath);

        Assert.Throws<InvalidOperationException>(() => engine.LoadTileSet("tiles", fixture.TilesetPath));
    }

    // ---------------------------------------------------------------------
    // Acceptance 6c: LoadTileSet (stream overload) registers by unique name; a
    // duplicate throws InvalidOperationException.
    // ---------------------------------------------------------------------
    /// <summary>Verifies LoadTileSet (stream overload) registers a TSX and rejects a duplicate name with InvalidOperationException.</summary>
    [Fact]
    public void LoadTileSet_FromStream_RegistersAndRejectsDuplicate()
    {
        using var fixture = new TiledTestFixture(2, 2, new[] { FilledLayer(2, 2) });
        var engine = new GameEngine();

        // The fixture TSX references "tiles.png" relative to its own directory; the stream
        // overload resolves images against the current directory, so rewrite the source to an
        // absolute path.
        var tsx = File.ReadAllText(fixture.TilesetPath)
            .Replace("source=\"tiles.png\"", $"source=\"{fixture.ImagePath}\"");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(tsx));
        engine.LoadTileSet("tiles", stream);

        using var duplicate = new MemoryStream(Encoding.UTF8.GetBytes(tsx));
        Assert.Throws<InvalidOperationException>(() => engine.LoadTileSet("tiles", duplicate));
    }

    // ---------------------------------------------------------------------
    // Acceptance 6d: Render uses the loaded spritesheets — a character
    // configured with a loaded sheet name and a valid character index (1..8)
    // renders non-empty. A character index outside 1..8 is rejected.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a player configured with a loaded sheet and a valid character index renders non-empty pixels.</summary>
    [Fact]
    public void Render_CharacterWithLoadedSheetAndValidIndex_RendersNonEmpty()
    {
        var engine = new GameEngine();
        using (var stream = CharacterTestHelper.CreateSheetStream(0))
        {
            engine.LoadSpriteSheet("hero", stream);
        }
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        using var bitmap = Render(engine, CellSize, CellSize);
        Assert.NotEqual(0, bitmap.GetPixel(24, 24).Alpha);
    }

    /// <summary>Verifies a SpriteSheetRef with a character index outside 1..8 is rejected at render time.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Render_InvalidCharacterIndex_ThrowsArgumentOutOfRangeException(int characterIndex)
    {
        var engine = new GameEngine();
        using (var stream = CharacterTestHelper.CreateSheetStream(0))
        {
            engine.LoadSpriteSheet("hero", stream);
        }
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", characterIndex));

        using var bitmap = new SKBitmap(CellSize, CellSize);
        using var canvas = new SKCanvas(bitmap);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Render(canvas, FrameDt));
    }

    // ---------------------------------------------------------------------
    // Additional coverage: the documented last-pressed-wins priority.
    // ---------------------------------------------------------------------
    /// <summary>Verifies that when two movement keys are held the most recently pressed one wins, and releasing it reverts to the other.</summary>
    [Fact]
    public void Update_LastPressedKeyWins()
    {
        var engine = new GameEngine();
        engine.Player.Position = new Position(100, 100);

        engine.Input(Key.W, true); // up
        engine.Input(Key.D, true); // right — most recently pressed
        engine.Update(FrameDt);
        Assert.True(engine.Player.Position.X > 100);
        Assert.Equal(100, engine.Player.Position.Y, precision: 6);

        // Releasing right reverts to up (still held).
        engine.Input(Key.D, false);
        engine.Update(FrameDt);
        Assert.True(engine.Player.Position.Y < 100);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>Creates a map fixture filled with red tiles in a single "ground" layer.</summary>
    private static TiledTestFixture CreateFilledMapFixture(int width, int height)
        => new(width, height, new[] { FilledLayer(width, height) });

    /// <summary>Builds a fully filled single-layer spec for a map of the given size.</summary>
    private static TileLayerSpec FilledLayer(int width, int height)
        => new("ground", Enumerable.Repeat(1u, width * height).ToArray());

    /// <summary>Loads a seeded full sheet under the name "hero" and configures the player to use it.</summary>
    private static void ConfigurePlayerSprite(GameEngine engine, int seed)
    {
        using var stream = CharacterTestHelper.CreateSheetStream(seed);
        engine.LoadSpriteSheet("hero", stream);
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
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

    /// <summary>Asserts the centre pixel of a 48×48 sprite at the given top-left is the expected seeded color.</summary>
    private static void AssertSpriteColor(SKBitmap bitmap, int topLeftX, int topLeftY, int seed, int characterIndex)
    {
        var expected = CharacterTestHelper.SpriteColor(seed, characterIndex, Direction.Down, StandingFrame);
        Assert.Equal(expected, bitmap.GetPixel(topLeftX + (CellSize / 2), topLeftY + (CellSize / 2)));
    }
}
