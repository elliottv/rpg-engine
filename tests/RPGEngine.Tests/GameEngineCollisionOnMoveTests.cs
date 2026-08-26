using RPGEngine.Sprites;
using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for story 55: <see cref="Player.OnMove"/> fires when a collision stops the
/// player. The engine reports a fully blocked move (no net displacement after the axis-separated
/// resolution) through <c>Player.ReportBlockedMove</c> instead of <c>Player.ReportMovement</c>,
/// so OnMove fires with <see cref="PlayerMoveEventArgs.IsMoving"/> set to <see langword="false"/>
/// exactly once when the player stops because of a collision, even while the movement key is held
/// against the wall. These tests assert the exact event sequence (not the exact frame on which
/// the stop is reported), so they hold with either collision-resolution behavior.
/// </summary>
public class GameEngineCollisionOnMoveTests
{
    private const double FrameDt = 1.0 / 60;

    /// <summary>
    /// Verifies holding D against a solid column fires (true, Right) when movement starts, then
    /// (false, Right) exactly once when the player is fully blocked, and no further events while
    /// D stays held for many more frames.
    /// </summary>
    [Fact]
    public void Update_KeyHeldAgainstSolidTile_FiresOnMoveFalseExactlyOnce()
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
        engine.Player.Position = new Position(0.5, 1.0);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        engine.Input(Key.D, true);

        // Frame-by-frame Update(1/60): the player starts moving right, hits the wall, and keeps
        // pressing D against it for many more frames.
        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player stopped at the solid column: its footprint never enters it.
        Assert.True(engine.Player.Position.X + 0.5 <= 2.0 + 1e-9, "The footprint must never overlap the solid column.");

        // Exact event sequence: one (true, Right) on start, then exactly one (false, Right) on
        // the collision stop, and nothing more while D stays held.
        Assert.Equal(
            new[]
            {
                new PlayerMoveEventArgs(true, Direction.Right),
                new PlayerMoveEventArgs(false, Direction.Right),
            },
            events);
    }

    /// <summary>
    /// Verifies releasing the movement key after the blocked (false, Right) was fired raises no
    /// additional event: the player is already idle, so the stop is reported exactly once.
    /// </summary>
    [Fact]
    public void Update_ReleaseAfterBlockedStop_FiresNoAdditionalEvent()
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
        engine.Player.Position = new Position(0.5, 1.0);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        engine.Input(Key.D, true);

        // Run enough frames to hit the wall and report the collision stop while D is held.
        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        // The collision stop was reported exactly once while D was held.
        Assert.Equal(1, events.Count(e => !e.IsMoving));
        events.Clear();

        // Releasing D fires no additional event: the player is already idle.
        engine.Input(Key.D, false);
        engine.Update(FrameDt);

        Assert.Empty(events);
    }

    /// <summary>
    /// Verifies a player already blocked and idle facing Right that presses Up (also blocked)
    /// fires (false, Up) once (a direction change while idle), and a subsequent frame with the
    /// same keys fires nothing.
    /// </summary>
    [Fact]
    public void Update_TurnWhileBlocked_FiresOnMoveFalseForNewDirectionOnce()
    {
        // 4x4 map with a solid column at x=2 (blocks Right) and a solid row at y=0 (blocks Up).
        // The player starts with feet at y=2.0 so the fixed 1x1 box (y in [1.0, 2.0]) just
        // touches the solid row's bottom edge (y=1.0) without overlapping it, leaving the start
        // legal and Up immediately blocked.
        var gids = new uint[16];
        for (var y = 0; y < 4; y++)
        {
            gids[(y * 4) + 2] = 1; // column x=2
        }
        for (var x = 0; x < 4; x++)
        {
            gids[x] = 1; // row y=0
        }

        using var fixture = CreateCollisionMapFixture(4, 4, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 2.0);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        // Walk right into the solid column and stop there (idle, facing Right).
        engine.Input(Key.D, true);
        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        // Blocked at the wall (footprint never enters the solid column), facing Right.
        Assert.True(engine.Player.Position.X + 0.5 <= 2.0 + 1e-9, "The footprint must never overlap the solid column.");
        Assert.Equal(new PlayerMoveEventArgs(false, Direction.Right), events[^1]);
        events.Clear();

        // Press Up (also blocked): a direction change while idle -> (false, Up) exactly once.
        engine.Input(Key.D, false);
        engine.Input(Key.W, true);
        engine.Update(FrameDt);

        Assert.Equal(new[] { new PlayerMoveEventArgs(false, Direction.Up) }, events);

        // A subsequent frame with the same keys fires nothing.
        events.Clear();
        engine.Update(FrameDt);
        Assert.Empty(events);
    }

    /// <summary>
    /// Verifies a diagonal move into a wall slides along the free axis: the player keeps moving
    /// (its position changes each frame), so no (false, ...) stop event fires mid-slide.
    /// </summary>
    [Fact]
    public void Update_DiagonalSlideIntoWall_DoesNotReportStopMidSlide()
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

        // Start flush against the left edge of the wall column: moving UpRight, the X
        // displacement is blocked while Y is free, so the player slides straight up along it.
        engine.Player.Position = new Position(2.5, 4.0);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        engine.Input(Key.W, true);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player slid up along the wall: X never moved into it, Y decreased.
        Assert.Equal(2.5, engine.Player.Position.X, precision: 6);
        Assert.True(engine.Player.Position.Y < 4.0);

        // The slide is still movement: the start fired (true, UpRight) and no stop event fired
        // mid-slide (the position changed every frame).
        Assert.Contains(new PlayerMoveEventArgs(true, Direction.UpRight), events);
        Assert.DoesNotContain(events, e => !e.IsMoving);
    }

    /// <summary>
    /// Verifies a player fully blocked on both axes (a corner) reports (false, direction)
    /// exactly once: walking into a corner fires (true, ...) on start and (false, ...) once when
    /// both axes become blocked, then nothing more while the keys stay held.
    /// </summary>
    [Fact]
    public void Update_FullyBlockedCorner_FiresOnMoveFalseOnce()
    {
        // A 2x2 filled map (no collision layer): only the map edge is solid.
        using var fixture = CreateFilledMapFixture(2, 2);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(1.5, 1.5);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        // Walk up-left into the top-left corner of the map: both axes are eventually blocked by
        // the map edge at (0.5, 1.0) (the fixed 1x1 box's top edge at the top map edge).
        engine.Input(Key.W, true);
        engine.Input(Key.A, true);

        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player is fully blocked in the top-left corner region: the fixed 1x1 lower-body
        // box never leaves the map (feet x in [0.5, 1.5], y in [1.0, 2.0]), and a further frame
        // leaves both the position and the events unchanged.
        var blockedPosition = engine.Player.Position;
        Assert.True(blockedPosition.X < 1.0 && blockedPosition.Y <= 1.0 + 1e-9, "The player must end near the top-left corner.");

        // Exact sequence: (true, UpLeft) when the walk started, then exactly one (false, UpLeft)
        // when both axes became blocked, and nothing more while W+A stay held.
        Assert.Equal(
            new[]
            {
                new PlayerMoveEventArgs(true, Direction.UpLeft),
                new PlayerMoveEventArgs(false, Direction.UpLeft),
            },
            events);

        // A subsequent frame with the same keys fires nothing and does not move the player.
        events.Clear();
        engine.Update(FrameDt);
        Assert.Empty(events);
        Assert.Equal(blockedPosition, engine.Player.Position);
    }

    // ---------------------------------------------------------------------
    // Helpers (mirror the private helpers of GameEngineTests so this class is
    // self-contained; both live in the same test assembly).
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
}
