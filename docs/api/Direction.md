# Direction

Namespace: `RPGEngine` — the eight facing directions used throughout the engine.

The cardinal values match the RPG Maker MZ character sheet row order
(`0 = down`, `1 = left`, `2 = right`, `3 = up`); the diagonal values follow them and have no
dedicated sprite-sheet row — a diagonally-facing character renders with the side-view row of its
horizontal component (see `DirectionExtensions.RowIndex`). This is the single source of truth
used by sprite cropping and rendering.

## Values

| Value | Value number | Sprite row | Meaning |
| --- | --- | --- | --- |
| `Down` | 0 | 0 | Facing down. |
| `Left` | 1 | 1 | Facing left. |
| `Right` | 2 | 2 | Facing right. |
| `Up` | 3 | 3 | Facing up. |
| `DownLeft` | 4 | 1 (side view) | Facing down-left. |
| `DownRight` | 5 | 2 (side view) | Facing down-right. |
| `UpLeft` | 6 | 1 (side view) | Facing up-left. |
| `UpRight` | 7 | 2 (side view) | Facing up-right. |

## Example

```csharp
var direction = Direction.UpRight;
Console.WriteLine((int)direction); // 7
Console.WriteLine(direction.RowIndex()); // 2 — falls back to the Right (side-view) row
```
