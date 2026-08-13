# TileFlags

Namespace: `RPGEngine.Tiled` — flip flags that Tiled stores in the high bits of a tile's global
ID (GID). `[Flags]` enum.

## Remarks

Tiled encodes the three orthogonal flip flags in the four most significant bits of a 32-bit GID.
Whenever a GID is read from layer data the flip bits are masked off so the remaining value is the
plain GID.

## Values

| Value | Bit | Meaning |
| --- | --- | --- |
| `None` | 0 | No flip flags are set. |
| `FlippedHorizontally` | `0x80000000` | The tile is flipped horizontally (left ↔ right). |
| `FlippedVertically` | `0x40000000` | The tile is flipped vertically (top ↔ bottom). |
| `FlippedDiagonally` | `0x20000000` | The tile is flipped (anti-)diagonally (swaps X and Y axes, enabling 90° rotations). |
| `Mask` | `0x0FFFFFFF` | Bit mask that clears every flag bit, leaving only the plain global tile ID. |

## Example

```csharp
var map = TileMap.Load("assets/map.tmx");
var ground = map.Layers[0];

var gid = ground.GetTileId(0, 0);
Console.WriteLine((gid & (uint)TileFlags.Mask) == gid); // True — IDs have flip bits masked off
```
