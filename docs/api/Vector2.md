# Vector2

Namespace: `RPGEngine` — a two-dimensional vector with double-precision components, used for
screen-space offsets, deltas and distances.

```csharp
var v = new Vector2(3, -4);
```

## Members

### `double X` / `double Y`

The horizontal (x) and vertical (y) components.

### `static Vector2 operator +(Vector2 a, Vector2 b)`

Returns the component-wise sum of `a` and `b`.

### `static Vector2 operator -(Vector2 a, Vector2 b)`

Returns the component-wise difference of `a` and `b`.

### `static Vector2 operator -(Vector2 v)`

Returns the negation of `v`.

### `static Vector2 operator *(Vector2 v, double scalar)` / `static Vector2 operator *(double scalar, Vector2 v)`

Returns the component-wise product of the vector and the scalar. Used by movement logic to scale
a direction delta by a distance.

```csharp
var delta = Direction.Up.Delta();         // (0, -1)
var step = delta * (96 * 1 * 0.5);        // (0, -48) — 48 px up at 96 px/s for 0.5 s
```

## Example

```csharp
var position = new Position(10, 20);
var offset = new Vector2(3, -4);
var moved = position + offset;            // (13, 16)
var tile = moved.ToTile(tileSize: 48);    // (0, 0)
```
