using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for <see cref="Player.ReportBlockedMove(Direction)"/> (story 55), the
/// internal bridge the engine uses to report a collision stop: the player faces the blocked
/// direction, transitions moving &#8594; idle and raises <see cref="Player.OnMove"/> with
/// <see cref="PlayerMoveEventArgs.IsMoving"/> set to <see langword="false"/> only on a
/// transition (the player was moving, or the facing direction changed while idle). When already
/// idle and facing the same direction it is a no-op, so the stop is reported exactly once while
/// a movement key is held against a wall.
/// </summary>
public class PlayerBlockedMoveTests
{
    /// <summary>
    /// Verifies ReportBlockedMove while moving faces the direction, transitions the player to
    /// idle and raises OnMove with IsMoving = false and the blocked direction.
    /// </summary>
    [Fact]
    public void ReportBlockedMove_WhenMoving_RaisesOnMoveWithIsMovingFalse()
    {
        var player = new Player();
        var events = new List<PlayerMoveEventArgs>();
        player.OnMove += (_, e) => events.Add(e);

        player.Move(Direction.Right, speedFactor: 1, dt: 1);
        events.Clear();

        player.ReportBlockedMove(Direction.Right);

        Assert.Equal(new[] { new PlayerMoveEventArgs(false, Direction.Right) }, events);
        Assert.Equal(Direction.Right, player.Direction);
    }

    /// <summary>
    /// Verifies ReportBlockedMove while already idle and facing the same direction is a no-op:
    /// it raises nothing (the collision stop is reported exactly once while the key is held
    /// against the wall).
    /// </summary>
    [Fact]
    public void ReportBlockedMove_WhenIdleSameDirection_IsNoOp()
    {
        var player = new Player { Direction = Direction.Right };
        var events = new List<PlayerMoveEventArgs>();
        player.OnMove += (_, e) => events.Add(e);

        player.ReportBlockedMove(Direction.Right);

        Assert.Empty(events);
    }

    /// <summary>
    /// Verifies ReportBlockedMove while idle facing a different direction raises OnMove with
    /// IsMoving = false and the new direction (a "turn while blocked", matching the turn
    /// semantics of a speed-factor-zero Move).
    /// </summary>
    [Fact]
    public void ReportBlockedMove_WhenIdleDirectionChanged_RaisesOnMoveWithIsMovingFalse()
    {
        var player = new Player { Direction = Direction.Right };
        var events = new List<PlayerMoveEventArgs>();
        player.OnMove += (_, e) => events.Add(e);

        player.ReportBlockedMove(Direction.Up);

        Assert.Equal(new[] { new PlayerMoveEventArgs(false, Direction.Up) }, events);
        Assert.Equal(Direction.Up, player.Direction);
    }

    /// <summary>
    /// Verifies the exact event sequence across a move, a blocked stop and repeated blocked
    /// reports: (true, ...) on start, then exactly one (false, ...) on the blocked stop, and
    /// nothing more while the same blocked direction is reported again.
    /// </summary>
    [Fact]
    public void MoveThenBlocked_ProducesExactEventSequence()
    {
        var player = new Player();
        var events = new List<PlayerMoveEventArgs>();
        player.OnMove += (_, e) => events.Add(e);

        player.Move(Direction.Right, speedFactor: 1, dt: 1);
        player.ReportBlockedMove(Direction.Right); // moving -> idle (the collision stop)
        player.ReportBlockedMove(Direction.Right); // already idle, same direction: no event

        Assert.Equal(
            new[]
            {
                new PlayerMoveEventArgs(true, Direction.Right),
                new PlayerMoveEventArgs(false, Direction.Right),
            },
            events);
    }
}
