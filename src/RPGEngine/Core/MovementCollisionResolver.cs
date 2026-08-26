using RPGEngine.Tiled;

namespace RPGEngine;

/// <summary>
/// Resolves a character's displacement against the map's solid tiles with
/// <em>per-axis slide-to-boundary clamping</em>: for each axis the full requested
/// displacement is applied when the resulting collision footprint (the fixed 1×1 tile
/// lower-body box of the player, see <see cref="GameEngine.PlayerFootprintOverlapsSolid"/>) is
/// clear; otherwise the axis is clamped to the <em>closest legal position</em> on that axis, so
/// the leading edge of the footprint stops exactly at the near edge of the first blocking solid
/// tile (or at the map edge, which <see cref="TileMap.IsSolid"/> treats as solid).
/// </summary>
/// <remarks>
/// <para>
/// The footprint is the fixed 1×1 tile (48×48 px) box representing the lower body of the
/// player sprite, anchored at the feet: with the half-width <c>hw</c> (0.5 tiles) and the height
/// above the feet <c>heightAboveFeet</c> (1.0 tiles, i.e. <c>hw = 0.5</c> and
/// <c>heightAboveFeet = 1.0</c> for the standard 48 px tile) the rectangle is
/// <c>x ∈ [pos.X - hw, pos.X + hw]</c>, <c>y ∈ [pos.Y - heightAboveFeet, pos.Y]</c>.
/// The middle of the feet sits at the bottom-centre of the box (<c>(24, 48)</c> in pixels when
/// the box origin is its upper-left). The box is independent of the rendered sprite size: a
/// taller/wider spritesheet never widens the collision box, so a 1-tile-wide corridor always
/// fits regardless of the spritesheet's cell size. Out-of-bounds tiles are solid (the map edge
/// blocks movement), exactly like <see cref="TileMap.IsAreaSolid"/>.
/// </para>
/// <para>
/// Each axis is clamped independently and the result of the X axis feeds the Y axis
/// (axis-separated movement preserved): the horizontal displacement is resolved first, then the
/// vertical displacement starts from the clamped horizontal result. This keeps wall-sliding
/// natural (a blocked axis clamps to the boundary while the free axis still moves) and prevents
/// diagonal corner-cutting. Unlike a revert-the-whole-step rule, the clamp always moves the
/// leading edge exactly onto the boundary, so the feet stop exactly at the solid tile's edge
/// (matching click-to-move) with no one-frame-step gap and no floating-point overshoot
/// accumulation. <see cref="GameEngine.ClampPlayerToMap"/> remains the post-move safety net.
/// </para>
/// <para>
/// The per-axis gained-range scan assumes the starting footprint is legal (never overlapping a
/// solid tile): it detects the <em>newly entered</em> solid columns/rows. When the starting
/// footprint is <em>already</em> illegal — which can happen if a previous frame placed the
/// character inside a solid tile (e.g. a host teleport) — the scan cannot see the
/// already-overlapped tile, so the resolved position is re-validated with
/// <see cref="TileMap.IsAreaSolid"/> and the displacement is refused (the starting position is
/// returned) rather than moving the character deeper into or through the solid tile. A move
/// that leads to a legal footprint (escaping the overlap) is still allowed.
/// </para>
/// </remarks>
internal static class MovementCollisionResolver
{
    /// <summary>
    /// Returns the position with the X displacement applied (clamped to the boundary if it would
    /// overlap a solid tile or leave the map), then the Y displacement applied the same way,
    /// starting from the clamped X result. When the resolved footprint would still overlap a
    /// solid tile (only possible when <paramref name="from"/> was already illegal), the starting
    /// position is returned unchanged so the character is never displaced through a solid tile.
    /// </summary>
    /// <param name="from">The starting feet position, in tiles.</param>
    /// <param name="dx">The requested horizontal displacement, in tiles.</param>
    /// <param name="dy">The requested vertical displacement, in tiles.</param>
    /// <param name="map">The map whose solid tiles (and edge) block movement.</param>
    /// <param name="halfWidth">The half-width of the collision footprint (<c>hw</c>), in tiles:
    /// 0.5 for the player's fixed 1×1 tile lower-body box.</param>
    /// <param name="heightAboveFeet">The height the collision footprint extends above the feet,
    /// in tiles: 1.0 for the player's fixed 1×1 tile lower-body box (its bottom edge is the
    /// feet).</param>
    /// <returns>The resolved feet position: either the full requested displacement when it is
    /// legal, the closest legal position on each blocked axis, or (when the starting footprint
    /// was already illegal and the move would keep it illegal) the starting position.</returns>
    internal static Position Resolve(Position from, double dx, double dy, TileMap map, double halfWidth, double heightAboveFeet)
    {
        var x = ClampHorizontal(from, dx, map, halfWidth, heightAboveFeet);
        var y = ClampVertical(from, dy, map, halfWidth, heightAboveFeet, xAfterX: x);
        var resolved = new Position(x, y);

        // Safety net: for a legal starting footprint the per-axis clamps above always produce a
        // legal result. When the starting footprint was already illegal the gained-range scan
        // cannot detect the tile the character is already overlapping, so re-validate the
        // resolved footprint and refuse the displacement rather than moving through the wall.
        if (FootprintOverlaps(resolved, map, halfWidth, heightAboveFeet))
        {
            return from;
        }

        return resolved;
    }

    /// <summary>
    /// Returns whether the fixed lower-body footprint anchored at <paramref name="position"/>
    /// overlaps a solid tile (or leaves the map). Reuses <see cref="TileMap.IsAreaSolid"/> with
    /// the same rectangle the engine's <see cref="GameEngine.PlayerFootprintOverlapsSolid"/>
    /// checks, so the resolver and the engine's footprint predicate cannot diverge (the engine's
    /// predicate delegates here with the player's fixed 1×1 tile box). Internal so the engine
    /// and the tests share one footprint definition.
    /// </summary>
    /// <param name="position">The feet position, in tiles.</param>
    /// <param name="map">The map whose solid tiles (and edge) block movement.</param>
    /// <param name="halfWidth">The half-width of the collision footprint (<c>hw</c>), in tiles.</param>
    /// <param name="heightAboveFeet">The height the collision footprint extends above the feet, in tiles.</param>
    /// <returns><see langword="true"/> when the footprint overlaps a solid tile.</returns>
    internal static bool FootprintOverlaps(Position position, TileMap map, double halfWidth, double heightAboveFeet)
        => map.IsAreaSolid(
            position.X - halfWidth,
            position.Y - heightAboveFeet,
            halfWidth * 2,
            heightAboveFeet);

    /// <summary>
    /// Applies the horizontal displacement <paramref name="dx"/> with slide-to-boundary
    /// clamping: the right edge (<c>x' + hw</c>, when moving right) stops at the left edge of the
    /// first solid column the footprint would gain (<c>maxX = c - hw</c>, with the map edge as
    /// <c>c = Width</c>); the left edge (<c>x' - hw</c>, when moving left) stops at the right edge
    /// of the last solid column gained (<c>maxX = c + 1 + hw</c>, with the left map edge as
    /// <c>c = -1</c>). When no gained column is solid, the full displacement is returned.
    /// </summary>
    /// <param name="from">The starting feet position, in tiles.</param>
    /// <param name="dx">The requested horizontal displacement, in tiles.</param>
    /// <param name="map">The map whose solid tiles (and edge) block movement.</param>
    /// <param name="halfWidth">The half-width of the collision footprint (<c>hw</c>), in tiles.</param>
    /// <param name="heightAboveFeet">The height the collision footprint extends above the feet, in tiles.</param>
    /// <returns>The resolved feet X coordinate.</returns>
    private static double ClampHorizontal(Position from, double dx, TileMap map, double halfWidth, double heightAboveFeet)
    {
        if (dx > 0)
        {
            // The columns the footprint would gain by moving right: the starting footprint
            // already overlaps columns up to ceil(from.X + hw) - 1, so the gained range starts at
            // ceil(from.X + hw) and ends at ceil(from.X + dx + hw) - 1 (a destination right edge
            // exactly on a tile boundary does not count the next tile, matching IsAreaSolid).
            var firstGainedColumn = (int)Math.Ceiling(from.X + halfWidth);
            var lastGainedColumn = (int)Math.Ceiling(from.X + dx + halfWidth) - 1;
            for (var column = firstGainedColumn; column <= lastGainedColumn; column++)
            {
                if (ColumnBlocks(column, from.Y, map, heightAboveFeet))
                {
                    return column - halfWidth;
                }
            }
        }
        else if (dx < 0)
        {
            // Mirror image for moving left: the gained columns end at
            // floor(from.X - hw) - 1 and start at floor(from.X + dx - hw). Scan from the largest
            // (closest to the start) to the smallest so the leading edge stops at the first
            // blocking column encountered.
            var firstGainedColumn = (int)Math.Floor(from.X + dx - halfWidth);
            var lastGainedColumn = (int)Math.Floor(from.X - halfWidth) - 1;
            for (var column = lastGainedColumn; column >= firstGainedColumn; column--)
            {
                if (ColumnBlocks(column, from.Y, map, heightAboveFeet))
                {
                    return column + 1 + halfWidth;
                }
            }
        }

        // The whole displacement is legal on this axis (no solid column gained).
        return from.X + dx;
    }

    /// <summary>
    /// Applies the vertical displacement <paramref name="dy"/> with slide-to-boundary clamping:
    /// the feet (the bottom edge, when moving down) stop at the top edge of the first solid row
    /// gained (<c>maxY = r</c>, with the bottom map edge as <c>r = Height</c>); the top edge
    /// (<c>y' - heightAboveFeet</c>, when moving up) stops at the bottom edge of the last solid
    /// row gained (<c>maxY = r + 1 + heightAboveFeet</c>, with the top map edge as
    /// <c>r = -1</c>). The column range is taken from the already-clamped horizontal result
    /// (<paramref name="xAfterX"/>), preserving axis-separated movement. When no gained row is
    /// solid, the full displacement is returned.
    /// </summary>
    /// <param name="from">The starting feet position, in tiles.</param>
    /// <param name="dy">The requested vertical displacement, in tiles.</param>
    /// <param name="map">The map whose solid tiles (and edge) block movement.</param>
    /// <param name="halfWidth">The half-width of the collision footprint (<c>hw</c>), in tiles.</param>
    /// <param name="heightAboveFeet">The height the collision footprint extends above the feet, in tiles.</param>
    /// <param name="xAfterX">The feet X coordinate after the horizontal clamp.</param>
    /// <returns>The resolved feet Y coordinate.</returns>
    private static double ClampVertical(Position from, double dy, TileMap map, double halfWidth, double heightAboveFeet, double xAfterX)
    {
        if (dy > 0)
        {
            // The rows the footprint would gain by moving down: the starting footprint already
            // overlaps rows up to ceil(from.Y) - 1 (the bottom edge is the feet at from.Y), so
            // the gained range starts at ceil(from.Y) and ends at ceil(from.Y + dy) - 1.
            var firstGainedRow = (int)Math.Ceiling(from.Y);
            var lastGainedRow = (int)Math.Ceiling(from.Y + dy) - 1;
            for (var row = firstGainedRow; row <= lastGainedRow; row++)
            {
                if (RowBlocks(row, xAfterX, map, halfWidth))
                {
                    return row;
                }
            }
        }
        else if (dy < 0)
        {
            // Mirror image for moving up: the gained rows end at floor(from.Y - heightAboveFeet)
            // - 1 and start at floor(from.Y + dy - heightAboveFeet). Scan from the largest
            // (closest to the start) to the smallest so the top edge stops at the first blocking
            // row encountered.
            var firstGainedRow = (int)Math.Floor(from.Y + dy - heightAboveFeet);
            var lastGainedRow = (int)Math.Floor(from.Y - heightAboveFeet) - 1;
            for (var row = lastGainedRow; row >= firstGainedRow; row--)
            {
                if (RowBlocks(row, xAfterX, map, halfWidth))
                {
                    return row + 1 + heightAboveFeet;
                }
            }
        }

        // The whole displacement is legal on this axis (no solid row gained).
        return from.Y + dy;
    }

    /// <summary>
    /// Returns whether any tile in the footprint's row range at <paramref name="column"/> is
    /// solid. The row range is the set of rows overlapped by the footprint at feet Y
    /// <paramref name="y"/>: <c>[floor(y - heightAboveFeet), ceil(y) - 1]</c>, matching
    /// <see cref="TileMap.IsAreaSolid"/> (and out-of-bounds rows are solid through
    /// <see cref="TileMap.IsSolid"/>).
    /// </summary>
    /// <param name="column">The tile column to test.</param>
    /// <param name="y">The feet Y coordinate whose footprint row range is tested, in tiles.</param>
    /// <param name="map">The map whose solid tiles (and edge) block movement.</param>
    /// <param name="heightAboveFeet">The height the collision footprint extends above the feet, in tiles.</param>
    /// <returns><see langword="true"/> when any overlapped tile at <paramref name="column"/> is solid.</returns>
    private static bool ColumnBlocks(int column, double y, TileMap map, double heightAboveFeet)
    {
        var firstRow = (int)Math.Floor(y - heightAboveFeet);
        var lastRow = (int)Math.Ceiling(y) - 1;
        for (var row = firstRow; row <= lastRow; row++)
        {
            if (map.IsSolid(column, row))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether any tile in the footprint's column range at <paramref name="row"/> is
    /// solid. The column range is the set of columns overlapped by the footprint at feet X
    /// <paramref name="x"/>: <c>[floor(x - hw), ceil(x + hw) - 1]</c>, matching
    /// <see cref="TileMap.IsAreaSolid"/> (and out-of-bounds columns are solid through
    /// <see cref="TileMap.IsSolid"/>).
    /// </summary>
    /// <param name="row">The tile row to test.</param>
    /// <param name="x">The feet X coordinate whose footprint column range is tested, in tiles.</param>
    /// <param name="map">The map whose solid tiles (and edge) block movement.</param>
    /// <param name="halfWidth">The half-width of the collision footprint (<c>hw</c>), in tiles.</param>
    /// <returns><see langword="true"/> when any overlapped tile at <paramref name="row"/> is solid.</returns>
    private static bool RowBlocks(int row, double x, TileMap map, double halfWidth)
    {
        var firstColumn = (int)Math.Floor(x - halfWidth);
        var lastColumn = (int)Math.Ceiling(x + halfWidth) - 1;
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            if (map.IsSolid(column, row))
            {
                return true;
            }
        }

        return false;
    }
}
