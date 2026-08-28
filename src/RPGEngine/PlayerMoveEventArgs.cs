namespace RPGEngine;

/// <summary>
/// Provides data for the <see cref="Player.OnStartMoving"/> and
/// <see cref="Player.OnStopMoving"/> events: the facing direction of the player.
/// </summary>
/// <param name="Direction">The direction the player is facing: the direction it started moving
/// in (<see cref="Player.OnStartMoving"/>) or the direction it was last moving in when it
/// stopped (<see cref="Player.OnStopMoving"/>).</param>
public sealed record PlayerMoveEventArgs(Direction Direction);
