namespace RPGEngine.Sprites;

/// <summary>
/// References a specific character within a named spritesheet.
/// </summary>
/// <param name="Name">The unique name of the spritesheet.</param>
/// <param name="CharacterIndex">
/// The 1-based index (1..8) of the character within the sheet, in row-major order over the
/// sheet's 4×2 character grid.
/// </param>
/// <remarks>
/// This is a dumb value type: the 1..8 range is intentionally not validated here. It is
/// enforced where the reference is consumed (e.g. by <see cref="SpriteSheet.GetSprite"/> or by
/// the character compositor introduced by a later story).
/// </remarks>
public readonly record struct SpriteSheetRef(string Name, int CharacterIndex);
