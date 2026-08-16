using System.Globalization;
using SkiaSharp;

namespace RPGEngine.Tiled;

/// <summary>
/// A single custom property attached to a map, a tile layer, an object layer or an object, in
/// the Tiled format. The value is boxed according to <see cref="Type"/> (see the
/// <see cref="MapPropertyType"/> members for the exact boxed shape per type).
/// </summary>
/// <param name="Name">The property name, as declared in the map (case-sensitive).</param>
/// <param name="Type">The property type.</param>
/// <param name="Value">The boxed value; <see langword="null"/> for <see cref="MapPropertyType.Class"/> and <see cref="MapPropertyType.Unknown"/>.</param>
public sealed record MapProperty(string Name, MapPropertyType Type, object? Value)
{
    /// <summary>
    /// Converts a DotTiled property into the engine's read-model representation. The read model
    /// wraps DotTiled so no DotTiled types leak into the public API.
    /// </summary>
    /// <param name="property">The DotTiled property to convert.</param>
    /// <returns>The corresponding <see cref="MapProperty"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is <see langword="null"/>.</exception>
    internal static MapProperty Create(DotTiled.IProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property switch
        {
            DotTiled.BoolProperty p => new MapProperty(p.Name, MapPropertyType.Bool, p.Value),
            DotTiled.IntProperty p => new MapProperty(p.Name, MapPropertyType.Int, p.Value),
            DotTiled.FloatProperty p => new MapProperty(p.Name, MapPropertyType.Float, p.Value),
            DotTiled.StringProperty p => new MapProperty(p.Name, MapPropertyType.String, p.Value),
            DotTiled.ColorProperty p => new MapProperty(p.Name, MapPropertyType.Color, ToSkColor(p)),
            DotTiled.FileProperty p => new MapProperty(p.Name, MapPropertyType.File, p.Value),
            DotTiled.ObjectProperty p => new MapProperty(p.Name, MapPropertyType.Object, p.Value.ToString(CultureInfo.InvariantCulture)),
            DotTiled.ClassProperty p => new MapProperty(p.Name, MapPropertyType.Class, null),
            _ => new MapProperty(property.Name, MapPropertyType.Unknown, null),
        };
    }

    /// <summary>
    /// Converts the optional DotTiled color into an <see cref="SKColor"/>, or
    /// <see langword="null"/> when the property carries no color value.
    /// </summary>
    private static SKColor? ToSkColor(DotTiled.ColorProperty property)
        => property.Value.HasValue
            ? new SKColor(
                property.Value.Value.R,
                property.Value.Value.G,
                property.Value.Value.B,
                property.Value.Value.A)
            : null;
}
