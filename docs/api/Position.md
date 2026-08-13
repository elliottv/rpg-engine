# Position

Namespace: `RPGEngine` — a position in 2D screen space with double-precision coordinates.

Y grows downward, so increasing `Y` moves the position down the screen.

```csharp
var position = new Position(10, 20);
```

## Members

### `double X` / `double Y`

The horizontal (x) and vertical (y) coordinates.

### `static Position operator +(Position position, Vector2 offset)`

Returns a new position offset from this one by `offset`.

```csharp
var position = new Position(10, 20);
var moved = position + new Vector2(3, -4); // (13, 16)
```

### `static Position operator -(Position position, Vector2 offset)`

Returns a new position offset from this one by `offset`.

### `static Vector2 operator -(Position left, Position right)`

Returns the vector from `right` to `left`.

### `Position WithOffset(double dx, double dy)`

Returns a new position offset from this one by `dx` and `dy`.

```csharp
var position = new Position(10, 20);
var offset = position.WithOffset(3, -4); // (13, 16)
```

### `(int TileX, int TileY) ToTile(int tileSize)`

Converts the pixel position to tile coordinates using floor division. Throws
`ArgumentOutOfRangeException` when `tileSize` is zero or negative.

```csharp
var position = new Position(100, 100);
var tile = position.ToTile(tileSize: 48); // (2, 2)
```

### `double DistanceTo(Position other)`

Returns the Euclidean distance between this position and `other`.

```csharp
var a = new Position(0, 0);
var b = new Position(3, 4);
Console.WriteLine(a.DistanceTo(b)); // 5
```
