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
/// </remarks>
internal sealed class CharacterSpriteCompositor
{
    private const int MinCharacterIndex = 1;
    private const int MaxCharacterIndex = 8;

    /// <summary>
    /// Draws the character described by <paramref name="spriteSheetRefs"/> at
    /// <paramref name="anchorPosition"/>.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <param name="anchorPosition">The middle-bottom (feet) anchor of the sprite, in pixels:
    /// the sprite is drawn above and centered on this point, so its top-left is at
    /// <c>(anchorPosition.X - width/2, anchorPosition.Y - height)</c>.</param>
    /// <param name="spriteSheetRefs">The spritesheet references to use (sheet name + character index).</param>
    /// <param name="direction">The direction the character faces.</param>
    /// <param name="frame">The animation frame (0..2).</param>
    /// <param name="manager">Resolves sheet names to <see cref="SpriteSheet"/> instances.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A reference has a <see cref="SpriteSheetRef.CharacterIndex"/> outside 1..8.
    /// </exception>
    /// <exception cref="KeyNotFoundException">A referenced sheet name is not loaded in <paramref name="manager"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The list mixes full and part sheets, or contains more than one full sheet.
    /// </exception>
    public void Draw(
        SKCanvas canvas,
        Position anchorPosition,
        IReadOnlyList<SpriteSheetRef> spriteSheetRefs,
        Direction direction,
        int frame,
        SpriteSheetManager manager)
    {
        if (spriteSheetRefs.Count == 0)
        {
            return;
        }

        var resolved = Resolve(spriteSheetRefs, manager);

        var fullCount = resolved.Count(r => r.Sheet.Type == SpriteSheetType.Full);
        var partCount = resolved.Count - fullCount;

        if (fullCount == 1 && partCount == 0)
        {
            // A single full sheet: draw its cell directly, no composition.
            DrawCell(canvas, anchorPosition, resolved[0], direction, frame);
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
