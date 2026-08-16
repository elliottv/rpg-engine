# MapProperty

Namespace: `RPGEngine.Tiled` — a single custom property attached to a map, a tile layer, an
object layer or an object, in the Tiled format.

The value is **boxed** according to `Type`, so the read model wraps DotTiled without leaking any
DotTiled types into the public API.

## Properties

| Property | Description |
| --- | --- |
| `Name` | The property name, as declared in the map (case-sensitive). |
| `Type` | The property type (see `MapPropertyType`). |
| `Value` | The boxed value. `bool` / `int` / `float` / `string` / `SKColor` (colors) / `string` (file paths). `Object` exposes the referenced object's ID as a string; `Class` and `Unknown` expose `null`. |

## Example

```csharp
var map = TileMap.Load("assets/map.tmx");
var difficulty = map.GetProperty("difficulty");

Console.WriteLine(difficulty?.Name);   // "difficulty"
Console.WriteLine(difficulty?.Type);   // MapPropertyType.String
Console.WriteLine(difficulty?.Value);  // e.g. "hard"
```
