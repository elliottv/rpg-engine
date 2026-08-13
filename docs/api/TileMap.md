# TileMap

Namespace: `RPGEngine.Tiled` — a tile map loaded from a Tiled `.tmx` file (and its referenced
`.tsx` tilesets).

## Remarks

- A map **owns** the `TileSet`s that its layers reference; they are created during `Load` and
  are not registered anywhere globally. Loading a map never touches the engine's tilesets.
- Tile coordinates are 0-based. Layer data is stored row-major. Only the orthogonal map layout
  is supported by this epic.

## Properties

| Property | Description |
| --- | --- |
| `Width` / `Height` | Get the size of the map in tiles. |
| `TileWidth` / `TileHeight` | Get the size of a single tile in pixels. |
| `PixelWidth` / `PixelHeight` | Get the total size of the map in pixels. |
| `Layers` | Gets the tile layers in file order (bottom → top). |

## Methods

### `static TileMap Load(string path)`

Loads the Tiled map at `path`, parsing the referenced external tilesets and decoding their
images from the local file system.

```csharp
var map = TileMap.Load("assets/map.tmx");
Console.WriteLine($"{map.Width}×{map.Height}"); // 16×12
```

### `static TileMap Load(Stream stream, Uri baseUri, TiledAssetFetcher fetcher)`

Loads the Tiled map content from a stream, resolving external `.tsx` tilesets and their images
against `baseUri` and fetching them through `fetcher`. This is the file-system-free entry point
used in WebAssembly.

```csharp
using var stream = File.OpenRead("assets/map.tmx");
var map = TileMap.Load(
    stream,
    new Uri("https://example.com/assets/map.tmx"),
    uri => httpClient.GetByteArrayAsync(uri).GetAwaiter().GetResult());
```

### `uint GetTileId(string layerName, int x, int y)`

Returns the global tile ID of the tile at `(x, y)` in the layer named `layerName`, with all flip
bits masked off. Returns 0 for an empty cell. Throws `KeyNotFoundException` for an unknown layer
name and `ArgumentOutOfRangeException` for out-of-bounds coordinates.

```csharp
uint gid = map.GetTileId("ground", 0, 0);
```

### `TileFlags GetTileFlags(string layerName, int x, int y)`

Returns the `TileFlags` of the tile at `(x, y)` in the named layer.

```csharp
var flags = map.GetTileFlags("ground", 0, 0);
```

### `bool IsSolid(int tileX, int tileY)`

Returns whether the tile at `(tileX, tileY)` blocks movement. The current contract always
returns `false`; collision data will be added by a later story.

```csharp
Console.WriteLine(map.IsSolid(1, 1)); // False
```

## Example

```csharp
var map = TileMap.Load("assets/map.tmx");

Console.WriteLine(map.Width);              // 16
Console.WriteLine(map.Height);             // 12
Console.WriteLine(map.Layers.Count);       // 2 ("ground" + "decor")
Console.WriteLine(map.Layers[0].Name);     // "ground"
Console.WriteLine(map.GetTileId("ground", 0, 0)); // >= 1 (a grass tile)
Console.WriteLine(map.IsSolid(1, 1));      // False
```
