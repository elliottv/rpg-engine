namespace RPGEngine;

/// <summary>
/// Provides data for the <see cref="Player.OnMove"/> event: whether the player is now moving
/// and the direction it is currently facing.
/// </summary>
/// <param name="IsMoving">
/// <see langword="true"/> when the player is now moving; <see langword="false"/> when it has
/// stopped. For a speed-factor-zero turn (see <see cref="Player.Move(Direction, double, double)"/>)
/// the value reflects the current movement state (moving or idle) at the moment of the turn.
/// </param>
/// <param name="Direction">The direction the player is currently facing.</param>
public sealed record PlayerMoveEventArgs(bool IsMoving, Direction Direction);
