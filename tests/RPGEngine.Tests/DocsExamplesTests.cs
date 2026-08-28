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
    // docs/api/GameEngine.md and docs/api/Player.md: click-to-move auto-walk
    // and the movement-state events OnStartMoving / OnStopMoving (story 38/69).
    // ---------------------------------------------------------------------
    /// <summary>
    /// The click-to-move example from the documentation: render once so the engine knows the
    /// canvas size, click a distant walkable tile, and the player auto-walks along the A* path
    /// to the clicked tile center while <see cref="Player.OnStartMoving"/> fires when the walk
    /// (and each step) starts and <see cref="Player.OnStopMoving"/> fires exactly once when it
    /// completes.
    /// </summary>
    [Fact]
    public void ClickToMove_AutoWalksToClickedTileAndFiresOnStartAndStopMoving()
    {
        using var fixture = new TiledTestFixture(
            10, 10,
            new[] { new TileLayerSpec("ground", Enumerable.Repeat(1u, 10 * 10).ToArray()) });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        engine.Player.Position = new Position(0.5, 1.5);

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        // Render at least once so the engine knows the canvas size, then translate a mouse click.
        const int canvas = 480; // 10 tiles x 48 px: the whole map is visible
        using (var bitmap = new SKBitmap(canvas, canvas))
        using (var renderCanvas = new SKCanvas(bitmap))
        {
            engine.Render(renderCanvas, FrameDt);
        }

        // Click at the canvas position of the tile the host wants the player to walk to.
        var surface = engine.WorldToSurface(new Position(5.5, 5.5), canvas, canvas);
        engine.Click(surface.X, surface.Y);

        // The player now auto-walks along the A* path; drive the loop normally.
        var target = new Position(5.5, 5.5);
        for (var frame = 0; frame < 5000 && engine.Player.Position != target; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(target, engine.Player.Position);
        Assert.NotEmpty(starts);  // the walk (and each step) started
        Assert.Single(stops);     // and completed (stopped) exactly once at the end
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
    // docs/api/GameEngine.md: the RenderMinimap example. The minimap renders
    // the map's prerendered layers plus a green player dot and yellow NPC dots
    // on a separate canvas; zoomLevel 1.0 fits the whole map, > 1 zooms in
    // around the player's dot (clamped to the map edges). When a map is set
    // the canvas is cleared to black first, so the unused margins are black.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies the RenderMinimap doc example: the default fit draws the whole map and the dots
    /// on a separate minimap canvas, and a zoomed render shows the region around the player's
    /// dot with the green/yellow dots present.
    /// </summary>
    [Fact]
    public void RenderMinimap_Example_DefaultFitAndZoom()
    {
        using var fixture = new TiledTestFixture(
            4, 2,
            new[] { new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1, 1, 1, 1, 1 }) });
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        engine.Player.Position = new Position(0.5, 0.5);
        engine.Characters.Add(new Character { Position = new Position(3.5, 1.5) });

        // Default fit: the whole map is drawn into the minimap canvas, centered, aspect preserved;
        // the unused margins are black (the minimap clears its canvas to black when a map is set).
        using var minimap = new SKBitmap(240, 240);
        using (var canvas = new SKCanvas(minimap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.RenderMinimap(canvas, zoomLevel: 1.0);
        }

        // A 4x2 (192x96) map on a 240x240 canvas: baseFit = min(240/192, 240/96) = 1.25, so the
        // map is scaled to 240x120 and centered vertically (60 px margins); a map pixel and the
        // dots are present at their scaled positions.
        Assert.NotEqual(0, minimap.GetPixel(120, 90).Alpha);   // a tile pixel inside the map
        Assert.Equal(SKColors.Green, minimap.GetPixel(30, 90));  // player dot (0.5,0.5) -> (30,90)
        Assert.Equal(SKColors.Yellow, minimap.GetPixel(210, 150)); // NPC dot (3.5,1.5) -> (210,150)
        Assert.Equal(SKColors.Black, minimap.GetPixel(120, 5)); // top margin black

        // Zoomed in: the view is centered on the player's dot and clamps at the map edges.
        using var zoomed = new SKBitmap(240, 240);
        using (var canvas = new SKCanvas(zoomed))
        {
            canvas.Clear(SKColors.Transparent);
            engine.RenderMinimap(canvas, zoomLevel: 4.0);
        }

        // scale = 1.25 * 4 = 5, visible region = 48x48 map px centered on the player at map px
        // (24,24) and clamped to the top-left corner, so tile (0,0) fills the canvas and the
        // player's green dot stays centered. (The NPC at (3.5,1.5) is outside this region and its
        // dot is skipped.)
        Assert.Equal(SKColors.Green, zoomed.GetPixel(120, 120));
        Assert.Equal(SKColors.Red, zoomed.GetPixel(40, 40));
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
        // Standard RPG Maker MZ sheet: 576×384 → 48×48 cells.
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
            Position = new Position(2, 2),
            Direction = Direction.Down,
            BaseSpeed = 2,
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

    /// <summary>
    /// Verifies the <c>AnimationCycleSpeed</c> example (docs/api/Character.md): the walk cycle is
    /// time-based and speed-scaled, so tuning <c>AnimationCycleSpeed</c> changes the cycle rate
    /// relative to <c>BaseSpeed</c>.
    /// </summary>
    [Fact]
    public void Character_AnimationCycleSpeed_TunesWalkCycle()
    {
        // At BaseSpeed == AnimationCycleSpeed == 2 (tiles/s) the 4-frame walk cycle
        // (0 -> 1 -> 2 -> 1) completes once per second: secondsPerFrame = 2 / (2 * 4) = 0.25 s/frame.
        var character = new Character { BaseSpeed = 2, AnimationCycleSpeed = 2 };
        character.Move(Direction.Down, speedFactor: 1, dt: 0.25);
        character.Update(dt: 0.25);
        Assert.Equal(0, character.AnimationFrame); // advanced one frame step (1 -> 0)

        // Doubling AnimationCycleSpeed (the reference speed) halves the cycle rate: the same
        // 0.25 s only accumulates half a frame, so the standing frame (1) is kept.
        var slow = new Character { BaseSpeed = 2, AnimationCycleSpeed = 4 };
        slow.Move(Direction.Down, speedFactor: 1, dt: 0.25);
        slow.Update(dt: 0.25);
        Assert.Equal(1, slow.AnimationFrame); // still the standing frame
    }

    /// <summary>
    /// Verifies the <c>StartMoving</c>/<c>StopMoving</c>/<c>IsMoving</c> example
    /// (docs/api/Character.md): an NPC added to the engine's character list drives itself
    /// autonomously through the update loop, and <c>StopMoving</c> halts it.
    /// </summary>
    [Fact]
    public void Character_StartMovingStopMoving_DrivesNpcAutonomously()
    {
        var engine = new GameEngine();
        var npc = new Character { BaseSpeed = 2, Position = new Position(3, 4) };
        npc.SpriteSheets.Add(new SpriteSheetRef("villager", CharacterIndex: 2));
        engine.Characters.Add(npc);

        npc.StartMoving(Direction.Right); // faces right and begins moving on the next Update
        Assert.True(npc.IsMoving);
        Assert.Equal(Direction.Right, npc.Direction);
        Assert.Equal(new Position(3, 4), npc.Position); // not moved until Update

        engine.Update(dt: 1); // the engine's update loop drives the NPC

        Assert.Equal(new Position(5, 4), npc.Position);

        npc.StopMoving(); // the NPC stops; the walk cycle snaps to the standing frame
        Assert.False(npc.IsMoving);

        engine.Update(dt: 1);
        Assert.Equal(new Position(5, 4), npc.Position); // no further movement
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

    /// <summary>
    /// Verifies the <c>GetMovementDirection</c> example (docs/api/GameConfig.md): the held bound
    /// keys combine into a single 8-direction vector, with opposite keys cancelling.
    /// </summary>
    [Fact]
    public void GameConfig_GetMovementDirection_CombinesHeldKeys()
    {
        var config = new GameConfig();

        // A single key maps to its cardinal direction.
        Assert.Equal(Direction.Up, config.GetMovementDirection([Key.W]));

        // Two perpendicular keys combine into a diagonal.
        Assert.Equal(Direction.UpRight, config.GetMovementDirection([Key.W, Key.D]));

        // Opposite keys cancel: no movement.
        Assert.Null(config.GetMovementDirection([Key.W, Key.S]));
        Assert.Null(config.GetMovementDirection([Key.A, Key.D]));

        // W + A + D: A and D cancel, leaving straight Up.
        Assert.Equal(Direction.Up, config.GetMovementDirection([Key.W, Key.A, Key.D]));

        // No bound keys (or only unmapped keys) produce no movement.
        Assert.Null(config.GetMovementDirection(Array.Empty<Key>()));
        Assert.Null(config.GetMovementDirection([Key.Space]));
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

        // Positions are in tiles: ToTile() floors to the containing cell, ToPixels(ts)
        // converts to pixels at the given tile size.
        var cell = moved.ToTile();
        Assert.Equal((13, 16), cell);
        Assert.Equal(new Position(624, 768), moved.ToPixels(tileSize: 48));

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

    /// <summary>
    /// Verifies the <c>TileSet.LoadAsync</c> example (docs/api/TileSet.md): a TSX loaded from an
    /// async-only stream with the image resolved through a <see cref="TiledAssetFetcherAsync"/>.
    /// </summary>
    [Fact]
    public async Task TileSet_LoadsStandaloneAsync()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();

        var tsxBytes = File.ReadAllText(fixtures.PathOf(FixtureAssets.TilesetFile));
        using var stream = new AsyncOnlyStream(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(tsxBytes), writable: false));

        // A TiledAssetFetcherAsync mirrors what HttpClient.GetByteArrayAsync would do: the image
        // source declared by the TSX is resolved relative to the TSX URI and awaited.
        TiledAssetFetcherAsync fetcher = async uri =>
        {
            await Task.Delay(1);
            Assert.EndsWith(FixtureAssets.TilesImage, uri.AbsolutePath, StringComparison.Ordinal);
            return FixtureAssets.DecodePng(FixtureAssets.TilesImage);
        };

        var tileset = await TileSet.LoadAsync(stream, new Uri("file:///fixtures/tiles.tsx"), fetcher);

        Assert.Equal("rpg_fixture_tiles", tileset.Name);
        Assert.Equal(48, tileset.TileWidth);
        Assert.Equal(48, tileset.TileHeight);
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
        Assert.Equal(3, map.Layers.Count); // ground + decor + trees_above
        Assert.Equal("ground", map.Layers[0].Name);
        Assert.Equal("decor", map.Layers[1].Name);
        Assert.Equal("trees_above", map.Layers[2].Name);
        Assert.True(map.Layers[2].AbovePlayer); // the committed above_player layer
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
    // docs/api/TileMap.md: reading map custom properties, object layers and
    // object properties (story 25).
    // ---------------------------------------------------------------------
    /// <summary>Verifies the TileMap custom-properties and object-layer examples: map properties via GetProperty, object layers in file order, and per-object properties.</summary>
    [Fact]
    public void TileMap_CustomPropertiesAndObjectLayers()
    {
        using var fixture = new TiledTestFixture(
            2,
            2,
            new[] { new TileLayerSpec("ground", new uint[] { 1, 1, 1, 1 }) },
            mapProperties: new[]
            {
                new FixtureProperty("difficulty", "string", "hard"),
                new FixtureProperty("ambient_light", "float", "0.8"),
            },
            objectLayers: new[]
            {
                new ObjectLayerSpec(
                    "objects",
                    new[]
                    {
                        new ObjectSpec(
                            Id: 1,
                            Name: "chest",
                            Type: "treasure",
                            X: 48,
                            Y: 96,
                            Width: 32,
                            Height: 32,
                            Shape: FixtureObjectShape.Rectangle,
                            Properties: new[] { new FixtureProperty("coins", "int", "100") }),
                        new ObjectSpec(Id: 2, Name: "spawn", Type: "point", X: 0, Y: 0, Width: 0, Height: 0, Shape: FixtureObjectShape.Point),
                    }),
            });
        var map = TileMap.Load(fixture.MapPath);

        // Map custom properties: GetProperty looks up by exact (case-sensitive) name.
        var difficulty = map.GetProperty("difficulty");
        Assert.NotNull(difficulty);
        Assert.Equal(MapPropertyType.String, difficulty.Type);
        Assert.Equal("hard", Assert.IsType<string>(difficulty.Value));
        Assert.Null(map.GetProperty("Difficulty"));
        Assert.Equal(0.8f, Assert.IsType<float>(map.GetProperty("ambient_light")!.Value));

        // Object layers are exposed separately from the tile layers, in file order.
        Assert.Single(map.ObjectLayers);
        Assert.Equal("objects", map.ObjectLayers[0].Name);
        Assert.Single(map.Layers); // only the tile layer

        // Objects expose identity, geometry, shape and their own custom properties.
        var chest = map.ObjectLayers[0].Objects.Single(o => o.Name == "chest");
        Assert.Equal(1u, chest.Id);
        Assert.Equal("treasure", chest.Type);
        Assert.Equal(new Position(48, 96), chest.Position);
        Assert.Equal(TileMapObjectShape.Rectangle, chest.Shape);
        Assert.Equal(100, chest.Properties.Single(p => p.Name == "coins").Value);

        var spawn = map.ObjectLayers[0].Objects.Single(o => o.Name == "spawn");
        Assert.Equal(TileMapObjectShape.Point, spawn.Shape);
        Assert.Equal(new Position(0, 0), spawn.Position);
    }

    /// <summary>
    /// Verifies the committed fixture map (docs/api/TileMap.md, docs/api/TileMapObject.md and
    /// docs/api/TileMapLayer.md): map custom properties, the object layer with its objects and
    /// their properties, and the <c>trees_above</c> <c>above_player</c> tile layer.
    /// </summary>
    [Fact]
    public void TileMap_CommittedFixture_ReadsPropertiesAndObjectLayers()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var map = TileMap.Load(fixtures.PathOf(FixtureAssets.MapFile));

        // Map custom properties: two strings, a bool flag and an int.
        Assert.Equal("RPG Engine Fixture Map", map.GetProperty("name")?.Value);
        Assert.Equal("RPG Engine QA", map.GetProperty("author")?.Value);
        Assert.Equal(true, map.GetProperty("is_demo")?.Value);
        Assert.Equal(3, map.GetProperty("difficulty")?.Value);
        Assert.Null(map.GetProperty("missing"));

        // The above_player tile layer (a "trees above" layer using the tree tile).
        var treesAbove = map.Layers.Single(layer => layer.Name == "trees_above");
        Assert.True(treesAbove.AbovePlayer);
        var aboveProperty = treesAbove.Properties.Single(p => p.Name == "above_player");
        Assert.Equal(MapPropertyType.Bool, aboveProperty.Type);
        Assert.Equal(true, aboveProperty.Value);

        // The object layer exposes its objects and their custom properties.
        var objects = map.ObjectLayers.Single(layer => layer.Name == "objects");
        Assert.Equal(3, objects.Objects.Count);

        var spawn = objects.Objects.Single(obj => obj.Name == "spawn");
        Assert.Equal("spawn", spawn.Type);
        Assert.Equal(TileMapObjectShape.Point, spawn.Shape);
        Assert.Equal(new Position(288, 288), spawn.Position);
        Assert.Equal("down", spawn.Properties.Single(p => p.Name == "facing").Value);

        var chest = objects.Objects.Single(obj => obj.Name == "chest");
        Assert.Equal("treasure", chest.Type);
        Assert.Equal(TileMapObjectShape.Rectangle, chest.Shape);
        Assert.Equal(new Position(48, 96), chest.Position);
        Assert.Equal(true, chest.Properties.Single(p => p.Name == "locked").Value);
        Assert.Equal(100, chest.Properties.Single(p => p.Name == "coins").Value);

        var guardPatrol = objects.Objects.Single(obj => obj.Name == "guard_patrol");
        Assert.Equal(TileMapObjectShape.Polyline, guardPatrol.Shape);
        Assert.Equal(48, guardPatrol.Properties.Single(p => p.Name == "speed").Value);
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

        // The committed trees_above layer declares the above_player bool property (story 24/26).
        var treesAbove = map.Layers.Single(layer => layer.Name == "trees_above");
        Assert.True(treesAbove.AbovePlayer);
        Assert.Contains(treesAbove.Properties, p => p.Name == "above_player" && p.Type == MapPropertyType.Bool);
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
        engine.Player.Position = new Position(0.5, 1.0); // sprite fills the 48×48 bitmap

        using var bitmap = new SKBitmap(48, 48);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.Render(canvas, FrameDt);
        }

        Assert.NotEqual(0, bitmap.GetPixel(24, 24).Alpha);
    }

    /// <summary>
    /// Verifies the async part-sheet loading examples (docs/api/SpriteSheetManager.md and
    /// docs/api/GameEngine.md): <c>SpriteSheetManager.LoadPartAsync</c> and
    /// <c>GameEngine.LoadPartSpriteSheetAsync</c> from async-only streams.
    /// </summary>
    [Fact]
    public async Task AsyncPartSheetLoading_LoadsAndRegisters()
    {
        // SpriteSheetManager.LoadPartAsync.
        using (var stream = new AsyncOnlyStream(FixtureAssets.DecodePngStream(FixtureAssets.PartBody)))
        {
            var manager = new SpriteSheetManager();
            var body = await manager.LoadPartAsync("body", stream, CharacterPartType.Body);

            Assert.Equal("body", body.Name);
            Assert.Equal(SpriteSheetType.Part, body.Type);
            Assert.Equal(CharacterPartType.Body, body.PartType);
        }

        // GameEngine.LoadPartSpriteSheetAsync: the engine-level delegating overload.
        var engine = new GameEngine();
        using (var stream = new AsyncOnlyStream(FixtureAssets.DecodePngStream(FixtureAssets.PartBody)))
        {
            await engine.LoadPartSpriteSheetAsync("hero_body", stream, CharacterPartType.Body);
        }

        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero_body", CharacterIndex: 1));
        Assert.Single(engine.Player.SpriteSheets);
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
        Assert.Equal(3, map.Layers.Count); // ground + decor + trees_above
    }
}
