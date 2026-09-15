using RPGEngine.Tiled;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Partial <see cref="GameEngineTests"/> containing the acceptance tests of the
/// <see cref="GameEngine.ReleaseAllInputs"/> contract (story 81).
/// </summary>
/// <remarks>
/// <see cref="GameEngine.ReleaseAllInputs"/> exists so that a host whose input surface loses focus
/// does not end up with a key stuck down forever: the engine would never receive the matching
/// key-up event. The contract documented on the method and in <c>docs/api/GameEngine.md</c> is
/// locked here so a later input feature cannot regress it: the pressed keys and the auto-walk path
/// are cleared, the player stops on the next <see cref="GameEngine.Update(double)"/>
/// (<see cref="Player.OnStopMoving"/> fires once, with the last direction), the call itself raises
/// no event and changes neither the position nor the facing, it is idempotent, and new
/// <see cref="GameEngine.Input(Key, bool)"/> / <see cref="GameEngine.Click(double, double)"/>
/// events work normally afterwards.
/// </remarks>
public partial class GameEngineTests
{
    // ---------------------------------------------------------------------
    // Story 81: the GameEngine.ReleaseAllInputs contract. The call releases
    // every held key (equivalent to a key-up for each of them) and cancels any
    // in-progress auto-walk; the player stops on the next Update through
    // Player.Stop (OnStopMoving fires once with the last direction), the call
    // itself raises no event and resets neither the position nor the facing,
    // and it is safe to call at any time and any number of times.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies the held key really is released: after the call the player stops on the next
    /// <see cref="GameEngine.Update(double)"/> - exactly one <see cref="Player.OnStopMoving"/>
    /// fires carrying the last direction - and stays stopped while Update keeps being called
    /// (the key was released, not ignored once).
    /// </summary>
    [Fact]
    public void ReleaseAllInputs_ClearsHeldKeys_PlayerStopsAndFiresOnStopMovingOnce()
    {
        var engine = new GameEngine(new TestGameConfig());
        engine.Player.Character.BaseSpeed = 2;
        engine.Player.Position = new Position(10, 10);

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        // D is held: the player starts moving right (OnStartMoving fires) and displaces.
        engine.Input(Key.D, isPressed: true);
        engine.Update(FrameDt);
        Assert.Equal(new[] { Direction.Right }, starts);
        Assert.Empty(stops);
        var positionWhenMoving = engine.Player.Position;
        Assert.True(positionWhenMoving.X > 10, "Holding D moves the player right.");

        engine.ReleaseAllInputs();

        // The call itself raises no event and changes neither the position nor the facing.
        Assert.Equal(positionWhenMoving, engine.Player.Position);
        Assert.Equal(Direction.Right, engine.Player.Direction);
        Assert.Empty(stops);

        // The key really was released: the player stops on the next Update ...
        engine.Update(FrameDt);
        Assert.Equal(positionWhenMoving, engine.Player.Position);
        Assert.Equal(new[] { Direction.Right }, stops);

        // ... and every following frame keeps it stopped, with no further event.
        for (var frame = 0; frame < 10; frame++)
        {
            engine.Update(FrameDt);
            Assert.Equal(positionWhenMoving, engine.Player.Position);
        }

        Assert.Single(stops);
        Assert.Empty(engine.AutoWalkPath);
    }

    /// <summary>
    /// Verifies an auto-walk queued by <see cref="GameEngine.Click(double, double)"/> is cancelled
    /// by the call (<see cref="GameEngine.AutoWalkPath"/> is emptied and the player does not move
    /// for a frame budget that is long enough for the walk to have crossed several tiles: the
    /// control engine, same setup without the call, walks to the clicked tile in exactly that many
    /// frames).
    /// </summary>
    [Fact]
    public void ReleaseAllInputs_CancelsAutoWalk()
    {
        const int canvas = 480; // 10 tiles x 48 px: the whole 10x10 map is visible
        var target = new Position(5.5, 5.5);

        // Control: without the call, the very same setup queues the walk and reaches the clicked
        // tile center. The number of frames it needs is the budget the released engine is measured
        // with, so "the player did not move" means "it would have crossed several tiles".
        int framesNeeded;
        using (var controlFixture = CreateFilledMapFixture(10, 10))
        {
            var control = new GameEngine(new TestGameConfig()) { Map = TileMap.Load(controlFixture.MapPath) };
            ConfigurePlayerSprite(control, seed: 1);
            control.Player.Position = new Position(0.5, 1.5);

            ClickOnTile(control, 5, 5, canvas, canvas);
            Assert.NotEmpty(control.AutoWalkPath);

            framesNeeded = 0;
            while (control.Player.Position != target && framesNeeded < 5000)
            {
                control.Update(FrameDt);
                framesNeeded++;
            }

            Assert.Equal(target, control.Player.Position);
        }

        Assert.True(framesNeeded > 60, "The walk must last more than one second (more than one tile at 2 tiles/s).");

        // The released engine: the click queues the walk, ReleaseAllInputs cancels it and the
        // player stays on its start position for the whole frame budget.
        using var fixture = CreateFilledMapFixture(10, 10);
        var engine = new GameEngine(new TestGameConfig()) { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(engine, seed: 1);
        engine.Player.Position = new Position(0.5, 1.5);

        ClickOnTile(engine, 5, 5, canvas, canvas);
        Assert.NotEmpty(engine.AutoWalkPath);

        engine.ReleaseAllInputs();

        Assert.Empty(engine.AutoWalkPath);

        var start = engine.Player.Position;
        for (var frame = 0; frame < framesNeeded; frame++)
        {
            engine.Update(FrameDt);
            Assert.Equal(start, engine.Player.Position);
        }

        Assert.Empty(engine.AutoWalkPath);
    }

    /// <summary>
    /// Verifies calling the method with nothing pressed and no walk running is a no-op: no
    /// exception (it is safe to call any number of times), no movement and neither
    /// <see cref="Player.OnStartMoving"/> nor <see cref="Player.OnStopMoving"/>.
    /// </summary>
    [Fact]
    public void ReleaseAllInputs_WhenNothingIsPressed_IsANoOp()
    {
        var engine = new GameEngine(new TestGameConfig());

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        var start = engine.Player.Position;

        // Safe at any time and any number of times, including when nothing is pressed.
        engine.ReleaseAllInputs();
        engine.ReleaseAllInputs();

        for (var frame = 0; frame < 10; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(start, engine.Player.Position);
        Assert.Empty(starts);
        Assert.Empty(stops);
        Assert.Empty(engine.AutoWalkPath);
    }

    /// <summary>
    /// Verifies new input works normally after the call: a second D press moves the player again
    /// and fires <see cref="Player.OnStartMoving"/> a second time (a host losing and regaining
    /// focus), and a <see cref="GameEngine.Click(double, double)"/> afterwards starts a fresh
    /// auto-walk.
    /// </summary>
    [Fact]
    public void ReleaseAllInputs_ThenNewInput_ResumesMovement()
    {
        var engine = new GameEngine(new TestGameConfig());
        engine.Player.Character.BaseSpeed = 2;
        engine.Player.Position = new Position(10, 10);

        var starts = new List<Direction>();
        var stops = new List<Direction>();
        engine.Player.OnStartMoving += (_, direction) => starts.Add(direction);
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        engine.Input(Key.D, isPressed: true);
        engine.Update(FrameDt);
        Assert.Equal(new[] { Direction.Right }, starts);

        engine.ReleaseAllInputs();
        engine.Update(FrameDt); // the player stops here
        Assert.Single(stops);
        var positionAfterStop = engine.Player.Position;

        // The host regains the focus and D is pressed again: movement resumes and OnStartMoving
        // fires a second time.
        engine.Input(Key.D, isPressed: true);
        engine.Update(FrameDt);

        Assert.Equal(2, starts.Count);
        Assert.All(starts, direction => Assert.Equal(Direction.Right, direction));
        Assert.True(engine.Player.Position.X > positionAfterStop.X, "The player moves right again.");

        // A Click after the call starts a fresh auto-walk (on an engine with a map).
        const int canvas = 480;
        using var fixture = CreateFilledMapFixture(10, 10);
        var walking = new GameEngine(new TestGameConfig()) { Map = TileMap.Load(fixture.MapPath) };
        ConfigurePlayerSprite(walking, seed: 1);
        walking.Player.Position = new Position(0.5, 1.5);

        ClickOnTile(walking, 5, 5, canvas, canvas);
        walking.Update(FrameDt); // the auto-walk starts
        Assert.NotEmpty(walking.AutoWalkPath);

        walking.ReleaseAllInputs();
        Assert.Empty(walking.AutoWalkPath);

        walking.Update(FrameDt); // the walk is cancelled: the player stops where it is
        var stopped = walking.Player.Position;

        ClickOnTile(walking, 0, 6, canvas, canvas); // a new click queues a fresh walk
        Assert.NotEmpty(walking.AutoWalkPath);

        walking.Update(FrameDt);
        Assert.NotEqual(stopped, walking.Player.Position);
    }

    /// <summary>
    /// Verifies the call clears the input state only: a player facing a diagonal keeps that
    /// <see cref="Player.Direction"/> and its standing animation frame, across the call and across
    /// the frame that stops it.
    /// </summary>
    [Fact]
    public void ReleaseAllInputs_DoesNotResetFacing()
    {
        var engine = new GameEngine(new TestGameConfig());
        engine.Player.Character.BaseSpeed = 2;
        engine.Player.Position = new Position(10, 10);

        var stops = new List<Direction>();
        engine.Player.OnStopMoving += (_, direction) => stops.Add(direction);

        // W + D: the player faces (and moves in) the UpRight diagonal.
        engine.Input(Key.W, isPressed: true);
        engine.Input(Key.D, isPressed: true);
        engine.Update(FrameDt);
        Assert.Equal(Direction.UpRight, engine.Player.Direction);

        var facing = engine.Player.Direction;
        var positionAtCall = engine.Player.Position;

        engine.ReleaseAllInputs();

        // The call changes neither the position nor the facing and raises no event.
        Assert.Equal(positionAtCall, engine.Player.Position);
        Assert.Equal(facing, engine.Player.Direction);
        Assert.Empty(stops);

        // The next Update stops the player; the diagonal facing and the standing frame stay.
        engine.Update(FrameDt);
        Assert.Equal(Direction.UpRight, engine.Player.Direction);
        Assert.Equal(1, engine.Player.Character.AnimationFrame); // the standing frame
        Assert.False(engine.Player.Character.IsMoving);
        Assert.Equal(new[] { Direction.UpRight }, stops);

        // Further calls and frames keep the facing and the standing animation state.
        engine.ReleaseAllInputs();
        for (var frame = 0; frame < 10; frame++)
        {
            engine.Update(FrameDt);
        }

        Assert.Equal(Direction.UpRight, engine.Player.Direction);
        Assert.Equal(1, engine.Player.Character.AnimationFrame);
        Assert.Single(stops);
    }
}
