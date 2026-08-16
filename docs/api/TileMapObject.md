# TileMapObject

Namespace: `RPGEngine.Tiled` — a single object in an object layer of a `TileMap`, wrapping the
Tiled object's identity, geometry, shape and custom properties. This is a read-only view;
editing or adding objects is out of scope.

## Properties

| Property | Description |
| --- | --- |
| `Id` | The object's unique ID within the map. |
| `Name` | The object's name, as declared in the map (may be empty). |
| `Type` | The object's "class" string, as declared in the map (may be empty). |
| `Position` | The object's top-left position in pixels. |
| `Width` / `Height` | The object's size in pixels (0 for shapes without a size, e.g. points). |
| `Shape` | The object's geometric shape (see `TileMapObjectShape`). |
| `Properties` | The object's custom properties, in file order. |

## Example

```csharp
var map = TileMap.Load("assets/map.tmx");
var chest = map.ObjectLayers
    .SelectMany(layer => layer.Objects)
    .Single(obj => obj.Name == "chest");

Console.WriteLine(chest.Type);                                  // "treasure"
Console.WriteLine(chest.Position);                              // e.g. (48, 96)
Console.WriteLine(chest.Shape);                                 // TileMapObjectShape.Rectangle
Console.WriteLine(chest.Properties.Single(p => p.Name == "coins").Value); // 100
```
