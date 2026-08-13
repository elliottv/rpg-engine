namespace RPGEngine.Sprites;

/// <summary>
/// The layer of a character that an RPG Maker MZ part spritesheet provides.
/// </summary>
public enum CharacterPartType
{
    /// <summary>The character's body.</summary>
    Body,

    /// <summary>The character's armour layer.</summary>
    Armour,

    /// <summary>The character's face.</summary>
    Face,

    /// <summary>The hair attached to the face layer.</summary>
    FaceHair,

    /// <summary>The first (base) hair layer.</summary>
    Hair1,

    /// <summary>The second (overlay) hair layer.</summary>
    Hair2,

    /// <summary>The character's head.</summary>
    Head,
}
