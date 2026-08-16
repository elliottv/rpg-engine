namespace RPGEngine.Tiled;

/// <summary>
/// The type of a custom property attached to a map, a tile layer, an object layer or an object.
/// Mirrors the property types Tiled can store in a <c>&lt;property&gt;</c> element.
/// </summary>
public enum MapPropertyType
{
    /// <summary>A boolean property (value is a <see cref="bool"/>).</summary>
    Bool,

    /// <summary>A 32-bit signed integer property (value is an <see cref="int"/>).</summary>
    Int,

    /// <summary>A single-precision floating point property (value is a <see cref="float"/>).</summary>
    Float,

    /// <summary>A text property (value is a <see cref="string"/>).</summary>
    String,

    /// <summary>
    /// A color property (value is a <see cref="SkiaSharp.SKColor"/>). Tiled stores colors in
    /// <c>#AARRGGBB</c> form; the engine exposes them as <c>SKColor</c> so the rest of the
    /// rendering stack can use them directly.
    /// </summary>
    Color,

    /// <summary>
    /// A file path property (value is a <see cref="string"/> containing the path, relative to
    /// the map as Tiled declared it).
    /// </summary>
    File,

    /// <summary>
    /// An object reference property. The value is the referenced object's ID as a
    /// <see cref="string"/> (the raw string form from the file); resolving the reference to a
    /// <see cref="TileMapObject"/> is not exposed yet.
    /// </summary>
    Object,

    /// <summary>
    /// A custom-class property. The value is <see langword="null"/>: structured access to the
    /// members of a custom class is explicitly out of scope.
    /// </summary>
    Class,

    /// <summary>A property whose type the engine does not recognise (value is <see langword="null"/>).</summary>
    Unknown,
}
