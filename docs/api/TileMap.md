# TileMap

Namespace: `RPGEngine.Tiled` — a tile map loaded from a Tiled `.tmx` file (and its referenced
`.tsx` tilesets).

## Remarks

- A map **owns** the `TileSet`s that its layers reference; they are created during `Load` and
  are not registered anywhere globally. Loading a map never touches the engine's tilesets.
- Tile coordinates are 0-based. Layer data is stored row-major. Only the orthogonal map layout
  is supported by this epic.
- **Layers are prerendered on load.** Every visible, non-empty tile layer is rasterized once
  into its own `SKImage` of the map's pixel size (`PixelWidth × PixelHeight`) when the map is
  loaded: the tiles are drawn at their world pixel positions with the flip transforms applied,
  and the layer's `Opacity` is baked into the layer alpha. Invisible layers and empty layers
  (no non-zero GID) are not prerendered. `Draw` and `DrawAbovePlayer` then only **blit** the
  cached layer images that intersect the viewport, so a frame never re-draws tiles.
- A `TileMap` is **`IDisposable`**: disposing it releases the prerendered layer images.
  Replacing `GameEngine.Map` disposes the previous map, and disposing the `GameEngine` disposes
  the current map; hosts that load maps directly are responsible for disposing them when they
  are replaced or no longer needed.
- Rendering happens in **two passes**: the layers **below** the player (every layer whose
  `TileMapLayer.AbovePlayer` is `false`) are drawn first, and the layers **above** the player
  (those declaring the Tiled `above_player` custom property set to `true`) are drawn afterwards,
  so those tiles appear in front of the player.
- **Collision**: a layer declaring the Tiled `is_collision` boolean custom property set to
  `true` is a collision layer; its non-empty tiles are solid and block character movement.
  `IsSolid` reports solid tiles and treats the map edge as solid (see the `IsSolid` method
  below).

## Properties

| Property | Description |
| --- | --- |
| `Width` / `Height` | Get the size of the map in tiles. |
| `TileWidth` / `TileHeight` | Get the size of a single tile in pixels. |
| `PixelWidth` / `PixelHeight` | Get the total size of the map in pixels. |
| `Layers` | Gets the tile layers in file order (bottom → top). |
| `Properties` | Gets the map's custom properties, in file order. |
| `ObjectLayers` | Gets the object layers (and their objects), in file order. |

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

### `static Task<TileMap> LoadAsync(Stream stream, Uri baseUri, TiledAssetFetcherAsync fetcher)`

The asynchronous counterpart of `Load(Stream, Uri, TiledAssetFetcher)` for streams and asset
fetchers that only support asynchronous I/O (e.g. certain network/browser streams). No
synchronous read is performed on the caller's stream: the TMX content is read with
`StreamReader.ReadToEndAsync()` and every external asset is fetched with `await fetcher(...)`.

DotTiled 1.0.0 only exposes synchronous external-tileset resolvers, so the async path
pre-fetches the external asset graph (the map's external `.tsx` tilesets and the images they
reference, plus any embedded `<tileset><image>` images) asynchronously into an in-memory cache
and then reuses the existing synchronous parsing/decoding against that cache, so no blocking
I/O remains. This is a pragmatic bridge until DotTiled exposes asynchronous resolvers; the
TMX/TSX format remains the source of truth.

```csharp
// e.g. an HttpClient shared by the host application.
using var http = new HttpClient();

using var stream = File.OpenRead("assets/map.tmx");
var map = await TileMap.LoadAsync(
    stream,
    new Uri("https://example.com/assets/map.tmx"),
    uri => http.GetByteArrayAsync(uri));
```

### `MapProperty? GetProperty(string name)`

Returns the map property named `name` using a **case-sensitive** comparison, or `null` when no
property with that exact name exists. The value is boxed according to `MapPropertyType` (e.g. a
Tiled `bool` property is a C# `bool`, a `color` property is an `SKColor`).

```csharp
var difficulty = map.GetProperty("difficulty");
if (difficulty is not null)
{
    Console.WriteLine($"{difficulty.Name} = {difficulty.Value}");
}
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

Returns whether the tile at `(tileX, tileY)` blocks movement. A tile is solid when **any**
collision layer (`TileMapLayer.IsCollision`) has a non-empty tile (GID != 0) at that cell.
Coordinates **outside the map are always solid**: the map edge blocks characters, so they cannot
leave the map through it. When the map has no collision layer, every in-bounds cell is walkable.

```csharp
Console.WriteLine(map.IsSolid(1, 1)); // False (no collision layer in this map)
```

Collision layers are declared with the Tiled `is_collision` boolean custom property set to
`true` (see `TileMapLayer.IsCollision`). The engine uses `IsSolid` to block the player against
solid tiles with axis-separated movement (see `docs/Architecture.md`); the same public API is
available for future NPC logic.

### `void Dispose()`

Releases the prerendered layer images owned by the map. This method is **idempotent** (calling
it more than once is a no-op). After disposal, `Draw` and `DrawAbovePlayer` throw
`ObjectDisposedException`. The engine disposes the previous map when `GameEngine.Map` is
replaced and when the engine itself is disposed, so hosts usually do not call `Dispose`
directly.

```csharp
// The engine owns the map: replacing it (or disposing the engine) disposes it automatically.
var engine = new GameEngine { Map = TileMap.Load("assets/map.tmx") };
engine.Map = TileMap.Load("assets/other.tmx"); // the first map is disposed here
engine.Dispose();                              // the current map is disposed here
```

## Example

```csharp
var map = TileMap.Load("assets/map.tmx");

Console.WriteLine(map.Width);              // 16
Console.WriteLine(map.Height);             // 12
Console.WriteLine(map.Layers.Count);       // 3 ("ground" + "decor" + "trees_above")
Console.WriteLine(map.Layers[0].Name);     // "ground"
Console.WriteLine(map.GetTileId("ground", 0, 0)); // >= 1 (a grass tile)
Console.WriteLine(map.IsSolid(1, 1));      // False (no collision layer here)

// Map custom properties (looked up case-sensitively; null when absent).
var difficulty = map.GetProperty("difficulty");
Console.WriteLine(difficulty?.Value);      // e.g. "hard"

// Object layers expose the map's objects and their custom properties.
foreach (var layer in map.ObjectLayers)
{
    Console.WriteLine(layer.Name);         // e.g. "objects"
    foreach (var obj in layer.Objects)
    {
        Console.WriteLine($"{obj.Name} @ {obj.Position} ({obj.Shape})");
        Console.WriteLine(obj.Properties.Single(p => p.Name == "coins").Value);
    }
}
```
