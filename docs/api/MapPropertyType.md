# MapPropertyType

Namespace: `RPGEngine.Tiled` — the type of a custom property attached to a map, a tile layer,
an object layer or an object. Mirrors the property types Tiled can store in a
`<property>` element.

## Values

| Value | Boxed `MapProperty.Value` |
| --- | --- |
| `Bool` | `bool` |
| `Int` | `int` |
| `Float` | `float` |
| `String` | `string` |
| `Color` | `SKColor` (Tiled stores colors as `#AARRGGBB`) |
| `File` | `string` (the file path, as declared in the map) |
| `Object` | `string` (the referenced object's ID, the raw string form) |
| `Class` | `null` (structured access to custom-class members is out of scope) |
| `Unknown` | `null` (an unrecognised property type) |

## Example

```csharp
var map = TileMap.Load("assets/map.tmx");
var tint = map.GetProperty("tint");

if (tint?.Type == MapPropertyType.Color)
{
    var color = (SKColor)tint.Value!;
    Console.WriteLine(color.Red); // 255
}
```
