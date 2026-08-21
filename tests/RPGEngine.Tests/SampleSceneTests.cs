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
    /// <summary>Verifies the fixture map loads with the committed dimensions and three layers.</summary>
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
        Assert.Equal(3, engine.Map.Layers.Count); // ground + decor + trees_above
    }

    /// <summary>
    /// Verifies the extended fixture map (story 26) exposes the documented map custom properties,
    /// the object layer with its objects (and their properties) and the <c>above_player</c> tile
    /// layer, so the sample hosts and end-to-end tests exercise the new read-model API.
    /// </summary>
    [Fact]
    public void FixtureMap_ExposesPropertiesObjectLayersAndAbovePlayer()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var engine = SampleScene.Create(fixtures.Root);
        var map = engine.Map!;

        // Map custom properties (string, string, bool, int), looked up case-sensitively.
        Assert.Equal("RPG Engine Fixture Map", map.GetProperty("name")?.Value);
        Assert.Equal("RPG Engine QA", map.GetProperty("author")?.Value);
        Assert.Equal(true, map.GetProperty("is_demo")?.Value);
        Assert.Equal(3, map.GetProperty("difficulty")?.Value);
        Assert.Null(map.GetProperty("Name")); // case-sensitive lookup

        // The trees_above tile layer declares above_player = true and renders above the player.
        var treesAbove = map.Layers.Single(layer => layer.Name == "trees_above");
        Assert.True(treesAbove.AbovePlayer);
        Assert.True(map.Layers.Where(l => l.AbovePlayer).SequenceEqual(new[] { treesAbove }));

        // The object layer exposes spawn / chest / guard_patrol and their properties.
        var objects = map.ObjectLayers.Single(layer => layer.Name == "objects");
        Assert.Equal(3, objects.Objects.Count);

        var spawn = objects.Objects.Single(obj => obj.Name == "spawn");
        Assert.Equal(TileMapObjectShape.Point, spawn.Shape);
        // The spawn object is a Tiled object-layer position, which stays in pixels: (6, 6)
        // tiles = (288, 288) px (the tile-unit equivalent of SampleScene.PlayerPosition).
        Assert.Equal(new Position(288, 288), spawn.Position);

        var chest = objects.Objects.Single(obj => obj.Name == "chest");
        Assert.Equal(true, chest.Properties.Single(p => p.Name == "locked").Value);
        Assert.Equal(100, chest.Properties.Single(p => p.Name == "coins").Value);

        // The sample scene still renders a non-empty frame with the extended map.
        using var bitmap = Render(engine, CanvasWidth, CanvasHeight);
        Assert.NotEqual(0, bitmap.GetPixel(CanvasWidth / 2, CanvasHeight / 2).Alpha);
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

        // The committed scene spawns the player at (6, 6), which with the middle-bottom anchor
        // places the sprite inside the tree canopy: the above_player tree layer fully occludes it
        // (the tree is drawn over the player, as designed). To verify the anchored render, stand
        // the player on the clear path at (6, 8): the camera origin for a 640×480 view becomes
        // (0, 2), so the feet are at (288, 288) px and the 48×48 sprite centre is (288, 264).
        engine.Player.Position = new Position(6, 8);
        using var bitmap = Render(engine, CanvasWidth, CanvasHeight);

        var expected = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, frame: 1);
        Assert.Equal(expected, bitmap.GetPixel(288, 264));
    }

    /// <summary>Verifies the villager NPC's centre pixel is the top-most part (hair1) per the fixed composition order.</summary>
    [Fact]
    public void Render_VillagerCentrePixel_IsTopMostPart()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var engine = SampleScene.Create(fixtures.Root);

        // Villager world (3, 4) tiles with the camera origin (0, 1) → feet at (144, 144) px;
        // the 48×48 sprite is anchored at its middle-bottom, so its centre is (144, 120).
        using var bitmap = Render(engine, CanvasWidth, CanvasHeight);

        // Parts drawn: hair2 (absent), face (seed 3), body (seed 2), hair1 (seed 4, on top).
        var expected = CharacterTestHelper.SpriteColor(seed: 4, characterIndex: 2, Direction.Down, frame: 1);
        Assert.Equal(expected, bitmap.GetPixel(144, 120));
    }

    /// <summary>Verifies the guard NPC's centre pixel is the top-most part (head) per the fixed composition order.</summary>
    [Fact]
    public void Render_GuardCentrePixel_IsTopMostPart()
    {
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        var engine = SampleScene.Create(fixtures.Root);

        // Guard world (11, 8) tiles with the camera origin (0, 1) → feet at (528, 336) px;
        // the 48×48 sprite is anchored at its middle-bottom, so its centre is (528, 312).
        using var bitmap = Render(engine, CanvasWidth, CanvasHeight);

        // Parts drawn: face (seed 3), body (seed 2), armour (seed 7), head (seed 8, on top).
        var expected = CharacterTestHelper.SpriteColor(seed: 8, characterIndex: 3, Direction.Down, frame: 1);
        Assert.Equal(expected, bitmap.GetPixel(528, 312));
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

        var character = new Character
        {
            // Anchor the sprite's middle-bottom at (24, 48) px so the 48×48 sprite fills the
            // 48×48 render bitmap (top-left (0, 0), centre (24, 24)).
            Position = new Position(0.5, 1.0),
        };
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
        // The committed scene spawns the player at (6, 6), which with the middle-bottom anchor
        // is occluded by the tree canopy; stand it on the clear path at (6, 8) so the anchored
        // sprite is visible (camera origin (0, 2), feet (288, 288), centre (288, 264)).
        engine.Player.Position = new Position(6, 8);
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

        // Player centre at (288, 264) with the player at (6, 8) and origin (0, 2).
        Assert.Equal(
            CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, frame: 1),
            bitmap.GetPixel(288, 264));
        // Villager at (3, 4) with origin (0, 2): feet (144, 96), centre (144, 72).
        Assert.Equal(
            CharacterTestHelper.SpriteColor(seed: 4, characterIndex: 2, Direction.Down, frame: 1),
            bitmap.GetPixel(144, 72));

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
