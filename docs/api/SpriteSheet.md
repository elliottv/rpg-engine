# SpriteSheet

Namespace: `RPGEngine.Sprites` — a single RPG Maker MZ spritesheet.

A 576×384 image made of 48×48 cells arranged in a 12-column × 8-row grid. It contains **8
characters** laid out as a 4×2 grid; each character occupies a 3×4 block of cells (3 animation
frames × 4 directions). Both full sheets and part sheets use this same layout.

## Remarks

- `SpriteSheetRef(Name, CharacterIndex)` and `SpriteSheet.GetSprite(characterIndex, ...)` select
  one of the **8 characters** in a **576×384** sheet (both full and part sheets).
- Instances are created through `SpriteSheetManager` (the constructor is internal) and own the
  single decoded `SKImage` that backs every sprite returned by `GetSprite`.

## Constants

### `const int CellSize = 48`

The width and height of a single character cell in pixels (normative RPG Maker MZ value).

### `const int SheetWidth = 576`

The width of a full or part spritesheet in pixels (12 cells).

### `const int SheetHeight = 384`

The height of a full or part spritesheet in pixels (8 cells).

## Properties

### `string Name`

Gets the unique name used to reference the sheet.

### `SpriteSheetType Type`

Gets the kind of sheet (full or part).

### `CharacterPartType? PartType`

Gets the character layer this part sheet provides, or `null` when `Type` is `Full`.

### `int CellWidth` / `int CellHeight`

Get the width and height of a single cell in pixels (always 48).

### `int CharacterCount`

Gets the number of characters contained in the sheet (always 8).

## Methods

### `SKImage GetSprite(int characterIndex, Direction direction, int frame)`

Returns the 48×48 sprite at `(direction, frame)` for the character `characterIndex` (1-based,
1..8). The returned image is an independent raster crop the caller owns and disposes. Throws
`ArgumentOutOfRangeException` when `characterIndex` is outside 1..8 or `frame` is outside 0..2.

Character `i` is located at `charCol = (i - 1) % 4`, `charRow = (i - 1) / 4`; its cell
`(frame, direction)` is at column `charCol * 3 + frame` and row `charRow * 4 + (int)direction`.

```csharp
using var sprite = sheet.GetSprite(characterIndex: 1, Direction.Down, frame: 1);
canvas.DrawImage(sprite, new SKPoint(0, 0));
```

## Example: the character index 1..8 semantics

```csharp
var manager = new SpriteSheetManager();
using (var stream = File.OpenRead("assets/characters/character_full.png"))
{
    var sheet = manager.Load("hero", stream);

    Console.WriteLine(sheet.CharacterCount); // 8

    // Every 1-based index 1..8 yields an independent 48×48 sprite.
    for (var characterIndex = 1; characterIndex <= 8; characterIndex++)
    {
        using var sprite = sheet.GetSprite(characterIndex, Direction.Down, frame: 1);
        Console.WriteLine($"{characterIndex}: {sprite.Width}×{sprite.Height}");
    }

    // An index outside 1..8 is rejected.
    try
    {
        sheet.GetSprite(0, Direction.Down, 1);
    }
    catch (ArgumentOutOfRangeException)
    {
    }
}
```
