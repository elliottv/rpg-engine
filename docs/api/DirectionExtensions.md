# DirectionExtensions

Namespace: `RPGEngine` — convenience members for the `Direction` enum: screen-space deltas,
opposites, sprite-sheet row indices and axis classification.

## Methods

### `Vector2 Delta(this Direction d)`

Returns the screen-space unit delta for the direction. Screen coordinates grow Y downward, so
`Down` is `(0, +1)` and `Up` is `(0, -1)`. Diagonal deltas are **normalized** (magnitude 1, not
√2), so diagonal movement is exactly as fast as cardinal movement. Throws
`ArgumentOutOfRangeException` for an undefined direction.

```csharp
Console.WriteLine(Direction.Up.Delta());       // (0, -1)
Console.WriteLine(Direction.Right.Delta());    // (1, 0)
Console.WriteLine(Direction.UpRight.Delta());  // (0.7071, -0.7071) — normalized diagonal
```

### `Direction Opposite(this Direction d)`

Returns the opposite direction (`Down` ↔ `Up`, `Left` ↔ `Right`, `DownLeft` ↔ `UpRight`,
`DownRight` ↔ `UpLeft`). Throws `ArgumentOutOfRangeException` for an undefined direction.

```csharp
Console.WriteLine(Direction.Up.Opposite());     // Down
Console.WriteLine(Direction.UpRight.Opposite()); // DownLeft
```

### `int RowIndex(this Direction d)`

Returns the RPG Maker MZ character sheet row for this direction (`0 = down`, `1 = left`,
`2 = right`, `3 = up`). Cardinal directions return their enum value; diagonal directions
deliberately fall back to their **horizontal** component's row (`DownLeft`/`UpLeft` → 1,
`DownRight`/`UpRight` → 2) so a diagonally-facing character renders with the side-view row,
which reads better than the front or back rows for an oblique facing.

```csharp
Console.WriteLine(Direction.Up.RowIndex());     // 3
Console.WriteLine(Direction.UpRight.RowIndex()); // 2 — the Right (side-view) row
```

### `bool IsHorizontal(this Direction d)`

Returns whether the direction is horizontal (`Left` or `Right`). Diagonals are neither
horizontal nor vertical.

```csharp
Console.WriteLine(Direction.Left.IsHorizontal());  // True
Console.WriteLine(Direction.Up.IsHorizontal());    // False
Console.WriteLine(Direction.UpRight.IsHorizontal()); // False
```

### `bool IsVertical(this Direction d)`

Returns whether the direction is vertical (`Down` or `Up`). Diagonals are neither horizontal nor
vertical.

```csharp
Console.WriteLine(Direction.Left.IsVertical());  // False
Console.WriteLine(Direction.Up.IsVertical());    // True
Console.WriteLine(Direction.UpRight.IsVertical()); // False
```

### `bool IsDiagonal(this Direction d)`

Returns whether the direction is one of the four diagonal directions (`DownLeft`, `DownRight`,
`UpLeft` or `UpRight`).

```csharp
Console.WriteLine(Direction.UpRight.IsDiagonal()); // True
Console.WriteLine(Direction.Up.IsDiagonal());      // False
```
