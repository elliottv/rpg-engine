using RPGEngine.Sprites;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for <see cref="Player"/> (story 9): the player is a thin wrapper that
/// composes a <see cref="Character"/> and forwards state access and movement to it. It does
/// not listen to input itself; the engine drives it via <c>Move(direction, 1, dt)</c>.
/// </summary>
public class PlayerTests
{
    // ---------------------------------------------------------------------
    // Acceptance 1: the parameterless constructor creates its own Character
    // (with the default BaseSpeed), and Character is settable.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the parameterless constructor creates a non-null Character with the default BaseSpeed.</summary>
    [Fact]
    public void ParameterlessConstructor_CreatesOwnCharacterWithDefaultSpeed()
    {
        var player = new Player();

        Assert.NotNull(player.Character);
        Assert.Equal(Player.DefaultBaseSpeed, player.Character.BaseSpeed);
    }

    /// <summary>Verifies Player.Character can be replaced with another Character instance.</summary>
    [Fact]
    public void Character_IsSettable()
    {
        var player = new Player();
        var character = new Character();

        player.Character = character;

        Assert.Same(character, player.Character);
    }

    // ---------------------------------------------------------------------
    // Acceptance 2: Position/Direction read/write forward to Character.
    // ---------------------------------------------------------------------
    /// <summary>Verifies writing Player.Position updates the underlying Character and reading it returns the character's value.</summary>
    [Fact]
    public void Position_ReadWriteForwardsToCharacter()
    {
        var player = new Player();
        var character = player.Character;

        player.Position = new Position(12, 34);

        Assert.Equal(new Position(12, 34), character.Position);
        Assert.Equal(character.Position, player.Position);
    }

    /// <summary>Verifies writing Player.Direction updates the underlying Character and reading it returns the character's value.</summary>
    [Fact]
    public void Direction_ReadWriteForwardsToCharacter()
    {
        var player = new Player();
        var character = player.Character;

        player.Direction = Direction.Left;

        Assert.Equal(Direction.Left, character.Direction);
        Assert.Equal(character.Direction, player.Direction);
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: SpriteSheets is the same list instance as
    // Character.SpriteSheets, so adding a SpriteSheetRef (with a character
    // index 1..8) via the player is visible on the character.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Player.SpriteSheets returns the exact same list instance as Character.SpriteSheets.</summary>
    [Fact]
    public void SpriteSheets_IsSameInstanceAsCharacterSpriteSheets()
    {
        var player = new Player();

        Assert.Same(player.Character.SpriteSheets, player.SpriteSheets);
    }

    /// <summary>Verifies a SpriteSheetRef added through Player.SpriteSheets is visible on the underlying Character.</summary>
    [Fact]
    public void SpriteSheets_AddViaPlayer_IsVisibleOnCharacter()
    {
        var player = new Player();

        player.SpriteSheets.Add(new SpriteSheetRef("hero_body", CharacterIndex: 3));

        Assert.Contains(new SpriteSheetRef("hero_body", 3), player.Character.SpriteSheets);
    }

    // ---------------------------------------------------------------------
    // Acceptance 4: Move(direction, factor, dt) moves the underlying Character
    // by BaseSpeed * factor * dt and updates its Direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Move(direction, factor, dt) moves the underlying Character exactly BaseSpeed × factor × dt tiles along the right axis and updates its Direction.</summary>
    [Theory]
    [InlineData(Direction.Down, 0.0, 100.0)]
    [InlineData(Direction.Up, 0.0, -100.0)]
    [InlineData(Direction.Left, -100.0, 0.0)]
    [InlineData(Direction.Right, 100.0, 0.0)]
    public void Move_WithDirectionFactorAndDt_MovesUnderlyingCharacter(
        Direction direction,
        double expectedX,
        double expectedY)
    {
        var player = new Player { Position = new Position(0, 0) };
        player.Character.BaseSpeed = 100;

        // BaseSpeed * factor * dt = 100 * 2 * 0.5 = 100 tiles.
        player.Move(direction, speedFactor: 2, dt: 0.5);

        Assert.Equal(expectedX, player.Position.X);
        Assert.Equal(expectedY, player.Position.Y);
        Assert.Equal(direction, player.Direction);
    }

    // ---------------------------------------------------------------------
    // Acceptance 5: Move(factor, dt) without a direction uses the Character's
    // previous direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Move(speedFactor, dt) without a direction reuses the underlying Character's previous Direction.</summary>
    [Fact]
    public void Move_WithoutDirection_ReusesPreviousDirection()
    {
        var player = new Player { Direction = Direction.Up };
        player.Character.BaseSpeed = 100;

        player.Move(speedFactor: 1, dt: 1);

        Assert.Equal(new Position(0, -100), player.Position);
        Assert.Equal(Direction.Up, player.Direction);
    }

    // ---------------------------------------------------------------------
    // Acceptance 6: driving the player the way the engine will — derive a
    // direction from Config and call Move(direction, 1, dt) each frame at
    // dt = 1/60 — tracks the expected path.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a player driven like the engine (Config-derived direction, Move each frame at dt = 1/60) tracks the expected path.</summary>
    [Fact]
    public void DrivenLikeTheEngine_TracksExpectedPath()
    {
        var engine = new GameEngine();
        var player = engine.Player;
        var config = engine.Config;

        const double dt = 1.0 / 60;
        var right = config.GetDirection(Key.D)!.Value;
        var down = config.GetDirection(Key.S)!.Value;

        // Move right for one second (60 frames at 1/60 s) → 2 tiles to the right (the new
        // default BaseSpeed of 2 tiles/s, the tile-unit equivalent of the previous 96 px/s).
        for (var frame = 0; frame < 60; frame++)
        {
            player.Move(right, speedFactor: 1, dt);
        }

        Assert.Equal(2, player.Position.X, precision: 6);
        Assert.Equal(0, player.Position.Y, precision: 6);

        // Then move down for half a second (30 frames) → 1 tile down.
        for (var frame = 0; frame < 30; frame++)
        {
            player.Move(down, speedFactor: 1, dt);
        }

        Assert.Equal(2, player.Position.X, precision: 6);
        Assert.Equal(1, player.Position.Y, precision: 6);
    }

    // ---------------------------------------------------------------------
    // Story 69: Player.OnStartMoving / Player.OnStopMoving movement-state
    // events. OnStartMoving fires exactly when the player begins moving in a
    // new direction (idle -> moving, or a direction change while moving, e.g.
    // right -> up-right when a second key is pressed) and BEFORE the position
    // is updated; it never fires per frame (a same-direction move while moving
    // raises nothing). OnStopMoving fires when the player stops moving (via
    // Stop()). A speed-factor-zero Move only turns and raises no event. Both
    // events carry only the facing Direction (PlayerMoveEventArgs was removed).
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies the first Move from idle raises OnStartMoving with the facing direction, and
    /// that the event fires BEFORE the position is updated (the position observed inside the
    /// handler is the pre-move position, and it has moved by the time Move returns).
    /// </summary>
    [Fact]
    public void Move_WhenIdle_RaisesOnStartMoving()
    {
        var player = new Player { Position = new Position(10, 20) };
        player.Character.BaseSpeed = 2;
        var events = new List<Direction>();
        player.OnStartMoving += (_, direction) =>
        {
            events.Add(direction);
            // The event fires before the displacement: the handler sees the pre-move position.
            Assert.Equal(new Position(10, 20), player.Position);
        };

        player.Move(Direction.Right, speedFactor: 1, dt: 1);

        Assert.Equal(new[] { Direction.Right }, events);
        Assert.Equal(new Position(12, 20), player.Position);
    }

    /// <summary>
    /// Verifies a Move while already moving in the <em>same</em> direction raises no event: it is
    /// neither a start nor a stop, and it never fires per frame.
    /// </summary>
    [Fact]
    public void Move_WhileMovingSameDirection_NoEvent()
    {
        var player = new Player();
        var starts = new List<Direction>();
        var stops = new List<Direction>();
        player.OnStartMoving += (_, direction) => starts.Add(direction);
        player.OnStopMoving += (_, direction) => stops.Add(direction);

        player.Move(Direction.Right, speedFactor: 1, dt: 1);
        starts.Clear();
        stops.Clear();

        // Same direction while moving: no event (no per-frame events).
        player.Move(Direction.Right, speedFactor: 1, dt: 1);

        Assert.Empty(starts);
        Assert.Empty(stops);
    }

    /// <summary>
    /// Verifies a Move that changes direction while moving raises OnStartMoving with the new
    /// direction (e.g. right &#8594; down-right, the diagonal produced when a second key is pressed),
    /// and no OnStopMoving: a direction change while moving is a new start, not a stop.
    /// </summary>
    [Fact]
    public void Move_WhileMovingDirectionChange_RaisesOnStartMoving()
    {
        var player = new Player();
        var starts = new List<Direction>();
        var stops = new List<Direction>();
        player.OnStartMoving += (_, direction) => starts.Add(direction);
        player.OnStopMoving += (_, direction) => stops.Add(direction);

        player.Move(Direction.Right, speedFactor: 1, dt: 1);
        starts.Clear();

        // Direction change while moving: a new start with the new direction.
        player.Move(Direction.DownRight, speedFactor: 1, dt: 1);

        Assert.Equal(new[] { Direction.DownRight }, starts);
        Assert.Empty(stops);
    }

    /// <summary>
    /// Verifies a speed-factor-zero Move only turns: it never marks the player as moving and
    /// raises no event (a turn is neither a start nor a stop).
    /// </summary>
    [Fact]
    public void Move_SpeedFactorZero_TurnOnly_NoEvent()
    {
        var player = new Player();
        var starts = new List<Direction>();
        var stops = new List<Direction>();
        player.OnStartMoving += (_, direction) => starts.Add(direction);
        player.OnStopMoving += (_, direction) => stops.Add(direction);

        player.Move(Direction.Right, speedFactor: 0);

        Assert.Empty(starts);
        Assert.Empty(stops);
        Assert.Equal(Direction.Right, player.Direction);
        Assert.Equal(new Position(0, 0), player.Position); // a turn never moves
    }

    /// <summary>Verifies Stop() when moving raises OnStopMoving with the last facing direction and keeps it.</summary>
    [Fact]
    public void Stop_WhenMoving_RaisesOnStopMoving()
    {
        var player = new Player();
        var stops = new List<Direction>();
        player.OnStopMoving += (_, direction) => stops.Add(direction);

        player.Move(Direction.Right, speedFactor: 1, dt: 1);
        stops.Clear();
        player.Stop();

        Assert.Equal(new[] { Direction.Right }, stops);
        Assert.Equal(Direction.Right, player.Direction);
    }

    /// <summary>Verifies Stop() when already idle is a no-op and raises nothing.</summary>
    [Fact]
    public void Stop_WhenIdle_NoEvent()
    {
        var player = new Player();
        var stops = new List<Direction>();
        player.OnStopMoving += (_, direction) => stops.Add(direction);

        player.Stop();

        Assert.Empty(stops);
    }

    /// <summary>
    /// Verifies the exact event sequence across start, same-direction move, direction change and
    /// stop: OnStartMoving fires on start and again on the direction change (a new start), and
    /// OnStopMoving fires once with the last facing direction.
    /// </summary>
    [Fact]
    public void MoveThenStop_ExactEventSequence()
    {
        var player = new Player();
        var starts = new List<Direction>();
        var stops = new List<Direction>();
        player.OnStartMoving += (_, direction) => starts.Add(direction);
        player.OnStopMoving += (_, direction) => stops.Add(direction);

        player.Move(Direction.Right, speedFactor: 1, dt: 1);
        player.Move(Direction.Right, speedFactor: 1, dt: 1); // same direction: no event
        player.Move(Direction.Down, speedFactor: 1, dt: 1);  // direction change: a new start
        player.Stop();
        player.Stop(); // already idle: no event

        Assert.Equal(new[] { Direction.Right, Direction.Down }, starts);
        Assert.Equal(new[] { Direction.Down }, stops);
    }
}
