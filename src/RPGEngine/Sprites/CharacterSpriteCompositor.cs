using SkiaSharp;

namespace RPGEngine.Sprites;

/// <summary>
/// Draws a <see cref="Character"/>'s sprite by resolving its spritesheet references and either
/// drawing a single <em>full</em> sheet or composing <em>part</em> sheets in the fixed RPG Maker
/// MZ order. Kept internal and separate from <c>Character</c> so the composition algorithm is
/// unit-testable through <c>InternalsVisibleTo</c> without being part of the public API.
/// </summary>
/// <remarks>
/// <para>
/// The part composition reproduces the epic's PIXI.js example. For the current animation
/// frame and direction the cell of each part (at the sheet's derived cell size, e.g. 48×48
/// for a 576×384 sheet or 78×108 for a 936×864 sheet) is drawn bottom → top in exactly
/// this order (missing parts are skipped):
/// </para>
/// <list type="number">
/// <item><see cref="CharacterPartType.Hair2"/> (when hair is shown) — behind everything.</item>
/// <item><see cref="CharacterPartType.Face"/>.</item>
/// <item><see cref="CharacterPartType.Body"/>.</item>
/// <item><see cref="CharacterPartType.Hair1"/> (when hair is shown).</item>
/// <item><see cref="CharacterPartType.FaceHair"/>.</item>
/// <item><see cref="CharacterPartType.Armour"/>.</item>
/// <item><see cref="CharacterPartType.Hair2"/> again, only when facing up (the per-direction
/// adjustment: rear hair is drawn over the body).</item>
/// <item><see cref="CharacterPartType.Head"/>.</item>
/// </list>
/// <para>
/// Hair (hair1 and hair2) is shown unless a <see cref="CharacterPartType.Head"/> sheet is present
/// whose name contains the <c>$</c> character (the <c>$</c>-prefix rule from the epic).
/// </para>
/// <para>
/// Every cell (whether a single full sheet or one part of a composition) is drawn with its
/// <em>middle-bottom</em> at the anchor position passed to <see cref="Draw"/>: the anchor is
/// the character's feet (where it stands), and the sprite is rendered above and centered on it.
/// A cell's top-left is therefore at
/// <c>(anchorPosition.X - width/2, anchorPosition.Y - height)</c> in pixels.
/// </para>
/// <para>
/// When the character has a non-null <c>IconIndex</c> (see <c>Character.IconIndex</c>) and an
/// icon set is supplied to <see cref="Draw"/>, the selected 32×32 icon is drawn <em>after</em>
/// the sprite, <em>above</em> it: centered horizontally on the character's feet X, with its
/// bottom edge at the sprite's top edge, within the character's Y-sorted draw pass (the engine
/// sorts characters by <c>Position.Y</c> before drawing, so the icon moves with its character).
/// A non-null icon index with no icon set loaded throws <see cref="InvalidOperationException"/>;
/// an icon index outside the set's range throws <see cref="ArgumentOutOfRangeException"/> from
/// <see cref="IconSet.GetIcon"/>.
/// </para>
/// </remarks>
internal sealed class CharacterSpriteCompositor
{
    private const int MinCharacterIndex = 1;
    private const int MaxCharacterIndex = 8;

    /// <summary>
    /// The cell height used to place an icon when the character has no spritesheet: the
    /// documented default sprite size of 48 (the same default as <c>Character.GetSpriteSize</c>),
    /// so a marker can be shown on a spriteless character.
    /// </summary>
    private const int DefaultCellHeight = 48;

    /// <summary>
    /// Draws the character described by <paramref name="spriteSheetRefs"/> at
    /// <paramref name="anchorPosition"/>. When <paramref name="iconIndex"/> is non-null and
    /// <paramref name="iconSet"/> is non-null, the selected 32×32 icon is drawn above the sprite
    /// after it (see <see cref="DrawIcon"/>).
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <param name="anchorPosition">The middle-bottom (feet) anchor of the sprite, in pixels:
    /// the sprite is drawn above and centered on this point, so its top-left is at
    /// <c>(anchorPosition.X - width/2, anchorPosition.Y - height)</c>.</param>
    /// <param name="spriteSheetRefs">The spritesheet references to use (sheet name + character index).</param>
    /// <param name="direction">The direction the character faces.</param>
    /// <param name="frame">The animation frame (0..2).</param>
    /// <param name="manager">Resolves sheet names to <see cref="SpriteSheet"/> instances.</param>
    /// <param name="iconSet">The icon set loaded into the engine, or <see langword="null"/> when
    /// none is loaded. Required to be non-null when <paramref name="iconIndex"/> is non-null.</param>
    /// <param name="iconIndex">The zero-based icon index to draw above the sprite, or
    /// <see langword="null"/> to draw no icon.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A reference has a <see cref="SpriteSheetRef.CharacterIndex"/> outside 1..8, or
    /// <paramref name="iconIndex"/> is outside the set's <c>0..Count-1</c> range.
    /// </exception>
    /// <exception cref="KeyNotFoundException">A referenced sheet name is not loaded in <paramref name="manager"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The list mixes full and part sheets, or contains more than one full sheet, or
    /// <paramref name="iconIndex"/> is non-null while <paramref name="iconSet"/> is
    /// <see langword="null"/> (no icon set is loaded).
    /// </exception>
    public void Draw(
        SKCanvas canvas,
        Position anchorPosition,
        IReadOnlyList<SpriteSheetRef> spriteSheetRefs,
        Direction direction,
        int frame,
        SpriteSheetManager manager,
        IconSet? iconSet,
        int? iconIndex)
    {
        // Strict validation up front, mirroring the existing full/part and character-index
        // checks: an icon can only be drawn from a loaded icon set, so a non-null index with no
        // loaded set is a misconfiguration.
        if (iconIndex is not null && iconSet is null)
        {
            throw new InvalidOperationException(
                "No icon set is loaded. Call GameEngine.LoadIconSet (or LoadIconSetAsync) " +
                "before setting a character's IconIndex.");
        }

        if (spriteSheetRefs.Count == 0)
        {
            // A spriteless character: draw only the icon, placed above the documented default
            // sprite size of 48×48 (the same default as Character.GetSpriteSize), so a marker
            // can be shown on a character without a sheet.
            DrawIcon(canvas, anchorPosition, DefaultCellHeight, iconSet, iconIndex);
            return;
        }

        var resolved = Resolve(spriteSheetRefs, manager);

        var fullCount = resolved.Count(r => r.Sheet.Type == SpriteSheetType.Full);
        var partCount = resolved.Count - fullCount;

        if (fullCount == 1 && partCount == 0)
        {
            // A single full sheet: draw its cell directly, no composition.
            DrawCell(canvas, anchorPosition, resolved[0], direction, frame);
            DrawIcon(canvas, anchorPosition, resolved[0].Sheet.CellHeight, iconSet, iconIndex);
            return;
        }

        if (fullCount >= 1)
        {
            // Either mixed full+part or more than one full sheet — both are misconfiguration.
            throw new InvalidOperationException(
                "A character must use either exactly one full spritesheet or one or more part " +
                $"sheets; the configured list contains {fullCount} full and {partCount} part sheet(s).");
        }

        ComposeParts(canvas, anchorPosition, resolved, direction, frame);

        // The icon is placed above the sprite using the derived cell height the parts were drawn
        // at (all parts of a composition come from the same RPG Maker MZ export, so they share a
        // cell size; the first resolved part is authoritative).
        DrawIcon(canvas, anchorPosition, resolved[0].Sheet.CellHeight, iconSet, iconIndex);
    }

    /// <summary>
    /// Resolves every reference to a sheet, rejecting character indices outside 1..8 before any
    /// drawing happens (so an invalid configuration never partially renders).
    /// </summary>
    private static List<ResolvedRef> Resolve(IReadOnlyList<SpriteSheetRef> refs, SpriteSheetManager manager)
    {
        var resolved = new List<ResolvedRef>(refs.Count);
        foreach (var reference in refs)
        {
            if (reference.CharacterIndex < MinCharacterIndex || reference.CharacterIndex > MaxCharacterIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(SpriteSheetRef.CharacterIndex),
                    reference.CharacterIndex,
                    $"Character index must be between {MinCharacterIndex} and {MaxCharacterIndex} " +
                    $"(RPG Maker MZ sheets contain 8 characters), but was {reference.CharacterIndex}.");
            }

            resolved.Add(new ResolvedRef(reference, manager.Get(reference.Name)));
        }

        return resolved;
    }

    /// <summary>
    /// Draws the parts in the fixed bottom → top composition order. The order is independent of
    /// the order of the entries in <c>SpriteSheets</c>: each part type is looked up on demand.
    /// </summary>
    private static void ComposeParts(
        SKCanvas canvas,
        Position anchorPosition,
        IReadOnlyList<ResolvedRef> parts,
        Direction direction,
        int frame)
    {
        var head = FindPart(parts, CharacterPartType.Head);
        var showHair = head is null || !head.Value.Sheet.Name.Contains('$');

        // 1. hair2 behind everything (when hair is shown).
        if (showHair)
        {
            DrawPart(canvas, anchorPosition, FindPart(parts, CharacterPartType.Hair2), direction, frame);
        }

        // 2. face
        DrawPart(canvas, anchorPosition, FindPart(parts, CharacterPartType.Face), direction, frame);

        // 3. body
        DrawPart(canvas, anchorPosition, FindPart(parts, CharacterPartType.Body), direction, frame);

        // 4. hair1 (when hair is shown)
        if (showHair)
        {
            DrawPart(canvas, anchorPosition, FindPart(parts, CharacterPartType.Hair1), direction, frame);
        }

        // 5. face_hair
        DrawPart(canvas, anchorPosition, FindPart(parts, CharacterPartType.FaceHair), direction, frame);

        // 6. armour
        DrawPart(canvas, anchorPosition, FindPart(parts, CharacterPartType.Armour), direction, frame);

        // 7. hair2 again, only when facing up (rear hair drawn over the body).
        if (showHair && direction == Direction.Up)
        {
            DrawPart(canvas, anchorPosition, FindPart(parts, CharacterPartType.Hair2), direction, frame);
        }

        // 8. head (if present)
        DrawPart(canvas, anchorPosition, head, direction, frame);
    }

    /// <summary>
    /// Draws a single part, doing nothing when the part is not present in the list.
    /// </summary>
    private static void DrawPart(
        SKCanvas canvas,
        Position anchorPosition,
        ResolvedRef? part,
        Direction direction,
        int frame)
    {
        if (part is null)
        {
            return;
        }

        DrawCell(canvas, anchorPosition, part.Value, direction, frame);
    }

    /// <summary>
    /// Draws the cell selected by the part's own character index at its native size, anchored so
    /// the cell's <em>middle-bottom</em> sits exactly at <paramref name="anchorPosition"/>.
    /// </summary>
    private static void DrawCell(
        SKCanvas canvas,
        Position anchorPosition,
        ResolvedRef part,
        Direction direction,
        int frame)
    {
        // GetSprite validates the index/frame again defensively and returns an independent
        // image at the sheet's derived cell size that the caller owns. The cell is drawn with its
        // middle-bottom (the character's feet) at the anchor: the top-left is offset left by half
        // the cell width and up by the full cell height, so the sprite stands on the anchor.
        using var sprite = part.Sheet.GetSprite(part.Ref.CharacterIndex, direction, frame);
        canvas.DrawImage(sprite, new SKPoint(
            (float)(anchorPosition.X - sprite.Width / 2.0),
            (float)(anchorPosition.Y - sprite.Height)));
    }

    /// <summary>
    /// Draws the selected icon above the sprite when one is configured: the 32×32 icon is drawn
    /// centered horizontally on the character's feet X, with its bottom edge at the sprite's top
    /// edge. The sprite's top is at <c>anchorPosition.Y - cellHeight</c>, so the icon's top-left
    /// is at <c>(anchorPosition.X - icon.Width / 2, anchorPosition.Y - cellHeight - icon.Height)</c>.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <param name="anchorPosition">The feet (middle-bottom) anchor of the sprite, in pixels.</param>
    /// <param name="cellHeight">The derived cell height the sprite was drawn at (the default 48
    /// for a spriteless character).</param>
    /// <param name="iconSet">The icon set loaded into the engine, or <see langword="null"/>.</param>
    /// <param name="iconIndex">The zero-based icon index, or <see langword="null"/> for no icon.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="iconIndex"/> is outside the set's <c>0..Count-1</c> range.
    /// </exception>
    private static void DrawIcon(
        SKCanvas canvas,
        Position anchorPosition,
        int cellHeight,
        IconSet? iconSet,
        int? iconIndex)
    {
        if (iconIndex is null || iconSet is null)
        {
            return;
        }

        // The icon is drawn above the sprite: 32x32, centered horizontally on the character's
        // feet X, its bottom edge at the sprite's top edge. The sprite's top is at
        // anchorPosition.Y - cellHeight, so the icon's top-left is at
        // (anchorPosition.X - 16, anchorPosition.Y - cellHeight - 32).
        using var icon = iconSet.GetIcon(iconIndex.Value);
        canvas.DrawImage(icon, new SKPoint(
            (float)(anchorPosition.X - icon.Width / 2.0),
            (float)(anchorPosition.Y - cellHeight - icon.Height)));
    }

    /// <summary>Returns the first resolved part of the given type, or <see langword="null"/> when absent.</summary>
    private static ResolvedRef? FindPart(IReadOnlyList<ResolvedRef> parts, CharacterPartType type)
    {
        foreach (var part in parts)
        {
            if (part.Sheet.PartType == type)
            {
                return part;
            }
        }

        return null;
    }

    /// <summary>A resolved spritesheet reference: the original reference plus its sheet.</summary>
    private readonly record struct ResolvedRef(SpriteSheetRef Ref, SpriteSheet Sheet);
}
