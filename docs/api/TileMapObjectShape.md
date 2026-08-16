# TileMapObjectShape

Namespace: `RPGEngine.Tiled` — the geometric shape of an object in an object layer. Detected
from the object's shape in the Tiled file.

## Values

| Value | Tiled declaration |
| --- | --- |
| `Rectangle` | A plain `<object>` (no marker; the Tiled default shape). |
| `Ellipse` | An `<ellipse/>` child element. |
| `Point` | A `<point/>` child element. |
| `Polygon` | A `<polygon>` child element. |
| `Polyline` | A `<polyline>` child element. |
| `Tile` | A `gid` attribute (a tile object). |
| `Text` | A `<text>` child element. |

## Example

```csharp
var map = TileMap.Load("assets/map.tmx");
foreach (var layer in map.ObjectLayers)
{
    foreach (var obj in layer.Objects)
    {
        Console.WriteLine($"{obj.Name}: {obj.Shape}");
    }
}
```
