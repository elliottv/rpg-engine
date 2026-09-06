# DirectionExtensions

Namespace: `RPGEngine` — the **8-direction adaptation layer** for the `Direction` vector type.
`Direction` is now a continuous 2-D vector (see `Direction.md`), but key input and sprites still
use the eight canonical directions; these helpers express that mapping:

- `Nearest8` — adapts a (possibly continuous) direction to the nearest of the eight canonical
  unit directions (the epic's *"the direction vector is adapted to the old 8 directions"*).
- `RowIndex` — maps a direction to its RPG Maker MZ character-sheet row, snapping first.
- `Opposite` — returns the opposite direction (vector negation).

## Methods

### `Direction Nearest8(this Direction d)`

Returns the closest of the eight canonical unit directions in `Direction.All`, chosen by dot
product against each canonical unit vector after normalizing `d` (so only the direction matters,
not its length). The zero vector `(0, 0)` has no direction and snaps to `Direction.Down`.

```csharp
Console.WriteLine(Direction.Up.Nearest8());        // Up — a canonical direction is unchanged
Console.WriteLine(new Direction(0.9, 0.2).Nearest8());  // Right — mostly horizontal, slightly down
Console.WriteLine(new Direction(0.03, -0.99).Nearest8()); // Up — almost straight up
Console.WriteLine(default(Direction).Nearest8());  // Down — the zero vector snaps to Down
```

### `int RowIndex(this Direction d)`

Returns the RPG Maker MZ character-sheet row the character renders with (`0 = down`,
`1 = left`, `2 = right`, `3 = up`). The direction is first snapped with `Nearest8`, so a
continuous vector maps to the row of its **nearest canonical** direction. Canonical cardinals
return their own row; canonical diagonals have no dedicated sheet row and deliberately fall back
to their **horizontal** component's row (`DownLeft`/`UpLeft` → 1, `DownRight`/`UpRight` → 2),
so a diagonally-facing character renders with the side-view row.

```csharp
Console.WriteLine(Direction.Up.RowIndex());        // 3
Console.WriteLine(Direction.UpRight.RowIndex());   // 2 — the Right (side-view) row
Console.WriteLine(new Direction(0.9, 0.2).RowIndex());    // 2 — nearest canonical is Right
Console.WriteLine(new Direction(-0.9, 0.2).RowIndex());   // 1 — nearest canonical is Left
```

### `Direction Opposite(this Direction d)`

Returns the direction opposite to this one. This is exactly the vector negation `-d`: for the
eight canonical directions it matches the enum's historical opposite (`Down` ↔ `Up`,
`Left` ↔ `Right`, `DownLeft` ↔ `UpRight`, `DownRight` ↔ `UpLeft`), and it remains well-defined
for any continuous direction.

```csharp
Console.WriteLine(Direction.Up.Opposite());        // Down
Console.WriteLine(Direction.UpRight.Opposite());   // DownLeft
Console.WriteLine(new Direction(0.6, 0.8).Opposite()); // (-0.6, -0.8)
```
