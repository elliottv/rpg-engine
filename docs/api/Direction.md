# Direction

Namespace: `RPGEngine` — the four facing directions used throughout the engine.

The numeric values match the RPG Maker MZ character sheet row order
(`0 = down`, `1 = left`, `2 = right`, `3 = up`). This is the single source of truth used by
sprite cropping and rendering; see `DirectionExtensions.RowIndex`.

## Values

| Value | Value number | Sprite row | Meaning |
| --- | --- | --- | --- |
| `Down` | 0 | 0 | Facing down. |
| `Left` | 1 | 1 | Facing left. |
| `Right` | 2 | 2 | Facing right. |
| `Up` | 3 | 3 | Facing up. |

## Example

```csharp
var direction = Direction.Down;
Console.WriteLine((int)direction); // 0 — row 0 of an RPG Maker MZ sheet
```
