# Direction

Namespace: `RPGEngine` — a direction in the game world, expressed as an **immutable 2-D vector
value type** with `double` components (`X` grows right, `Y` grows down) — the same double model
as `Position`. It is a `readonly record struct Direction(double X, double Y)`.

The historical 8-value enum is gone, but the **eight canonical unit directions** it represented
survive as static members (`Direction.Down`, `Direction.Left`, …) and are listed in
`Direction.All`. Key input and sprite rows still use exactly these eight: key input yields one of
the eight canonical directions, and a facing — which may in general be a continuous unit vector —
is adapted to the nearest canonical direction for the sprite sheet row via
`DirectionExtensions.Nearest8` / `DirectionExtensions.RowIndex`.

`default(Direction)` is the zero vector `(0, 0)`. Consumers that need a facing default (e.g.
`Character.Direction`) initialize it explicitly to `Direction.Down` to preserve the historical
default facing.

## Canonical unit directions

The eight canonical unit directions, in the historical enum order. Diagonals are **normalized**
(each component is ±√½ ≈ ±0.7071), so diagonal movement covers the same distance as cardinal
movement.

| Static | Components | Sprite row | Meaning |
| --- | --- | --- | --- |
| `Down` | `(0, +1)` | 0 | Facing down. |
| `Left` | `(-1, 0)` | 1 | Facing left. |
| `Right` | `(1, 0)` | 2 | Facing right. |
| `Up` | `(0, -1)` | 3 | Facing up. |
| `DownLeft` | `(-√½, +√½)` | 1 (side view) | Facing down-left. |
| `DownRight` | `(+√½, +√½)` | 2 (side view) | Facing down-right. |
| `UpLeft` | `(-√½, -√½)` | 1 (side view) | Facing up-left. |
| `UpRight` | `(+√½, -√½)` | 2 (side view) | Facing up-right. |

`All` exposes these eight in the same order (`Down`, `Left`, `Right`, `Up`, `DownLeft`,
`DownRight`, `UpLeft`, `UpRight`).

## Members

### `double X` / `double Y`

The horizontal (x, grows right) and vertical (y, grows down) components. Both are `double`.

### `static Direction Down`, `Left`, `Right`, `Up`, `DownLeft`, `DownRight`, `UpLeft`, `UpRight`

The eight canonical **unit** directions (see the table above).

### `static IReadOnlyList<Direction> All`

The eight canonical unit directions, in the historical enum order.

### `static Direction operator -(Direction d)`

Returns the opposite direction: component-wise negation `(-X, -Y)`. For the eight canonical
directions this matches the enum's historical opposite (`Down` ↔ `Up`, `Left` ↔ `Right`, and
each diagonal flips both signs); it stays well-defined for any continuous direction.

### `static Direction operator *(Direction d, double scalar)` / `static Direction operator *(double scalar, Direction d)`

Returns the component-wise product of the direction and the scalar. Movement multiplies the
(unit) direction vector by the travelled distance: `direction * (BaseSpeed * factor * dt)`.

### `static implicit operator Vector2(Direction d)`

Converts the direction to an equivalent `Vector2` with the same components. A direction *is* a
2-D vector, so this makes a direction directly usable wherever a `Vector2` offset is expected
(e.g. adding a scaled direction to a `Position`).

### `double Length`

The Euclidean length (magnitude) of the direction. The canonical unit directions all have
length 1, so their displacement equals `BaseSpeed * factor * dt`. Movement assumes a unit-length
direction: a non-unit vector scales the displacement by its length.

### `bool IsZero`

Whether the direction is the zero vector `(0, 0)` — the value of `default(Direction)`.

### `Direction Normalized`

The direction normalized to unit length, keeping its direction unchanged. Throws
`InvalidOperationException` when the direction is the zero vector `(0, 0)`, which has no
direction to normalize.

### `string ToString()`

Returns the canonical name (e.g. `"Up"`) when the direction equals one of the eight canonical
directions in `All`, for readability and logging; otherwise returns the component pair
`"(X, Y)"`.

## Continuous movement

A character moves by `Direction * (BaseSpeed * factor * dt)` (or `Direction * (BaseSpeed * dt)`
inside `Update`), where `Direction` is a **unit** vector. Because `Direction` is a vector, any
unit vector produces continuous movement — the engine no longer limits facing to 8 directions.
A non-unit vector scales the displacement by its length, so always move with unit vectors: use
`Normalized` to turn a raw delta (e.g. a touch/analogue-stick vector or a mouse drag) into a
unit direction first. Sprites stay 8-direction by adapting the continuous facing with
`DirectionExtensions.Nearest8` at draw time.

```csharp
// A raw delta (not necessarily unit length) is normalized before movement.
var rawDelta = new Direction(30, 40);     // e.g. a stick vector or mouse drag
var direction = rawDelta.Normalized;      // (0.6, 0.8) — unit length
var character = new Character { BaseSpeed = 2 };
character.Move(direction, speedFactor: 1, dt: 1); // moves (1.2, 1.6) tiles: direction * 2

// Sprites adapt the (possibly continuous) facing to the nearest canonical 8-direction row.
var nearest = direction.Nearest8();       // DownRight
Console.WriteLine(nearest.RowIndex());    // 2 — the Right (side-view) row
```

## Example

```csharp
// The eight canonical directions are still available as unit vectors.
var up = Direction.Up;            // (0, -1)
Console.WriteLine(up);            // "Up"
Console.WriteLine(up.RowIndex()); // 3 — RPG Maker MZ row 3

// A continuous facing is expressed as a 2-D vector of doubles, just like Position.
var continuous = new Direction(0.6, 0.8);
Console.WriteLine(continuous.Length); // 1 (0.6² + 0.8² = 1)
Console.WriteLine(continuous);        // "(0.6, 0.8)" — not a canonical name

// Key input always yields one of the eight canonical directions...
var config = new GameConfig();
Console.WriteLine(config.GetMovementDirection([Key.W, Key.D])); // UpRight

// ...and sprites adapt a continuous facing to the nearest canonical 8-direction row.
Console.WriteLine(continuous.Nearest8()); // DownRight
Console.WriteLine(continuous.RowIndex()); // 2 — the Right (side-view) row

// Movement multiplies the direction vector by the speed.
var character = new Character { BaseSpeed = 2 };
character.Move(Direction.Right, speedFactor: 1, dt: 0.5); // +1 tile right (2 * 0.5)
```
