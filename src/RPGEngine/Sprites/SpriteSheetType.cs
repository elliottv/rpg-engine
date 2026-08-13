namespace RPGEngine.Sprites;

/// <summary>
/// The kind of an RPG Maker MZ spritesheet.
/// </summary>
public enum SpriteSheetType
{
    /// <summary>
    /// A complete character sheet: every layer (body, armour, face, hair, head) is baked into
    /// one image.
    /// </summary>
    Full,

    /// <summary>
    /// A single character layer (see <see cref="CharacterPartType"/>) that is composed with
    /// other part sheets to build a complete character.
    /// </summary>
    Part,
}
