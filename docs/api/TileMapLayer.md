# TileMapLayer

Namespace: `RPGEngine.Tiled` — a single tile layer of a `TileMap`.

Tile IDs are stored row-major (index `y * Width + x`) with all flip bits masked off; the
corresponding flip flags are stored in a parallel list.

## Properties

| Property | Description |
| --- | --- |
| `Name` | Gets the name of the layer (as declared in the map). |
| `Visible` | Gets whether the layer is visible (shown) in the map. |
| `Opacity` | Gets the opacity of the layer, from 0 (fully transparent) to 1 (fully opaque). |
| `Width` / `Height` | Get the size of the layer in tiles. |
| `TileIds` | Gets the tile IDs in row-major order (0 = empty cell). |

## Methods

### `uint GetTileId(int x, int y)`

Returns the global tile ID at `(x, y)` with flip bits masked off, or 0 when the cell is empty.
Throws `ArgumentOutOfRangeException` for out-of-bounds coordinates.

```csharp
uint gid = ground.GetTileId(0, 0);
```

### `TileFlags GetTileFlags(int x, int y)`

Returns the flip flags of the tile at `(x, y)`.

```csharp
var flags = ground.GetTileFlags(0, 0);
```

## Example

```csharp
var map = TileMap.Load("assets/map.tmx");
var ground = map.Layers[0];

Console.WriteLine(ground.Name);           // "ground"
Console.WriteLine(ground.Visible);        // True
Console.WriteLine(ground.Opacity);        // 1
Console.WriteLine(ground.Width);          // 16
Console.WriteLine(ground.Height);         // 12
Console.WriteLine(ground.TileIds.Count);  // 192
```
