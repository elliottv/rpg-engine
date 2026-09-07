namespace RPGEngine;
using System.Text.Json.Serialization;

/// <summary>
/// A direction in the game world, expressed as a 2-D screen-space vector with
/// double-precision components (X grows right, Y grows down) — the same double model as
/// <see cref="Position"/>. This replaces the historical 8-value enum: a direction is now any
/// unit-length 2-D vector, so facing can be continuous (not just one of eight discrete values).
/// </summary>
/// <param name="X">The horizontal component of the direction (grows right).</param>
/// <param name="Y">The vertical component of the direction (grows down).</param>
/// <remarks>
/// <para>
/// Movement assumes a <em>unit-length</em> direction: the displacement applied is
/// <c>direction * BaseSpeed * factor * dt</c>, so a non-unit vector scales the displacement by
/// its <see cref="Length"/>. All engine-produced directions (key input, auto-walk facing) are
/// unit vectors; the eight canonical unit directions remain available as static members
/// (<see cref="Down"/>, <see cref="Left"/>, <see cref="Right"/>, <see cref="Up"/>,
/// <see cref="DownLeft"/>, <see cref="DownRight"/>, <see cref="UpLeft"/>,
/// <see cref="UpRight"/>), and <see cref="All"/> lists them in the historical enum order.
/// Sprites are still 8-direction: a (possibly continuous) facing is adapted to the nearest
/// canonical direction for the sheet row (see <see cref="DirectionExtensions.Nearest8"/> and
/// <see cref="DirectionExtensions.RowIndex"/>).
/// </para>
/// <para>
/// The default <c>default(Direction)</c> is the zero vector <c>(0, 0)</c>. Consumers that need
/// a facing default (e.g. <c>Character.Direction</c>) initialize it explicitly to
/// <see cref="Down"/> to preserve the historical default facing.
/// </para>
/// </remarks>
public readonly record struct Direction(double X, double Y)
{
    /// <summary>The magnitude of a normalized diagonal component: √½ ≈ 0.7071067811865476.</summary>
    private const double RootHalf = 0.7071067811865476;

    /// <summary>Gets the canonical unit direction facing down: <c>(0, +1)</c> (row 0 of an RPG Maker MZ character sheet).</summary>
    public static Direction Down { get; } = new(0, 1);

    /// <summary>Gets the canonical unit direction facing left: <c>(-1, 0)</c> (row 1 of an RPG Maker MZ character sheet).</summary>
    public static Direction Left { get; } = new(-1, 0);

    /// <summary>Gets the canonical unit direction facing right: <c>(1, 0)</c> (row 2 of an RPG Maker MZ character sheet).</summary>
    public static Direction Right { get; } = new(1, 0);

    /// <summary>Gets the canonical unit direction facing up: <c>(0, -1)</c> (row 3 of an RPG Maker MZ character sheet).</summary>
    public static Direction Up { get; } = new(0, -1);

    /// <summary>Gets the canonical unit direction facing down-left: <c>(-√½, +√½)</c>.</summary>
    public static Direction DownLeft { get; } = new(-RootHalf, RootHalf);

    /// <summary>Gets the canonical unit direction facing down-right: <c>(+√½, +√½)</c>.</summary>
    public static Direction DownRight { get; } = new(RootHalf, RootHalf);

    /// <summary>Gets the canonical unit direction facing up-left: <c>(-√½, -√½)</c>.</summary>
    public static Direction UpLeft { get; } = new(-RootHalf, -RootHalf);

    /// <summary>Gets the canonical unit direction facing up-right: <c>(+√½, -√½)</c>.</summary>
    public static Direction UpRight { get; } = new(RootHalf, -RootHalf);

    /// <summary>
    /// Gets the eight canonical unit directions (the &quot;old 8 directions&quot;), in the
    /// historical enum order: <see cref="Down"/>, <see cref="Left"/>, <see cref="Right"/>,
    /// <see cref="Up"/>, <see cref="DownLeft"/>, <see cref="DownRight"/>, <see cref="UpLeft"/>,
    /// <see cref="UpRight"/>. Key input always yields one of these, and sprites adapt a
    /// continuous facing to the nearest of them (see <see cref="DirectionExtensions.Nearest8"/>).
    /// </summary>
    public static IReadOnlyList<Direction> All { get; } = [Down, Left, Right, Up, DownLeft, DownRight, UpLeft, UpRight];

    /// <summary>
    /// Returns the opposite direction: the component-wise negation <c>(-X, -Y)</c>. For the
    /// eight canonical directions this matches the enum's historical opposite
    /// (<see cref="Down"/> ↔ <see cref="Up"/>, <see cref="Left"/> ↔
    /// <see cref="Right"/>, and each diagonal flips both signs), and it stays well-defined for
    /// any continuous direction.
    /// </summary>
    /// <param name="d">The direction to negate.</param>
    /// <returns>The opposite direction.</returns>
    public static Direction operator -(Direction d) => new(-d.X, -d.Y);

    /// <summary>
    /// Returns the component-wise product of the direction and the scalar. Movement multiplies
    /// the (unit) direction vector by the travelled distance: <c>direction * (BaseSpeed *
    /// factor * dt)</c>.
    /// </summary>
    /// <param name="d">The direction.</param>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <returns>The scaled direction.</returns>
    public static Direction operator *(Direction d, double scalar) => new(d.X * scalar, d.Y * scalar);

    /// <summary>Returns the component-wise product of the scalar and the direction.</summary>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <param name="d">The direction.</param>
    /// <returns>The scaled direction.</returns>
    public static Direction operator *(double scalar, Direction d) => d * scalar;

    /// <summary>
    /// Converts the direction to the equivalent <see cref="Vector2"/> with the same components.
    /// A direction <em>is</em> a 2-D vector, so this makes the direction directly usable wherever
    /// a <see cref="Vector2"/> offset is expected (e.g. adding a scaled direction to a
    /// <see cref="Position"/>).
    /// </summary>
    /// <param name="d">The direction to convert.</param>
    /// <returns>The equivalent <see cref="Vector2"/>.</returns>
    public static implicit operator Vector2(Direction d) => new(d.X, d.Y);

    /// <summary>
    /// Gets the Euclidean length (magnitude) of the direction. The canonical unit directions all
    /// have length 1, so their displacement equals <c>BaseSpeed * factor * dt</c>.
    /// </summary>
    public double Length => Math.Sqrt((X * X) + (Y * Y));

    /// <summary>Gets whether the direction is the zero vector <c>(0, 0)</c> (the default <c>default(Direction)</c>).</summary>
    public bool IsZero => X == 0 && Y == 0;

    /// <summary>
    /// Gets this direction normalized to unit length (its <see cref="Length"/> becomes 1), with
    /// its direction unchanged. Use this before movement to guarantee an exact
    /// <c>BaseSpeed * dt</c> displacement regardless of the input vector's length.
    /// </summary>
    /// <exception cref="InvalidOperationException">The direction is the zero vector <c>(0, 0)</c>, which has no direction to normalize.</exception>
    [JsonIgnore]
    public Direction Normalized
    {
        get
        {
            if (IsZero)
            {
                throw new InvalidOperationException("Cannot normalize the zero direction (0, 0).");
            }

            var length = Length;
            return new Direction(X / length, Y / length);
        }
    }

    /// <summary>
    /// Returns the canonical name (e.g. <c>&quot;Up&quot;</c>) when the direction equals one of
    /// the eight canonical unit directions in <see cref="All"/>, for readability and logging;
    /// otherwise returns <c>&quot;(X, Y)&quot;</c> with the numeric components.
    /// </summary>
    /// <returns>The canonical name for a canonical direction, else the component pair.</returns>
    public override string ToString()
    {
        if (this == Down)
        {
            return nameof(Down);
        }

        if (this == Left)
        {
            return nameof(Left);
        }

        if (this == Right)
        {
            return nameof(Right);
        }

        if (this == Up)
        {
            return nameof(Up);
        }

        if (this == DownLeft)
        {
            return nameof(DownLeft);
        }

        if (this == DownRight)
        {
            return nameof(DownRight);
        }

        if (this == UpLeft)
        {
            return nameof(UpLeft);
        }

        if (this == UpRight)
        {
            return nameof(UpRight);
        }

        return $"({X}, {Y})";
    }
}
