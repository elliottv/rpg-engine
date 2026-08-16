namespace RPGEngine.Tiled;

/// <summary>
/// An object layer of a <see cref="TileMap"/>: a named collection of <see cref="TileMapObject"/>s
/// with their own visibility, opacity and custom properties. Object layers do not render tiles;
/// rendering objects on the map is out of scope.
/// </summary>
public sealed class TileMapObjectLayer
{
    /// <summary>Gets the name of the object layer (as declared in the map).</summary>
    public string Name { get; }

    /// <summary>Gets whether the object layer is visible (shown) in the map.</summary>
    public bool Visible { get; }

    /// <summary>Gets the opacity of the object layer, from 0 (fully transparent) to 1 (fully opaque).</summary>
    public float Opacity { get; }

    /// <summary>Gets the objects of the layer, in file order.</summary>
    public IReadOnlyList<TileMapObject> Objects { get; }

    /// <summary>Gets the object layer's custom properties, in file order.</summary>
    public IReadOnlyList<MapProperty> Properties { get; }

    internal TileMapObjectLayer(
        string name,
        bool visible,
        float opacity,
        IReadOnlyList<TileMapObject> objects,
        IReadOnlyList<MapProperty> properties)
    {
        Name = name;
        Visible = visible;
        Opacity = opacity;
        Objects = objects;
        Properties = properties;
    }

    /// <summary>
    /// Builds a <see cref="TileMapObjectLayer"/> from a DotTiled <see cref="DotTiled.ObjectLayer"/>,
    /// converting each object and the layer's custom properties.
    /// </summary>
    /// <param name="layer">The DotTiled object layer to convert.</param>
    /// <returns>The corresponding <see cref="TileMapObjectLayer"/>.</returns>
    internal static TileMapObjectLayer FromDotTiled(DotTiled.ObjectLayer layer)
        => new(
            layer.Name ?? string.Empty,
            layer.Visible,
            layer.Opacity,
            layer.Objects.Select(TileMapObject.FromDotTiled).ToArray(),
            layer.Properties.Select(MapProperty.Create).ToArray());
}
