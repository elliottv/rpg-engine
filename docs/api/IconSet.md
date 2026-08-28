# IconSet

Namespace: `RPGEngine.Sprites` — a single icon set: an image divided into a **32×32 tile grid**
from which characters can display a small icon **above their sprite** (e.g. a quest marker or a
status balloon).

The number of rows and columns is **deduced from the image dimensions** (`RowCount = height / 32`,
`ColumnCount = width / 32`), so a 96×64 image is a 3-column × 2-row set of 6 icons and a 32×32
image is a 1×1 set of a single icon.

## Remarks

- This is the icon-set counterpart of `SpriteSheet`: a plain data + slice object that owns the
  single decoded `SKImage` backing every icon returned by `GetIcon`. The decoded image is never
  mutated.
- Instances are created through the `Load(string)` / `Load(Stream)` / `LoadAsync(Stream)`
  factories (the constructor is internal) and are **not** `IDisposable`; the engine owns the
  single loaded instance for its lifetime.
- Icons are addressed with a zero-based index using the **row-major** formula
  `row = iconIndex / ColumnCount`, `col = iconIndex % ColumnCount` (integer division, i.e. floor).
  Consecutive indices walk **left-to-right across a row first**, then wrap to the next row:
  index 0 is the top-left tile, index 1 is immediately to its **right**, and a full row is
  filled before moving down. On a 96×64 set (3 columns × 2 rows), index 4 is the tile at
  `(row = 1, col = 1)`. This row-major ordering is the only one that is valid for arbitrary
  (non-square) sets and matches the engine's existing character-index convention
  (`SpriteSheet.GetSprite`: `charCol = (index − 1) % 4`, `charRow = (index − 1) / 4`).

## Constants

### `const int TileSize = 32`

The normative icon tile size in pixels.

## Properties

### `int RowCount`

Gets the number of icon rows in the set (`source.Height / TileSize`) — the **vertical** count of
32×32 tiles.

### `int ColumnCount`

Gets the number of icon columns in the set (`source.Width / TileSize`) — the **horizontal** count
of 32×32 tiles.

### `int Count`

Gets the total number of icons in the set (`RowCount × ColumnCount`).

## Methods

### `static IconSet Load(string path)`

Loads the icon set from a file path: the image is decoded and its dimensions validated as a 32×32
grid. Throws `ArgumentNullException` when `path` is null; `ArgumentException` when `path` is empty
after trimming, the image cannot be decoded, or its dimensions are not a positive multiple of 32
on both axes.

```csharp
var iconSet = IconSet.Load("assets/icons/icons.png"); // e.g. a 96×64 PNG (3 columns × 2 rows)
```

### `static IconSet Load(Stream stream)`

Loads the icon set from a stream (the file-system-free entry point, e.g. WebAssembly builds where
assets are fetched over HTTP). The caller remains the owner of the stream. Throws
`ArgumentNullException` when `stream` is null; `ArgumentException` when the image cannot be decoded
or its dimensions are not a positive multiple of 32 on both axes.

```csharp
using var stream = new MemoryStream(await http.GetByteArrayAsync("assets/icons/icons.png"));
var iconSet = IconSet.Load(stream);
```

### `static Task<IconSet> LoadAsync(Stream stream)`

The asynchronous counterpart of `Load(Stream)` for streams that only support asynchronous reads
(e.g. certain network/browser streams). The stream is copied into an in-memory buffer asynchronously
and decoded from that seekable buffer, so no synchronous read is performed on the caller's stream.
The caller remains the owner of the stream.

```csharp
using var stream = new MemoryStream(await http.GetByteArrayAsync("assets/icons/icons.png"));
var iconSet = await IconSet.LoadAsync(stream);
```

### `SKImage GetIcon(int iconIndex)`

Returns the 32×32 icon at `iconIndex` (zero-based, `0..Count - 1`). The returned image is an
**independent raster crop** the caller owns and disposes. Throws `ArgumentOutOfRangeException`
when `iconIndex` is outside the set.

The icon is selected with the **row-major** formula `row = iconIndex / ColumnCount`,
`col = iconIndex % ColumnCount`: consecutive indices walk left-to-right across a row first, then
wrap to the next row. On a 3-column set, index 1 is the **second tile of the top row** — to the
**right** of index 0 — not the tile below it.

```csharp
using var icon = iconSet.GetIcon(iconIndex: 4); // 96×64 set: the tile at (row = 4/3 = 1, col = 4%3 = 1)
canvas.DrawImage(icon, new SKPoint(0, 0));
```

## Example: loading a set and displaying an icon above a character

```csharp
var engine = new GameEngine();

// Load the player's full spritesheet and the icon set (a 96×64 PNG = 3 columns × 2 rows).
engine.LoadSpriteSheet("hero", "assets/characters/character_full.png");
engine.LoadIconSet("assets/icons/icons.png");

engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
engine.Player.Position = new Position(2.5, 3.0);

// Index 0 is the top-left tile; on a 3-column set, index 1 is to its right (across the row).
engine.Player.Character.IconIndex = 0;

// Render as usual: the selected 32×32 icon is drawn above the player's sprite, centered
// horizontally on the character's feet, its bottom edge at the sprite's top edge.
using var bitmap = new SKBitmap(240, 240);
using (var canvas = new SKCanvas(bitmap))
{
    canvas.Clear(SKColors.Transparent);
    engine.Render(canvas, dt: 1.0 / 60);
}
```

> A non-null `Character.IconIndex` with no icon set loaded throws `InvalidOperationException` at
> draw time; an index outside the loaded set's range throws `ArgumentOutOfRangeException`. The
> engine holds exactly **one** icon set — a subsequent `LoadIconSet`/`LoadIconSetAsync` call
> **replaces** the previous set (there is no name, so no duplicate-name error).
