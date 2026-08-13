using System.Globalization;
using System.Text;
using SkiaSharp;

namespace RPGEngine.Tests.Tiled;

/// <summary>Specifies a tile layer to be written into a generated TMX fixture.</summary>
/// <param name="Name">The layer name.</param>
/// <param name="Gids">The raw tile GIDs in row-major order (flip bits allowed).</param>
/// <param name="Visible">Whether the layer is visible.</param>
/// <param name="Opacity">The layer opacity, from 0 to 1.</param>
internal sealed record TileLayerSpec(string Name, uint[] Gids, bool Visible = true, float Opacity = 1f);

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

    public TiledTestFixture(int width, int height, IReadOnlyList<TileLayerSpec> layers, TilePattern pattern = TilePattern.Solid)
    {
        Width = width;
        Height = height;
        _root = Path.Combine(Path.GetTempPath(), "rpg-engine-tiled-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        ImagePath = Path.Combine(_root, "tiles.png");
        TilesetPath = Path.Combine(_root, "tiles.tsx");
        MapPath = Path.Combine(_root, "map.tmx");

        CreateTileImage(ImagePath, pattern);
        File.WriteAllText(TilesetPath, TilesetXml);
        File.WriteAllText(MapPath, MapXml(layers));
    }

    /// <summary>Creates the standard 2×2 fixture used by most tests.</summary>
    public static TiledTestFixture Create2x2(IReadOnlyList<TileLayerSpec> layers, TilePattern pattern = TilePattern.Solid)
        => new(2, 2, layers, pattern);

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

    private static void CreateTileImage(string path, TilePattern pattern)
    {
        using var bitmap = new SKBitmap(TileSize, TileSize);
        using (var canvas = new SKCanvas(bitmap))
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

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private string TilesetXml => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.10.2" name="test_tiles" tilewidth="{TileSize}" tileheight="{TileSize}" tilecount="1" columns="1">
          <image source="tiles.png" width="{TileSize}" height="{TileSize}"/>
        </tileset>
        """;

    private string MapXml(IReadOnlyList<TileLayerSpec> layers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(
            $"""<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down" width="{Width}" height="{Height}" tilewidth="{TileSize}" tileheight="{TileSize}" infinite="0" nextlayerid="{layers.Count + 1}" nextobjectid="1">""");
        sb.AppendLine("  <tileset firstgid=\"1\" source=\"tiles.tsx\"/>");

        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            sb.AppendLine(
                $"""  <layer id="{i + 1}" name="{layer.Name}" width="{Width}" height="{Height}" visible="{(layer.Visible ? 1 : 0)}" opacity="{layer.Opacity.ToString(CultureInfo.InvariantCulture)}">""");
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

        sb.AppendLine("</map>");
        return sb.ToString();
    }
}
