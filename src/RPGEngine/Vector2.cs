namespace RPGEngine;

/// <summary>
/// A two-dimensional vector with double-precision components, used for screen-space offsets,
/// deltas and distances.
/// </summary>
/// <param name="X">The horizontal (x) component.</param>
/// <param name="Y">The vertical (y) component.</param>
public readonly record struct Vector2(double X, double Y)
{
    /// <summary>Returns the component-wise sum of <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>Returns the component-wise difference of <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>Returns the negation of <paramref name="v"/>.</summary>
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);
}
