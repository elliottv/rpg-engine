using RPGEngine.Sprites;
using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for story 55/69: <see cref="Player.OnStartMoving"/> and
/// <see cref="Player.OnStopMoving"/> fire around a collision stop. The engine reports a fully
/// blocked move (no net displacement after the axis-separated resolution) through
/// <c>Player.ReportBlockedMove</c> after reporting the start through <c>Player.ReportMovement</c>,
/// so a move that starts from idle and is immediately blocked fires OnStartMoving then
/// OnStopMoving in the same frame, and a move that becomes blocked mid-walk fires OnStartMoving
/// when it starts and OnStopMoving exactly once when it stops — nothing more while the movement
/// key stays held against the wall. These tests assert the exact event sequence (not the exact
/// frame on which the stop is reported), so they hold with either collision-resolution behavior.
/// </summary>
public class GameEngineCollisionOnMoveTests
{
    private const double FrameDt = 1.0 / 60;

    /// <summary>
    /// Verifies holding D against a solid column fires OnStartMoving(Right) when movement starts,
    /// then exactly one OnStopMoving(Right) when the player is fully blocked, and no further
    /// events while D stays held for many more frames.
    /// </summary>
    [Fact]
    public void Update_KeyHeldAgainstSolidTile_FiresOnStartMovingThenExactlyOneOnStopMoving()
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

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        engine.Input(Key.D, true);

        // Frame-by-frame Update(1/60): the player starts moving right, hits the wall, and keeps
        // pressing D against it for many more frames.
        for (var frame = 0; frame < 300; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player stopped at the solid column: its footprint never enters it.
        Assert.True(engine.Player.Position.X + 0.25 <= 2.0 + 1e-9, "The footprint must never overlap the solid column.");

        // Exact event sequence: one OnStartMoving(Right) on start, then exactly one
        // OnStopMoving(Right) on the collision stop, and nothing more while D stays held.
        Assert.Equal(new[] { Direction.Right }, starts);
        Assert.Equal(new[] { Direction.Right }, stops);
    }

    /// <summary>
    /// Verifies releasing the movement key after the blocked OnStopMoving was fired raises no
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

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        engine.Input(Key.D, true);

        // Run enough frames to hit the wall and report the collision stop while D is held.
        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        // The collision stop was reported exactly once while D was held.
        Assert.Single(stops);
        starts.Clear();
        stops.Clear();

        // Releasing D fires no additional event: the player is already idle.
        engine.Input(Key.D, false);
        engine.Update(FrameDt);

        Assert.Empty(starts);
        Assert.Empty(stops);
    }

    /// <summary>
    /// Verifies a player already blocked and idle facing Right that presses Up (also blocked)
    /// fires OnStartMoving(Up) then OnStopMoving(Up) once (a fresh start from idle that is
    /// immediately blocked), and a subsequent frame with the same keys fires nothing.
    /// </summary>
    [Fact]
    public void Update_TurnWhileBlocked_FiresStartThenStopForNewDirectionOnce()
    {
        // 4x4 map with a solid column at x=2 (blocks Right) and a solid row at y=0 (blocks Up).
        // The player starts with feet at y=1.5 so the fixed 0.5x0.5 box (y in [1.0, 1.5]) just
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
        engine.Player.Position = new Position(0.5, 1.5);

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        // Walk right into the solid column and stop there (idle, facing Right).
        engine.Input(Key.D, true);
        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        // Blocked at the wall (footprint never enters the solid column), facing Right, with the
        // collision stop reported once.
        Assert.True(engine.Player.Position.X + 0.25 <= 2.0 + 1e-9, "The footprint must never overlap the solid column.");
        Assert.Equal(Direction.Right, stops[^1]);
        starts.Clear();
        stops.Clear();

        // Press Up (also blocked): a fresh start from idle -> OnStartMoving(Up) then the
        // collision stop -> OnStopMoving(Up), both in the same frame.
        engine.Input(Key.D, false);
        engine.Input(Key.W, true);
        engine.Update(FrameDt);

        Assert.Equal(new[] { Direction.Up }, starts);
        Assert.Equal(new[] { Direction.Up }, stops);

        // A subsequent frame with the same keys fires nothing.
        starts.Clear();
        stops.Clear();
        engine.Update(FrameDt);
        Assert.Empty(starts);
        Assert.Empty(stops);
    }

    /// <summary>
    /// Verifies a diagonal move into a wall where only one axis is free stops the player
    /// entirely (diagonal movement is all-or-nothing: no wall-sliding along the free axis). The
    /// blocked move fires OnStartMoving(UpRight) then OnStopMoving(UpRight) on the first frame
    /// and nothing more fires while the keys stay held.
    /// </summary>
    [Fact]
    public void Update_DiagonalIntoWall_FiresStartThenStopOnce()
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

        // Start flush against the left edge of the wall column: the fixed 0.5x0.5 box's right
        // edge (feet X + 0.25) is exactly at the wall column x=3, so moving UpRight, the X
        // displacement is blocked while Y is free. Diagonal movement is all-or-nothing, so the
        // player stops entirely instead of sliding straight up along the wall.
        engine.Player.Position = new Position(2.75, 4.0);

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        engine.Input(Key.W, true);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 60; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player never moved: fully blocked on the first frame (X blocked, Y free - no slide).
        Assert.Equal(2.75, engine.Player.Position.X, precision: 6);
        Assert.Equal(4.0, engine.Player.Position.Y, precision: 6);

        // From idle, the immediately-blocked diagonal fires OnStartMoving(UpRight) then
        // OnStopMoving(UpRight) in the same frame, and nothing more while W+D stay held.
        Assert.Equal(new[] { Direction.UpRight }, starts);
        Assert.Equal(new[] { Direction.UpRight }, stops);
    }

    /// <summary>
    /// Verifies a diagonal move that becomes blocked mid-walk stops the player entirely and
    /// reports OnStopMoving exactly once: OnStartMoving(UpRight) fires while both axes are free
    /// and the player moves, then exactly one OnStopMoving(UpRight) fires when one axis is
    /// blocked, and nothing more while the keys stay held (the free axis never slides).
    /// </summary>
    [Fact]
    public void Update_DiagonalMovement_StopsEntirelyWhenOneAxisBlocked()
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

        // Start in open space: moving UpRight is free until the box's right edge reaches the
        // wall column, at which point the X axis becomes blocked while Y is still free. The
        // player must stop entirely rather than sliding up along the wall.
        engine.Player.Position = new Position(1.5, 4.0);

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        engine.Input(Key.W, true);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player moved up-right while both axes were free but never entered the wall, and
        // stopped entirely once the X axis became blocked (no slide: Y stays put after the stop).
        var stopped = engine.Player.Position;
        Assert.True(stopped.X > 1.5, "The player moved right before stopping.");
        Assert.True(stopped.Y < 4.0, "The player moved up before stopping.");
        Assert.True(stopped.X + 0.25 <= 3.0 + 1e-9, "The footprint must never overlap the solid column.");

        // Exact sequence: one OnStartMoving(UpRight) on start, then exactly one
        // OnStopMoving(UpRight) when one axis became blocked, and nothing more while W+D stay held.
        Assert.Equal(new[] { Direction.UpRight }, starts);
        Assert.Equal(new[] { Direction.UpRight }, stops);

        // A subsequent frame with the same keys fires nothing and does not move the player.
        starts.Clear();
        stops.Clear();
        engine.Update(FrameDt);
        Assert.Empty(starts);
        Assert.Empty(stops);
        Assert.Equal(stopped, engine.Player.Position);
    }

    /// <summary>
    /// Verifies a diagonal move whose full displacement is clear on both axes moves the player
    /// diagonally: OnStartMoving fires (UpRight) once on start, the position changes on both
    /// axes each frame, and no OnStopMoving fires mid-move.
    /// </summary>
    [Fact]
    public void Update_DiagonalMovement_BothAxesFree_MovesDiagonally()
    {
        // A 5x5 map with no collision layer: only the map edge is solid, far away from the path.
        using var fixture = CreateFilledMapFixture(5, 5);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(1.5, 3.0);

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        engine.Input(Key.W, true);
        engine.Input(Key.D, true);

        for (var frame = 0; frame < 30; frame++)
        {
            engine.Update(FrameDt);
        }

        // Both axes are free the whole way: the player moved up-right.
        Assert.True(engine.Player.Position.X > 1.5, "The player must move right.");
        Assert.True(engine.Player.Position.Y < 3.0, "The player must move up.");

        // Movement only: OnStartMoving(UpRight) on start, and no stop event (the position
        // changed every frame).
        Assert.Equal(new[] { Direction.UpRight }, starts);
        Assert.Empty(stops);
    }

    /// <summary>
    /// Verifies a player fully blocked on both axes (a corner) reports the collision stop
    /// exactly once: walking into a corner fires OnStartMoving(UpLeft) on start and
    /// OnStopMoving(UpLeft) once when both axes become blocked, then nothing more while the keys
    /// stay held.
    /// </summary>
    [Fact]
    public void Update_FullyBlockedCorner_FiresStartThenStopOnce()
    {
        // A 2x2 filled map (no collision layer): only the map edge is solid.
        using var fixture = CreateFilledMapFixture(2, 2);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(1.5, 1.5);

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        // Walk up-left into the top-left corner of the map. Diagonal movement is all-or-nothing:
        // the player moves while both axes are free and stops entirely at the first position
        // where either axis is blocked (the Y axis blocks first, one diagonal step short of the
        // top map edge, because a diagonal cannot take a partial step onto the boundary).
        engine.Input(Key.W, true);
        engine.Input(Key.A, true);

        for (var frame = 0; frame < 120; frame++)
        {
            engine.Update(FrameDt);
        }

        // The player is fully blocked in the top-left corner region. All-or-nothing diagonal
        // movement stops the player at the first position where either axis is blocked (here the
        // Y axis, one diagonal step short of the top map edge, since a diagonal cannot take a
        // partial step onto the boundary): the fixed 0.5x0.5 lower-body box never leaves the map
        // (feet x in [0.25, 1.75], y in [0.5, 2.0]), and a further frame leaves both the position
        // and the events unchanged.
        var blockedPosition = engine.Player.Position;
        Assert.True(blockedPosition.X >= 0.25 - 1e-9 && blockedPosition.X < 1.1, "The player must be stopped in the top-left corner region.");
        Assert.True(blockedPosition.Y >= 0.5 - 1e-9 && blockedPosition.Y < 0.6, "The player must be stopped near the top map edge.");

        // Exact sequence: OnStartMoving(UpLeft) when the walk started, then exactly one
        // OnStopMoving(UpLeft) when both axes became blocked, and nothing more while W+A stay held.
        Assert.Equal(new[] { Direction.UpLeft }, starts);
        Assert.Equal(new[] { Direction.UpLeft }, stops);

        // A subsequent frame with the same keys fires nothing and does not move the player.
        starts.Clear();
        stops.Clear();
        engine.Update(FrameDt);
        Assert.Empty(starts);
        Assert.Empty(stops);
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
