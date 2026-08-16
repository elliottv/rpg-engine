namespace RPGEngine.Tiled;

/// <summary>
/// A single object in an object layer of a <see cref="TileMap"/>, wrapping the Tiled object's
/// identity, geometry, shape and custom properties. This is a read-only view; editing or adding
/// objects is out of scope.
/// </summary>
public sealed class TileMapObject
{
    /// <summary>Gets the object's unique ID within the map.</summary>
    public uint Id { get; }

    /// <summary>Gets the object's name, as declared in the map (may be empty).</summary>
    public string Name { get; }

    /// <summary>
    /// Gets the object's "class" string, as declared in the map (may be empty). This is the
    /// object's <c>type</c>/<c>class</c> attribute in the Tiled file.
    /// </summary>
    public string Type { get; }

    /// <summary>Gets the object's top-left position in pixels.</summary>
    public Position Position { get; }

    /// <summary>Gets the object's width in pixels (0 for shapes without a size, e.g. points).</summary>
    public float Width { get; }

    /// <summary>Gets the object's height in pixels (0 for shapes without a size, e.g. points).</summary>
    public float Height { get; }

    /// <summary>Gets the object's geometric shape.</summary>
    public TileMapObjectShape Shape { get; }

    /// <summary>Gets the object's custom properties, in file order.</summary>
    public IReadOnlyList<MapProperty> Properties { get; }

    internal TileMapObject(
        uint id,
        string name,
        string type,
        Position position,
        float width,
        float height,
        TileMapObjectShape shape,
        IReadOnlyList<MapProperty> properties)
    {
        Id = id;
        Name = name;
        Type = type;
        Position = position;
        Width = width;
        Height = height;
        Shape = shape;
        Properties = properties;
    }

    /// <summary>
    /// Builds a <see cref="TileMapObject"/> from a DotTiled <see cref="DotTiled.Object"/>,
    /// detecting the shape from the object subtype and converting the custom properties.
    /// </summary>
    /// <param name="obj">The DotTiled object to convert.</param>
    /// <returns>The corresponding <see cref="TileMapObject"/>.</returns>
    internal static TileMapObject FromDotTiled(DotTiled.Object obj)
    {
        var shape = obj switch
        {
            DotTiled.RectangleObject => TileMapObjectShape.Rectangle,
            DotTiled.EllipseObject => TileMapObjectShape.Ellipse,
            DotTiled.PointObject => TileMapObjectShape.Point,
            DotTiled.PolygonObject => TileMapObjectShape.Polygon,
            DotTiled.PolylineObject => TileMapObjectShape.Polyline,
            DotTiled.TileObject => TileMapObjectShape.Tile,
            DotTiled.TextObject => TileMapObjectShape.Text,
            _ => throw new InvalidOperationException(
                $"Unsupported Tiled object type '{obj.GetType().Name}'."),
        };

        return new TileMapObject(
            obj.ID.GetValueOr(0),
            obj.Name ?? string.Empty,
            obj.Type ?? string.Empty,
            new Position(obj.X, obj.Y),
            obj.Width,
            obj.Height,
            shape,
            obj.Properties.Select(MapProperty.Create).ToArray());
    }
}
