namespace RPGEngine;

/// <summary>
/// A position in 2D screen space with double-precision coordinates. Y grows downward, so
/// increasing <see cref="Y"/> moves the position down the screen.
/// </summary>
/// <param name="X">The horizontal (x) coordinate.</param>
/// <param name="Y">The vertical (y) coordinate.</param>
public readonly record struct Position(double X, double Y)
{
    /// <summary>Returns a new position offset from this one by <paramref name="offset"/>.</summary>
    public static Position operator +(Position position, Vector2 offset)
        => new(position.X + offset.X, position.Y + offset.Y);

    /// <summary>Returns a new position offset from this one by <paramref name="offset"/>.</summary>
    public static Position operator -(Position position, Vector2 offset)
        => new(position.X - offset.X, position.Y - offset.Y);

    /// <summary>Returns the vector from <paramref name="right"/> to <paramref name="left"/>.</summary>
    public static Vector2 operator -(Position left, Position right)
        => new(left.X - right.X, left.Y - right.Y);

    /// <summary>Returns a new position offset from this one by <paramref name="dx"/> and <paramref name="dy"/>.</summary>
    /// <param name="dx">The horizontal offset to apply.</param>
    /// <param name="dy">The vertical offset to apply.</param>
    /// <returns>The offset position.</returns>
    public Position WithOffset(double dx, double dy) => new(X + dx, Y + dy);

    /// <summary>
    /// Converts the pixel position to tile coordinates using floor division.
    /// </summary>
    /// <param name="tileSize">The size of a tile in pixels; must be greater than zero.</param>
    /// <returns>The tile coordinates containing this position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tileSize"/> is zero or negative.</exception>
    public (int TileX, int TileY) ToTile(int tileSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileSize);

        return ((int)Math.Floor(X / tileSize), (int)Math.Floor(Y / tileSize));
    }

    /// <summary>Returns the Euclidean distance between this position and <paramref name="other"/>.</summary>
    /// <param name="other">The other position.</param>
    /// <returns>The non-negative distance.</returns>
    public double DistanceTo(Position other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
