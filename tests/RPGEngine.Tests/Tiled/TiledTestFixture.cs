using System.Globalization;
using System.Text;
using SkiaSharp;

namespace RPGEngine.Tests.Tiled;

/// <summary>A single custom property to be written into a generated <c>&lt;properties&gt;</c> block.</summary>
/// <param name="Name">The property name (e.g. <c>above_player</c>).</param>
/// <param name="Type">The Tiled property type (e.g. <c>bool</c>, <c>string</c>, <c>int</c>).</param>
/// <param name="Value">The property value as a string (e.g. <c>true</c>).</param>
internal sealed record FixtureProperty(string Name, string Type, string Value);

/// <summary>Specifies a tile layer to be written into a generated TMX fixture.</summary>
/// <param name="Name">The layer name.</param>
/// <param name="Gids">The raw tile GIDs in row-major order (flip bits allowed).</param>
/// <param name="Visible">Whether the layer is visible.</param>
/// <param name="Opacity">The layer opacity, from 0 to 1.</param>
/// <param name="Properties">Optional custom properties emitted inside the layer.</param>
internal sealed record TileLayerSpec(
    string Name,
    uint[] Gids,
    bool Visible = true,
    float Opacity = 1f,
    IReadOnlyList<FixtureProperty>? Properties = null);

/// <summary>The geometric shape of a generated object; controls which marker element is emitted.</summary>
internal enum FixtureObjectShape
{
    /// <summary>A plain <c>&lt;object&gt;</c> (no marker; the Tiled default shape).</summary>
    Rectangle,

    /// <summary>An <c>&lt;ellipse/&gt;</c> marker.</summary>
    Ellipse,

    /// <summary>A <c>&lt;point/&gt;</c> marker.</summary>
    Point,

    /// <summary>A <c>&lt;polygon&gt;</c> marker.</summary>
    Polygon,

    /// <summary>A <c>&lt;polyline&gt;</c> marker.</summary>
    Polyline,

    /// <summary>A <c>gid</c> attribute (a tile object).</summary>
    Tile,

    /// <summary>A <c>&lt;text&gt;</c> marker.</summary>
    Text,
}

/// <summary>Specifies a single object to be written into a generated object layer.</summary>
/// <param name="Id">The object ID (must be unique within the map).</param>
/// <param name="Name">The object name.</param>
/// <param name="Type">The object "class"/type string.</param>
/// <param name="X">The object's X position in pixels.</param>
/// <param name="Y">The object's Y position in pixels.</param>
/// <param name="Width">The object's width in pixels.</param>
/// <param name="Height">The object's height in pixels.</param>
/// <param name="Shape">The object's geometric shape.</param>
/// <param name="Properties">Optional custom properties emitted inside the object.</param>
internal sealed record ObjectSpec(
    uint Id,
    string Name,
    string Type,
    float X,
    float Y,
    float Width,
    float Height,
    FixtureObjectShape Shape,
    IReadOnlyList<FixtureProperty>? Properties = null);

/// <summary>Specifies an object layer to be written into a generated TMX fixture.</summary>
/// <param name="Name">The layer name.</param>
/// <param name="Objects">The objects of the layer, in file order.</param>
/// <param name="Visible">Whether the layer is visible.</param>
/// <param name="Opacity">The layer opacity, from 0 to 1.</param>
/// <param name="Properties">Optional custom properties emitted inside the layer.</param>
internal sealed record ObjectLayerSpec(
    string Name,
    IReadOnlyList<ObjectSpec> Objects,
    bool Visible = true,
    float Opacity = 1f,
    IReadOnlyList<FixtureProperty>? Properties = null);

/// <summary>The visual pattern painted into the generated 48×48 tile PNG.</summary>
internal enum TilePattern
{
    /// <summary>A fully opaque red tile (symmetric, so flips are invisible).</summary>
    Solid,

    /// <summary>
    /// An asymmetric tile: a red 4×4 marker in the top-right area. Used to verify that flip
    /// transforms are actually applied, since the marker is not symmetric under any flip.
    /// </summary>
    Marker,
}

/// <summary>
/// Writes a self-contained Tiled map fixture to a temporary directory: a generated 48×48 PNG,
/// an external single-tile TSX referencing it, and a TMX referencing the TSX. Everything is
/// cleaned up on <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// <para>
/// By default the fixture generates a single 48×48 tile image (see <see cref="TilePattern"/>).
/// Passing a list of tile colors to the constructor generates a tileset with one tile per color
/// (the image is laid out in a single row, each tile solid in its color), which lets tests
/// distinguish two tile GIDs by color (e.g. a below-player layer in red and an
/// <c>above_player</c> layer in green).
/// </para>
/// <para>
/// Each tile layer may declare custom properties via <see cref="TileLayerSpec.Properties"/>, the
/// map may declare its own via the <c>mapProperties</c> constructor argument, and object layers
/// (with per-object custom properties) can be added via the <c>objectLayers</c> argument; these
/// are emitted as <c>&lt;properties&gt;</c> blocks and <c>&lt;objectgroup&gt;</c> elements
/// matching the Tiled format.
/// </para>
/// </remarks>
internal sealed class TiledTestFixture : IDisposable
{
    public const int TileSize = 48;

    private readonly string _root;

    /// <summary>Gets the width of the generated map in tiles.</summary>
    public int Width { get; }

    /// <summary>Gets the height of the generated map in tiles.</summary>
    public int Height { get; }

    /// <summary>Gets the path to the generated TMX map file.</summary>
    public string MapPath { get; }

    /// <summary>Gets the path to the generated TSX tileset file.</summary>
    public string TilesetPath { get; }

    /// <summary>Gets the path to the generated tileset PNG image.</summary>
    public string ImagePath { get; }

    public TiledTestFixture(
        int width,
        int height,
        IReadOnlyList<TileLayerSpec> layers,
        TilePattern pattern = TilePattern.Solid,
        IReadOnlyList<SKColor>? tileColors = null,
        IReadOnlyList<FixtureProperty>? mapProperties = null,
        IReadOnlyList<ObjectLayerSpec>? objectLayers = null,
        IReadOnlyDictionary<uint, IReadOnlyList<(uint FrameTileId, int DurationMs)>>? animations = null,
        IReadOnlyList<TilePattern>? tilePatterns = null)
    {
        Width = width;
        Height = height;
        _root = Path.Combine(Path.GetTempPath(), "rpg-engine-tiled-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        ImagePath = Path.Combine(_root, "tiles.png");
        TilesetPath = Path.Combine(_root, "tiles.tsx");
        MapPath = Path.Combine(_root, "map.tmx");

        CreateTileImage(ImagePath, pattern, tileColors, tilePatterns);
        File.WriteAllText(TilesetPath, TilesetXml(tileColors, animations));
        File.WriteAllText(MapPath, MapXml(layers, mapProperties, objectLayers));
    }

    /// <summary>Creates the standard 2×2 fixture used by most tests.</summary>
    public static TiledTestFixture Create2x2(
        IReadOnlyList<TileLayerSpec> layers,
        TilePattern pattern = TilePattern.Solid,
        IReadOnlyList<FixtureProperty>? mapProperties = null,
        IReadOnlyList<ObjectLayerSpec>? objectLayers = null,
        IReadOnlyDictionary<uint, IReadOnlyList<(uint FrameTileId, int DurationMs)>>? animations = null)
        => new(2, 2, layers, pattern, mapProperties: mapProperties, objectLayers: objectLayers, animations: animations);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; a leftover temp directory is harmless.
        }
    }

    private static void CreateTileImage(
        string path,
        TilePattern pattern,
        IReadOnlyList<SKColor>? tileColors,
        IReadOnlyList<TilePattern>? tilePatterns = null)
    {
        if (tileColors is { Count: > 0 })
        {
            // Multi-tile image: one 48×48 tile per color, laid out in a single row. By default
            // each tile is solid in its color; passing tilePatterns draws the marker pattern in
            // the selected tiles instead (the marker is not symmetric under any flip, so flips
            // are pixel-verifiable on animated tiles too).
            using var bitmap = new SKBitmap(TileSize * tileColors.Count, TileSize);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                using var paint = new SKPaint { IsAntialias = false };
                for (var i = 0; i < tileColors.Count; i++)
                {
                    paint.Color = tileColors[i];
                    if (tilePatterns is { Count: > 0 } && i < tilePatterns.Count && tilePatterns[i] == TilePattern.Marker)
                    {
                        // Marker in the top-right area of the tile (mirrors the single-tile
                        // marker at 36..39, 12..15). Flipped positions are the same as the
                        // single-tile marker test: horizontal → 8..11, 12..15.
                        canvas.DrawRect(new SKRect((i * TileSize) + 36, 12, (i * TileSize) + 40, 16), paint);
                    }
                    else
                    {
                        canvas.DrawRect(new SKRect(i * TileSize, 0, (i + 1) * TileSize, TileSize), paint);
                    }
                }
            }

            EncodePng(bitmap, path);
            return;
        }

        using var singleBitmap = new SKBitmap(TileSize, TileSize);
        using (var canvas = new SKCanvas(singleBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Red, IsAntialias = false };
            if (pattern == TilePattern.Solid)
            {
                canvas.DrawRect(new SKRect(0, 0, TileSize, TileSize), paint);
            }
            else
            {
                // Marker in the top-right area of the tile. Its flipped positions are:
                // horizontal → left (8..11, 12..15), vertical → bottom (36..39, 32..35),
                // diagonal → bottom-left (12..15, 36..39).
                canvas.DrawRect(new SKRect(36, 12, 40, 16), paint);
            }
        }

        EncodePng(singleBitmap, path);
    }

    /// <summary>Encodes <paramref name="bitmap"/> as a PNG file at <paramref name="path"/>.</summary>
    private static void EncodePng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private string TilesetXml(
        IReadOnlyList<SKColor>? tileColors,
        IReadOnlyDictionary<uint, IReadOnlyList<(uint FrameTileId, int DurationMs)>>? animations)
    {
        var tileCount = tileColors?.Count ?? 1;
        var columns = tileCount;
        var imageWidth = TileSize * columns;
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(
            $"""<tileset version="1.10" tiledversion="1.10.2" name="test_tiles" tilewidth="{TileSize}" tileheight="{TileSize}" tilecount="{tileCount}" columns="{columns}">""");
        sb.AppendLine($"""  <image source="tiles.png" width="{imageWidth}" height="{TileSize}"/>""");

        if (animations is { Count: > 0 })
        {
            foreach (var (tileId, frames) in animations)
            {
                sb.AppendLine($"""  <tile id="{tileId}">""");
                sb.AppendLine("    <animation>");
                foreach (var (frameTileId, durationMs) in frames)
                {
                    sb.AppendLine($"""      <frame tileid="{frameTileId}" duration="{durationMs}"/>""");
                }

                sb.AppendLine("    </animation>");
                sb.AppendLine("  </tile>");
            }
        }

        sb.AppendLine("</tileset>");
        return sb.ToString();
    }

    private string MapXml(
        IReadOnlyList<TileLayerSpec> layers,
        IReadOnlyList<FixtureProperty>? mapProperties,
        IReadOnlyList<ObjectLayerSpec>? objectLayers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");

        var objectCount = objectLayers?.Sum(layer => layer.Objects.Count) ?? 0;
        var nextObjectId = Math.Max(1, objectCount + 1); // object IDs are 1-based
        var nextLayerId = layers.Count + (objectLayers?.Count ?? 0) + 1;

        sb.AppendLine(
            $"""<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down" width="{Width}" height="{Height}" tilewidth="{TileSize}" tileheight="{TileSize}" infinite="0" nextlayerid="{nextLayerId}" nextobjectid="{nextObjectId}">""");
        sb.AppendLine("  <tileset firstgid=\"1\" source=\"tiles.tsx\"/>");

        AppendProperties(sb, "  ", mapProperties);

        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            sb.AppendLine(
                $"""  <layer id="{i + 1}" name="{layer.Name}" width="{Width}" height="{Height}" visible="{(layer.Visible ? 1 : 0)}" opacity="{layer.Opacity.ToString(CultureInfo.InvariantCulture)}">""");

            AppendProperties(sb, "    ", layer.Properties);

            sb.AppendLine("    <data encoding=\"csv\">");
            for (var y = 0; y < Height; y++)
            {
                var row = string.Join(
                    ",",
                    Enumerable.Range(0, Width).Select(x => layer.Gids[(y * Width) + x].ToString(CultureInfo.InvariantCulture)));
                sb.Append(row);
                sb.AppendLine(y < Height - 1 ? "," : string.Empty);
            }

            sb.AppendLine("    </data>");
            sb.AppendLine("  </layer>");
        }

        if (objectLayers is { Count: > 0 })
        {
            var layerId = layers.Count + 1;
            foreach (var objectLayer in objectLayers)
            {
                sb.AppendLine(
                    $"""  <objectgroup id="{layerId}" name="{objectLayer.Name}" visible="{(objectLayer.Visible ? 1 : 0)}" opacity="{objectLayer.Opacity.ToString(CultureInfo.InvariantCulture)}">""");

                AppendProperties(sb, "    ", objectLayer.Properties);

                foreach (var obj in objectLayer.Objects)
                {
                    AppendObject(sb, obj);
                }

                sb.AppendLine("  </objectgroup>");
                layerId++;
            }
        }

        sb.AppendLine("</map>");
        return sb.ToString();
    }

    /// <summary>Appends a <c>&lt;properties&gt;</c> block for <paramref name="properties"/> (nothing when empty).</summary>
    private static void AppendProperties(StringBuilder sb, string indent, IReadOnlyList<FixtureProperty>? properties)
    {
        if (properties is { Count: > 0 })
        {
            sb.AppendLine(indent + "<properties>");
            foreach (var property in properties)
            {
                sb.AppendLine(
                    indent + $"  <property name=\"{property.Name}\" type=\"{property.Type}\" value=\"{property.Value}\"/>");
            }

            sb.AppendLine(indent + "</properties>");
        }
    }

    /// <summary>Appends a single <c>&lt;object&gt;</c> element (with its properties and shape marker).</summary>
    private static void AppendObject(StringBuilder sb, ObjectSpec obj)
    {
        // A tile object is declared with a gid attribute (instead of a marker element).
        var gidAttribute = obj.Shape == FixtureObjectShape.Tile ? " gid=\"1\"" : string.Empty;
        sb.AppendLine(
            $"    <object id=\"{obj.Id}\" name=\"{obj.Name}\" type=\"{obj.Type}\" x=\"{obj.X.ToString(CultureInfo.InvariantCulture)}\" y=\"{obj.Y.ToString(CultureInfo.InvariantCulture)}\" width=\"{obj.Width.ToString(CultureInfo.InvariantCulture)}\" height=\"{obj.Height.ToString(CultureInfo.InvariantCulture)}\"{gidAttribute}>");

        AppendProperties(sb, "      ", obj.Properties);

        switch (obj.Shape)
        {
            case FixtureObjectShape.Ellipse:
                sb.AppendLine("      <ellipse/>");
                break;
            case FixtureObjectShape.Point:
                sb.AppendLine("      <point/>");
                break;
            case FixtureObjectShape.Polygon:
                sb.AppendLine("      <polygon points=\"0,0 16,0 16,16 0,16\"/>");
                break;
            case FixtureObjectShape.Polyline:
                sb.AppendLine("      <polyline points=\"0,0 16,16\"/>");
                break;
            case FixtureObjectShape.Text:
                sb.AppendLine("      <text>Object text</text>");
                break;
            default:
                // A plain <object> is a rectangle; a tile object carries only the gid attribute.
                break;
        }

        sb.AppendLine("    </object>");
    }
}
