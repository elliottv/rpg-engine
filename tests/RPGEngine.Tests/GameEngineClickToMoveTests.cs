using RPGEngine.Sprites;
using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Partial <see cref="GameEngineTests"/> containing the click-to-move auto-walk tests. Splitting the large engine
/// test class into one file per functional area keeps the test files manageable; the shared
/// helpers (map fixtures, sprite configuration, rendering) live in the core
/// <c>GameEngineTests</c> partial file and are reused here.
/// </summary>
public partial class GameEngineTests
{
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
        engine.Player.Position = new Position(0.5, 1.5);

        const int canvas = 480; // 10 tiles x 48 px, the whole map is visible
        ClickOnTile(engine, 5, 5, canvas, canvas);

        // Snapshot the computed path before the walk consumes it: it leads from the player's
        // tile (0,1) to the clicked tile (5,5).
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
        engine.Player.Position = new Position(0.5, 1.5);

        var events = new List<PlayerMoveEventArgs>();
        engine.Player.OnMove += (_, e) => events.Add(e);

        const int canvas = 480;
        ClickOnTile(engine, 3, 4, canvas, canvas);

        // The first Update starts the walk toward (1.5, 2.5): down-right.
        engine.Update(FrameDt);
        Assert.Equal(new[] { new PlayerMoveEventArgs(true, Direction.DownRight) }, events);
        events.Clear();

        var target = new Position(3.5, 4.5);
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
        engine.Player.Position = new Position(0.5, 1.5);

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
    /// the player walks to the target. The wall is two tiles tall (rows 0-1) so the fixed 0.5×0.5
    /// tile lower-body box can pass through the gap at row 3 with its whole height clear of the
    /// wall's bottom edge.
    /// </summary>
    [Fact]
    public void Click_OnReachableTargetBehindWall_WalksAroundTheWall()
    {
        // 7x5 map: a "walls" collision layer with a solid column at x=3 for rows 0..1, leaving a
        // gap at rows 2-4 so the target at (5,3) is reachable only through the gap (the 0.5x0.5
        // box needs its footprint clear, so it passes at row 3).
        var gids = new uint[35];
        gids[(0 * 7) + 3] = 1;
        gids[(1 * 7) + 3] = 1;

        using var fixture = CreateCollisionMapFixture(7, 5, gids);
        var engine = new GameEngine { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 3.5);

        const int canvas = 336; // 7 tiles x 48 px
        ClickOnTile(engine, 5, 3, canvas, canvas);

        Assert.NotEmpty(engine.AutoWalkPath);
        // The path must detour around the wall: it cannot cross x=3 at rows 0..1, so it has to go
        // through a row at or below y=2 (the gap).
        Assert.Contains(engine.AutoWalkPath, tile => tile.Y >= 2);

        var target = new Position(5.5, 3.5);
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
        engine.Player.Position = new Position(0.5, 1.5);

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
        engine.Player.Position = new Position(0.5, 1.5);

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
        engine.Player.Position = new Position(0.5, 1.5);

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
        // The clamp keeps the player's fixed 0.5x0.5 lower-body box in the map: the default feet
        // start at (0, 0) and are clamped to (0.25, 0.5) (the box's left edge at x = 0 and its
        // top edge at y = 0).
        Assert.Equal(new Position(0.25, 0.5), engine.Player.Position);
    }
}
