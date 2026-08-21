using RPGEngine.Sprites;
using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for <see cref="GameEngine"/> (story 3, updated by story 37: tile-based
/// world coordinates): the root object that owns the player, NPCs, map, configuration and
/// spritesheet registry, and exposes the <c>Update</c>/<c>Render</c>/<c>Input</c>/
/// <c>LoadSpriteSheet</c> API used by the host game loop. All world coordinates are in tiles;
/// pixels are produced only at the canvas boundary.
/// </summary>
public class GameEngineTests
{
    private const double FrameDt = 1.0 / 60;
    private const int CellSize = 48;
    private const int StandingFrame = 1; // the frame a fresh/stopped character renders at

    // ---------------------------------------------------------------------
    // Acceptance 1: Input → movement. Pressing W and calling Update(1/60)
    // for 60 frames with BaseSpeed = 2 moves the player up ~2 tiles; releasing
    // the key stops the movement.
    // ---------------------------------------------------------------------
    /// <summary>Verifies holding W for one second at the default 2 tiles/s moves the player up 2 tiles, and releasing the key stops the movement.</summary>
    [Fact]
    public void Input_UpKeyForOneSecond_MovesPlayerUpByBaseSpeed()
    {
        var engine = new GameEngine();
        engine.Player.Character.BaseSpeed = 2;
        engine.Player.Position = new Position(200, 200);

        engine.Input(Key.W, true);
        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // Moved straight up for one second: -2 tiles on Y, X unchanged.
        Assert.Equal(200, engine.Player.Position.X, precision: 6);
        Assert.Equal(200 - 2, engine.Player.Position.Y, precision: 6);
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
    // Acceptance 3 (story 37): the camera follows the player and clamps inside
    // the map, in tile units. With a 10×10 (48px) map and a 240×240 canvas the
    // origin is (0,0) tiles at the top-left corner, (5,5) tiles at the
    // bottom-right corner (previously (240,240) px), and (2,2) tiles for the
    // player at (4.5,4.5) tiles (previously (216,216) px) — the camera contract
    // is unchanged when re-expressed in tiles. Verified via ComputeCameraOrigin
    // and rendered output.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the camera origin clamps to the map bounds and centers the player, in tile units, asserted through rendered output.</summary>
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

        // Bottom-right corner: origin clamped to (Map.Width - canvas/ts, Map.Height - canvas/ts)
        // = (10 - 5, 10 - 5) = (5, 5) tiles (previously (240, 240) px).
        engine.Player.Position = new Position(9, 9); // 432 px
        Assert.Equal(new Position(5, 5), engine.ComputeCameraOrigin(canvasSize, canvasSize));
        using (var bitmap = Render(engine, canvasSize, canvasSize))
        {
            AssertSpriteColor(bitmap, topLeftX: 192, topLeftY: 192, seed: 1, characterIndex: 1);
        }

        // Middle: origin = player - canvas/(2*ts) (no clamping), so the player is centered.
        engine.Player.Position = new Position(4.5, 4.5); // 216 px
        Assert.Equal(new Position(2, 2), engine.ComputeCameraOrigin(canvasSize, canvasSize));
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

        var npc = new Character { Position = new Position(2, 2) }; // 96 px
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
        // 240×240 canvas (origin -1.5,-1.5 tiles), so the player/NPC move to screen
        // (72,72)/(168,168) and the pixel at (200,100) is outside the map and every sprite: it
        // is black (the black background around a smaller map), instead of the red tile of the
        // 10×10 map.
        using (var smallFixture = CreateFilledMapFixture(2, 2))
        {
            engine.Map = TileMap.Load(smallFixture.MapPath);
            using var replaced = Render(engine, canvasSize, canvasSize);
            Assert.Equal(SKColors.Black, replaced.GetPixel(200, 100));
            Assert.NotEqual(0, bitmap.GetPixel(200, 200).Alpha);
        }

        // Removing the NPC removes its pixels; re-adding restores them. On the centered 2×2
        // map the NPC (world 2,2) is at screen (168,168); its centre pixel is (192,192).
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
    // Acceptance (story 49): the engine's update loop drives autonomous
    // movement started with Character.StartMoving — an NPC added to
    // engine.Characters moves on its own (no map → no clamp).
    // ---------------------------------------------------------------------
    /// <summary>Verifies an NPC started with StartMoving is moved by the engine's Update loop (no map, so no clamping).</summary>
    [Fact]
    public void Update_NpcStartedWithStartMoving_MovesNpc()
    {
        var engine = new GameEngine();
        var npc = new Character { BaseSpeed = 2, Position = new Position(5, 5) };
        npc.StartMoving(Direction.Left);
        engine.Characters.Add(npc);

        engine.Update(dt: 1);

        // No map: no clamping. The NPC moved 2 tiles left, driven by the engine's update loop.
        Assert.Equal(new Position(3, 5), npc.Position);
        Assert.Equal(Direction.Left, npc.Direction);
        Assert.True(npc.IsMoving);

        // The player (no input, no map) is unaffected.
        Assert.Equal(new Position(0, 0), engine.Player.Position);
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
    // Story 34: SpriteSheetExists reports whether a full or part sheet is
    // registered under a name, without touching the render path. The check
    // is case-sensitive, trims surrounding whitespace, and a null name
    // throws ArgumentNullException.
    // ---------------------------------------------------------------------

    /// <summary>Verifies SpriteSheetExists returns false before a full sheet is loaded and true after LoadSpriteSheet.</summary>
    [Fact]
    public void SpriteSheetExists_FullSheet_ReflectsLoadState()
    {
        var engine = new GameEngine();

        Assert.False(engine.SpriteSheetExists("hero"));

        using (var stream = CharacterTestHelper.CreateSheetStream(0))
        {
            engine.LoadSpriteSheet("hero", stream);
        }

        Assert.True(engine.SpriteSheetExists("hero"));
    }

    /// <summary>Verifies SpriteSheetExists returns true after a part sheet is loaded (part sheets share the registry).</summary>
    [Fact]
    public void SpriteSheetExists_PartSheet_ReturnsTrue()
    {
        var engine = new GameEngine();
        using (var stream = CharacterTestHelper.CreateSheetStream(1))
        {
            engine.LoadPartSpriteSheet("hair", stream, CharacterPartType.Hair1);
        }

        Assert.True(engine.SpriteSheetExists("hair"));
    }

    /// <summary>Verifies SpriteSheetExists returns false for a name that was never loaded.</summary>
    [Fact]
    public void SpriteSheetExists_UnknownName_ReturnsFalse()
    {
        var engine = new GameEngine();

        Assert.False(engine.SpriteSheetExists("missing"));
    }

    /// <summary>Verifies the check is case-sensitive: after loading "hero", "Hero" is not found.</summary>
    [Fact]
    public void SpriteSheetExists_DifferentCase_ReturnsFalse()
    {
        var engine = new GameEngine();
        using (var stream = CharacterTestHelper.CreateSheetStream(0))
        {
            engine.LoadSpriteSheet("hero", stream);
        }

        Assert.True(engine.SpriteSheetExists("hero"));
        Assert.False(engine.SpriteSheetExists("Hero"));
    }

    /// <summary>Verifies surrounding whitespace is trimmed before the lookup, matching how sheets are registered.</summary>
    [Fact]
    public void SpriteSheetExists_SurroundingWhitespace_IsTrimmed()
    {
        var engine = new GameEngine();
        using (var stream = CharacterTestHelper.CreateSheetStream(0))
        {
            engine.LoadSpriteSheet("hero", stream);
        }

        Assert.True(engine.SpriteSheetExists(" hero "));
        Assert.True(engine.SpriteSheetExists("\thero\n"));
    }

    /// <summary>Verifies SpriteSheetExists(null) throws ArgumentNullException.</summary>
    [Fact]
    public void SpriteSheetExists_NullName_ThrowsArgumentNullException()
    {
        var engine = new GameEngine();

        Assert.Throws<ArgumentNullException>(() => engine.SpriteSheetExists(null!));
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
    /// <summary>Verifies holding W+D for one second at 2 tiles/s moves diagonally up-right (~±1.414 tiles per axis) and sets Direction to UpRight; releasing D then moves straight up.</summary>
    [Fact]
    public void Update_HoldingDiagonalPair_MovesDiagonallyAndRevertsOnRelease()
    {
        var engine = new GameEngine();
        engine.Player.Character.BaseSpeed = 2;
        engine.Player.Position = new Position(200, 200);

        engine.Input(Key.W, true);
        engine.Input(Key.D, true);
        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // UpRight = (+√½, -√½); one second at 2 tiles/s → (±2·√½) ≈ (±1.414) per axis.
        var component = 2 * Math.Sqrt(0.5);
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
    // actual sprite size (78×108 for a 936×864 sheet), converted to tiles.
    // ---------------------------------------------------------------------
    /// <summary>Verifies ClampPlayerToMap uses the player's derived 78×108 sprite size converted to tiles (maxX = 10 - 78/48, maxY = 10 - 108/48).</summary>
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

        // Beyond the bottom-right corner: clamps to (10 - 78/48, 10 - 108/48) = (8.375, 7.75)
        // tiles (the previous (402, 372) px).
        engine.Player.Position = new Position(1000, 1000);
        engine.Update(FrameDt);
        Assert.Equal(10 - (78 / (double)CellSize), engine.Player.Position.X, precision: 6);
        Assert.Equal(10 - (108 / (double)CellSize), engine.Player.Position.Y, precision: 6);

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
        engine.Update(FrameDt); // clamps to (8.375, 7.75)

        // The camera origin clamps to the map: maxX = maxY = 10 - 240/48 = 5 tiles.
        Assert.Equal(new Position(5, 5), engine.ComputeCameraOrigin(canvasSize, canvasSize));

        // The 78×108 sprite renders at screen ((8.375 - 5)*48, (7.75 - 5)*48) = (162, 132).
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
    /// <summary>Verifies a map smaller than the canvas is centered (negative origin, in tiles) and the surrounding pixels are black.</summary>
    [Fact]
    public void Camera_MapSmallerThanCanvas_CentersMapWithBlackBackground()
    {
        const int canvasSize = 240;
        using var fixture = CreateFilledMapFixture(2, 2); // 96×96 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        engine.Player.Position = new Position(0, 0);

        // offset = (240 - 96) / (2 * 48) = 1.5 on each axis; the origin is the negative offset.
        Assert.Equal(new Position(-1.5, -1.5), engine.ComputeCameraOrigin(canvasSize, canvasSize));

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

    /// <summary>Verifies a map that fills (or exceeds) the canvas keeps the previous follow + clamp camera behavior (offset == 0), in tiles.</summary>
    [Fact]
    public void Camera_MapFillsCanvas_KeepsFollowAndClampBehavior()
    {
        const int canvasSize = 240;
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // Top-left: origin (0,0).
        engine.Player.Position = new Position(0, 0);
        Assert.Equal(new Position(0, 0), engine.ComputeCameraOrigin(canvasSize, canvasSize));

        // Bottom-right: origin clamped to (Map.Width - canvas/ts, Map.Height - canvas/ts) = (5, 5).
        engine.Player.Position = new Position(9, 9);
        Assert.Equal(new Position(5, 5), engine.ComputeCameraOrigin(canvasSize, canvasSize));

        // Middle: origin = player - canvas/(2*ts) (follow, no clamping).
        engine.Player.Position = new Position(4.5, 4.5);
        Assert.Equal(new Position(2, 2), engine.ComputeCameraOrigin(canvasSize, canvasSize));
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
                    Properties: new[] { new FixtureProperty("above_player", "bool", "true") }),
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
    // Story 37 acceptance 4: SurfaceToWorld / WorldToSurface are camera-aware
    // and inverses within floating-point tolerance. With no map the origin is
    // (0,0), so SurfaceToWorld(408, 408, 960, 960) == (8.5, 8.5) tiles.
    // ---------------------------------------------------------------------
    /// <summary>Verifies SurfaceToWorld and WorldToSurface are inverses within 1e-9, and the documented origin-(0,0) round trip.</summary>
    [Fact]
    public void SurfaceToWorld_WorldToSurface_AreInversesWithinTolerance()
    {
        var engine = new GameEngine(); // no map -> camera origin (0,0), ts 48

        // With origin (0,0) and ts 48, SurfaceToWorld(408, 408, 960, 960) == (8.5, 8.5).
        var world = engine.SurfaceToWorld(408, 408, 960, 960);
        Assert.Equal(8.5, world.X, precision: 9);
        Assert.Equal(8.5, world.Y, precision: 9);

        // Round-trip: WorldToSurface(SurfaceToWorld(p)) == p within floating-point tolerance.
        var surface = engine.WorldToSurface(world, 960, 960);
        Assert.Equal(408, surface.X, precision: 9);
        Assert.Equal(408, surface.Y, precision: 9);

        // Inverses across a spread of surface points.
        foreach (var (sx, sy) in new[] { (0.0, 0.0), (123.0, 456.0), (959.0, 1.0), (408.0, 408.0) })
        {
            var roundTripped = engine.WorldToSurface(engine.SurfaceToWorld(sx, sy, 960, 960), 960, 960);
            Assert.Equal(sx, roundTripped.X, precision: 9);
            Assert.Equal(sy, roundTripped.Y, precision: 9);
        }
    }

    /// <summary>Verifies SurfaceToWorld / WorldToSurface use the same follow + clamp camera as Render for a given canvas size.</summary>
    [Fact]
    public void SurfaceToWorld_WorldToSurface_RespectCameraOffset()
    {
        const int canvasSize = 240;
        using var fixture = CreateFilledMapFixture(10, 10); // 480×480 px
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Player at (4.5, 4.5) tiles -> camera origin (2, 2) tiles, so the player's world
        // position maps to the canvas centre (120, 120) px.
        engine.Player.Position = new Position(4.5, 4.5);
        var surface = engine.WorldToSurface(new Position(4.5, 4.5), canvasSize, canvasSize);
        Assert.Equal(120, surface.X, precision: 9);
        Assert.Equal(120, surface.Y, precision: 9);

        // And the canvas centre maps back to the player's world position.
        var world = engine.SurfaceToWorld(120, 120, canvasSize, canvasSize);
        Assert.Equal(4.5, world.X, precision: 9);
        Assert.Equal(4.5, world.Y, precision: 9);

        // The conversions are inverses within floating-point tolerance.
        var roundTripped = engine.WorldToSurface(engine.SurfaceToWorld(80, 160, canvasSize, canvasSize), canvasSize, canvasSize);
        Assert.Equal(80, roundTripped.X, precision: 9);
        Assert.Equal(160, roundTripped.Y, precision: 9);
    }

    // ---------------------------------------------------------------------
    // Story 37 acceptance 5: rendering reproduces the previous pixel output.
    // Player.Position = (8.5, 8.5) tiles with camera origin (0,0) draws the
    // sprite at 408 px.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a player at (8.5, 8.5) tiles with origin (0,0) renders its sprite at screen position 408 px (pixel assertion).</summary>
    [Fact]
    public void Render_PlayerAt8_5_WithOriginZero_DrawsSpriteAt408Px()
    {
        // A 21×21 map (1008×1008 px) on a 960×960 canvas: Render derives the canvas size
        // from the clip bounds (962×962), for which the camera origin is exactly (0,0) tiles —
        // the player's desired origin (8.5 - 962/96 < 0) clamps to 0 and the map is larger than
        // the canvas, so there is no centering offset.
        using var fixture = CreateFilledMapFixture(21, 21);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(8.5, 8.5);

        using var bitmap = new SKBitmap(960, 960);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.Render(canvas, FrameDt);

            // Assert the camera origin used inside Render is exactly (0,0) tiles.
            var canvasWidth = (int)Math.Ceiling(canvas.LocalClipBounds.Width);
            var canvasHeight = (int)Math.Ceiling(canvas.LocalClipBounds.Height);
            Assert.Equal(new Position(0, 0), engine.ComputeCameraOrigin(canvasWidth, canvasHeight));
        }

        // The 48×48 sprite is drawn at screen top-left (408, 408); its centre is (432, 432).
        var expected = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, StandingFrame);
        Assert.Equal(expected, bitmap.GetPixel(408 + (CellSize / 2), 408 + (CellSize / 2)));
        Assert.Equal(expected, bitmap.GetPixel(408, 408));
        Assert.Equal(expected, bitmap.GetPixel(408 + CellSize - 1, 408 + CellSize - 1));
    }

    // ---------------------------------------------------------------------
    // Story 37: Render records the canvas size from the clip bounds (internal
    // state the click-to-move story will consume).
    // ---------------------------------------------------------------------
    /// <summary>Verifies Render records the canvas size derived from the clip bounds for the future click-to-move story.</summary>
    [Fact]
    public void Render_RecordsCanvasSize()
    {
        var engine = new GameEngine();
        Assert.Equal(0, engine.LastCanvasWidth);
        Assert.Equal(0, engine.LastCanvasHeight);

        using var bitmap = new SKBitmap(240, 160);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.Render(canvas, FrameDt);

            // Render stores the same clip-derived size it uses for the camera.
            Assert.Equal(Math.Max(0, (int)Math.Ceiling(canvas.LocalClipBounds.Width)), engine.LastCanvasWidth);
            Assert.Equal(Math.Max(0, (int)Math.Ceiling(canvas.LocalClipBounds.Height)), engine.LastCanvasHeight);
        }
    }

    // ---------------------------------------------------------------------
    // Story 39: the engine owns the assigned map. Replacing GameEngine.Map
    // disposes the previous map and disposing the engine disposes the current
    // map; rendering through a disposed map is guarded.
    // ---------------------------------------------------------------------
    /// <summary>Verifies replacing GameEngine.Map disposes the previous map without error, and disposing the engine disposes the current map.</summary>
    [Fact]
    public void Map_ReplacementAndEngineDispose_DisposeMaps()
    {
        using var firstFixture = CreateFilledMapFixture(2, 2);
        var firstMap = TileMap.Load(firstFixture.MapPath);
        var engine = new GameEngine { Map = firstMap };
        Assert.False(firstMap.IsDisposed);

        // Replacing the map disposes the previous map.
        using var secondFixture = CreateFilledMapFixture(3, 3);
        var secondMap = TileMap.Load(secondFixture.MapPath);
        engine.Map = secondMap;
        Assert.True(firstMap.IsDisposed);
        Assert.False(secondMap.IsDisposed);

        // Disposing the engine disposes the current map; it is safe to call again.
        engine.Dispose();
        Assert.True(secondMap.IsDisposed);
        engine.Dispose();
    }

    /// <summary>Verifies rendering through the engine after the map was disposed is guarded (throws ObjectDisposedException).</summary>
    [Fact]
    public void Render_AfterMapDisposed_ThrowsObjectDisposedException()
    {
        using var fixture = CreateFilledMapFixture(2, 2);
        var map = TileMap.Load(fixture.MapPath);
        var engine = new GameEngine { Map = map };
        map.Dispose(); // e.g. a host that disposed the map it had loaded directly.

        using var bitmap = new SKBitmap(96, 96);
        using var canvas = new SKCanvas(bitmap);
        Assert.Throws<ObjectDisposedException>(() => engine.Render(canvas, FrameDt));
    }

    // ---------------------------------------------------------------------
    // Story 35: map collisions. A tile layer declaring the Tiled is_collision
    // bool property contains solid tiles that block the player; the engine
    // resolves the player's displacement with axis-separated movement so the
    // player stops at solid boundaries (never overlapping them) and slides
    // along walls on the free axis, while the map edge is solid (characters
    // cannot leave the map) and non-collision layers never block.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a player walking right into a solid tile stops at its exact tile-unit boundary and never overlaps it.</summary>
    [Fact]
    public void Update_PlayerWalksIntoSolidTile_StopsAtBoundary()
    {
        // 4x4 map: a "walls" collision layer with a solid column at x=2 for every row.
        using var fixture = CreateCollisionMapFixture(4, 4, new uint[]
        {
            0, 0, 1, 0,
            0, 0, 1, 0,
            0, 0, 1, 0,
            0, 0, 1, 0,
        });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // The default 48x48 sprite is a 1x1-tile footprint. Start at (0.5, 1.0) and hold D.
        engine.Player.Position = new Position(0.5, 1.0);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player slides to exactly the left edge of the solid tile at x=2: position.x = 1.0
        // (the footprint's right edge is at 2.0) and it never overlaps the solid tile.
        Assert.Equal(1.0, engine.Player.Position.X, precision: 6);
        Assert.Equal(1.0, engine.Player.Position.Y, precision: 6); // straight line, Y unchanged
        Assert.True(engine.Player.Position.X + 1.0 <= 2.0 + 1e-9, "The footprint must never overlap the solid tile.");
        Assert.Equal(Direction.Right, engine.Player.Direction);
    }

    /// <summary>Verifies a player walking down into a solid tile stops at its exact tile-unit boundary and never overlaps it.</summary>
    [Fact]
    public void Update_PlayerWalksDownIntoSolidTile_StopsAtBoundary()
    {
        // 4x4 map: a "walls" collision layer with a solid row at y=2 for every column.
        using var fixture = CreateCollisionMapFixture(4, 4, new uint[]
        {
            0, 0, 0, 0,
            0, 0, 0, 0,
            1, 1, 1, 1,
            1, 1, 1, 1,
        });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        engine.Player.Position = new Position(1.0, 0.5);
        engine.Input(Key.S, true);

        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player stops at exactly the top edge of the solid row at y=2: position.y = 1.0.
        Assert.Equal(1.0, engine.Player.Position.X, precision: 6);
        Assert.Equal(1.0, engine.Player.Position.Y, precision: 6);
        Assert.True(engine.Player.Position.Y + 1.0 <= 2.0 + 1e-9, "The footprint must never overlap the solid tile.");
        Assert.Equal(Direction.Down, engine.Player.Direction);
    }

    /// <summary>Verifies a player moving diagonally into a vertical wall slides along the wall on the free axis (axis-separated movement).</summary>
    [Fact]
    public void Update_PlayerMovesDiagonallyIntoWall_SlidesAlongWall()
    {
        // 5x5 map: a "walls" collision layer with a solid column at x=3 for every row.
        var gids = new uint[25];
        for (var y = 0; y < 5; y++)
        {
            gids[(y * 5) + 3] = 1;
        }

        using var fixture = CreateCollisionMapFixture(5, 5, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Start flush against the left edge of the wall column (x=3): the 1x1 footprint spans
        // [2,3) at x=2. Moving UpRight, the X displacement is blocked by the wall while Y is
        // free, so the player slides straight up along the wall.
        engine.Player.Position = new Position(2.0, 4.0);
        engine.Input(Key.W, true);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // X never moves into the wall: it stays at exactly 2.0 (the wall's left edge).
        Assert.Equal(2.0, engine.Player.Position.X, precision: 6);
        // Y slides up along the wall: one second at 2 tiles/s diagonally = 2 * sqrt(0.5) ~ 1.414.
        Assert.Equal(4.0 - (2 * Math.Sqrt(0.5)), engine.Player.Position.Y, precision: 6);
    }

    /// <summary>Verifies tiles drawn from a normal (non-collision) layer never block: the player walks across them freely.</summary>
    [Fact]
    public void Update_NonCollisionLayer_NeverBlocksMovement()
    {
        // A 4x4 map whose ground layer draws a tile in every cell but has no collision layer.
        using var fixture = CreateFilledMapFixture(4, 4);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        engine.Player.Position = new Position(0.5, 0.5);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // One second at 2 tiles/s: moved ~2 tiles right, Y unchanged (the drawn tiles never block).
        Assert.Equal(2.5, engine.Player.Position.X, precision: 6);
        Assert.Equal(0.5, engine.Player.Position.Y, precision: 6);
    }

    /// <summary>Verifies the map edge is solid: with no collision layer the player cannot walk out of the map.</summary>
    [Fact]
    public void Update_MapEdgeIsSolid_PlayerCannotLeaveMap()
    {
        // A 2x2 map with a ground layer only (no collision layer).
        using var fixture = CreateFilledMapFixture(2, 2);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);

        // Walk right: the 1x1 footprint stops when its right edge reaches the map edge at x=2,
        // so the player clamps to x = 1.0 instead of leaving the map.
        engine.Player.Position = new Position(0.5, 0.5);
        engine.Input(Key.D, true);
        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(1.0, engine.Player.Position.X, precision: 6);
        Assert.Equal(0.5, engine.Player.Position.Y, precision: 6);
        Assert.True(engine.Player.Position.X + 1.0 <= 2.0 + 1e-9);

        // Then walk down: the same rule clamps Y to 1.0 (the footprint's bottom edge at y=2).
        engine.Input(Key.D, false);
        engine.Input(Key.S, true);
        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(1.0, engine.Player.Position.X, precision: 6);
        Assert.Equal(1.0, engine.Player.Position.Y, precision: 6);
        Assert.True(engine.Player.Position.Y + 1.0 <= 2.0 + 1e-9);
    }

    // ---------------------------------------------------------------------
    // Story 36: minimap rendering. RenderMinimap draws the map's prerendered
    // tile layers (both below- and above-player layers, in file order), a
    // green dot for the player and a yellow dot for each NPC, onto a canvas
    // separate from the main game canvas. zoomLevel 1.0 fits the whole map
    // centered with the aspect preserved; > 1 zooms in around the player's
    // dot with the same edge clamp as the main camera; the canvas is not
    // cleared and unused margins stay blank. The method is pure (it never
    // mutates engine state), a null map is a no-op, and zoomLevel <= 0 throws
    // ArgumentOutOfRangeException.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies the default zoom fits the whole (non-square) map into the canvas, centered with
    /// the aspect ratio preserved, leaves the unused margins blank, and shows every distinct
    /// tile color of a two-color map plus both dots at their scaled positions.
    /// </summary>
    [Fact]
    public void RenderMinimap_DefaultZoom_FitsWholeMapCenteredWithBlankMargins()
    {
        // A 4x2 map (192x96 px): red tiles on the left half, blue on the right half, so the map
        // is non-square and the two tile colors are distinguishable.
        using var fixture = new TiledTestFixture(
            4, 2,
            new[] { new TileLayerSpec("ground", new uint[]
            {
                1, 1, 2, 2,
                1, 1, 2, 2,
            }) },
            tileColors: new[] { SKColors.Red, SKColors.Blue });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // Player near the top-left, NPC near the bottom-right, both away from the sampled tiles.
        engine.Player.Position = new Position(0.5, 0.5);
        engine.Characters.Add(new Character { Position = new Position(3.5, 1.5) });

        const int canvasWidth = 400;
        const int canvasHeight = 300;
        using var bitmap = RenderMinimap(engine, canvasWidth, canvasHeight, zoomLevel: 1.0);

        // Aspect-preserving fit: baseFit = min(400/192, 300/96) = 25/12, so the map is scaled to
        // exactly 400x200 and centered with 50 px margins top and bottom (it fills the width).
        const double scale = 25.0 / 12;
        Assert.Equal(192 * scale, 400, precision: 6);
        Assert.Equal(96 * scale, 200, precision: 6);

        // Both distinct tile colors are visible at their scaled centers, away from the dots.
        // Red tile (1,0) center (72,24) px -> screen (150,100); blue tile (2,0) center (120,24)
        // px -> screen (250,100).
        Assert.Equal(SKColors.Red, bitmap.GetPixel(150, 100));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(250, 100));

        // The dots are drawn at the scaled world positions: player (0.5,0.5) -> map px (24,24)
        // -> screen (50,100) green; NPC (3.5,1.5) -> map px (168,72) -> screen (350,200) yellow.
        Assert.Equal(SKColors.Green, bitmap.GetPixel(50, 100));
        Assert.Equal(SKColors.Yellow, bitmap.GetPixel(350, 200));

        // The map is centered vertically with ~50 px margins above and below (it fills the 400 px
        // width), so it is not stretched to fill the 300 px canvas. Sample well inside each
        // region to avoid the rasterizer's sub-pixel edge rows.
        Assert.Equal(0, bitmap.GetPixel(200, 20).Alpha);   // top margin blank
        Assert.Equal(0, bitmap.GetPixel(200, 40).Alpha);   // top margin blank
        Assert.NotEqual(0, bitmap.GetPixel(200, 60).Alpha);  // map interior (top half)
        Assert.NotEqual(0, bitmap.GetPixel(200, 240).Alpha); // map interior (bottom half)
        Assert.Equal(0, bitmap.GetPixel(200, 260).Alpha);  // bottom margin blank
        Assert.Equal(0, bitmap.GetPixel(200, 280).Alpha);  // bottom margin blank
    }

    /// <summary>
    /// Verifies the player dot (green) and NPC dots (yellow) are drawn as small filled circles
    /// at the scaled world positions, and that with no NPCs only the green dot is drawn.
    /// </summary>
    [Fact]
    public void RenderMinimap_Dots_AtScaledPositions()
    {
        using var fixture = CreateFilledMapFixture(2, 2); // 96x96 red map
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // A 200x200 canvas: baseFit = min(200/96, 200/96) = 25/12, so the 96x96 map is scaled to
        // exactly 200x200 and fills the canvas (origin 0,0).
        engine.Player.Position = new Position(0.5, 0.5);   // map px (24,24) -> screen (50,50)
        var npc = new Character { Position = new Position(1.5, 1.5) }; // map px (72,72) -> screen (150,150)
        engine.Characters.Add(npc);

        using var bitmap = RenderMinimap(engine, 200, 200, zoomLevel: 1.0);
        Assert.Equal(SKColors.Green, bitmap.GetPixel(50, 50));
        Assert.Equal(SKColors.Yellow, bitmap.GetPixel(150, 150));

        // With no NPCs only the green dot is drawn.
        engine.Characters.Clear();
        using var withoutNpc = RenderMinimap(engine, 200, 200, zoomLevel: 1.0);
        Assert.Equal(SKColors.Green, withoutNpc.GetPixel(50, 50));
        Assert.Equal(0, CountColor(withoutNpc, SKColors.Yellow));
    }

    /// <summary>
    /// Verifies zoom-in shows only the sub-region around the player, and that moving the player
    /// to the top-left / bottom-right corners clamps the view so the map edge is shown (never
    /// blank space) — the same clamping behavior as the main camera.
    /// </summary>
    [Fact]
    public void RenderMinimap_ZoomIn_ShowsSubRegionAndClampsToMapEdges()
    {
        // A 10x10 (480x480) map: red everywhere, with blue corner tiles at (0,0) and (9,9) so the
        // visible sub-region can be told apart from the rest of the map.
        var gids = Enumerable.Repeat(1u, 100).ToArray();
        gids[0] = 2;   // tile (0,0) is blue
        gids[99] = 2;  // tile (9,9) is blue
        using var fixture = new TiledTestFixture(
            10, 10,
            new[] { new TileLayerSpec("ground", gids) },
            tileColors: new[] { SKColors.Red, SKColors.Blue });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        const int canvasSize = 240; // baseFit 0.5; zoom 4 -> scale 2, visible region 120x120 map px

        // Center: player at (5,5) tiles = map px (240,240); the visible region clamps to
        // (180,180)-(300,300). The blue corner tiles lie outside it, so no blue is visible and a
        // tile inside the region (e.g. tile (4,4) at screen (72,72)) is shown.
        engine.Player.Position = new Position(5, 5);
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(0, CountColor(bitmap, SKColors.Blue));     // corner tiles are outside the region
            Assert.Equal(SKColors.Red, bitmap.GetPixel(72, 72));    // a tile inside the region is visible
            Assert.Equal(SKColors.Green, bitmap.GetPixel(120, 120)); // the player dot is centered
        }

        // Top-left corner: player at (0,0); the visible region clamps to (0,0)-(120,120) so the
        // map edge is shown at the canvas edge instead of blank space — the blue corner tile (0,0)
        // is visible at the top-left of the canvas (a few pixels away from the green dot at 0,0).
        engine.Player.Position = new Position(0, 0);
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 10));
        }

        // Bottom-right corner: player at (9,9); the visible region clamps to (360,360)-(480,480)
        // so the blue corner tile (9,9) is visible at the bottom-right of the canvas.
        engine.Player.Position = new Position(9, 9);
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(239, 239));
        }
    }

    /// <summary>
    /// Verifies a dot outside the visible region is not drawn (no yellow pixels), while a dot
    /// inside the region is drawn at its scaled position.
    /// </summary>
    [Fact]
    public void RenderMinimap_DotOutsideVisibleRegion_IsNotDrawn()
    {
        using var fixture = CreateFilledMapFixture(10, 10); // 480x480 red map
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        const int canvasSize = 240; // zoom 4 -> scale 2, visible region 120x120 map px
        engine.Player.Position = new Position(5, 5); // visible region (180,180)-(300,300)

        // An NPC inside the visible region is drawn at its scaled position.
        engine.Characters.Add(new Character { Position = new Position(6, 5) }); // map px (288,240) -> screen (216,120)
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(SKColors.Yellow, bitmap.GetPixel(216, 120));
        }

        // An NPC outside the visible region is skipped entirely.
        engine.Characters.Clear();
        engine.Characters.Add(new Character { Position = new Position(1, 5) }); // map px (48,240), left of the region
        using (var bitmap = RenderMinimap(engine, canvasSize, canvasSize, zoomLevel: 4))
        {
            Assert.Equal(0, CountColor(bitmap, SKColors.Yellow));
        }
    }

    /// <summary>
    /// Verifies that with no map RenderMinimap is a no-op: the canvas is left untouched.
    /// </summary>
    [Fact]
    public void RenderMinimap_NoMap_LeavesCanvasUntouched()
    {
        var engine = new GameEngine(); // no map

        using var bitmap = new SKBitmap(120, 90);
        using (var canvas = new SKCanvas(bitmap))
        {
            // Pre-fill the canvas with an arbitrary backdrop the minimap must not overwrite.
            canvas.Clear(SKColors.Orange);
            engine.RenderMinimap(canvas, zoomLevel: 1.0);
        }

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                Assert.Equal(SKColors.Orange, bitmap.GetPixel(x, y));
            }
        }
    }

    /// <summary>Verifies a zoom level of zero or negative throws ArgumentOutOfRangeException.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-0.5)]
    public void RenderMinimap_NonPositiveZoom_ThrowsArgumentOutOfRangeException(double zoomLevel)
    {
        using var fixture = CreateFilledMapFixture(2, 2);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        using var bitmap = new SKBitmap(96, 96);
        using var canvas = new SKCanvas(bitmap);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.RenderMinimap(canvas, zoomLevel));
    }

    /// <summary>Verifies a small positive zoom (zoom out) is accepted and still draws the map.</summary>
    [Fact]
    public void RenderMinimap_SmallPositiveZoom_IsAccepted()
    {
        using var fixture = CreateFilledMapFixture(2, 2);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // zoom 0.5 on a 200x200 canvas: scale = 25/24, the 96x96 map is scaled to 100x100 and
        // centered with 50 px margins; the map is still drawn (e.g. its center is a map pixel).
        using var bitmap = RenderMinimap(engine, 200, 200, zoomLevel: 0.5);
        Assert.NotEqual(0, bitmap.GetPixel(100, 100).Alpha);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha); // the margin stays blank
    }

    /// <summary>
    /// Verifies RenderMinimap is pure: rendering a minimap on a separate surface does not change
    /// the output of the main Render nor the engine state (regression guard for the minimap work).
    /// </summary>
    [Fact]
    public void RenderMinimap_DoesNotMutateEngineState_MainRenderUnchanged()
    {
        using var fixture = CreateFilledMapFixture(4, 4);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(1.5, 1.5);
        engine.Characters.Add(new Character { Position = new Position(2.5, 2.5) });

        var before = Render(engine, 240, 240);

        // Render a minimap on a separate surface; it must not touch the main render path or state.
        using (var minimap = RenderMinimap(engine, 120, 120, zoomLevel: 1.0))
        {
            Assert.NotEqual(0, minimap.GetPixel(60, 60).Alpha); // the minimap did draw something
        }

        var after = Render(engine, 240, 240);
        AssertBitmapsEqual(before, after);

        Assert.Equal(new Position(1.5, 1.5), engine.Player.Position);
        Assert.Single(engine.Characters);
    }

    // ---------------------------------------------------------------------
    // Story 38: click-to-move with A* auto-walk and Player.OnMove. Click
    // converts a host-surface click (using the canvas size recorded by the most
    // recent Render) to a tile, computes an A* path over the non-solid tiles,
    // and auto-walks the player along it at BaseSpeed, stopping on the clicked
    // tile center. Clicking a solid tile or an unreachable target cancels the
    // walk without moving; a key press cancels it (a release does not); a click
    // mid-walk replaces the destination. OnMove fires for auto-walk too.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies clicking a walkable tile computes an A* path and auto-walks the player along it,
    /// ending exactly centered on the clicked tile and visiting every waypoint tile.
    /// </summary>
    [Fact]
    public void Click_OnWalkableTile_WalksAlongPathAndEndsCenteredOnClickedTile()
    {
        using var fixture = CreateFilledMapFixture(10, 10);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 0.5);

        const int canvas = 480; // 10 tiles x 48 px, the whole map is visible
        ClickOnTile(engine, 5, 5, canvas, canvas);

        // Snapshot the computed path before the walk consumes it: it leads from the player's
        // tile (0,0) to the clicked tile (5,5).
        var path = engine.AutoWalkPath;
        Assert.NotEmpty(path);
        Assert.Equal((5, 5), path[^1]);

        var target = new Position(5.5, 5.5);
        var visited = new HashSet<(int X, int Y)>();
        for (var frame = 0; frame < 5000; frame++)
        {
            engine.Update(FrameDt);
            visited.Add(engine.Player.Position.ToTile());
            if (engine.Player.Position == target)
            {
                break;
            }
        }

        // The player ends exactly centered on the clicked tile and the path is consumed.
        Assert.Equal(target, engine.Player.Position);
        Assert.Empty(engine.AutoWalkPath);

        // The path was followed tile by tile: every waypoint tile was visited.
        foreach (var tile in path)
        {
            Assert.Contains(tile, visited);
        }
    }

    /// <summary>
    /// Verifies OnMove fires for auto-walk: clicking a distant walkable tile starts the walk
    /// (IsMoving = true on the first Update) and completing it stops the player (IsMoving =
    /// false), both with the correct facing direction.
    /// </summary>
    [Fact]
    public void Click_OnMove_FiresTrueWhenWalkStartsAndFalseWhenItCompletes()
    {
        using var fixture = CreateFilledMapFixture(10, 10);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 0.5);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        const int canvas = 480;
        ClickOnTile(engine, 3, 3, canvas, canvas);

        // The first Update starts the walk toward (1.5, 1.5): down-right.
        engine.Update(FrameDt);
        Assert.Equal(new[] { new PlayerMoveEventArgs(true, Direction.DownRight) }, events);
        events.Clear();

        var target = new Position(3.5, 3.5);
        for (var frame = 0; frame < 5000 && engine.Player.Position != target; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(target, engine.Player.Position);
        Assert.Equal(new[] { new PlayerMoveEventArgs(false, Direction.DownRight) }, events);
    }

    /// <summary>
    /// Verifies clicking a solid tile cancels any in-progress auto-walk and leaves the player
    /// unmoved on the next Update.
    /// </summary>
    [Fact]
    public void Click_OnSolidTile_CancelsAutoWalkAndDoesNotMove()
    {
        // 6x6 map with a solid wall column at x=3.
        var gids = new uint[36];
        for (var y = 0; y < 6; y++)
        {
            gids[(y * 6) + 3] = 1;
        }

        using var fixture = CreateCollisionMapFixture(6, 6, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 0.5);

        const int canvas = 288; // 6 tiles x 48 px
        ClickOnTile(engine, 1, 1, canvas, canvas);
        engine.Update(FrameDt);
        Assert.NotEmpty(engine.AutoWalkPath);

        // Mid-walk, click a solid tile: the walk is cancelled and the player does not move.
        ClickOnTile(engine, 3, 0, canvas, canvas);
        Assert.Empty(engine.AutoWalkPath);

        var positionBefore = engine.Player.Position;
        engine.Update(FrameDt);
        Assert.Equal(positionBefore, engine.Player.Position);
    }

    /// <summary>
    /// Verifies a click on a reachable target behind a wall computes a detour around the wall and
    /// the player walks to the target.
    /// </summary>
    [Fact]
    public void Click_OnReachableTargetBehindWall_WalksAroundTheWall()
    {
        // 7x5 map: a "walls" collision layer with a solid column at x=3 for rows 0..2, leaving a
        // gap at the bottom (rows 3-4) so the target at (5,0) is reachable only with a detour.
        var gids = new uint[35];
        gids[(0 * 7) + 3] = 1;
        gids[(1 * 7) + 3] = 1;
        gids[(2 * 7) + 3] = 1;

        using var fixture = CreateCollisionMapFixture(7, 5, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 0.5);

        const int canvas = 336; // 7 tiles x 48 px
        ClickOnTile(engine, 5, 0, canvas, canvas);

        Assert.NotEmpty(engine.AutoWalkPath);
        // The path must detour around the wall: it cannot cross x=3 at rows 0..2, so it has to go
        // through a row at or below y=3.
        Assert.Contains(engine.AutoWalkPath, tile => tile.Y >= 3);

        var target = new Position(5.5, 0.5);
        for (var frame = 0; frame < 5000 && engine.Player.Position != target; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(target, engine.Player.Position);
    }

    /// <summary>
    /// Verifies clicking an unreachable target (a tile enclosed by walls) leaves the player
    /// unmoved and cancels any in-progress auto-walk.
    /// </summary>
    [Fact]
    public void Click_OnUnreachableTarget_DoesNotMove()
    {
        // 7x7 map with a closed 3x3 ring of walls around the center tile (3,3).
        var gids = new uint[49];
        for (var y = 0; y < 7; y++)
        {
            for (var x = 0; x < 7; x++)
            {
                var onRing = ((y == 2 || y == 4) && x >= 2 && x <= 4) ||
                             ((x == 2 || x == 4) && y >= 2 && y <= 4);
                gids[(y * 7) + x] = onRing ? 1u : 0u;
            }
        }

        using var fixture = CreateCollisionMapFixture(7, 7, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 0.5);

        const int canvas = 336; // 7 tiles x 48 px
        ClickOnTile(engine, 3, 3, canvas, canvas);

        // No path to the enclosed tile: the walk is cancelled and the player does not move.
        Assert.Empty(engine.AutoWalkPath);
        var positionBefore = engine.Player.Position;
        engine.Update(FrameDt);
        Assert.Equal(positionBefore, engine.Player.Position);
    }

    /// <summary>
    /// Verifies a key press during auto-walk cancels the path and the player stops on the next
    /// Update, while a key release alone does not cancel the walk.
    /// </summary>
    [Fact]
    public void Input_KeyPressDuringAutoWalk_CancelsWalk_ButReleaseAloneDoesNot()
    {
        using var fixture = CreateFilledMapFixture(10, 10);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 0.5);

        const int canvas = 480;
        ClickOnTile(engine, 5, 5, canvas, canvas);

        // A key release alone does not cancel the walk.
        engine.Input(Key.X, isPressed: false);
        Assert.NotEmpty(engine.AutoWalkPath);

        // A key press cancels it.
        engine.Input(Key.X, isPressed: true);
        Assert.Empty(engine.AutoWalkPath);

        // The player stops on the next Update (no input, no path) and does not move further.
        var positionBefore = engine.Player.Position;
        engine.Update(FrameDt);
        Assert.Equal(positionBefore, engine.Player.Position);
        Assert.Empty(engine.AutoWalkPath);
    }

    /// <summary>
    /// Verifies a click during auto-walk replaces the destination: the player changes course
    /// toward the new target without stopping first (no IsMoving = false before the final stop).
    /// </summary>
    [Fact]
    public void Click_DuringAutoWalk_ReplacesDestinationWithoutStopping()
    {
        using var fixture = CreateFilledMapFixture(10, 10);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 0.5);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        const int canvas = 480;
        ClickOnTile(engine, 5, 5, canvas, canvas);
        engine.Update(FrameDt); // the walk starts (IsMoving = true)
        Assert.NotEmpty(engine.AutoWalkPath);

        // Mid-walk, click a different target: the path is replaced.
        ClickOnTile(engine, 8, 1, canvas, canvas);
        Assert.NotEmpty(engine.AutoWalkPath);
        Assert.Equal((8, 1), engine.AutoWalkPath[^1]);

        var target = new Position(8.5, 1.5);
        for (var frame = 0; frame < 5000 && engine.Player.Position != target; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(target, engine.Player.Position);
        // The player never stopped mid-course: the only stop event is the final one.
        Assert.Equal(1, events.Count(e => !e.IsMoving));
        Assert.False(events[^1].IsMoving);
        // It did start moving from the first click.
        Assert.Contains(events, e => e.IsMoving);
    }

    /// <summary>
    /// Verifies a click before any Render (unknown canvas size) is ignored without throwing, even
    /// when a map is loaded.
    /// </summary>
    [Fact]
    public void Click_BeforeAnyRender_IsIgnoredWithoutThrowing()
    {
        using var fixture = CreateFilledMapFixture(10, 10);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        // No Render has happened, so the canvas size is unknown: the click must be a no-op.
        engine.Click(120, 90);
        Assert.Empty(engine.AutoWalkPath);
        Assert.Equal(new Position(0, 0), engine.Player.Position);

        engine.Update(FrameDt);
        Assert.Equal(new Position(0, 0), engine.Player.Position);
    }

    /// <summary>
    /// Verifies manual key movement still raises OnMove with the exact start/stop sequence (the
    /// engine reports movement through Player.ReportMovement after its collision resolution).
    /// </summary>
    [Fact]
    public void Update_KeyMovement_RaisesOnMoveOnStartAndStop()
    {
        var engine = new GameEngine();
        engine.Player.Character.BaseSpeed = 2;
        engine.Player.Position = new Position(10, 10);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        engine.Input(Key.D, true);
        engine.Update(FrameDt);
        Assert.Equal(new[] { new PlayerMoveEventArgs(true, Direction.Right) }, events);

        engine.Input(Key.D, false);
        events.Clear();
        engine.Update(FrameDt);
        Assert.Equal(new[] { new PlayerMoveEventArgs(false, Direction.Right) }, events);
    }

    /// <summary>Verifies changing direction with the keys while moving raises OnMove with the new direction.</summary>
    [Fact]
    public void Update_KeyMovement_ChangingDirectionRaisesOnMove()
    {
        var engine = new GameEngine();
        engine.Player.Character.BaseSpeed = 2;
        engine.Player.Position = new Position(10, 10);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        engine.Input(Key.D, true);
        engine.Update(FrameDt); // moving right

        // Switch to moving down: direction change while moving.
        engine.Input(Key.D, false);
        engine.Input(Key.S, true);
        engine.Update(FrameDt);

        Assert.Contains(new PlayerMoveEventArgs(true, Direction.Down), events);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>Creates a map fixture filled with red tiles in a single "ground" layer.</summary>
    private static TiledTestFixture CreateFilledMapFixture(int width, int height)
        => new(width, height, new[] { FilledLayer(width, height) });

    /// <summary>
    /// Creates a map fixture with a filled "ground" layer (walkable) and a "walls" collision
    /// layer declaring the Tiled <c>is_collision</c> bool property set to <c>true</c>.
    /// </summary>
    private static TiledTestFixture CreateCollisionMapFixture(int width, int height, uint[] collisionGids)
        => new(
            width,
            height,
            new[]
            {
                FilledLayer(width, height),
                new TileLayerSpec(
                    "walls",
                    collisionGids,
                    Properties: new[] { new FixtureProperty("is_collision", "bool", "true") }),
            });

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

    /// <summary>
    /// Renders once (recording the canvas size in the engine) and clicks the center of the given
    /// tile in host-surface coordinates, translated through the same camera as <see cref="Render"/>.
    /// </summary>
    private static void ClickOnTile(GameEngine engine, int tileX, int tileY, int canvasWidth, int canvasHeight)
    {
        using var bitmap = Render(engine, canvasWidth, canvasHeight);
        var surface = engine.WorldToSurface(new Position(tileX + 0.5, tileY + 0.5), canvasWidth, canvasHeight);
        engine.Click(surface.X, surface.Y);
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

    /// <summary>Renders the minimap into a fresh transparent bitmap of the requested size.</summary>
    private static SKBitmap RenderMinimap(GameEngine engine, int width, int height, double zoomLevel)
    {
        var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.RenderMinimap(canvas, zoomLevel);
        }

        return bitmap;
    }

    /// <summary>Counts the pixels in <paramref name="bitmap"/> equal to <paramref name="color"/>.</summary>
    private static int CountColor(SKBitmap bitmap, SKColor color)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) == color)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Asserts two bitmaps have identical dimensions and pixels.</summary>
    private static void AssertBitmapsEqual(SKBitmap expected, SKBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
            }
        }
    }

}
