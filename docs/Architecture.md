# Architecture

The RPG Engine is built around a small composition model. This page explains how the pieces fit
together and documents the fixed RPG Maker MZ part-composition order.

## The composition model

### Player → Character

`Player` does **not** inherit from `Character`. It *composes* one:

```
Player ── Character (Position, Direction, BaseSpeed, SpriteSheets, walk-cycle animation)
```

`Player` is a thin wrapper that forwards state access and movement to its `Character`. All of
the in-world state lives on `Character`:

- `Character.Position` — top-left world pixel position of the 48×48 sprite.
- `Character.Direction` — the facing direction (8 directions: Down/Left/Right/Up plus the four diagonals).
- `Character.BaseSpeed` — movement speed in pixels per second.
- `Character.SpriteSheets` — the list of `SpriteSheetRef`s (sheet name + 1..8 character index).
- `Character.Update(dt)` — advances the walk-cycle animation (internal).

Because the player *is* a character, configuring `Player.SpriteSheets` is exactly equivalent to
configuring the underlying `Character.SpriteSheets` (it is the same list instance).

### Engine → managers

`GameEngine` is the root object. It owns the game state and the registries the renderer needs:

```
GameEngine
├── Player (a Player wrapper around a Character)
├── Characters (IList<Character> of NPCs)
├── Map (TileMap?, owns its own TileSets)
├── Config (GameConfig — WASD movement bindings)
├── SpriteSheetManager (internal — loads and resolves full/part sheets by name)
└── pressed-keys state (internal — fed by Input(Key, isPressed))
```

The host writes the game loop:

```
for each frame:
    engine.Input(translatedKey, isPressed)   // only on key events
    engine.Update(dt)                        // move + animate
    engine.Render(canvas, dt)                // draw map + NPCs + player
```

`GameEngine` never runs its own loop and never blocks. The camera is internal to the engine:
`Render` follows the player and clamps the viewport inside the map. No public camera API exists
in this epic.

### Tiled model

`TileMap` is loaded from a Tiled `.tmx` file and **owns** the `TileSet`s its layers reference
(they are created when the map is loaded — there is no global tileset registry). Standalone
tilesets are loaded through `TileSet.Load` factories when needed.

## Spritesheet layout (576×384, 8 characters)

Both **full** sheets and **part** sheets use the same normative RPG Maker MZ layout:

- Image size: **576 × 384** pixels.
- Cell size: **48 × 48** pixels.
- Grid: **12 columns × 8 rows** of cells.
- Characters: **8** (a 4 × 2 grid), each occupying a 3-cell × 4-row block
  (3 animation frames × 4 directions).

Character `i` (1-based, **1..8**) is located at:

```
charCol = (i - 1) % 4
charRow = (i - 1) / 4
```

Its cell `(frame, direction)` is at column `charCol * 3 + frame` and row
`charRow * 4 + direction.RowIndex()`, where the direction rows are
`0 = Down`, `1 = Left`, `2 = Right`, `3 = Up`. Diagonal directions have no dedicated row and
fall back to their horizontal component's row (`DownLeft`/`UpLeft` → 1, `DownRight`/`UpRight`
→ 2), so a diagonally-facing character renders with the side-view row.

A `SpriteSheetRef(Name, CharacterIndex)` pairs a loaded sheet name with one of the **8
characters** in that sheet. The index is enforced (1..8) where the reference is consumed — at
render time by the character compositor, and by `SpriteSheet.GetSprite`.

## Part-composition ordering table

A character uses either a single **full** sheet or one or more **part** sheets. Parts are drawn
**bottom → top** in exactly the order below, regardless of the order of the entries in
`Character.SpriteSheets`. Missing parts are skipped. Mixing full and part sheets (or using more
than one full sheet) throws `InvalidOperationException` at draw time.

| Step | Part | Notes |
| --- | --- | --- |
| 1 | `Hair2` | Drawn behind everything (when hair is shown). |
| 2 | `Face` | |
| 3 | `Body` | |
| 4 | `Hair1` | (when hair is shown). |
| 5 | `FaceHair` | |
| 6 | `Armour` | |
| 7 | `Hair2` (again) | **Only when facing Up** — rear hair is drawn over the body. |
| 8 | `Head` | (if present). |

Hair (`Hair1` and `Hair2`) is shown unless a `Head` part sheet is present whose **name contains
`$`** (the epic's `$`-prefix rule): `head$` hides hair so a bald/hatted head can be used.

### Example: a "villager" and a "guard"

The canonical sample scene (used by the desktop and WebAssembly hosts and by the end-to-end
tests) configures:

- **Player**: full sheet `hero`, character index **1**.
- **Villager**: part sheets `body` + `face` + `hair1`, character index **2**.
- **Guard**: part sheets `body` + `face` + `armour` + `head`, character index **3**.

```csharp
// Player — a single full sheet, character slot 1.
engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

// Villager — parts composed in the fixed order, character slot 2.
villager.SpriteSheets.Add(new SpriteSheetRef("villager_body", CharacterIndex: 2));
villager.SpriteSheets.Add(new SpriteSheetRef("villager_face", CharacterIndex: 2));
villager.SpriteSheets.Add(new SpriteSheetRef("villager_hair1", CharacterIndex: 2));

// Guard — parts composed in the fixed order, character slot 3.
guard.SpriteSheets.Add(new SpriteSheetRef("guard_body", CharacterIndex: 3));
guard.SpriteSheets.Add(new SpriteSheetRef("guard_face", CharacterIndex: 3));
guard.SpriteSheets.Add(new SpriteSheetRef("guard_armour", CharacterIndex: 3));
guard.SpriteSheets.Add(new SpriteSheetRef("guard_head", CharacterIndex: 3));
```

## Movement and input

Movement input combines every held bound key into a single **8-direction vector**: each key that
is bound to a movement direction (via `GameConfig.GetDirection`) contributes its unit delta, the
deltas are summed, normalized and quantized to the nearest of the eight `Direction` values.
Opposite keys cancel (`W`+`S` or `A`+`D`), and a diagonal pair combines into a diagonal
(`W`+`D` → up-right) at the same speed as cardinal movement (the diagonal deltas are
normalized, magnitude 1). When no bound key is held the player stops and the animation snaps back
to the standing frame. The engine reads `GameConfig` at input time and never caches a snapshot.

The walk-cycle animation is **time-based and speed-scaled**: `Character.Update(dt)` advances the
walk cycle at a rate proportional to `BaseSpeed` and `AnimationCycleSpeed`, so at
`BaseSpeed == AnimationCycleSpeed == 96` the cycle (`0 → 1 → 2 → 1`) completes exactly
once per second.
