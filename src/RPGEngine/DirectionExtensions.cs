namespace RPGEngine;

/// <summary>
/// Provides convenience members for the <see cref="Direction"/> enum: screen-space deltas,
/// opposites, sprite-sheet row indices and axis classification.
/// </summary>
public static class DirectionExtensions
{
    /// <summary>
    /// Returns the screen-space unit delta for the direction. Screen coordinates grow Y
    /// downward, so <see cref="Direction.Down"/> is <c>(0, +1)</c> and
    /// <see cref="Direction.Up"/> is <c>(0, -1)</c>.
    /// </summary>
    /// <param name="d">The direction.</param>
    /// <returns>The unit vector the direction points to.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="d"/> is not a defined <see cref="Direction"/>.</exception>
    public static Vector2 Delta(this Direction d) => d switch
    {
        Direction.Down => new Vector2(0, 1),
        Direction.Left => new Vector2(-1, 0),
        Direction.Right => new Vector2(1, 0),
        Direction.Up => new Vector2(0, -1),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown direction."),
    };

    /// <summary>Returns the direction opposite to this one (<see cref="Direction.Down"/> ↔ <see cref="Direction.Up"/>, <see cref="Direction.Left"/> ↔ <see cref="Direction.Right"/>).</summary>
    /// <param name="d">The direction.</param>
    /// <returns>The opposite direction.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="d"/> is not a defined <see cref="Direction"/>.</exception>
    public static Direction Opposite(this Direction d) => d switch
    {
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        Direction.Up => Direction.Down,
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown direction."),
    };

    /// <summary>
    /// Returns the RPG Maker MZ character sheet row for this direction
    /// (<c>0 = down</c>, <c>1 = left</c>, <c>2 = right</c>, <c>3 = up</c>). It always equals the
    /// enum value; it is exposed explicitly to guard the sprite-row contract.
    /// </summary>
    /// <param name="d">The direction.</param>
    /// <returns>The 0-based sprite-sheet row (0..3).</returns>
    public static int RowIndex(this Direction d) => (int)d;

    /// <summary>Returns whether the direction is horizontal (<see cref="Direction.Left"/> or <see cref="Direction.Right"/>).</summary>
    /// <param name="d">The direction.</param>
    /// <returns><see langword="true"/> when the direction is horizontal; otherwise <see langword="false"/>.</returns>
    public static bool IsHorizontal(this Direction d) => d is Direction.Left or Direction.Right;

    /// <summary>Returns whether the direction is vertical (<see cref="Direction.Down"/> or <see cref="Direction.Up"/>).</summary>
    /// <param name="d">The direction.</param>
    /// <returns><see langword="true"/> when the direction is vertical; otherwise <see langword="false"/>.</returns>
    public static bool IsVertical(this Direction d) => d is Direction.Down or Direction.Up;
}
