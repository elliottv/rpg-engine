# TileSet

Namespace: `RPGEngine.Tiled` — a single Tiled tileset: a grid of tiles cut from one image,
together with the global tile ID (`FirstGid`) at which the tileset starts within a map.

## Remarks

- Standalone tilesets are created through the static factories `Load(string)` (local file
  system) and `Load(Stream, Uri, TiledAssetFetcher)` (streams, e.g. fetched from a URL in
  WebAssembly). The tilesets referenced by a map are created internally by `TileMap.Load`.
- The backing image is decoded exactly once; `GetTileImage` returns a cropped raster copy of a
  single tile that the caller owns and may dispose freely.

## Properties

| Property | Description |
| --- | --- |
| `Name` | Gets the name of the tileset. |
| `FirstGid` | Gets the global tile ID of the first tile in this tileset (0 for standalone tilesets). |
| `TileWidth` / `TileHeight` | Get the size of a single tile in pixels. |

## Methods

### `static TileSet Load(string path)`

Loads the Tiled tileset (`.tsx`) at `path` and decodes its image. The image `source` declared by
the tileset is resolved relative to the directory containing the `.tsx` file.

```csharp
var tileset = TileSet.Load("assets/tiles.tsx");
Console.WriteLine(tileset.Name); // "rpg_fixture_tiles"
```

### `static TileSet Load(Stream stream, Uri baseUri, TiledAssetFetcher fetcher)`

Loads a Tiled tileset (`.tsx`) from a stream and decodes its image. The image `source` is
resolved relative to `baseUri` and fetched through `fetcher` — the file-system-free entry point
used in WebAssembly.

```csharp
using var stream = File.OpenRead("assets/tiles.tsx");
var tileset = TileSet.Load(
    stream,
    new Uri("https://example.com/assets/tiles.tsx"),
    uri => httpClient.GetByteArrayAsync(uri).GetAwaiter().GetResult());
```

### `static Task<TileSet> LoadAsync(Stream stream, Uri baseUri, TiledAssetFetcherAsync fetcher)`

The asynchronous counterpart of `Load(Stream, Uri, TiledAssetFetcher)` for streams and asset
fetchers that only support asynchronous I/O (e.g. certain network/browser streams). The TSX
content is read with `StreamReader.ReadToEndAsync()` and the image is fetched with
`await fetcher(...)`, so no synchronous read is performed on the caller's stream. The caller
remains the owner of the stream.

```csharp
// e.g. an HttpClient shared by the host application.
using var http = new HttpClient();

using var stream = File.OpenRead("assets/tiles.tsx");
var tileset = await TileSet.LoadAsync(
    stream,
    new Uri("https://example.com/assets/tiles.tsx"),
    uri => http.GetByteArrayAsync(uri));
```

### `SKImage GetTileImage(int localTileId)`

Returns the image of the tile with the given 0-based `localTileId`, cropped from the tileset
image. Throws `ArgumentOutOfRangeException` for a negative or out-of-range id. The returned
image owns a copy of its pixels and is disposed by the caller.

```csharp
using var tile = tileset.GetTileImage(localTileId: 3);
canvas.DrawImage(tile, new SKPoint(0, 0));
```

## Example

```csharp
var tileset = TileSet.Load("assets/tiles.tsx");
Console.WriteLine(tileset.TileWidth); // 48

using var tile = tileset.GetTileImage(localTileId: 0);
Console.WriteLine($"{tile.Width}×{tile.Height}"); // 48×48
```
