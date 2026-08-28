using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for <see cref="Player.ReportBlockedMove(Direction)"/> (story 55/69), the
/// internal bridge the engine uses to report a collision stop: the player faces the blocked
/// direction, transitions moving &#8594; idle and raises <see cref="Player.OnStopMoving"/> only
/// when it was moving. When the player was already idle it is a no-op (no event, even on a
/// direction change: the engine-level idle-blocked start-then-stop sequence is covered by the
/// engine tests), so the stop is reported exactly once while a movement key is held against a
/// wall.
/// </summary>
public class PlayerBlockedMoveTests
{
    /// <summary>
    /// Verifies ReportBlockedMove while moving faces the direction, transitions the player to
    /// idle and raises OnStopMoving with the blocked direction.
    /// </summary>
    [Fact]
    public void ReportBlockedMove_WhenMoving_RaisesOnStopMoving()
    {
        var player = new Player();
        var events = new List<Direction>();
        player.OnStopMoving += (_, direction) => events.Add(direction);

        player.Move(Direction.Right, speedFactor: 1, dt: 1);
        events.Clear();

        player.ReportBlockedMove(Direction.Right);

        Assert.Equal(new[] { Direction.Right }, events);
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
        var events = new List<Direction>();
        player.OnStopMoving += (_, direction) => events.Add(direction);

        player.ReportBlockedMove(Direction.Right);

        Assert.Empty(events);
    }

    /// <summary>
    /// Verifies ReportBlockedMove while idle raises no event even when the facing direction
    /// changes: from idle, a blocked move is reported by the engine as OnStartMoving then
    /// OnStopMoving in the same frame (see the engine collision tests), so the bridge itself is
    /// silent when the player is not moving.
    /// </summary>
    [Fact]
    public void ReportBlockedMove_WhenIdleDirectionChanged_RaisesNoEvent()
    {
        var player = new Player { Direction = Direction.Right };
        var events = new List<Direction>();
        player.OnStopMoving += (_, direction) => events.Add(direction);

        player.ReportBlockedMove(Direction.Up);

        Assert.Empty(events);
        Assert.Equal(Direction.Up, player.Direction);
    }

    /// <summary>
    /// Verifies the exact event sequence across a move, a blocked stop and repeated blocked
    /// reports: OnStartMoving on start, then exactly one OnStopMoving on the blocked stop, and
    /// nothing more while the same blocked direction is reported again.
    /// </summary>
    [Fact]
    public void MoveThenBlocked_ProducesExactEventSequence()
    {
        var player = new Player();
        var starts = new List<Direction>();
        var stops = new List<Direction>();
        player.OnStartMoving += (_, direction) => starts.Add(direction);
        player.OnStopMoving += (_, direction) => stops.Add(direction);

        player.Move(Direction.Right, speedFactor: 1, dt: 1);
        player.ReportBlockedMove(Direction.Right); // moving -> idle (the collision stop)
        player.ReportBlockedMove(Direction.Right); // already idle, same direction: no event

        Assert.Equal(new[] { Direction.Right }, starts);
        Assert.Equal(new[] { Direction.Right }, stops);
    }
}
