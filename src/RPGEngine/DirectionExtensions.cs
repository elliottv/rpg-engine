namespace RPGEngine;

/// <summary>
/// Provides the 8-direction adaptation layer for the <see cref="Direction"/> vector type: it
/// adapts a (possibly continuous) direction to the nearest of the eight canonical unit
/// directions (<see cref="Nearest8"/>), maps a direction to its RPG Maker MZ character-sheet
/// row (<see cref="RowIndex"/>) and returns its opposite (<see cref="Opposite"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Direction"/> is a continuous 2-D vector (see its documentation). Key input still
/// yields one of the eight canonical unit directions (see
/// <see cref="GameConfig.GetMovementDirection(System.Collections.Generic.IEnumerable{Key})"/>),
/// but a facing direction may in general be continuous. Sprites remain 8-direction: the
/// direction vector is adapted to the nearest of the eight canonical directions
/// (<see cref="Nearest8"/>) and then to a sheet row (<see cref="RowIndex"/>) — the epic's
/// &quot;the direction vector is adapted to the old 8 directions&quot;.
/// </para>
/// </remarks>
public static class DirectionExtensions
{
    /// <summary>
    /// Returns the closest of the eight canonical unit directions in <see cref="Direction.All"/>
    /// to this direction, chosen by dot product against each canonical unit vector after
    /// normalizing <paramref name="d"/> (so only the direction matters, not its length). The zero
    /// vector <c>(0, 0)</c> has no direction and snaps to <see cref="Direction.Down"/>.
    /// </summary>
    /// <remarks>
    /// This is the &quot;adapt the direction vector to the old 8 directions&quot; operation used
    /// at the sprite boundary: a continuous facing such as <c>(0.9, 0.2)</c> maps to
    /// <see cref="Direction.Right"/>.
    /// </remarks>
    /// <param name="d">The direction to snap.</param>
    /// <returns>The nearest canonical unit direction (one of <see cref="Direction.All"/>).</returns>
    public static Direction Nearest8(this Direction d)
    {
        if (d.IsZero)
        {
            return Direction.Down;
        }

        // The canonical directions are all unit vectors, so after normalizing d the dot product
        // with each candidate is exactly the cosine of the angle between them: the largest dot
        // product is the closest direction. Ties (vectors exactly between two directions) are
        // resolved by All's order (the first best wins).
        var normalized = d.Normalized;
        Direction best = Direction.Down;
        var bestDot = double.NegativeInfinity;
        foreach (var candidate in Direction.All)
        {
            var dot = (normalized.X * candidate.X) + (normalized.Y * candidate.Y);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Returns the RPG Maker MZ character-sheet row the character should render with for this
    /// direction: <c>0 = down</c>, <c>1 = left</c>, <c>2 = right</c>, <c>3 = up</c>.
    /// </summary>
    /// <remarks>
    /// The direction is first snapped with <see cref="Nearest8"/>, so a continuous vector maps
    /// to the row of its nearest canonical direction. Cardinal directions return their own row;
    /// canonical diagonals have no dedicated sheet row, so they deliberately fall back to their
    /// <em>horizontal</em> component's row (<see cref="Direction.DownLeft"/> and
    /// <see cref="Direction.UpLeft"/> → 1, <see cref="Direction.DownRight"/> and
    /// <see cref="Direction.UpRight"/> → 2): a diagonally-facing character renders with the
    /// side-view row, which reads better than the front or back rows for an oblique facing.
    /// </remarks>
    /// <param name="d">The direction.</param>
    /// <returns>The 0-based sprite-sheet row (0..3).</returns>
    public static int RowIndex(this Direction d)
    {
        var nearest = d.Nearest8();

        if (nearest == Direction.Left || nearest == Direction.DownLeft || nearest == Direction.UpLeft)
        {
            return 1;
        }

        if (nearest == Direction.Right || nearest == Direction.DownRight || nearest == Direction.UpRight)
        {
            return 2;
        }

        if (nearest == Direction.Up)
        {
            return 3;
        }

        return 0; // Direction.Down
    }

    /// <summary>
    /// Returns the direction opposite to this one. This is exactly the vector negation
    /// <c>-d</c>: for the eight canonical directions it matches the enum's historical opposite
    /// (<see cref="Direction.Down"/> ↔ <see cref="Direction.Up"/>,
    /// <see cref="Direction.Left"/> ↔ <see cref="Direction.Right"/>, and each diagonal
    /// flips both signs), and it remains well-defined for any continuous direction.
    /// </summary>
    /// <param name="d">The direction.</param>
    /// <returns>The opposite direction.</returns>
    public static Direction Opposite(this Direction d) => -d;
}
