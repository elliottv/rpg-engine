namespace RPGEngine.Tiled;

/// <summary>
/// The geometric shape of an object in an object layer. Detected from the object's shape in the
/// Tiled file: a plain <c>&lt;object&gt;</c> is a rectangle, while <c>&lt;ellipse/&gt;</c>,
/// <c>&lt;point/&gt;</c>, <c>&lt;polygon&gt;</c>, <c>&lt;polyline&gt;</c>, a <c>gid</c>
/// attribute and <c>&lt;text&gt;</c> produce the other shapes.
/// </summary>
public enum TileMapObjectShape
{
    /// <summary>A rectangle (the default shape of a plain <c>&lt;object&gt;</c>).</summary>
    Rectangle,

    /// <summary>An ellipse (declared with an <c>&lt;ellipse/&gt;</c> child element).</summary>
    Ellipse,

    /// <summary>A point (declared with a <c>&lt;point/&gt;</c> child element).</summary>
    Point,

    /// <summary>A polygon (declared with a <c>&lt;polygon&gt;</c> child element).</summary>
    Polygon,

    /// <summary>A polyline (declared with a <c>&lt;polyline&gt;</c> child element).</summary>
    Polyline,

    /// <summary>A tile object (declared with a <c>gid</c> attribute referencing a tile).</summary>
    Tile,

    /// <summary>A text object (declared with a <c>&lt;text&gt;</c> child element).</summary>
    Text,
}
