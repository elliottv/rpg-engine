namespace RPGEngine.Tiled;

/// <summary>
/// Flip flags that Tiled stores in the high bits of a tile's global ID (GID).
/// The flags are independent of any particular tileset and apply to the tile
/// when it is rendered on a layer.
/// </summary>
/// <remarks>
/// <para>
/// Tiled encodes the three orthogonal flip flags in the four most significant
/// bits of a 32-bit GID:
/// </para>
/// <list type="bullet">
/// <item><see cref="FlippedHorizontally"/> = <c>0x80000000</c></item>
/// <item><see cref="FlippedVertically"/> = <c>0x40000000</c></item>
/// <item><see cref="FlippedDiagonally"/> = <c>0x20000000</c></item>
/// </list>
/// <para>
/// The remaining high bit (<c>0x10000000</c>) is only used by hexagonal maps and is
/// ignored by this engine. Whenever a GID is read from layer data the flip bits are
/// masked off (see <see cref="TileFlags.Mask"/>) so the remaining value is the plain GID.
/// </para>
/// </remarks>
[Flags]
public enum TileFlags : uint
{
    /// <summary>No flip flags are set.</summary>
    None = 0,

    /// <summary>The tile is flipped horizontally (left ↔ right).</summary>
    FlippedHorizontally = 0x80000000u,

    /// <summary>The tile is flipped vertically (top ↔ bottom).</summary>
    FlippedVertically = 0x40000000u,

    /// <summary>
    /// The tile is flipped (anti-)diagonally, which swaps its X and Y axes and
    /// therefore enables 90° rotations when combined with the other flags.
    /// </summary>
    FlippedDiagonally = 0x20000000u,

    /// <summary>
    /// Bit mask that clears every flag bit, leaving only the plain global tile ID.
    /// Equivalent to <c>0x0FFFFFFF</c>.
    /// </summary>
    Mask = 0x0FFFFFFFu,
}
