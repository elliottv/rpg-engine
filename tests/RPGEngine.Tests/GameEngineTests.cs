using RPGEngine.Sprites;
using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for <see cref="GameEngine"/> (story 3): the root object that owns the
/// player, NPCs, map, configuration and spritesheet registry, and exposes the
/// <c>Update</c>/<c>Render</c>/<c>Input</c>/<c>LoadSpriteSheet</c> API used by the host game
/// loop. Tile sets are loaded by the <c>TileMap</c> itself and are not part of the engine's
/// public API.
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

        // Replacing the map changes the output: the 2×2 (96×96) map is now centered on the
        // 240×240 canvas (origin -72,-72), so the player/NPC move to screen (72,72)/(168,168)
        // and the pixel at (200,100) is outside the map and every sprite: it is black (the black
        // background around a smaller map), instead of the red tile of the 10×10 map.
        using (var smallFixture = CreateFilledMapFixture(2, 2))
        {
            engine.Map = TileMap.Load(smallFixture.MapPath);
            using var replaced = Render(engine, canvasSize, canvasSize);
            Assert.Equal(SKColors.Black, replaced.GetPixel(200, 100));
            Assert.NotEqual(0, bitmap.GetPixel(200, 200).Alpha);
        }

        // Removing the NPC removes its pixels; re-adding restores them. On the centered 2×2
        // map the NPC (world 96,96) is at screen (168,168); its centre pixel is (192,192).
        engine.Characters.Remove(npc);
        using (var withoutNpc = Render(engine, canvasSize, canvasSize))
        {
            Assert.NotEqual(CharacterTestHelper.SpriteColor(2, 2, Direction.Down, StandingFrame), withoutNpc.GetPixel(192, 192));
        }

        engine.Characters.Add(npc);
        using (var withNpc = Render(engine, canvasSize, canvasSize))
        {
            Assert.Equal(CharacterTestHelper.SpriteColor(2, 2, Direction.Down, StandingFrame), withNpc.GetPixel(192, 192));
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
    // Acceptance 6: LoadSpriteSheet registers by unique name (duplicate →
    // InvalidOperationException), and Render uses the loaded sheets (a character
    // configured with a loaded sheet name and a valid character index 1..8
    // renders non-empty; an index outside 1..8 is rejected).
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
    // Async loading (story 22): the engine's LoadSpriteSheetAsync /
    // LoadPartSpriteSheetAsync delegate to the sprite sheet manager and must
    // work with streams that only support asynchronous reads.
    // ---------------------------------------------------------------------
    /// <summary>Verifies LoadSpriteSheetAsync registers a sheet so a SpriteSheetRef renders non-empty pixels.</summary>
    [Fact]
    public async Task LoadSpriteSheetAsync_AndSpriteSheetRef_RendersNonEmpty()
    {
        var engine = new GameEngine();
        using (var stream = new AsyncOnlyStream(CharacterTestHelper.CreateSheetStream(0)))
        {
            await engine.LoadSpriteSheetAsync("hero", stream);
        }
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        using var bitmap = Render(engine, CellSize, CellSize);
        Assert.NotEqual(0, bitmap.GetPixel(24, 24).Alpha);
    }

    /// <summary>Verifies LoadPartSpriteSheetAsync registers part sheets that compose into a complete character.</summary>
    [Fact]
    public async Task LoadPartSpriteSheetAsync_ComposesParts()
    {
        var engine = new GameEngine();

        // body (opaque) + hair2 (opaque) + head (fully transparent, name without '$'). The
        // transparent head lets the hair2 layer show through when facing up, proving the parts
        // were actually composed instead of a single sheet being drawn alone.
        using (var bodyStream = new AsyncOnlyStream(CharacterTestHelper.CreateSheetStream(1)))
        {
            await engine.LoadPartSpriteSheetAsync("body", bodyStream, CharacterPartType.Body);
        }
        using (var hairStream = new AsyncOnlyStream(CharacterTestHelper.CreateSheetStream(3)))
        {
            await engine.LoadPartSpriteSheetAsync("hair2", hairStream, CharacterPartType.Hair2);
        }
        using (var headStream = new AsyncOnlyStream(CharacterTestHelper.CreateSheetStream(4, transparent: true)))
        {
            await engine.LoadPartSpriteSheetAsync("head", headStream, CharacterPartType.Head);
        }

        engine.Player.SpriteSheets.Add(new SpriteSheetRef("body", CharacterIndex: 1));
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hair2", CharacterIndex: 1));
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("head", CharacterIndex: 1));

        // Facing up: the per-direction adjustment draws hair2 over the body; the transparent
        // head keeps it visible, so the rendered centre pixel is the hair2 colour.
        engine.Player.Character.Move(Direction.Up, speedFactor: 0);

        using var bitmap = Render(engine, CellSize, CellSize);
        var expected = CharacterTestHelper.SpriteColor(seed: 3, characterIndex: 1, Direction.Up, StandingFrame);
        Assert.Equal(expected, bitmap.GetPixel(CellSize / 2, CellSize / 2));
    }

    // ---------------------------------------------------------------------
    // Additional coverage (story 21): 8-direction vector-combined movement.
    // The movement model is no longer last-pressed-wins: holding W+D moves
    // diagonally, opposite keys cancel, and releasing one key of a held
    // diagonal pair reverts to the remaining cardinal direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies holding W+D for one second at 96 px/s moves diagonally up-right (~±67.88 px per axis) and sets Direction to UpRight; releasing D then moves straight up.</summary>
    [Fact]
    public void Update_HoldingDiagonalPair_MovesDiagonallyAndRevertsOnRelease()
    {
        var engine = new GameEngine();
        engine.Player.Character.BaseSpeed = 96;
        engine.Player.Position = new Position(200, 200);

        engine.Input(Key.W, true);
        engine.Input(Key.D, true);
        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // UpRight = (+√½, -√½); one second at 96 px/s → (±96·√½) ≈ (±67.88) per axis.
        var component = 96 * Math.Sqrt(0.5);
        Assert.Equal(200 + component, engine.Player.Position.X, precision: 6);
        Assert.Equal(200 - component, engine.Player.Position.Y, precision: 6);
        Assert.Equal(Direction.UpRight, engine.Player.Direction);

        // Releasing D reverts to the remaining cardinal direction (straight up).
        var xAfterDiagonal = engine.Player.Position.X;
        engine.Input(Key.D, false);
        engine.Update(FrameDt);
        Assert.True(engine.Player.Position.Y < 200 - component);
        Assert.Equal(xAfterDiagonal, engine.Player.Position.X, precision: 6);
        Assert.Equal(Direction.Up, engine.Player.Direction);
    }

    /// <summary>Verifies holding opposite keys (W+S) produces no movement.</summary>
    [Fact]
    public void Update_OppositeKeysCancel_ProducesNoMovement()
    {
        var engine = new GameEngine();
        engine.Player.Position = new Position(100, 100);

        engine.Input(Key.W, true);
        engine.Input(Key.S, true);
        engine.Update(FrameDt);

        Assert.Equal(new Position(100, 100), engine.Player.Position);
    }

    /// <summary>Verifies diagonal resolution respects config rebinding (Z+D → UpRight after UpKey = Z).</summary>
    [Fact]
    public void Update_DiagonalResolution_RespectsConfigRebinding()
    {
        var engine = new GameEngine();
        engine.Config.UpKey = Key.Z;
        engine.Player.Position = new Position(100, 100);

        engine.Input(Key.Z, true);
        engine.Input(Key.D, true);
        engine.Update(FrameDt);

        Assert.Equal(Direction.UpRight, engine.Player.Direction);
        Assert.True(engine.Player.Position.X > 100);
        Assert.True(engine.Player.Position.Y < 100);
    }

    // ---------------------------------------------------------------------
    // Acceptance (story 23): the player is clamped inside the map using its
    // actual sprite size (78×108 for a 936×864 sheet), verified through
    // ComputeCameraOrigin/rendering.
    // ---------------------------------------------------------------------
    /// <summary>Verifies ClampPlayerToMap uses the player's derived 78×108 sprite size (maxX = PixelWidth - 78, maxY = PixelHeight - 108).</summary>
    [Fact]
    public void ClampPlayerToMap_UsesPlayerSpriteSize()
    {
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        using (var stream = CharacterTestHelper.CreateSheetStream(seed: 1, width: 936, height: 864))
        {
            engine.LoadSpriteSheet("hero", stream);
        }
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        // Beyond the bottom-right corner: clamps to (480 - 78, 480 - 108) = (402, 372).
        engine.Player.Position = new Position(1000, 1000);
        engine.Update(FrameDt);
        Assert.Equal(480 - 78, engine.Player.Position.X, precision: 6);
        Assert.Equal(480 - 108, engine.Player.Position.Y, precision: 6);

        // Negative position clamps to (0, 0).
        engine.Player.Position = new Position(-100, -100);
        engine.Update(FrameDt);
        Assert.Equal(0, engine.Player.Position.X, precision: 6);
        Assert.Equal(0, engine.Player.Position.Y, precision: 6);
    }

    /// <summary>Verifies the 78×108 clamp through ComputeCameraOrigin and rendered output.</summary>
    [Fact]
    public void ClampPlayerToMap_LargeSheet_VerifiedViaCameraAndRender()
    {
        const int canvasSize = 240;
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        using (var stream = CharacterTestHelper.CreateSheetStream(seed: 1, width: 936, height: 864))
        {
            engine.LoadSpriteSheet("hero", stream);
        }
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        engine.Player.Position = new Position(1000, 1000);
        engine.Update(FrameDt); // clamps to (402, 372)

        // The camera origin clamps to the map: maxX = maxY = 480 - 240 = 240.
        Assert.Equal(new Position(240, 240), engine.ComputeCameraOrigin(canvasSize, canvasSize));

        // The 78×108 sprite renders at screen (402 - 240, 372 - 240) = (162, 132).
        using var bitmap = Render(engine, canvasSize, canvasSize);
        var expected = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, StandingFrame);
        Assert.Equal(expected, bitmap.GetPixel(162 + 39, 132 + 54));
    }

    // ---------------------------------------------------------------------
    // Story 24: map centering + black background, and above_player layers.
    // When a map is smaller than the canvas the camera origin becomes negative so the
    // map is centered, and Render clears the surrounding area to black. Tile layers
    // declaring the Tiled above_player property are drawn after the player.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a map smaller than the canvas is centered (negative origin) and the surrounding pixels are black.</summary>
    [Fact]
    public void Camera_MapSmallerThanCanvas_CentersMapWithBlackBackground()
    {
        const int canvasSize = 240;
        using var fixture = CreateFilledMapFixture(2, 2); // 96×96 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        engine.Player.Position = new Position(0, 0);

        // offset = (240 - 96) / 2 = 72 on each axis; the origin is the negative offset.
        Assert.Equal(new Position(-72, -72), engine.ComputeCameraOrigin(canvasSize, canvasSize));

        using var bitmap = Render(engine, canvasSize, canvasSize);

        // The centered 96×96 map occupies (72..168, 72..168); its centre (120,120) is a tile.
        Assert.NotEqual(0, bitmap.GetPixel(120, 120).Alpha);

        // Every pixel outside the map is black (alpha 255, RGB 0).
        var black = new SKColor(0, 0, 0, 255);
        Assert.Equal(black, bitmap.GetPixel(10, 10));
        Assert.Equal(black, bitmap.GetPixel(230, 10));
        Assert.Equal(black, bitmap.GetPixel(10, 230));
        Assert.Equal(black, bitmap.GetPixel(230, 230));
        Assert.Equal(black, bitmap.GetPixel(20, 120));  // left of the map
        Assert.Equal(black, bitmap.GetPixel(220, 120)); // right of the map
        Assert.Equal(black, bitmap.GetPixel(120, 20));  // above the map
        Assert.Equal(black, bitmap.GetPixel(120, 220)); // below the map
    }

    /// <summary>Verifies a map that fills (or exceeds) the canvas keeps the previous follow + clamp camera behavior (offset == 0).</summary>
    [Fact]
    public void Camera_MapFillsCanvas_KeepsFollowAndClampBehavior()
    {
        const int canvasSize = 240;
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // Top-left: origin (0,0).
        engine.Player.Position = new Position(0, 0);
        Assert.Equal(new Position(0, 0), engine.ComputeCameraOrigin(canvasSize, canvasSize));

        // Bottom-right: origin clamped to PixelSize - canvasSize.
        engine.Player.Position = new Position(480 - CellSize, 480 - CellSize);
        Assert.Equal(new Position(240, 240), engine.ComputeCameraOrigin(canvasSize, canvasSize));

        // Middle: origin = player - canvas/2 (follow, no clamping).
        engine.Player.Position = new Position(216, 216);
        Assert.Equal(new Position(96, 96), engine.ComputeCameraOrigin(canvasSize, canvasSize));
    }

    /// <summary>Verifies a tile on an above_player layer is drawn on top of the player, and without the flag the player is on top.</summary>
    [Fact]
    public void Render_AbovePlayerLayer_TileDrawsOverPlayer()
    {
        const int canvasSize = 96; // a 2×2 map exactly fills the canvas, so the origin is (0,0)
        var ground = FilledLayer(2, 2);
        var colors = new[] { SKColors.Red, SKColors.Green };

        // With the flag: the green tile at (0,0) overlaps the player and is drawn on top.
        using (var fixture = new TiledTestFixture(
            2,
            2,
            new[]
            {
                ground,
                new TileLayerSpec(
                    "above",
                    new uint[] { 2, 0, 0, 0 },
                    Properties: new[] { new LayerProperty("above_player", "bool", "true") }),
            },
            tileColors: colors))
        {
            var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
            ConfigurePlayerSprite(engine, seed: 1);
            engine.Player.Position = new Position(0, 0);

            using var bitmap = Render(engine, canvasSize, canvasSize);
            Assert.Equal(new SKColor(0, 128, 0, 255), bitmap.GetPixel(24, 24));
        }

        // Without the flag: the green tile is part of the below-player pass and the player is on top.
        using (var fixture = new TiledTestFixture(
            2,
            2,
            new[]
            {
                ground,
                new TileLayerSpec("above", new uint[] { 2, 0, 0, 0 }),
            },
            tileColors: colors))
        {
            var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
            ConfigurePlayerSprite(engine, seed: 1);
            engine.Player.Position = new Position(0, 0);

            using var bitmap = Render(engine, canvasSize, canvasSize);
            var expected = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, StandingFrame);
            Assert.Equal(expected, bitmap.GetPixel(24, 24));
        }
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
