using DotTiled;

namespace RPGEngine.Tiled;

/// <summary>
/// A single tile layer of a <see cref="TileMap"/>. Tile IDs are stored row-major
/// (index <c>y * Width + x</c>) with all flip bits masked off; the corresponding
/// flip flags are stored in a parallel list.
/// </summary>
public sealed class TileMapLayer
{
    private readonly IReadOnlyList<uint> _tileIds;
    private readonly IReadOnlyList<TileFlags> _tileFlags;

    /// <summary>Gets the name of the layer (as declared in the map).</summary>
    public string Name { get; }

    /// <summary>Gets whether the layer is visible (shown) in the map.</summary>
    public bool Visible { get; }

    /// <summary>Gets the opacity of the layer, from 0 (fully transparent) to 1 (fully opaque).</summary>
    public float Opacity { get; }

    /// <summary>Gets the width of the layer in tiles.</summary>
    public int Width { get; }

    /// <summary>Gets the height of the layer in tiles.</summary>
    public int Height { get; }

    /// <summary>
    /// Gets whether the layer is rendered <em>above</em> the player. A layer declares this by
    /// setting a custom boolean property named <c>above_player</c> to <see langword="true"/>
    /// (Tiled convention). When the property is absent, is not a boolean, or is
    /// <see langword="false"/>, this is <see langword="false"/> and the layer is rendered
    /// below the player.
    /// </summary>
    public bool AbovePlayer { get; }

    /// <summary>
    /// Gets the tile IDs of the layer in row-major order. A value of 0 means the cell is empty;
    /// all other values are global tile IDs with the flip bits masked off.
    /// </summary>
    public IReadOnlyList<uint> TileIds => _tileIds;

    /// <summary>
    /// Gets the flip flags for each tile, in the same order as <see cref="TileIds"/>.
    /// </summary>
    internal IReadOnlyList<TileFlags> Flags => _tileFlags;

    internal TileMapLayer(
        string name,
        bool visible,
        float opacity,
        IReadOnlyList<uint> tileIds,
        IReadOnlyList<TileFlags> tileFlags,
        int width,
        int height,
        bool abovePlayer)
    {
        Name = name;
        Visible = visible;
        Opacity = opacity;
        _tileIds = tileIds;
        _tileFlags = tileFlags;
        Width = width;
        Height = height;
        AbovePlayer = abovePlayer;
    }

    /// <summary>
    /// Returns the global tile ID at (<paramref name="x"/>, <paramref name="y"/>) with flip bits
    /// masked off, or 0 when the cell is empty.
    /// </summary>
    public uint GetTileId(int x, int y) => TileIds[Index(x, y)];

    /// <summary>Returns the flip flags of the tile at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public TileFlags GetTileFlags(int x, int y) => Flags[Index(x, y)];

    /// <summary>
    /// Builds a <see cref="TileMapLayer"/> from a DotTiled <see cref="TileLayer"/>. The raw GIDs
    /// (which may carry flip bits) are split into masked tile IDs and <see cref="TileFlags"/>,
    /// and the layer's custom properties are consulted for the <c>above_player</c> flag.
    /// </summary>
    internal static TileMapLayer FromDotTiled(TileLayer layer)
    {
        var width = layer.Width;
        var height = layer.Height;
        var count = width * height;

        uint[] ids;
        TileFlags[] flags;

        if (layer.Data.HasValue && layer.Data.Value.GlobalTileIDs.HasValue)
        {
            var rawIds = layer.Data.Value.GlobalTileIDs.Value;
            var rawFlags = layer.Data.Value.FlippingFlags.HasValue
                ? layer.Data.Value.FlippingFlags.Value
                : Array.Empty<DotTiled.FlippingFlags>();

            if (rawIds.Length != count)
            {
                throw new InvalidOperationException(
                    $"Tile layer '{layer.Name}' declares {count} cells ({width}\u00d7{height}) but its data contains {rawIds.Length} values.");
            }

            ids = new uint[count];
            flags = new TileFlags[count];
            for (var i = 0; i < count; i++)
            {
                // Mask off every flag bit so the stored ID is always a plain GID.
                ids[i] = rawIds[i] & (uint)TileFlags.Mask;
                flags[i] = i < rawFlags.Length ? (TileFlags)rawFlags[i] : TileFlags.None;
            }
        }
        else
        {
            ids = new uint[count];
            flags = new TileFlags[count];
        }

        var abovePlayer = layer.Properties.Any(p =>
            p.Name == "above_player" && p is BoolProperty boolProperty && boolProperty.Value);

        return new TileMapLayer(layer.Name, layer.Visible, layer.Opacity, ids, flags, width, height, abovePlayer);
    }

    private int Index(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(
                y < 0 || y >= Height ? nameof(y) : nameof(x),
                $"Coordinates ({x}, {y}) are outside the bounds of layer '{Name}' ({Width}\u00d7{Height}).");
        }

        return (y * Width) + x;
    }
}
