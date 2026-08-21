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
| `AbovePlayer` | Gets whether the layer is rendered **above** the player (declared by the Tiled `above_player` boolean custom property set to `true`). |
| `IsCollision` | Gets whether the layer is a **collision** layer (declared by the Tiled `is_collision` boolean custom property set to `true`); its non-empty tiles are solid and block movement. |
| `Properties` | Gets the layer's custom properties, in file order (includes `above_player`/`is_collision` when declared). |
| `Width` / `Height` | Get the size of the layer in tiles. |
| `TileIds` | Gets the tile IDs in row-major order (0 = empty cell). |

`AbovePlayer` is `true` when the layer declares a custom boolean property named `above_player`
with value `true` (Tiled convention, case-sensitive). When the property is absent, is not a
boolean, or is `false`, it is `false` and the layer is rendered below the player.

`IsCollision` is `true` when the layer declares a custom boolean property named `is_collision`
with value `true` (Tiled convention, case-sensitive, mirroring `above_player`). When the
property is absent, is not a boolean, or is `false`, it is `false` and the layer never blocks
movement. The engine treats every non-empty tile (GID != 0) of a collision layer as solid — see
`TileMap.IsSolid` and `docs/Architecture.md` for how characters collide with them.

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
var ground = map.Layers.Single(layer => layer.Name == "ground");

Console.WriteLine(ground.Name);           // "ground"
Console.WriteLine(ground.Visible);        // True
Console.WriteLine(ground.Opacity);        // 1
Console.WriteLine(ground.AbovePlayer);    // False
Console.WriteLine(ground.Width);          // 16
Console.WriteLine(ground.Height);         // 12
Console.WriteLine(ground.TileIds.Count);  // 192
Console.WriteLine(ground.Properties.Count); // custom properties declared on the layer

// An above_player layer is rendered after the player (e.g. tree canopies).
var treesAbove = map.Layers.Single(layer => layer.Name == "trees_above");
Console.WriteLine(treesAbove.AbovePlayer); // True
Console.WriteLine(treesAbove.Properties.Single(p => p.Name == "above_player").Value); // True

// A collision layer (is_collision = true) contains solid tiles that block characters.
// The committed fixture map has no collision layer; this snippet assumes the loaded map
// declares one named "walls" (e.g. your own map with a walls layer).
var walls = map.Layers.Single(layer => layer.Name == "walls");
Console.WriteLine(walls.IsCollision);      // True
Console.WriteLine(walls.Properties.Single(p => p.Name == "is_collision").Value); // True
Console.WriteLine(map.IsSolid(3, 4));      // e.g. True where the walls layer has a tile
```
