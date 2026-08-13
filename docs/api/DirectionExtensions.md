# DirectionExtensions

Namespace: `RPGEngine` — convenience members for the `Direction` enum: screen-space deltas,
opposites, sprite-sheet row indices and axis classification.

## Methods

### `Vector2 Delta(this Direction d)`

Returns the screen-space unit delta for the direction. Screen coordinates grow Y downward, so
`Down` is `(0, +1)` and `Up` is `(0, -1)`. Throws `ArgumentOutOfRangeException` for an undefined
direction.

```csharp
Console.WriteLine(Direction.Up.Delta());    // (0, -1)
Console.WriteLine(Direction.Right.Delta()); // (1, 0)
```

### `Direction Opposite(this Direction d)`

Returns the opposite direction (`Down` ↔ `Up`, `Left` ↔ `Right`). Throws
`ArgumentOutOfRangeException` for an undefined direction.

```csharp
Console.WriteLine(Direction.Up.Opposite()); // Down
```

### `int RowIndex(this Direction d)`

Returns the RPG Maker MZ character sheet row for this direction (`0 = down`, `1 = left`,
`2 = right`, `3 = up`). It always equals the enum value; it is exposed explicitly to guard the
sprite-row contract.

```csharp
Console.WriteLine(Direction.Up.RowIndex()); // 3
```

### `bool IsHorizontal(this Direction d)`

Returns whether the direction is horizontal (`Left` or `Right`).

```csharp
Console.WriteLine(Direction.Left.IsHorizontal());  // True
Console.WriteLine(Direction.Up.IsHorizontal());    // False
```

### `bool IsVertical(this Direction d)`

Returns whether the direction is vertical (`Down` or `Up`).

```csharp
Console.WriteLine(Direction.Left.IsVertical());  // False
Console.WriteLine(Direction.Up.IsVertical());    // True
```
