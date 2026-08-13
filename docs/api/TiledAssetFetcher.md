# TiledAssetFetcher

Namespace: `RPGEngine.Tiled` — a delegate that fetches the raw bytes of a Tiled asset (a map,
tileset or image) located at a URI.

```csharp
public delegate byte[] TiledAssetFetcher(Uri uri);
```

## Remarks

The engine uses this delegate whenever it needs an asset that is not available on the local file
system. This is what makes the engine usable in environments without a file system, such as a
WebAssembly build running in a browser, where assets are fetched over HTTP. Implementations are
expected to fetch the URI and return its content.

## Example

```csharp
TiledAssetFetcher fetcher = uri =>
{
    var name = Path.GetFileName(uri.AbsolutePath);
    return name switch
    {
        "tiles.tsx" => System.Text.Encoding.UTF8.GetBytes(File.ReadAllText("assets/tiles.tsx")),
        "tiles.png" => File.ReadAllBytes("assets/tiles.png"),
        _ => throw new FileNotFoundException($"Unknown asset '{uri}'."),
    };
};

using var mapStream = File.OpenRead("assets/map.tmx");
var map = TileMap.Load(
    mapStream,
    new Uri("https://example.com/assets/map.tmx"),
    fetcher);
```
