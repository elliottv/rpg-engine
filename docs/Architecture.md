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

- `Character.Position` — the **feet** position (the *middle-bottom* of the sprite, where the
  character stands), in **tiles**. The sprite is rendered above and centered on this point; its
  size is the configured sheet's derived cell size, in pixels, used only for clamping and the
  collision footprint.
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
its **feet** (anchor) screen position `(pos.X*ts - origin.X*ts, pos.Y*ts - origin.Y*ts)`. The
compositor anchors every sprite at its **middle-bottom**, so a character's sprite top-left is
at `(pos.X*ts - w/2 - origin.X*ts, pos.Y*ts - h - origin.Y*ts)` in world pixels (the sprite is
drawn above and centered on its feet).

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

There are **two ways a character moves**, and both go through the same `Character.Update(dt, map)`
(internal, `map` nullable) called by the engine's update loop every frame:

1. **Engine key input (the player).** `GameEngine.Update` combines every held bound key into a
   single **8-direction vector**: each key that is bound to a movement direction (via
   `GameConfig.GetDirection`) contributes its unit delta, the deltas are summed, normalized and
   quantized to the nearest of the eight `Direction` values. Opposite keys cancel (`W`+`S` or
   `A`+`D`), and a diagonal pair combines into a diagonal (`W`+`D` → up-right) at the same
   speed as cardinal movement (the diagonal deltas are normalized, magnitude 1). When no bound
   key is held the player stops and the animation snaps back to the standing frame. The engine
   reads `GameConfig` at input time and never caches a snapshot. The player's displacement is
   resolved (and collision-checked) by the engine itself, then handed to `Player.ReportMovement`.
2. **Autonomous movement (`Character.StartMoving` / `StopMoving`).** A host starts a character
   (typically an NPC in `GameEngine.Characters`) with `StartMoving(direction)`, which faces it
   and sets `IsMoving = true`. While `IsMoving`, every `Character.Update(dt, map)` moves the
   character towards its current `Direction` by `BaseSpeed * dt` tiles — exactly like the player
   while a movement key is held — until `StopMoving()` is called. The engine's update loop calls
   `Update(dt, map)` on every character each frame (supplying the current map), so a started
   character moves automatically with no per-frame host code.

**Autonomous movement is collision-resolved exactly like the player's key-driven movement**: the
engine passes the map to every character's `Update`, so a started character's displacement is
resolved against the map's solid tiles and the map edge with the same footprint and the same
per-axis slide-to-boundary (cardinal) / all-or-nothing (diagonal) semantics as the player (see
`MovementCollisionResolver.ResolveDisplacement`). NPCs therefore stop at walls and at the map
edge instead of walking through the world; a fully blocked character simply stays put (and its
walk cycle snaps to the standing frame). Do not combine `StartMoving` on the player's character
with the engine's key-driven player movement: `GameEngine.Update` calls
`Player.Character.Update(dt, map)` each frame, so both displacements would add up.
`StartMoving`/`StopMoving` target characters the host drives itself (NPCs in
`GameEngine.Characters`). The one-shot `Move(...)` displacement stays a raw, non-resolved
displacement (only the `StartMoving`/`Update` path is collision-resolved).

The walk-cycle animation detects movement the same way for both paths: `Character.Update(dt, map)`
compares `Position` to the previous update's position, so the cycle advances while a character is
moving (however it is being driven) and snaps back to the standing frame as soon as it stops —
including when a fully blocked character is resolved to the same position.

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

The engine resolves every character's movement (the player's key-driven and auto-walk movement
and every character's autonomous `StartMoving`/`Update` path) with **axis-separated movement**
against a footprint in tile units:

- The footprint is the **fixed 0.5×0.5-tile (24×24 px at 48 px tiles) lower-body box** of a
  character sprite, **anchored at the feet** (`Position` is the sprite's middle-bottom, and the
  middle of the feet sits at the bottom-centre of the box — `(12, 24)` when the box's origin is
  its upper-left). The rectangle is `x ∈ [pos.X - 0.25, pos.X + 0.25]`,
  `y ∈ [pos.Y - 0.5, pos.Y]` in tiles, **independent of the rendered sprite size**: a
  taller/wider spritesheet never widens or raises the box, so a **1-tile-wide corridor always
  fits** (the previous sprite-derived footprint could be wider than 1 tile for larger sprites,
  which stopped the player before the corridor entrance), and the feet always stop at the solid
  tile's edge whether the tile is below, above or beside the character. The player and every NPC
  share this footprint (the constants live on `MovementCollisionResolver`, the footprint
  authority).
- `TileMap.IsAreaSolid(x, y, width, height)` (internal) tests the tiles overlapped by that
  tile-unit rectangle: the bounds are floored to the containing cells, and a rectangle that ends
  exactly on a tile boundary does not count the next tile.
- Each frame the engine applies the **X displacement first**, then the **Y displacement** the same
  way, starting from the horizontal result. On each axis the displacement uses **per-axis
  slide-to-boundary clamping** (see `MovementCollisionResolver`): when the destination footprint
  is clear the full requested displacement is applied; otherwise the axis slides to the
  **closest legal position on that axis**, so the leading edge of the footprint stops **exactly**
  at the near edge of the first blocking solid tile (or at the map edge, which is solid). With
  the 0.5×0.5 box (half-width `hw = 0.25`, height above the feet `heightAboveFeet = 0.5`), the exact
  boundaries are: moving **right**, the right edge stops at `x = c - hw` (first solid gained
  column `c`; the right map edge is `c = Width`); moving **left**, the left edge stops at
  `x = c + 1 + hw` (last solid gained column `c`; the left map edge is `c = -1`); moving **down**,
  the feet stop at `y = r` (first solid gained row `r`; the bottom map edge is `r = Height`);
  moving **up**, the top edge stops at `y = r + 1 + heightAboveFeet` (last solid gained row `r`;
  the top map edge is `r = -1`). Because a blocked axis slides to the exact boundary instead of
  reverting the whole step, the **feet stop exactly at the solid tile's edge** (or the map edge)
  — matching click-to-move — with no one-frame-step gap and no floating-point overshoot
  accumulation, in every direction (not just downward).
- The per-axis gained-range scan assumes the starting footprint is legal; as a safety net the
  resolver re-validates the resulting footprint with `TileMap.IsAreaSolid` and **refuses the
  displacement** (returning the starting position) if it would still overlap a solid tile — this
  can only happen when the starting footprint was already illegal (e.g. left embedded in a wall),
  and it guarantees key movement never moves the player through or deeper into a solid tile. A
  move that clears the overlap (escaping the wall) is still allowed.

This keeps **wall-sliding** natural (a blocked axis clamps to the boundary while the other axis
still moves, so a diagonal move into a wall slides along the wall on the free axis) and prevents
**diagonal corner-cutting** (each axis is resolved independently, so a diagonal cannot squeeze
diagonally through a corner). After the resolution the engine reports the outcome to the player: a
move with **no net displacement** (fully blocked on every axis, e.g. walking straight into a wall
or into a corner) is reported as a **collision stop** through `Player.ReportBlockedMove`, so
`Player.OnStopMoving` fires even while the movement key is held against the wall (exactly once,
with the direction the player tried to move in); the start of the move is reported *before* the
displacement through `Player.ReportMovement`, so `Player.OnStartMoving` fires when the player
begins moving in a new direction (idle → moving, or a direction change while moving — e.g.
pressing a second key makes the effective direction a diagonal) and a blocked move from idle
fires start then stop in the same frame. Any move that actually displaced the player (including
a diagonal whose full displacement was clear) reports only the start through
`Player.ReportMovement` as before. The map-bounds clamp (`ClampPlayerToMap`) keeps the **fixed
0.5×0.5 box** inside the map for the player (the feet clamp to
`x ∈ [0.25, max(0.25, Map.Width - 0.25)]`,
`y ∈ [0.5, max(0.5, Map.Height)]`) and remains as the player-only post-move safety net for
positions placed outside the map by other means; NPCs are kept in bounds by the solid map edge
through their resolved autonomous movement (see `TileMap.IsSolid`). The map edge is solid.

Autonomous movement of **every** character (the `StartMoving`/`Update` path) is collision-resolved
against the map's solid tiles and the map edge exactly like the player's key-driven movement, via
`MovementCollisionResolver.ResolveDisplacement` — NPCs added to `GameEngine.Characters` with
`StartMoving` stop at walls and at the map edge instead of walking through the world.
Character-vs-character collision (characters blocking each other), per-tile collision shapes other
than full-cell solidity, dynamic (runtime-mutable) collision layers and one-way platforms are out
of scope for this story.

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


## Click-to-move and auto-walk

Click-to-move wires the three pieces above — the surface↔world camera, `TileMap.IsSolid` and
`AStarPathfinder` — into one feature: the host passes a click to `GameEngine.Click`, and the
engine walks the player to the clicked tile.

**The click pipeline** (`GameEngine.Click(surfaceX, surfaceY)`):

1. The host reports a click on the **main** game canvas in host-surface (canvas) pixels, the
   same coordinate space as `SurfaceToWorld`. The engine converts it with the canvas size
   recorded by the most recent `Render` (unknown size — no render yet — ⇒ the click is
   ignored). Without a map the click cancels any in-progress walk and does nothing else.
2. The target tile is `floor(world)` of the converted position.
3. If the target tile is solid (`TileMap.IsSolid`) the current auto-walk is **cancelled** and the
   player does not move.
4. Otherwise the engine computes
   `AStarPathfinder.FindPath(playerTile, targetTile, (x, y) => !Map.IsSolid(x, y), Map.Width,
   Map.Height)`. An empty path (start == goal, or the target is unreachable) also cancels the
   walk without moving; a non-empty path **replaces** the current auto-walk path, even mid-walk.

**Auto-walk** (inside `GameEngine.Update`): the engine keeps an internal queue of target tile
waypoints (the A* path). When there is **no manual key movement** this frame and the path is
non-empty, it moves the player toward the center of the next waypoint tile
(`(tileX + 0.5, tileY + 0.5)`) at `BaseSpeed` (tile units). When the distance to the waypoint
center is ≤ the frame's step, the player snaps to the center, the waypoint is popped, and the
walk continues; when the queue empties, the engine calls `Player.Stop()`. The path is computed
over walkable tiles (no corner cutting), so between tile centres the movement is clear; to cover
the case where the player starts a walk from a **non-tile-centred** position (e.g. a key-movement
boundary beside a wall), each auto-walk displacement is resolved with the **same per-axis
slide-to-boundary clamping** as key movement (see `MovementCollisionResolver`). A displacement
that would cross a solid corner is clamped, and because the waypoint then cannot be reached
without crossing a solid tile, the walk is **cancelled** and the player is not displaced — the
auto-walk never moves the player through or into a solid tile.

**Input precedence during auto-walk**:

- A **key press** (`Input(key, true)`) cancels the auto-walk path; a key **release** does not.
- While a bound movement key is held, manual movement takes priority and the auto-walk does not
  advance (the press has already cleared the path).
- A `Click` always **replaces** the path (even mid-walk); an invalid click (solid tile or no
  path) **cancels** it.

**Movement-state events**: `Player` exposes `OnStartMoving` and `OnStopMoving`
(`EventHandler<Direction>`, carrying only the facing `Direction` — the old
`PlayerMoveEventArgs` wrapper was removed). `OnStartMoving` fires **exactly** when the player
begins moving in a new direction and **before** the position is updated: on the first frame a
movement key takes effect (idle → moving) for move-by-key, on **direction changes while
moving** (e.g. pressing a second key makes the effective direction a diagonal, or releasing one
key of a held diagonal pair reverts to the remaining cardinal), and once **per auto-walk step**
(per waypoint, i.e. the first step and every time the next waypoint is reached while another
remains) for click-to-move — and never on every frame (a same-direction move while moving raises
nothing). `OnStopMoving` fires when every movement key is released (the player goes idle), when
the last auto-walk step is reached (the path completes), and when the player is blocked by a
collision. The engine drives the events through the internal bridges `Player.ReportMovement`
(key movement, start on idle → moving or on a direction change while moving, called before the
displacement), `Player.ReportAutoWalkStep` (auto-walk, start every call, once per step boundary
before that step's position update) and `Player.ReportBlockedMove` (a collision stop), so a fully
blocked move from idle fires start then stop in the same frame while a held key against the same
wall fires nothing more. `Player.Stop()` raises `OnStopMoving` only when the player was moving;
the engine calls it when there is no input and no auto-walk target.

## Rendering

`GameEngine.Render(canvas, dt)` draws a single frame in a fixed order:

```
below-player layers → characters (NPCs + player) sorted by Y → above-player layers
```

All characters — every NPC in `GameEngine.Characters` **and the player** — are drawn in a
single pass sorted by `Position.Y` ascending: a character with a **higher** `Position.Y` (lower
on the screen, closer to the viewer) is drawn **last** and appears on top of the others, so a
character lower on the screen occludes one higher up. The player is part of this ordering and
may be drawn **behind** other characters when its Y is lower. `OrderBy` is stable, so equal-Y
characters keep their relative order (NPCs in `Characters`-list order, then the player) and the
draw order is deterministic. `RenderMinimap` is unchanged: its dots have no Y ordering.

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
  layers **below** the player (every layer whose `TileMapLayer.AbovePlayer` is `false`), then
  all characters (the NPCs in `GameEngine.Characters` and the player) are drawn on top, sorted
  by `Position.Y` ascending (a higher Y is drawn last / on top, including the player), and
  finally `TileMap.DrawAbovePlayer` blits the layers whose `above_player` custom property is
  `true` so those tiles appear **in front of** every character (e.g. tree canopies the player
  walks under). Each blit draws the intersection of the viewport with the layer image bounds, so
  viewport culling is preserved and no per-tile work happens per frame.
- **The camera works in tiles and renders in pixels.** `Render` computes the camera origin in
  tiles (follow + clamp, see above), derives a pixel viewport
  `(origin.X*ts, origin.Y*ts, origin.X*ts + canvasWidth, origin.Y*ts + canvasHeight)` and passes
  it to the map renderer (which stays a pure pixel renderer), and draws each character at its
  **feet** (middle-bottom anchor) screen position
  `(pos.X*ts - origin.X*ts, pos.Y*ts - origin.Y*ts)`; the sprite is drawn above and centered on
  that point. The canvas size is read from the canvas clip bounds.
- When the map is **smaller than the canvas** on an axis it is **centered** in the canvas and
  the area around it stays black (`offset = max(0, (canvasSize − mapPixelSize) / (2*ts))` tiles),
  so a small map is never letterboxed with transparent or leftover pixels. When the map fills
  (or exceeds) the canvas, the behaviour is the classic follow-and-clamp camera.

## Animated tiles

`TileMap` and `TileSet` support **animated tiles** as defined by the Tiled format: a tileset tile
may declare

```xml
<tile id="5">
  <animation>
    <frame tileid="5" duration="100"/>
    <frame tileid="6" duration="100"/>
    <frame tileid="7" duration="100"/>
  </animation>
</tile>
```

Each `<frame>` references a **local tile ID** within the tileset and a **duration in
milliseconds**. Every layer cell that uses such a tile plays the frame sequence, looping forever.
This is parsed at load time into an internal `TileAnimation` (frames in file order plus the total
cycle duration) keyed by local tile ID on the owning `TileSet`.

- **An internal clock.** `TileMap` keeps an animation clock in seconds. `GameEngine.Update`
  calls `TileMap.UpdateAnimations(dt)` once per frame, so animated tiles advance with game time.
  The current frame of a cell is derived from the clock with `elapsedMs % TotalDurationMs` and a
  short walk over the frames (`GetFrameTileId`); a sequence whose total duration is zero is
  treated as its first frame only.
- **Animated cells are detected per layer at load time.** For every non-empty cell the owning
  tileset and local tile ID are resolved (the same `ResolveTileSet` logic used for prerendering)
  and cells whose tile declares an animation are recorded as `AnimatedTileCell`s.
- **They are excluded from the prerendered layer images.** A static prerendered `SKImage` cannot
  bake an animation, so `PrerenderLayer` leaves animated cells transparent. The render passes
  (`DrawLayerImages`, both the below- and above-player passes) draw each layer's animated cells
  **on top of that layer's own prerendered image**, at the cell rect, applying the layer's flip
  flags (`TileMapLayer.GetTileFlags`) and the layer's `Opacity` (via the paint alpha) with the
  same `DrawTile` transform used for static tiles. Because the animated cells are drawn inside
  the per-layer loop, they stay above their own layer and below the layers above, preserving the
  layer z-order.
- **The minimap shows them too.** `GameEngine.RenderMinimap` draws each layer's animated cells
  after its prerendered blit using the same scale/origin mapping as the layer blits, so the
  minimap is not left with holes where animated tiles are.
- **Performance note.** Frame images are resolved per frame (`GetAnimatedTileId` →
  `TileSet.GetTileImage`); a later optimization can cache the per-tile frame images. This is
  acceptable for the first implementation because animation sequences are short and maps
  typically contain few animated cells.

## Minimap

`GameEngine.RenderMinimap(canvas, zoomLevel)` renders a **minimap** of the current map onto a
canvas separate from the main game canvas. It is a pure renderer: it never mutates engine state,
and it reuses the same prerendered layer images the main render path uses.

- **What is drawn.** Every visible tile layer's prerendered `SKImage` — both the below- and the
  above-player layers, in file order bottom → top, because a minimap shows the full picture (the
  `above_player` distinction only matters when characters are drawn between the two passes). On
  top of the map, a **green dot** marks the player and a **yellow dot** marks each NPC in
  `Characters`, drawn as small filled circles at their world positions converted to map pixels
  (`Position * tileWidth`) then scaled to the canvas.
- **Zoom semantics.** `zoomLevel` is relative to the "fit the whole map" view:
  - `1.0` (the default) fits the entire map into the canvas, centered, with the aspect ratio
    preserved.
  - `> 1` zooms in: the map is drawn larger than the canvas and the view pans around the
    player's dot, clamped to the map edges — the same follow-and-clamp behaviour as the main
    camera.
  - `0 < zoomLevel < 1` zooms out further (the map is drawn smaller with larger black margins).
  - `<= 0` throws `ArgumentOutOfRangeException`.
- **Layout.** The base fit scale is
  `baseFit = min(canvasWidth / Map.PixelWidth, canvasHeight / Map.PixelHeight)` and the effective
  scale is `scale = baseFit * zoomLevel`, so the aspect ratio is preserved by construction (one
  scale for both axes). When the whole scaled map fits in the canvas it is centered and drawn
  entirely; when zoomed in, the visible region is `(canvasWidth / scale, canvasHeight / scale)`
  map pixels, centered on the player's position in map pixels and clamped inside
  `(0, 0, PixelWidth, PixelHeight)`. Each layer is blitted with `canvas.DrawImage` where the
  **source** rect is the visible region ∩ layer bounds (in map pixels) and the **dest** rect is
  the same region scaled by `scale` and offset by the centering/pan origin. Dots outside the
  visible region are skipped.
- **Background.** When a map is set the method first clears the whole canvas to **black** (like
  the main camera in `Render`), then draws the map and dots on top, so a map smaller than the
  canvas is centered on a black background and the unused margins are black — the minimap's
  background matches the main camera's black background. With no map it is a no-op and the canvas
  is left untouched.

The minimap's camera is the same follow + clamp + center model as the main camera
(`ComputeCameraOrigin`), expressed in map pixels instead of tiles: per axis,
`origin = clamp(playerPx − visibleSize/2, 0, max(0, mapPixelSize − visibleSize)) −
max(0, (canvasSize − scaledMapSize) / (2*scale))`. The first clamp keeps the view inside the map;
the subtracted term centers the map when it is smaller than the visible region on that axis.
