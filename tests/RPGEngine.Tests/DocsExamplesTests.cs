using RPGEngine.Sprites;
using RPGEngine.Tests.Fixtures;
using RPGEngine.Tests.Sprites;
using RPGEngine.Tests.Tiled;
using RPGEngine.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Compiles and runs the code examples that appear in <c>docs/</c> against the real engine API
/// (acceptance criterion 6). Every public type documented under <c>docs/api/</c> is exercised
/// here at least once, using the committed fixture assets where an image or map is needed, so a
/// documentation snippet can never drift away from the API.
/// </summary>
/// <remarks>
/// The snippets in <c>docs/api/*.md</c> mirror these methods (they are kept in sync by hand; the
/// tests are the source of truth for "does this snippet compile and run").
/// </remarks>
public class DocsExamplesTests
{
    private const double FrameDt = 1.0 / 60;

    // ---------------------------------------------------------------------
    // docs/README.md and docs/api/GameEngine.md: the "hello world" game loop.
    // ---------------------------------------------------------------------
    /// <summary>
    /// The canonical hello-world example from the documentation: create a <see cref="GameEngine"/>,
    /// load a spritesheet and a tile map, add an NPC, then drive one frame with
    /// <c>Update</c>/<c>Render</c>/<c>Input</c>.
    /// </summary>
    [Fact]
    public void HelloWorld_EndToEnd()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();

        // 1. Create the engine. It starts with a fresh player, an empty NPC list,
        //    the default WASD configuration and no map.
        var engine = new GameEngine();

        // 2. Load assets. The map owns its tilesets; characters reference sheets by name.
        engine.Map = TileMap.Load(fixtures.PathOf(FixtureAssets.MapFile));
        engine.LoadSpriteSheet("hero", fixtures.PathOf(FixtureAssets.FullSheet));

        // 3. Place the player and give it the "hero" sheet, character slot 1.
        engine.Player.Position = SampleScene.PlayerPosition;
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        // 4. Add an NPC built from part sheets (body + face + hair1, character slot 2).
        engine.LoadPartSpriteSheet("body", fixtures.PathOf(FixtureAssets.PartBody), CharacterPartType.Body);
        engine.LoadPartSpriteSheet("face", fixtures.PathOf(FixtureAssets.PartFace), CharacterPartType.Face);
        engine.LoadPartSpriteSheet("hair1", fixtures.PathOf(FixtureAssets.PartHair1), CharacterPartType.Hair1);
        var npc = new Character { Position = SampleScene.VillagerPosition };
        npc.SpriteSheets.Add(new SpriteSheetRef("body", CharacterIndex: 2));
        npc.SpriteSheets.Add(new SpriteSheetRef("face", CharacterIndex: 2));
        npc.SpriteSheets.Add(new SpriteSheetRef("hair1", CharacterIndex: 2));
        engine.Characters.Add(npc);

        // 5. Drive the loop: forward input, update the simulation, render the frame.
        engine.Input(Key.D, isPressed: true);
        var positionBefore = engine.Player.Position;
        engine.Update(FrameDt);
        Assert.True(engine.Player.Position.X > positionBefore.X, "Holding D moves the player right.");
        engine.Input(Key.D, isPressed: false);

        using var bitmap = new SKBitmap(640, 480);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.Render(canvas, FrameDt);
        }

        Assert.NotEqual(0, bitmap.GetPixel(320, 240).Alpha);
    }

    // ---------------------------------------------------------------------
    // docs/api/GameEngine.md: the Render example. When a map is smaller than the
    // canvas it is centered and the area around it is black (story 24).
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies the Render example's black background: a map smaller than the canvas is
    /// centered and every pixel outside it is black (alpha 255, RGB 0), with the map's own
    /// tiles drawn over the black backdrop.
    /// </summary>
    [Fact]
    public void Render_Example_BlackBackgroundAroundSmallMap()
    {
        // A 2×2 map (96×96 px) rendered on a 240×240 canvas, mirroring the docs'
        // Render snippet but with a map smaller than the canvas so the letterboxing is visible.
        using var fixture = new TiledTestFixture(
            2,
            2,
            new[] { new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1 }) });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };

        using var bitmap = new SKBitmap(240, 240);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.Render(canvas, FrameDt);
        }

        // The centered map occupies (72..168, 72..168); a tile pixel is present at its centre.
        Assert.NotEqual(0, bitmap.GetPixel(120, 120).Alpha);

        // Outside the centered map the background is black.
        var black = new SKColor(0, 0, 0, 255);
        Assert.Equal(black, bitmap.GetPixel(10, 10));
        Assert.Equal(black, bitmap.GetPixel(230, 230));
        Assert.Equal(black, bitmap.GetPixel(20, 120));
        Assert.Equal(black, bitmap.GetPixel(120, 220));
    }

    // ---------------------------------------------------------------------
    // docs/api/SpriteSheet.md: the character index 1..8 semantics.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies <see cref="SpriteSheet.GetSprite"/> selects one of the 8 characters in a sheet
    /// and that the returned sprite is an independent crop at the sheet's derived cell size
    /// (48×48 for a standard 576×384 sheet, 78×108 for a 936×864 sheet) which the caller
    /// owns. Both full and part sheets share this layout.
    /// </summary>
    [Fact]
    public void CharacterIndex_SelectsOneOfEightCharacters()
    {
        // Standard RPG Maker MZ sheet: 576×384 &#8594; 48×48 cells.
        using (var sheetStream = FixtureAssets.DecodePngStream(FixtureAssets.FullSheet))
        {
            var manager = new SpriteSheetManager();
            var sheet = manager.Load("hero", sheetStream);

            Assert.Equal(8, sheet.CharacterCount);
            Assert.Equal(48, sheet.CellWidth);
            Assert.Equal(48, sheet.CellHeight);

            // Every 1-based index 1..8 yields an independent sprite at the derived cell size;
            // the 8 slots are the sheet's 4×2 character grid, each a 3-frame × 4-direction block.
            for (var characterIndex = 1; characterIndex <= 8; characterIndex++)
            {
                using var sprite = sheet.GetSprite(characterIndex, Direction.Down, frame: 1);
                Assert.Equal(sheet.CellWidth, sprite.Width);
                Assert.Equal(sheet.CellHeight, sprite.Height);
            }

            // An index outside 1..8 is rejected.
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetSprite(0, Direction.Down, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetSprite(9, Direction.Down, 1));
        }

        // A 936×864 sheet derives 78×108 cells.
        using (var largeStream = new MemoryStream(SpriteSheetTestHelper.CreateSheetPng(936, 864), writable: false))
        {
            var manager = new SpriteSheetManager();
            var sheet = manager.Load("large", largeStream);

            Assert.Equal(8, sheet.CharacterCount);
            Assert.Equal(78, sheet.CellWidth);
            Assert.Equal(108, sheet.CellHeight);

            using var sprite = sheet.GetSprite(1, Direction.Down, frame: 1);
            Assert.Equal(78, sprite.Width);
            Assert.Equal(108, sprite.Height);
        }

        // SpriteSheetRef pairs a sheet name with the 1-based character index; the range is
        // enforced where the reference is consumed (e.g. at render time).
        var reference = new SpriteSheetRef("hero", CharacterIndex: 3);
        Assert.Equal("hero", reference.Name);
        Assert.Equal(3, reference.CharacterIndex);
    }

    // ---------------------------------------------------------------------
    // docs/api/Character.md and docs/api/Player.md: configuring sheets with a
    // name + index.
    // ---------------------------------------------------------------------
    /// <summary>Verifies configuring <see cref="Character.SpriteSheets"/> with a sheet name and a 1..8 character index.</summary>
    [Fact]
    public void ConfigureCharacter_WithSheetNameAndIndex()
    {
        var character = new Character
        {
            Position = new Position(96, 96),
            Direction = Direction.Down,
            BaseSpeed = 96,
        };

        character.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
        character.SpriteSheets.Add(new SpriteSheetRef("cape", CharacterIndex: 4));

        Assert.Equal(2, character.SpriteSheets.Count);
        Assert.Contains(new SpriteSheetRef("hero", 1), character.SpriteSheets);
        Assert.Contains(new SpriteSheetRef("cape", 4), character.SpriteSheets);
    }

    /// <summary>Verifies configuring <see cref="Player.SpriteSheets"/> forwards to the underlying character's list.</summary>
    [Fact]
    public void ConfigurePlayer_WithSheetNameAndIndex()
    {
        var player = new Player();
        player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        Assert.Same(player.Character.SpriteSheets, player.SpriteSheets);
        Assert.Contains(new SpriteSheetRef("hero", 1), player.Character.SpriteSheets);
    }

    // ---------------------------------------------------------------------
    // docs/api/GameConfig.md and docs/api/Key.md.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the GameConfig examples: defaults, rebinding and uniqueness.</summary>
    [Fact]
    public void GameConfig_KeyBindings()
    {
        var config = new GameConfig();

        // Defaults are WASD and GetDirection maps each key to a direction.
        Assert.Equal(Key.W, config.UpKey);
        Assert.Equal(Direction.Up, config.GetDirection(Key.W));
        Assert.Equal(Direction.Down, config.GetDirection(Key.S));

        // Rebinding takes effect immediately.
        config.UpKey = Key.Up;
        Assert.Equal(Direction.Up, config.GetDirection(Key.Up));
        Assert.Null(config.GetDirection(Key.W));

        // A key already bound to another direction is rejected and leaves config unchanged.
        Assert.Throws<ArgumentException>(() => config.DownKey = Key.Up);
        Assert.Equal(Key.S, config.DownKey);
    }

    /// <summary>Verifies hosts translate their framework key events to the engine <see cref="Key"/> values.</summary>
    [Fact]
    public void Key_HostTranslation_ForwardsToEngine()
    {
        var engine = new GameEngine();

        // e.g. a Blazor KeyboardEventArgs with key == "ArrowUp" → engine Key.Up.
        // The arrow keys are not bound by default (WASD is), so rebind UpKey first.
        engine.Config.UpKey = Key.Up;
        engine.Input(Key.Up, isPressed: true);
        engine.Update(FrameDt);
        Assert.Equal(Direction.Up, engine.Player.Direction);
        engine.Input(Key.Up, isPressed: false);
    }

    // ---------------------------------------------------------------------
    // docs/api/Position.md, docs/api/Vector2.md and docs/api/Direction.md.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the Position/Vector2 examples.</summary>
    [Fact]
    public void Position_AndVector2_Math()
    {
        var position = new Position(10, 20);
        var offset = new Vector2(3, -4);

        var moved = position + offset;
        Assert.Equal(new Position(13, 16), moved);

        var tile = moved.ToTile(tileSize: 48);
        Assert.Equal((0, 0), tile);

        var distance = position.DistanceTo(new Position(13, 16));
        Assert.Equal(5, distance, precision: 10);
    }

    /// <summary>Verifies the Direction extension examples (deltas, opposites, rows, diagonals).</summary>
    [Fact]
    public void Direction_DeltasOppositesAndRows()
    {
        Assert.Equal(new Vector2(0, -1), Direction.Up.Delta());
        Assert.Equal(Direction.Down, Direction.Up.Opposite());
        Assert.Equal(3, Direction.Up.RowIndex()); // RPG Maker MZ row 3
        Assert.True(Direction.Left.IsHorizontal());
        Assert.False(Direction.Left.IsVertical());

        // Diagonal support: normalized delta, diagonal opposite, side-view row fallback and the
        // IsDiagonal classification.
        var upRight = Direction.UpRight.Delta();
        Assert.Equal(Math.Sqrt(0.5), upRight.X, precision: 9);
        Assert.Equal(-Math.Sqrt(0.5), upRight.Y, precision: 9);
        Assert.Equal(Direction.DownLeft, Direction.UpRight.Opposite());
        Assert.Equal(2, Direction.UpRight.RowIndex()); // falls back to the Right (side-view) row
        Assert.True(Direction.UpRight.IsDiagonal());
        Assert.False(Direction.UpRight.IsHorizontal());
        Assert.False(Direction.UpRight.IsVertical());
    }

    // ---------------------------------------------------------------------
    // docs/api/SpriteSheetManager.md and docs/api/TileSet.md / TileMap.md.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the SpriteSheetManager examples: load by path, load by stream, get and contains.</summary>
    [Fact]
    public void SpriteSheetManager_LoadsAndGets()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var manager = new SpriteSheetManager();

        // By path (desktop host).
        var byPath = manager.Load("hero", fixtures.PathOf(FixtureAssets.FullSheet));
        Assert.Equal("hero", byPath.Name);
        Assert.Equal(SpriteSheetType.Full, byPath.Type);
        Assert.Null(byPath.PartType);

        // By stream (WebAssembly host).
        using (var stream = FixtureAssets.DecodePngStream(FixtureAssets.PartBody))
        {
            var part = manager.LoadPart("body", stream, CharacterPartType.Body);
            Assert.Equal(SpriteSheetType.Part, part.Type);
            Assert.Equal(CharacterPartType.Body, part.PartType);
        }

        Assert.True(manager.Contains("hero"));
        Assert.Same(byPath, manager.Get("hero"));
        Assert.Throws<InvalidOperationException>(() => manager.Load("hero", fixtures.PathOf(FixtureAssets.FullSheet)));
    }

    /// <summary>Verifies the TileSet standalone-loading examples (TSX by path and by stream).</summary>
    [Fact]
    public void TileSet_LoadsStandalone()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();

        var byPath = TileSet.Load(fixtures.PathOf(FixtureAssets.TilesetFile));
        Assert.Equal("rpg_fixture_tiles", byPath.Name);

        // The committed tileset contains 4 tiles (local IDs 0..3); each is 48×48.
        using var tile = byPath.GetTileImage(localTileId: 3);
        Assert.Equal(48, tile.Width);
        Assert.Equal(48, tile.Height);
        Assert.Throws<ArgumentOutOfRangeException>(() => byPath.GetTileImage(localTileId: 4));
    }

    /// <summary>Verifies the TileMap examples: file-based and stream-based loading, layers and tile lookup.</summary>
    [Fact]
    public void TileMap_LoadsAndQueries()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();

        // File-based (desktop host).
        var map = TileMap.Load(fixtures.PathOf(FixtureAssets.MapFile));
        Assert.Equal(16, map.Width);
        Assert.Equal(12, map.Height);
        Assert.Equal(2, map.Layers.Count);
        Assert.Equal("ground", map.Layers[0].Name);
        Assert.True(map.GetTileId("ground", 0, 0) >= 1); // a grass tile
        Assert.False(map.IsSolid(1, 1));

        // Stream-based (WebAssembly host): the external TSX and its image are resolved through
        // the fetcher relative to the map URI.
        var mapBytes = File.ReadAllBytes(fixtures.PathOf(FixtureAssets.MapFile));
        var streamMap = TileMap.Load(
            new MemoryStream(mapBytes, writable: false),
            new Uri("file:///fixtures/map.tmx"),
            uri => uri.AbsolutePath.EndsWith(FixtureAssets.TilesetFile, StringComparison.Ordinal)
                ? System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(fixtures.PathOf(FixtureAssets.TilesetFile)))
                : File.ReadAllBytes(fixtures.PathOf(FixtureAssets.TilesImage)));

        Assert.Equal(map.Width, streamMap.Width);
        Assert.Equal(map.Height, streamMap.Height);
    }

    // ---------------------------------------------------------------------
    // docs/api/TileMapLayer.md and docs/api/TileFlags.md.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the TileMapLayer and TileFlags examples.</summary>
    [Fact]
    public void TileMapLayer_AndTileFlags()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var map = TileMap.Load(fixtures.PathOf(FixtureAssets.MapFile));
        var ground = map.Layers[0];

        Assert.True(ground.Visible);
        Assert.Equal(1f, ground.Opacity);
        Assert.Equal(16, ground.Width);
        Assert.Equal(12, ground.Height);
        Assert.Equal(16 * 12, ground.TileIds.Count);

        var gid = ground.GetTileId(0, 0);
        Assert.True((gid & (uint)TileFlags.Mask) == gid, "Tile IDs stored by the engine have flip bits masked off.");
    }

    // ---------------------------------------------------------------------
    // docs/api/SpriteSheetType.md and docs/api/CharacterPartType.md.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the full set of sheet types and part types is documented and usable.</summary>
    [Fact]
    public void SpriteSheetType_AndCharacterPartType_Values()
    {
        Assert.Equal(
            new[] { SpriteSheetType.Full, SpriteSheetType.Part },
            Enum.GetValues<SpriteSheetType>());

        Assert.Equal(
            new[]
            {
                CharacterPartType.Body, CharacterPartType.Armour, CharacterPartType.Face,
                CharacterPartType.FaceHair, CharacterPartType.Hair1, CharacterPartType.Hair2,
                CharacterPartType.Head,
            },
            Enum.GetValues<CharacterPartType>());
    }

    // ---------------------------------------------------------------------
    // docs/api/TiledAssetFetcher.md.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a TiledAssetFetcher delegate resolves assets by URI.</summary>
    [Fact]
    public void TiledAssetFetcher_ResolvesAssetsByUri()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();

        TiledAssetFetcher fetcher = uri =>
        {
            var name = Path.GetFileName(uri.AbsolutePath);
            if (name == FixtureAssets.TilesetFile)
            {
                return System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(fixtures.PathOf(FixtureAssets.TilesetFile)));
            }

            if (name == FixtureAssets.TilesImage)
            {
                return File.ReadAllBytes(fixtures.PathOf(FixtureAssets.TilesImage));
            }

            throw new FileNotFoundException($"Unknown asset '{uri}'.");
        };

        var mapBytes = File.ReadAllBytes(fixtures.PathOf(FixtureAssets.MapFile));
        var map = TileMap.Load(new MemoryStream(mapBytes, writable: false), new Uri("file:///fixtures/map.tmx"), fetcher);

        Assert.Equal(16, map.Width);
    }

    // ---------------------------------------------------------------------
    // Async loading (story 22): the async stream-based loaders work with
    // read-async-only streams and async asset fetchers.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the async spritesheet loading example: SpriteSheetManager.LoadAsync and GameEngine.LoadSpriteSheetAsync from an async-only stream.</summary>
    [Fact]
    public async Task AsyncSheetLoading_LoadsAndRegisters()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();

        // SpriteSheetManager.LoadAsync: a stream that only supports asynchronous reads, as a
        // browser host would provide.
        using (var stream = new AsyncOnlyStream(FixtureAssets.DecodePngStream(FixtureAssets.FullSheet)))
        {
            var manager = new SpriteSheetManager();
            var sheet = await manager.LoadAsync("hero", stream);

            Assert.Equal("hero", sheet.Name);
            Assert.Equal(SpriteSheetType.Full, sheet.Type);
        }

        // GameEngine.LoadSpriteSheetAsync: the engine-level delegating overload.
        var engine = new GameEngine();
        using (var stream = new AsyncOnlyStream(FixtureAssets.DecodePngStream(FixtureAssets.FullSheet)))
        {
            await engine.LoadSpriteSheetAsync("hero", stream);
        }
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        using var bitmap = new SKBitmap(48, 48);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.Render(canvas, FrameDt);
        }

        Assert.NotEqual(0, bitmap.GetPixel(24, 24).Alpha);
    }

    /// <summary>Verifies the async map loading example: TileMap.LoadAsync with a TiledAssetFetcherAsync resolving external assets over HTTP.</summary>
    [Fact]
    public async Task AsyncMapLoading_WithAsyncFetcher()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();

        var mapBytes = File.ReadAllBytes(fixtures.PathOf(FixtureAssets.MapFile));
        using var stream = new AsyncOnlyStream(new MemoryStream(mapBytes, writable: false));

        // A TiledAssetFetcherAsync mirrors what HttpClient.GetByteArrayAsync would do.
        TiledAssetFetcherAsync fetcher = async uri =>
        {
            await Task.Delay(1);
            var name = Path.GetFileName(uri.AbsolutePath);
            if (name == FixtureAssets.TilesetFile)
            {
                return System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(fixtures.PathOf(FixtureAssets.TilesetFile)));
            }

            if (name == FixtureAssets.TilesImage)
            {
                return File.ReadAllBytes(fixtures.PathOf(FixtureAssets.TilesImage));
            }

            throw new FileNotFoundException($"Unknown asset '{uri}'.");
        };

        var map = await TileMap.LoadAsync(stream, new Uri("file:///fixtures/map.tmx"), fetcher);

        Assert.Equal(16, map.Width);
        Assert.Equal(12, map.Height);
        Assert.Equal(2, map.Layers.Count);
    }
}
