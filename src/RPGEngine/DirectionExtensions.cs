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
    /// <see cref="Direction.Up"/> is <c>(0, -1)</c>. Diagonal deltas are normalized (magnitude 1,
    /// not &#8730;2), so diagonal movement is exactly as fast as cardinal movement.
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
        Direction.DownLeft => new Vector2(-RootHalf, RootHalf),
        Direction.DownRight => new Vector2(RootHalf, RootHalf),
        Direction.UpLeft => new Vector2(-RootHalf, -RootHalf),
        Direction.UpRight => new Vector2(RootHalf, -RootHalf),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown direction."),
    };

    /// <summary>
    /// Returns the direction opposite to this one: <see cref="Direction.Down"/> &#8596;
    /// <see cref="Direction.Up"/>, <see cref="Direction.Left"/> &#8596;
    /// <see cref="Direction.Right"/>, <see cref="Direction.DownLeft"/> &#8596;
    /// <see cref="Direction.UpRight"/> and <see cref="Direction.DownRight"/> &#8596;
    /// <see cref="Direction.UpLeft"/>.
    /// </summary>
    /// <param name="d">The direction.</param>
    /// <returns>The opposite direction.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="d"/> is not a defined <see cref="Direction"/>.</exception>
    public static Direction Opposite(this Direction d) => d switch
    {
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        Direction.Up => Direction.Down,
        Direction.DownLeft => Direction.UpRight,
        Direction.DownRight => Direction.UpLeft,
        Direction.UpLeft => Direction.DownRight,
        Direction.UpRight => Direction.DownLeft,
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown direction."),
    };

    /// <summary>
    /// Returns the RPG Maker MZ character sheet row for this direction
    /// (<c>0 = down</c>, <c>1 = left</c>, <c>2 = right</c>, <c>3 = up</c>).
    /// </summary>
    /// <remarks>
    /// Cardinal directions return their enum value (<c>0..3</c>). Diagonal directions have no
    /// dedicated sheet row, so they deliberately fall back to their <em>horizontal</em>
    /// component's row: <see cref="Direction.DownLeft"/> and <see cref="Direction.UpLeft"/> map to
    /// row 1 (the Left row) and <see cref="Direction.DownRight"/> and
    /// <see cref="Direction.UpRight"/> map to row 2 (the Right row). A diagonally-facing
    /// character therefore renders with the side-view row, which reads better than the front or
    /// back rows for an oblique facing.
    /// </remarks>
    /// <param name="d">The direction.</param>
    /// <returns>The 0-based sprite-sheet row (0..3).</returns>
    public static int RowIndex(this Direction d) => d switch
    {
        Direction.DownLeft or Direction.UpLeft => 1,
        Direction.DownRight or Direction.UpRight => 2,
        _ => (int)d,
    };

    /// <summary>
    /// Returns whether the direction is horizontal (<see cref="Direction.Left"/> or
    /// <see cref="Direction.Right"/>). Diagonals are neither horizontal nor vertical.
    /// </summary>
    /// <param name="d">The direction.</param>
    /// <returns><see langword="true"/> when the direction is horizontal; otherwise <see langword="false"/>.</returns>
    public static bool IsHorizontal(this Direction d) => d is Direction.Left or Direction.Right;

    /// <summary>
    /// Returns whether the direction is vertical (<see cref="Direction.Down"/> or
    /// <see cref="Direction.Up"/>). Diagonals are neither horizontal nor vertical.
    /// </summary>
    /// <param name="d">The direction.</param>
    /// <returns><see langword="true"/> when the direction is vertical; otherwise <see langword="false"/>.</returns>
    public static bool IsVertical(this Direction d) => d is Direction.Down or Direction.Up;

    /// <summary>
    /// Returns whether the direction is one of the four diagonal directions
    /// (<see cref="Direction.DownLeft"/>, <see cref="Direction.DownRight"/>,
    /// <see cref="Direction.UpLeft"/> or <see cref="Direction.UpRight"/>).
    /// </summary>
    /// <param name="d">The direction.</param>
    /// <returns><see langword="true"/> when the direction is diagonal; otherwise <see langword="false"/>.</returns>
    public static bool IsDiagonal(this Direction d)
        => d is Direction.DownLeft or Direction.DownRight or Direction.UpLeft or Direction.UpRight;

    /// <summary>The magnitude of a normalized diagonal component: &#8730;&#189; &#8776; 0.7071067811865476.</summary>
    private const double RootHalf = 0.7071067811865476;
}
