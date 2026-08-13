# SpriteSheetRef

Namespace: `RPGEngine.Sprites` — references a specific character within a named spritesheet.

```csharp
var reference = new SpriteSheetRef("hero", CharacterIndex: 1);
```

## Remarks

- `Name` is the unique name of the spritesheet; `CharacterIndex` is the **1-based index (1..8)**
  of the character within the sheet, in row-major order over the sheet's 4×2 character grid.
- This is a dumb value type: the 1..8 range is intentionally **not** validated here. It is
  enforced where the reference is consumed (by `SpriteSheet.GetSprite` and by the character
  compositor at render time).

## Example: selecting one of the 8 characters

```csharp
var hero = new SpriteSheetRef("hero", CharacterIndex: 1);
var villager = new SpriteSheetRef("villager_body", CharacterIndex: 2);
var guard = new SpriteSheetRef("guard_body", CharacterIndex: 3);

Console.WriteLine(hero.Name);              // "hero"
Console.WriteLine(hero.CharacterIndex);    // 1
```
