# TileMapObjectLayer

Namespace: `RPGEngine.Tiled` — an object layer of a `TileMap`: a named collection of
`TileMapObject`s with their own visibility, opacity and custom properties. Object layers do not
render tiles; rendering objects on the map is out of scope.

## Properties

| Property | Description |
| --- | --- |
| `Name` | The name of the object layer (as declared in the map). |
| `Visible` | Whether the object layer is visible (shown) in the map. |
| `Opacity` | The opacity of the object layer, from 0 (fully transparent) to 1 (fully opaque). |
| `Objects` | The objects of the layer, in file order. |
| `Properties` | The object layer's custom properties, in file order. |

## Example

```csharp
var map = TileMap.Load("assets/map.tmx");
var layer = map.ObjectLayers.Single();

Console.WriteLine(layer.Name);            // "objects"
Console.WriteLine(layer.Visible);         // True
Console.WriteLine(layer.Opacity);         // 1
Console.WriteLine(layer.Objects.Count);   // number of objects in the layer
```
