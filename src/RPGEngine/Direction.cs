namespace RPGEngine;

/// <summary>
/// The four facing directions used throughout the engine.
/// </summary>
/// <remarks>
/// The numeric values match the RPG Maker MZ character sheet row order
/// (<c>0 = down</c>, <c>1 = left</c>, <c>2 = right</c>, <c>3 = up</c>). This is the single
/// source of truth used by sprite cropping and rendering; see
/// <see cref="DirectionExtensions.RowIndex"/>.
/// </remarks>
public enum Direction
{
    /// <summary>Facing down (row 0 of an RPG Maker MZ character sheet).</summary>
    Down = 0,

    /// <summary>Facing left (row 1 of an RPG Maker MZ character sheet).</summary>
    Left = 1,

    /// <summary>Facing right (row 2 of an RPG Maker MZ character sheet).</summary>
    Right = 2,

    /// <summary>Facing up (row 3 of an RPG Maker MZ character sheet).</summary>
    Up = 3,
}
