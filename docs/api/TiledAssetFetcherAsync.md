# TiledAssetFetcherAsync

Namespace: `RPGEngine.Tiled` — a delegate that **asynchronously** fetches the raw bytes of a
Tiled asset (a map, tileset or image) located at a URI.

```csharp
public delegate Task<byte[]> TiledAssetFetcherAsync(Uri uri);
```

## Remarks

This is the asynchronous counterpart of `TiledAssetFetcher`. The engine uses it in the async
loaders (`TileSet.LoadAsync`, `TileMap.LoadAsync`) whenever an asset is not available on the
local file system and must be fetched with asynchronous I/O (e.g. certain network/browser
streams). Unlike `TiledAssetFetcher`, implementations never block: they return a
`Task<byte[]>` that completes with the asset's bytes.

## Example

```csharp
// e.g. an HttpClient shared by the host application.
using var http = new HttpClient();

TiledAssetFetcherAsync fetcher = uri => http.GetByteArrayAsync(uri);

using var mapStream = File.OpenRead("assets/map.tmx");
var map = await TileMap.LoadAsync(
    mapStream,
    new Uri("https://example.com/assets/map.tmx"),
    fetcher);
```
