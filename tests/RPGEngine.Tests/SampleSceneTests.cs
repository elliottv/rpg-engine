using RPGEngine.Sprites;
using RPGEngine.Tests.Fixtures;
using RPGEngine.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// End-to-end smoke tests (story 6) that run the exact scene the desktop and WebAssembly sample
/// hosts use against the committed fixture assets, and assert that <c>Render</c> produces a
/// non-empty frame. These tests close the epic's "proof the engine works from a real host"
/// requirement in an automatable way.
/// </summary>
public class SampleSceneTests
{
    private const int CanvasWidth = 640;
    private const int CanvasHeight = 480;

    // ---------------------------------------------------------------------
    // Acceptance 4 (automated part): a smoke test asserting Render produces a
    // non-empty frame for the exact scene the samples use.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the fixture map loads with the committed dimensions and two layers.</summary>
    [Fact]
    public void FixtureMap_LoadsWithExpectedDimensionsAndLayers()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var engine = SampleScene.Create(fixtures.Root);

        Assert.NotNull(engine.Map);
        Assert.Equal(16, engine.Map!.Width);
        Assert.Equal(12, engine.Map.Height);
        Assert.Equal(48, engine.Map.TileWidth);
        Assert.Equal(48, engine.Map.TileHeight);
        Assert.Equal(16 * 48, engine.Map.PixelWidth);
        Assert.Equal(12 * 48, engine.Map.PixelHeight);
        Assert.Equal(2, engine.Map.Layers.Count); // ground + decor
    }

    /// <summary>Verifies rendering the sample scene produces a non-empty frame (map + player + two NPCs).</summary>
    [Fact]
    public void Render_ProducesNonEmptyFrame_ForTheSampleScene()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var engine = SampleScene.Create(fixtures.Root);

        using var bitmap = Render(engine, CanvasWidth, CanvasHeight);

        var nonEmptyPixels = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    nonEmptyPixels++;
                }
            }
        }

        Assert.True(nonEmptyPixels > 0, "The sample scene must render at least one non-transparent pixel.");
    }

    // ---------------------------------------------------------------------
    // The scene is deterministic: the player uses the full sheet (seed 1,
    // character slot 1) and the NPCs use part sheets whose top-most drawn part
    // matches the fixed composition order, so the centre pixel of each sprite
    // is exactly predictable from the committed fixtures.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the player's centre pixel matches the full-sheet fixture (character slot 1, standing, facing down).</summary>
    [Fact]
    public void Render_PlayerCentrePixel_MatchesFullSheetFixture()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var engine = SampleScene.Create(fixtures.Root);

        // Camera origin for a 640×480 view with the player at (288, 288): (0, 48).
        // Player screen top-left = (288, 240); centre = (312, 264).
        using var bitmap = Render(engine, CanvasWidth, CanvasHeight);

        var expected = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, frame: 1);
        Assert.Equal(expected, bitmap.GetPixel(312, 264));
    }

    /// <summary>Verifies the villager NPC's centre pixel is the top-most part (hair1) per the fixed composition order.</summary>
    [Fact]
    public void Render_VillagerCentrePixel_IsTopMostPart()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var engine = SampleScene.Create(fixtures.Root);

        // Villager world (144, 192) → screen (144, 144); centre (168, 168).
        using var bitmap = Render(engine, CanvasWidth, CanvasHeight);

        // Parts drawn: hair2 (absent), face (seed 3), body (seed 2), hair1 (seed 4, on top).
        var expected = CharacterTestHelper.SpriteColor(seed: 4, characterIndex: 2, Direction.Down, frame: 1);
        Assert.Equal(expected, bitmap.GetPixel(168, 168));
    }

    /// <summary>Verifies the guard NPC's centre pixel is the top-most part (head) per the fixed composition order.</summary>
    [Fact]
    public void Render_GuardCentrePixel_IsTopMostPart()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var engine = SampleScene.Create(fixtures.Root);

        // Guard world (528, 384) → screen (528, 336); centre (552, 360).
        using var bitmap = Render(engine, CanvasWidth, CanvasHeight);

        // Parts drawn: face (seed 3), body (seed 2), armour (seed 7), head (seed 8, on top).
        var expected = CharacterTestHelper.SpriteColor(seed: 8, characterIndex: 3, Direction.Down, frame: 1);
        Assert.Equal(expected, bitmap.GetPixel(552, 360));
    }

    // ---------------------------------------------------------------------
    // Explicit composition-ordering test against the committed part sheets:
    // removing the top-most part reveals the next one.
    // ---------------------------------------------------------------------
    /// <summary>Verifies that removing the top-most part of a part-composed character reveals the next part in the fixed order.</summary>
    [Fact]
    public void PartComposition_RemovingTopPart_RevealsNextPart()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();

        var engine = new GameEngine();
        engine.LoadPartSpriteSheet("body", fixtures.PathOf(FixtureAssets.PartBody), CharacterPartType.Body);
        engine.LoadPartSpriteSheet("face", fixtures.PathOf(FixtureAssets.PartFace), CharacterPartType.Face);
        engine.LoadPartSpriteSheet("hair1", fixtures.PathOf(FixtureAssets.PartHair1), CharacterPartType.Hair1);

        var character = new Character();
        character.SpriteSheets.Add(new SpriteSheetRef("body", CharacterIndex: 1));
        character.SpriteSheets.Add(new SpriteSheetRef("face", CharacterIndex: 1));
        character.SpriteSheets.Add(new SpriteSheetRef("hair1", CharacterIndex: 1));
        engine.Characters.Add(character);

        // Draw order is face -> body -> hair1, so hair1 (seed 4) is on top.
        using (var bitmap = Render(engine, 48, 48))
        {
            Assert.Equal(
                CharacterTestHelper.SpriteColor(seed: 4, characterIndex: 1, Direction.Down, frame: 1),
                bitmap.GetPixel(24, 24));
        }

        // Remove hair1: body (seed 2) is now on top (drawn after face).
        character.SpriteSheets.Remove(new SpriteSheetRef("hair1", CharacterIndex: 1));
        using (var bitmap = Render(engine, 48, 48))
        {
            Assert.Equal(
                CharacterTestHelper.SpriteColor(seed: 2, characterIndex: 1, Direction.Down, frame: 1),
                bitmap.GetPixel(24, 24));
        }

        // Remove body: face (seed 3) is now on top.
        character.SpriteSheets.Remove(new SpriteSheetRef("body", CharacterIndex: 1));
        using (var bitmap = Render(engine, 48, 48))
        {
            Assert.Equal(
                CharacterTestHelper.SpriteColor(seed: 3, characterIndex: 1, Direction.Down, frame: 1),
                bitmap.GetPixel(24, 24));
        }
    }

    // ---------------------------------------------------------------------
    // The WebAssembly host loads the same scene through the file-system-free
    // (stream) entry points; verify those code paths work with the committed
    // fixtures too.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the full scene can be built through the stream-based (file-system-free) loaders used by the WebAssembly host.</summary>
    [Fact]
    public void BuildSceneFromStreams_MatchesFileBasedScene()
    {
        var mapBytes = File.ReadAllBytes(FixtureAssets.FilePath(FixtureAssets.MapFile));

        var engine = new GameEngine
        {
            Map = TileMap.Load(new MemoryStream(mapBytes, writable: false), new Uri("file:///fixtures/map.tmx"), FetchAsset),
        };

        using (var full = FixtureAssets.DecodePngStream(FixtureAssets.FullSheet))
        {
            engine.LoadSpriteSheet("hero", full);
        }
        engine.Player.Position = SampleScene.PlayerPosition;
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        engine.LoadPartSpriteSheet("villager_body", FixtureAssets.DecodePngStream(FixtureAssets.PartBody), CharacterPartType.Body);
        engine.LoadPartSpriteSheet("villager_face", FixtureAssets.DecodePngStream(FixtureAssets.PartFace), CharacterPartType.Face);
        engine.LoadPartSpriteSheet("villager_hair1", FixtureAssets.DecodePngStream(FixtureAssets.PartHair1), CharacterPartType.Hair1);

        var villager = new Character { Position = SampleScene.VillagerPosition };
        villager.SpriteSheets.Add(new SpriteSheetRef("villager_body", CharacterIndex: 2));
        villager.SpriteSheets.Add(new SpriteSheetRef("villager_face", CharacterIndex: 2));
        villager.SpriteSheets.Add(new SpriteSheetRef("villager_hair1", CharacterIndex: 2));
        engine.Characters.Add(villager);

        using var bitmap = Render(engine, CanvasWidth, CanvasHeight);

        Assert.Equal(
            CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, frame: 1),
            bitmap.GetPixel(312, 264));
        Assert.Equal(
            CharacterTestHelper.SpriteColor(seed: 4, characterIndex: 2, Direction.Down, frame: 1),
            bitmap.GetPixel(168, 168));

        // The fetcher resolves the external tileset and its image relative to the map URI.
        static byte[] FetchAsset(Uri uri)
        {
            if (uri.AbsolutePath.EndsWith(FixtureAssets.TilesetFile, StringComparison.Ordinal))
            {
                return System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(FixtureAssets.FilePath(FixtureAssets.TilesetFile)));
            }

            if (uri.AbsolutePath.EndsWith(FixtureAssets.TilesImage, StringComparison.Ordinal))
            {
                return FixtureAssets.DecodePng(FixtureAssets.TilesImage);
            }

            throw new FileNotFoundException($"Unexpected asset URI '{uri}'.");
        }
    }

    /// <summary>Renders the engine into a fresh transparent bitmap of the requested size.</summary>
    private static SKBitmap Render(GameEngine engine, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            engine.Render(canvas, dt: 1.0 / 60);
        }

        return bitmap;
    }
}
