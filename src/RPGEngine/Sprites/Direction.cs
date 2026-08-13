namespace RPGEngine.Sprites;

/// <summary>
/// The four facing directions used by RPG Maker MZ character spritesheets.
/// </summary>
/// <remarks>
/// The numeric values match the row order within a character block on the sheet:
/// <c>0 = down</c>, <c>1 = left</c>, <c>2 = right</c>, <c>3 = up</c>
/// (see <see cref="SpriteSheet.GetSprite"/>).
/// </remarks>
public enum Direction
{
    /// <summary>Facing down (row 0 of a character block).</summary>
    Down = 0,

    /// <summary>Facing left (row 1 of a character block).</summary>
    Left = 1,

    /// <summary>Facing right (row 2 of a character block).</summary>
    Right = 2,

    /// <summary>Facing up (row 3 of a character block).</summary>
    Up = 3,
}
