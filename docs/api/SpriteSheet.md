# SpriteSheet

Namespace: `RPGEngine.Sprites` — a single RPG Maker MZ spritesheet.

An image whose cells form the normative **12-column × 8-row grid**. The cell size is **derived
from the image dimensions** (`width / 12` × `height / 8`), so both the standard 576×384 sheet
(48×48 cells) and larger sheets such as a 936×864 sheet (78×108 cells) are supported. It contains
**8 characters** laid out as a 4×2 grid; each character occupies a 3×4 block of cells (3 animation
frames × 4 directions). Both full sheets and part sheets use this same layout.

## Remarks

- `SpriteSheetRef(Name, CharacterIndex)` and `SpriteSheet.GetSprite(characterIndex, ...)` select
  one of the **8 characters** in a sheet (both full and part sheets).
- Instances are created through `SpriteSheetManager` (the constructor is internal) and own the
  single decoded `SKImage` that backs every sprite returned by `GetSprite`.

## Constants

### `const int Columns = 12`

The normative grid width, in cells.

### `const int Rows = 8`

The normative grid height, in cells.

## Properties

### `string Name`

Gets the unique name used to reference the sheet.

### `SpriteSheetType Type`

Gets the kind of sheet (full or part).

### `CharacterPartType? PartType`

Gets the character layer this part sheet provides, or `null` when `Type` is `Full`.

### `int CellWidth` / `int CellHeight`

Get the derived width and height of a single cell in pixels (`sheet width / Columns` and
`sheet height / Rows`). For a 576×384 sheet this is 48×48; for a 936×864 sheet it is 78×108.

### `int SheetWidth` / `int SheetHeight`

Get the total sheet size in pixels (`Columns × CellWidth`, `Rows × CellHeight`).

### `int CharacterCount`

Gets the number of characters contained in the sheet (always 8).

## Methods

### `SKImage GetSprite(int characterIndex, Direction direction, int frame)`

Returns the `CellWidth × CellHeight` sprite at `(direction, frame)` for the character
`characterIndex` (1-based, 1..8). The returned image is an independent raster crop the caller owns
and disposes. Throws `ArgumentOutOfRangeException` when `characterIndex` is outside 1..8 or
`frame` is outside 0..2.

Character `i` is located at `charCol = (i - 1) % 4`, `charRow = (i - 1) / 4`; its cell
`(frame, direction)` is at column `charCol * 3 + frame` and row `charRow * 4 + direction.RowIndex()`.
Row selection uses `DirectionExtensions.RowIndex`, so diagonal directions (which have no dedicated
sheet row) fall back to the side-view row of their horizontal component.

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
    Console.WriteLine($"{sheet.CellWidth}×{sheet.CellHeight}"); // 48×48 for a 576×384 sheet

    // Every 1-based index 1..8 yields an independent sprite at the derived cell size.
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

A 936×864 sheet derives 78×108 cells, so the same example reports `78×108` sprites.
