namespace RPGEngine;

/// <summary>
/// The eight facing directions used throughout the engine.
/// </summary>
/// <remarks>
/// The cardinal values match the RPG Maker MZ character sheet row order
/// (<c>0 = down</c>, <c>1 = left</c>, <c>2 = right</c>, <c>3 = up</c>) and the diagonal values
/// follow them (<c>4..7</c>). Diagonals have no dedicated sprite-sheet row: a diagonally-facing
/// character renders with the side-view row of its horizontal component (see
/// <see cref="DirectionExtensions.RowIndex"/>). This enum is the single source of truth used by
/// sprite cropping and rendering.
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

    /// <summary>Facing down-left (renders with the Left/row-1 sprite).</summary>
    DownLeft = 4,

    /// <summary>Facing down-right (renders with the Right/row-2 sprite).</summary>
    DownRight = 5,

    /// <summary>Facing up-left (renders with the Left/row-1 sprite).</summary>
    UpLeft = 6,

    /// <summary>Facing up-right (renders with the Right/row-2 sprite).</summary>
    UpRight = 7,
}
