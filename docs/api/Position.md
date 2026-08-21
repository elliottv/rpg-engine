# Position

Namespace: `RPGEngine` — a position in the game world with double-precision coordinates
measured in **tiles**.

Y grows downward, so increasing `Y` moves the position down the map. Positions are stored in
tile units; pixels are produced only at the canvas boundary (rendering and the `ToPixels`
conversion).

```csharp
var position = new Position(8.5, 8.5); // eight and a half tiles to the right and down
```

## Members

### `double X` / `double Y`

The horizontal (x) and vertical (y) coordinates, in tiles.

### `static Position operator +(Position position, Vector2 offset)`

Returns a new position offset from this one by `offset` (in tiles).

```csharp
var position = new Position(10, 20);
var moved = position + new Vector2(3, -4); // (13, 16)
```

### `static Position operator -(Position position, Vector2 offset)`

Returns a new position offset from this one by `offset` (in tiles).

### `static Vector2 operator -(Position left, Position right)`

Returns the vector from `right` to `left` (in tiles).

### `Position WithOffset(double dx, double dy)`

Returns a new position offset from this one by `dx` and `dy` (in tiles).

```csharp
var position = new Position(10, 20);
var offset = position.WithOffset(3, -4); // (13, 16)
```

### `(int TileX, int TileY) ToTile()`

Floors the tile-unit position to the coordinates of the containing cell. Because positions are
already expressed in tiles, this simply floors each component: `(8.5, 8.5)` lies in cell
`(8, 8)`, and `(-1.5, -1.5)` lies in cell `(-2, -2)` (floor division, so negatives round toward
negative infinity).

```csharp
var tile = new Position(8.5, 8.5).ToTile();  // (8, 8)
var negative = new Position(-1.5, -1.5).ToTile(); // (-2, -2)
```

### `Position ToPixels(int tileSize)`

Converts the tile-unit position to pixel coordinates: `(X * tileSize, Y * tileSize)`. Used by
rendering (to place sprites and the camera viewport on the canvas) and by the
`GameEngine.SurfaceToWorld` / `GameEngine.WorldToSurface` conversions. Throws
`ArgumentOutOfRangeException` when `tileSize` is zero or negative.

```csharp
var pixels = new Position(8.5, 8.5).ToPixels(48); // (408, 408)
```

### `double DistanceTo(Position other)`

Returns the Euclidean distance between this position and `other` (in tiles).

```csharp
var a = new Position(0, 0);
var b = new Position(3, 4);
Console.WriteLine(a.DistanceTo(b)); // 5
```
