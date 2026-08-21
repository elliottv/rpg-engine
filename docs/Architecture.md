# Architecture

The RPG Engine is built around a small composition model. This page explains how the pieces fit
together and documents the fixed RPG Maker MZ part-composition order.

## Coordinate system: tiles

All world coordinates — `Character.Position`, `Player.Position`, camera origins and the
`GameEngine.SurfaceToWorld` / `GameEngine.WorldToSurface` conversions — are expressed in
**tiles** (double). Y grows downward. Pixels are produced only at the canvas boundary: the
engine multiplies the tile positions by the map's tile size (`TileMap.TileWidth`, default 48
when no map is set) when rendering. Movement speeds are in **tiles per second**.

The tile size is fixed by the map (48 px for the fixture map and the RPG Maker MZ sheets), so a
world position like `(8.5, 8.5)` tiles corresponds to the pixel position `(408, 408)` at
48 px/tile.

## The composition model

### Player → Character

`Player` does **not** inherit from `Character`. It *composes* one:

```
Player ── Character (Position, Direction, BaseSpeed, SpriteSheets, walk-cycle animation)
```

`Player` is a thin wrapper that forwards state access and movement to its `Character`. All of
the in-world state lives on `Character`:

- `Character.Position` — top-left world position of the sprite, in **tiles** (its size is the
  configured sheet's derived cell size, in pixels, used only for clamping).
- `Character.Direction` — the facing direction (8 directions: Down/Left/Right/Up plus the four diagonals).
- `Character.BaseSpeed` — movement speed in **tiles per second** (the player default is 2,
  i.e. the tile-unit equivalent of 96 px/s at 48 px tiles).
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

`GameEngine` never runs its own loop and never blocks.

### The camera and surface ↔ world conversion

The camera is internal to the engine: `Render` follows the player and clamps the viewport inside
the map. The camera origin is computed in **tiles** by `ComputeCameraOrigin(canvasWidth,
canvasHeight)` (internal, asserted by the tests):

- `max = max(0, Map.Width - canvasWidth / ts)` per axis, where `ts` is the map's tile width —
  the farthest the viewport may scroll while staying inside the map.
- `desired = player.Position - (canvasWidth / (2*ts), canvasHeight / (2*ts))` — centers the player.
- `origin = clamp(desired, 0, max) - max(0, (canvasSize - Map.PixelSize) / (2*ts))` per axis —
  the centering offset when the map is smaller than the canvas (a negative origin).
- No map → `(0, 0)`.

`Render` turns the tile origin into a pixel viewport for the map (a pure pixel renderer) and
into pixel screen positions for the characters. The map's `TileMap.Draw` / `DrawAbovePlayer`
receive a pixel `SKRect` and blit their prerendered layer images; each character is drawn at
screen position `(pos.X*ts - origin.X*ts, pos.Y*ts - origin.Y*ts)`.

The engine exposes two public conversions that use the **same camera** (follow + clamp) for the
given canvas size, so a host can translate between canvas pixels and world tiles without
reimplementing the camera:

- `Position SurfaceToWorld(double surfaceX, double surfaceY, double canvasWidth, double canvasHeight)`
  — `world = (surfaceX / ts + origin.X, surfaceY / ts + origin.Y)`. This is the foundation of
  the "click to move" feature: a mouse click at `(surfaceX, surfaceY)` becomes a world position.
- `Position WorldToSurface(Position worldPosition, double canvasWidth, double canvasHeight)`
  — `surface = ((world.X - origin.X) * ts, (world.Y - origin.Y) * ts)`. This is the foundation
  of the "GUI around game objects" feature: a world position becomes a canvas position to draw
  UI at.

The two conversions are inverses of each other within floating-point tolerance. With no map the
origin is `(0, 0)`, so `SurfaceToWorld(408, 408, 960, 960)` returns `(8.5, 8.5)` tiles at
`ts = 48`.

### Tiled model

`TileMap` is loaded from a Tiled `.tmx` file and **owns** the `TileSet`s its layers reference
(they are created when the map is loaded — there is no global tileset registry). Standalone
tilesets are loaded through `TileSet.Load` factories when needed.

The map exposes a read-only view of everything the Tiled file declares:

- `TileMap.Layers` — the **tile layers** only, in file order (bottom → top). Each
  `TileMapLayer` exposes its `Name`, `Visible`, `Opacity`, `TileIds`/`GetTileId`, the
  `AbovePlayer` flag (a layer whose `above_player` boolean custom property is `true` is
  rendered **after** the player), the `IsCollision` flag (a layer whose `is_collision` boolean
  custom property is `true` contains solid tiles) and its own custom `Properties`.
- `TileMap.Properties` / `TileMap.GetProperty(name)` — the **map's custom properties**,
  looked up case-sensitively. Properties are typed (`MapPropertyType`: bool/int/float/string/
  color/file/object/class) and boxed into C# values by `MapProperty`.
- `TileMap.ObjectLayers` — the **object layers** (and their objects), in file order. Each
  `TileMapObjectLayer` exposes its `Name`, `Visible`, `Opacity`, `Properties` and `Objects`;
  each `TileMapObject` exposes its `Id`, `Name`, `Type`, `Position`, `Width`/`Height`, `Shape`
  (`TileMapObjectShape`) and its own custom `Properties`. Object layers do not render tiles.
  Object-layer positions stay in **pixels** (they come straight from the Tiled file).

The read model wraps DotTiled, so no DotTiled types leak into the public API.

## Spritesheet layout (normative 12×8 grid, 8 characters)

Both **full** sheets and **part** sheets use the same normative RPG Maker MZ layout:

- Grid: **12 columns × 8 rows** of cells (the grid is normative; the image size is not).
- Cell size: **derived from the image** (`width / 12` × `height / 8`). The standard 576 × 384
  sheet yields 48 × 48 cells; a 936 × 864 sheet yields 78 × 108 cells.
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

- **Player**: full sheet `hero`, character index **1**, at **(6, 6) tiles**.
- **Villager**: part sheets `body` + `face` + `hair1`, character index **2**, at **(3, 4) tiles**.
- **Guard**: part sheets `body` + `face` + `armour` + `head`, character index **3**, at **(11, 8) tiles**.

```csharp
// Player — a single full sheet, character slot 1.
engine.Player.Position = new Position(6, 6);
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

Movement speeds are in **tiles per second**; a move of one second at the default speed
(`Player.DefaultBaseSpeed == 2`) travels exactly **2 tiles** (96 px with 48 px tiles).

The walk-cycle animation is **time-based and speed-scaled**: `Character.Update(dt)` advances the
walk cycle at a rate proportional to `BaseSpeed` and `AnimationCycleSpeed`. The walk cycle is the
bounce `0 → 1 → 2 → 1` (4 frame steps), and the time per frame is

```
secondsPerFrame = AnimationCycleSpeed / (BaseSpeed * FramesPerCycle)
```

so at `BaseSpeed == AnimationCycleSpeed == 2` (both in tiles/s, the defaults) one frame lasts
0.25 s and the cycle completes exactly **once per second** (4 frames/s). Doubling `BaseSpeed`
doubles the cycle rate; raising `AnimationCycleSpeed` (the reference speed) slows the animation
relative to movement speed.

## Collision

Tile maps can declare **collision layers**: a tile layer whose custom boolean property
`is_collision` is set to `true` (Tiled convention, mirroring `above_player`) contains **solid**
tiles that Characters (including the player) cannot walk through. `TileMapLayer.IsCollision`
surfaces the flag, and `TileMap.IsSolid(tileX, tileY)` returns `true` when *any* collision layer
has a non-empty tile (GID != 0) at that cell. The **map edge is solid**: `IsSolid` returns
`true` for out-of-bounds coordinates, so characters cannot leave the map through its edge. When
a map has no collision layer, every in-bounds cell is walkable. Non-collision layers never block
(a tile drawn from a normal layer is walkable even if it visually overlaps the character).

The engine resolves the player's movement with **axis-separated movement** against a footprint
in tile units:

- The footprint is the player's sprite size in pixels (`Character.GetSpriteSize`) converted to
  tiles (`px / ts`, where `ts` is the map's tile width), positioned at the player's top-left.
- `TileMap.IsAreaSolid(x, y, width, height)` (internal) tests the tiles overlapped by that
  tile-unit rectangle: the bounds are floored to the containing cells, and a rectangle that ends
  exactly on a tile boundary does not count the next tile.
- Each frame the engine applies the **X displacement first**, then reverts it if the resulting
  footprint overlaps a solid tile or leaves the map (the map edge is solid); it then applies the
  **Y displacement the same way**, starting from the horizontal result.

This keeps **wall-sliding** natural (a blocked axis reverts while the other axis still moves, so
a diagonal move into a wall slides along the wall on the free axis) and prevents **diagonal
corner-cutting** (each axis is resolved independently, so a diagonal cannot squeeze diagonally
through a corner). The existing map-bounds clamp (`ClampPlayerToMap`) remains as a safety net
for positions placed outside the map by other means.

NPCs are not moved by the engine (they have no AI yet), so collision resolution currently only
applies to the player; the public `TileMap.IsSolid` API is available for future NPC logic.
Per-tile collision shapes other than full-cell solidity, dynamic (runtime-mutable) collision
layers and one-way platforms are out of scope for this story.

## Pathfinding

Click-to-move (next story) needs to walk a character from its current tile to a clicked tile. The
engine's pathfinding is **tile-based** — it plans over integer tile coordinates, never
pixel-by-pixel — so it stays cheap and independent of the renderer.

`AStarPathfinder` (an **internal** static class in `RPGEngine`) exposes
`FindPath(start, goal, isWalkable, width, height)`, which returns the ordered tiles from `start`
(exclusive) to `goal` (inclusive), or an empty list when no path exists (including when start and
goal coincide, or the start/goal is blocked or out of bounds). It is deliberately decoupled from
`TileMap`: callers supply an `isWalkable(x, y)` predicate (the click-to-move story wires it to
`TileMap` solidity), so the algorithm is unit-testable without rendering. It adds no public API.

Movement is **8-direction** (cardinal + diagonal). A cardinal step costs `1`, a diagonal step costs
`√2`, and **corner cutting is prevented**: a diagonal step `(±1, ±1)` is allowed only when both
orthogonally adjacent cells are walkable. The heuristic is the **octile distance**
(`max(|dx|, |dy|) + (√2 − 1) · min(|dx|, |dy|)`), which is consistent, so the returned path is
optimal. The open set is a `PriorityQueue` keyed by `f = g + h`, and a tile is re-opened only when
a strictly better `g` is found.

## Rendering

`GameEngine.Render(canvas, dt)` draws a single frame in a fixed order:

```
below-player layers → NPCs → player → above-player layers
```

- When a `TileMap` is set, the canvas is **cleared to black first** — this is the black
  background behind and around the map.
- **Tile layers are prerendered once on load.** Each visible, non-empty tile layer is rasterized
  into its own `SKImage` of the map's pixel size (`TileMap.PixelWidth × PixelHeight`) when the
  map is loaded: the tiles are drawn at their world pixel positions with the flip transforms
  applied and the layer opacity baked into the layer alpha. Invisible and empty layers are not
  prerendered (a `null` slot). A `TileMap` is `IDisposable` and releases these prerendered
  images; the engine disposes the previous map when `GameEngine.Map` is replaced and when the
  engine itself is disposed.
- **Drawing is a per-layer image blit.** `TileMap.Draw` blits the prerendered images of the
  layers **below** the player (every layer whose `TileMapLayer.AbovePlayer` is `false`), each
  NPC and the player are drawn on top, and finally `TileMap.DrawAbovePlayer` blits the layers
  whose `above_player` custom property is `true` so those tiles appear **in front of** the
  player (e.g. tree canopies the player walks under). Each blit draws the intersection of the
  viewport with the layer image bounds, so viewport culling is preserved and no per-tile work
  happens per frame.
- **The camera works in tiles and renders in pixels.** `Render` computes the camera origin in
  tiles (follow + clamp, see above), derives a pixel viewport
  `(origin.X*ts, origin.Y*ts, origin.X*ts + canvasWidth, origin.Y*ts + canvasHeight)` and passes
  it to the map renderer (which stays a pure pixel renderer), and draws each character at the
  screen position `(pos.X*ts - origin.X*ts, pos.Y*ts - origin.Y*ts)`. The canvas size is read
  from the canvas clip bounds.
- When the map is **smaller than the canvas** on an axis it is **centered** in the canvas and
  the area around it stays black (`offset = max(0, (canvasSize − mapPixelSize) / (2*ts))` tiles),
  so a small map is never letterboxed with transparent or leftover pixels. When the map fills
  (or exceeds) the canvas, the behaviour is the classic follow-and-clamp camera.
