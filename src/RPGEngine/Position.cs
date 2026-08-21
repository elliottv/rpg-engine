namespace RPGEngine;

/// <summary>
/// A position in the game world with double-precision coordinates measured in <em>tiles</em>.
/// Y grows downward, so increasing <see cref="Y"/> moves the position down the map. Positions
/// are stored in tile units; pixels are produced only at the canvas boundary (rendering and the
/// <see cref="ToPixels"/> conversion).
/// </summary>
/// <param name="X">The horizontal (x) coordinate, in tiles.</param>
/// <param name="Y">The vertical (y) coordinate, in tiles.</param>
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
    /// <param name="dx">The horizontal offset to apply, in tiles.</param>
    /// <param name="dy">The vertical offset to apply, in tiles.</param>
    /// <returns>The offset position.</returns>
    public Position WithOffset(double dx, double dy) => new(X + dx, Y + dy);

    /// <summary>
    /// Floors the tile-unit position to the coordinates of the containing cell. Because
    /// positions are already expressed in tiles, this simply floors each component: a position
    /// of <c>(8.5, 8.5)</c> lies in cell <c>(8, 8)</c>, and a position of <c>(-1.5, -1.5)</c>
    /// lies in cell <c>(-2, -2)</c> (floor division, so negatives round toward negative infinity).
    /// </summary>
    /// <returns>The 0-based tile cell containing this position.</returns>
    public (int TileX, int TileY) ToTile()
        => ((int)Math.Floor(X), (int)Math.Floor(Y));

    /// <summary>
    /// Converts the tile-unit position to pixel coordinates: <c>(X * tileSize, Y * tileSize)</c>.
    /// Used by rendering (to place sprites and the camera viewport on the canvas) and by the
    /// <c>GameEngine.SurfaceToWorld</c>/<c>WorldToSurface</c> conversions.
    /// </summary>
    /// <param name="tileSize">The size of a tile in pixels; must be greater than zero.</param>
    /// <returns>The equivalent pixel position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tileSize"/> is zero or negative.</exception>
    public Position ToPixels(int tileSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileSize);

        return new Position(X * tileSize, Y * tileSize);
    }

    /// <summary>Returns the Euclidean distance between this position and <paramref name="other"/> (in tiles).</summary>
    /// <param name="other">The other position.</param>
    /// <returns>The non-negative distance.</returns>
    public double DistanceTo(Position other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
