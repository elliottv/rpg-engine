# GameEngine

Namespace: `RPGEngine` — the root of the engine.

`GameEngine` owns the game state (player, characters, map and configuration), the spritesheet
registry and the pressed-keys state, and exposes the game-loop entry points `Update`, `Render`,
`Input` and the asset-loading methods used by the host application.

## Remarks

- The game loop is written by the host: each frame the host calls `Update` with its own elapsed
  time (`dt`, in seconds), then `Render` with the same `dt` to draw the frame. The engine never
  runs its own loop and never blocks.
- Rendering issues SkiaSharp canvas/image drawing operations only; it never rasterizes the final
  output to a CPU bitmap. When the host passes a GPU-backed `SKCanvas` (e.g. the surface of a
  SkiaSharp GL view or a WebAssembly `SKSurface` created from a `GRContext`), the drawing is
  hardware accelerated. The engine has **zero platform-specific dependencies**.
- **All world coordinates are in tiles** (double): `Player.Position`, `Character.Position`,
  camera origins and the `SurfaceToWorld`/`WorldToSurface` conversions. Pixels are produced only
  at the canvas boundary — `Render` multiplies the tile positions by the map's tile size to
  place sprites and the camera viewport (the tile size is read from `TileMap.TileWidth`, with a
  default of 48 when no map is set).
- The camera is internal: `Render` follows the player and clamps the viewport inside the map.
  When the map is smaller than the canvas on an axis it is centered and the area around it is
  filled with black, so the map is never letterboxed with transparent or leftover pixels.
  `SurfaceToWorld` and `WorldToSurface` expose the same follow + clamp camera for the given
  canvas size — the foundation the "click to move" and "GUI around game objects" features will
  build on.
- When a map is set, `Render` clears the whole canvas to black first, then draws the map's
  below-player layers, then every NPC, then the player, and finally the map's `above_player`
  layers (tile layers declaring the Tiled `above_player` custom property) so those tiles appear
  on top of the player. Without a map the canvas is left untouched and only the characters are
  drawn.
- Movement input combines every held bound key into a single 8-direction vector: opposite keys
  cancel (`W`+`S` or `A`+`D`), and a diagonal pair combines into a diagonal (`W`+`D` → up-right)
  at the same speed as cardinal movement (see [Architecture](../Architecture.md)).
- **Click-to-move** (auto-walk): `Click(surfaceX, surfaceY)` converts a host-surface click on the
  main canvas (using the canvas size recorded by the most recent `Render`) to a world position,
  computes an **A*** tile path from the player's tile to the clicked tile over the non-solid
  tiles, and queues those tiles. Each `Update` then moves the player toward the center of the next
  waypoint at `BaseSpeed`, popping waypoints as they are reached and calling `Player.Stop()` when
  the path completes. Clicking a **solid tile** or an **unreachable target** cancels the walk
  without moving; a click that yields a path **replaces** the current walk even mid-walk. Each
  auto-walk displacement is resolved against the map's solid tiles like key movement, so the
  auto-walk never moves the player through (or into) a solid tile: when the direct displacement
  toward a waypoint is blocked (e.g. the player is not tile-centred and the first segment would
  cross a solid corner), the walk is cancelled and the player is not displaced.
- **Input precedence during auto-walk**: a **key press** (`Input(key, true)`) cancels the
  auto-walk path; a key **release** does not; and while a bound movement key is held the
  auto-walk does not advance (manual key movement takes priority). A `Click` always replaces the
  path unless the new target is invalid (solid / no path), in which case it cancels the walk.
  See [Architecture](../Architecture.md).
- When a map is set, the player's displacement is resolved with **axis-separated movement and
  per-axis slide-to-boundary clamping** against the map's solid tiles (layers declaring the Tiled
  `is_collision` bool property): each axis is applied in turn and, when the player's collision
  footprint would overlap a solid tile or leave the map (the map edge is solid), it slides to
  the **closest legal position on that axis** — so the leading edge of the footprint stops
  **exactly** at the near edge of the first blocking solid tile (or at the map edge). The
  footprint is the **fixed 1×1 tile (48×48 px) lower-body box anchored at the feet**
  (`Position` is the middle-bottom of the sprite; the middle of the feet sits at the bottom-centre
  of the box, `(24, 48)` when the box's origin is its upper-left). The box is independent of the
  rendered sprite size, so a 1-tile-wide corridor always fits and the feet stop exactly at the
  solid tile's edge in every direction — below, above or beside the player; the map-bounds clamp
  keeps that 1×1 box inside the map. Because a blocked axis slides to the exact boundary instead
  of reverting the whole step, the feet stop exactly at the solid tile's edge, matching
  click-to-move, with no one-frame-step gap and no floating-point overshoot accumulation. As a
  safety net the resolver refuses a displacement whose resulting footprint would still overlap a
  solid tile (only possible when the starting footprint was already illegal, e.g. embedded in a
  wall), so key movement never moves the player through a solid tile. See
  [Architecture](../Architecture.md).
- A minimap can be rendered on a separate surface with `RenderMinimap`: it draws the map's
  prerendered tile layers, a green dot for the player and a yellow dot for each NPC.
  `zoomLevel` `1.0` fits the whole map to the canvas; values above `1` zoom in and pan around
  the player's dot (clamped to the map edges, like the main camera); values between `0` and `1`
  zoom out further. When a map is set the minimap clears its canvas to **black** first (like
  `Render`) and draws the map and dots on top, so the unused margins are black.
- The engine is **`IDisposable`**: it owns the assigned map and disposes it when `Map` is
  replaced or when the engine itself is disposed (a `TileMap` is disposable because it
  prerenders each tile layer into an `SKImage` on load).
- Tile sets are not loaded through the engine: a `TileMap` owns the tilesets its layers
  reference. Standalone tilesets are loaded directly through the `TileSet.Load` factories.

## Constructors

### `GameEngine()`

Initializes a new instance with a fresh `Player`, an empty `Characters` list, a `GameConfig`
with the default WASD bindings, no map and an empty spritesheet registry.

```csharp
var engine = new GameEngine();
```

## Properties

### `Player Player`

Gets the player character. The camera always follows the player.

```csharp
var engine = new GameEngine();
engine.Player.Position = new Position(2, 2);
engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
```

### `IList<Character> Characters`

Gets the mutable list of NPC characters present in the game world. The player is never in this
list (it is rendered separately, on top).

```csharp
var npc = new Character { Position = new Position(3, 4) };
npc.SpriteSheets.Add(new SpriteSheetRef("villager_body", CharacterIndex: 2));
engine.Characters.Add(npc);
```

### `TileMap? Map`

Gets or sets the tile map to be displayed, or `null` when no map is loaded. When changed, the
next `Render` uses the new map immediately. The engine **owns** the assigned map: replacing the
value disposes the previous map, and `Dispose()` releases the current one.

```csharp
engine.Map = TileMap.Load("assets/map.tmx");
engine.Map = TileMap.Load("assets/other.tmx"); // the first map is disposed here
```

### `GameConfig Config`

Gets or sets the configuration values used by the engine. The engine reads the configuration at
input time and never caches a snapshot, so updates take effect immediately.

```csharp
engine.Config.UpKey = Key.Up; // rebind movement up to the up-arrow key
```

## Methods

### `void Input(Key key, bool isPressed)`

Reports a key event to the engine. `true` presses the key; `false` releases it. The engine keeps
a set of currently pressed keys and derives the movement direction in `Update` via
`GameConfig.GetMovementDirection`, which combines all held bound keys into one of the eight
directions. Host applications translate their framework's key events to a `Key` value before
calling this method.

A **key press** (`isPressed: true`) cancels any in-progress auto-walk; a key **release** does
not. The walk is replaced by the next `Click`, or the player simply stops on the next `Update`
when no movement key is held.

```csharp
engine.Input(Key.D, isPressed: true);   // key-down (also cancels any auto-walk)
engine.Update(dt);
engine.Input(Key.D, isPressed: false);  // key-up
```

### `void Update(double dt)`

Advances the simulation by `dt` seconds. Manual key movement takes priority over auto-walk:
when a bound movement key is held the player moves in that direction (in tiles, resolving
collisions against the map's solid tiles with axis-separated movement when a map is set).
Otherwise, when an auto-walk path is queued (from `Click`), the player walks toward the center
of the next waypoint tile at `BaseSpeed`, popping waypoints as they are reached and calling
`Player.Stop()` when the path completes. When there is no key input and no auto-walk target the
player stops. The player is then clamped inside the map and the walk-cycle animation of the
player and every NPC advances.

```csharp
engine.Update(dt: 1.0 / 60);
```


### `void Click(double surfaceX, double surfaceY)`

Reports a **click on the main game canvas** to the engine, in host-surface (canvas) coordinates
— the same coordinate space as `SurfaceToWorld`. The engine converts the click to a world
position using the canvas size recorded by the most recent `Render`, computes an **A*** tile
path from the player's tile to the clicked tile over the non-solid tiles, and queues it for
auto-walk (see the class remarks and [Architecture](../Architecture.md)).

- If no `Render` has happened yet (the recorded canvas size is zero) the click is **ignored**.
- Without a map the click cancels any in-progress auto-walk and does nothing else.
- The target tile is `floor(world)` of the converted position. Clicking a **solid tile**
  (`TileMap.IsSolid`) cancels any current auto-walk without moving.
- Otherwise the engine computes `AStarPathfinder.FindPath(playerTile, targetTile, (x, y) =>
  !Map.IsSolid(x, y), Map.Width, Map.Height)`. An **empty path** (start == goal, or the target
  is unreachable) also cancels the walk without moving.
- A **valid path replaces** the current auto-walk path, even mid-walk: the player changes course
  toward the new target without stopping first.

```csharp
// Render at least once so the engine knows the canvas size, then translate a mouse click.
engine.Render(canvas, dt: 1.0 / 60);

// Click at the canvas position of the tile the host wants the player to walk to.
double surfaceX = 264; // e.g. a mouse position in canvas pixels
double surfaceY = 264;
engine.Click(surfaceX, surfaceY);

// The player now auto-walks along the A* path; drive the loop normally.
engine.Update(dt: 1.0 / 60);
```

### `void Render(SKCanvas canvas, double dt)`

Draws one frame onto the canvas. When a map is set the canvas is cleared to black first (the
black background behind/around a map smaller than the canvas), then the map's below-player
layers are drawn, then every NPC, then the player on top, and finally the map's `above_player`
layers so those tiles appear above the player. The camera follows the player, centers a map
smaller than the canvas, and is clamped so the viewport stays inside the map; the canvas size is
read from the canvas clip bounds. The camera origin is computed in tiles and converted to a
pixel viewport for the map and to pixel screen positions for the characters.

```csharp
using var bitmap = new SKBitmap(640, 480);
using (var canvas = new SKCanvas(bitmap))
{
    canvas.Clear(SKColors.Transparent);
    engine.Render(canvas, dt: 1.0 / 60);
}
```

### `void RenderMinimap(SKCanvas canvas, double zoomLevel)`

Draws a **minimap** of the current map onto `canvas`, a surface separate from the main game
canvas. It renders the map's prerendered tile layers (both below- and above-player layers, in
file order bottom → top — a minimap shows the full picture), a **green dot** for the player and a
**yellow dot** for each NPC in `Characters`. The canvas size is read from the canvas clip bounds,
the same convention as `Render`.

**Zoom semantics** (`zoomLevel`, relative to the "fit the whole map" view):

- `1.0` (the default) **fits the entire map** into the canvas, centered, with the aspect ratio
  preserved and the unused margins left black.
- `> 1` **zooms in**: the map is drawn larger than the canvas and the view pans around the
  player's dot, clamped to the map edges (like the main camera).
- `0 < zoomLevel < 1` zooms out further.
- `<= 0` throws `ArgumentOutOfRangeException`.

The base fit scale is `min(canvasWidth / Map.PixelWidth, canvasHeight / Map.PixelHeight)` and the
effective scale is `baseFit * zoomLevel`. When the whole scaled map fits, it is centered; when
zoomed in, the visible region (`canvasWidth / scale` × `canvasHeight / scale` map pixels) is
centered on the player's position in map pixels (`Player.Position * ts`) and clamped inside the
map bounds. With no map it is a **no-op** (the canvas is left untouched). When a map is set it
first **clears the canvas to black** (like `Render`), then draws the map and dots on top, so a map
smaller than the canvas is centered on a black background and the unused margins are black. It is
a pure render (it never mutates engine state).

```csharp
// Default fit: the whole map is drawn into the minimap canvas, centered, aspect preserved; the
// unused margins are black (the minimap clears its canvas to black when a map is set).
using var minimap = new SKBitmap(240, 240);
using (var canvas = new SKCanvas(minimap))
{
    canvas.Clear(SKColors.Transparent);
    engine.RenderMinimap(canvas, zoomLevel: 1.0);
}

// Zoomed in: the view is centered on the player's green dot and clamps at the map edges (like
// the main camera), so only the region around the player is visible.
using var zoomed = new SKBitmap(240, 240);
using (var canvas = new SKCanvas(zoomed))
{
    canvas.Clear(SKColors.Transparent);
    engine.RenderMinimap(canvas, zoomLevel: 4.0);
}
```

### `Position SurfaceToWorld(double surfaceX, double surfaceY, double canvasWidth, double canvasHeight)`

Translates a host-surface (canvas) coordinate, in pixels, to a world coordinate, in tiles, using
the same camera as `Render` (follow + clamp) for the given canvas size:
`world = (surfaceX / ts + origin.X, surfaceY / ts + origin.Y)`, where `ts` is the map's tile
width (48 when no map is set) and `origin` is the camera origin for that canvas size. This is
the inverse of `WorldToSurface` within floating-point tolerance. Hosts use it to translate mouse
clicks into world coordinates for "click to move".

```csharp
// With no map the camera origin is (0,0): a surface point of (408, 408) on a 960×960 canvas is
// world (8.5, 8.5) tiles at ts = 48.
var world = engine.SurfaceToWorld(408, 408, 960, 960); // (8.5, 8.5)
```

### `Position WorldToSurface(Position worldPosition, double canvasWidth, double canvasHeight)`

Translates a world coordinate, in tiles, to a host-surface (canvas) coordinate, in pixels, using
the same camera as `Render` (follow + clamp) for the given canvas size:
`surface = ((world.X - origin.X) * ts, (world.Y - origin.Y) * ts)`, where `ts` is the map's tile
width (48 when no map is set) and `origin` is the camera origin for that canvas size. This is
the inverse of `SurfaceToWorld` within floating-point tolerance. Hosts use it to position GUI
elements around game objects.

```csharp
var surface = engine.WorldToSurface(new Position(8.5, 8.5), 960, 960); // (408, 408)
```

### `void Dispose()`

Releases the engine's resources: the current map (if any) is disposed, which releases its
prerendered layer images. Replacing the map through `Map` already disposes the previous map, so
hosts only need to call this when the engine itself is being torn down. Safe to call more than
once.

```csharp
using var engine = new GameEngine { Map = TileMap.Load("assets/map.tmx") };
// ... game loop ...
// engine.Dispose() runs at the end of the using block and disposes the map.
```

### `void LoadSpriteSheet(string name, string path)`

Loads a full character spritesheet from a file path and registers it under `name`. Throws when
the name is already loaded, the image cannot be decoded, or its dimensions do not form a
valid 12×8 grid (positive width divisible by 12 and positive height divisible by 8).

```csharp
engine.LoadSpriteSheet("hero", "assets/characters/character_full.png");
```

### `void LoadSpriteSheet(string name, Stream stream)`

Loads a full character spritesheet from a stream (the file-system-free entry point, e.g.
WebAssembly builds where assets are fetched over HTTP). The caller remains the owner of the
stream.

```csharp
using var stream = new MemoryStream(await http.GetByteArrayAsync("assets/character_full.png"));
engine.LoadSpriteSheet("hero", stream);
```

### `void LoadPartSpriteSheet(string name, string path, CharacterPartType partType)`

Loads a **part** character spritesheet of layer `partType` from a file path. Part sheets are
composed in the fixed RPG Maker MZ order (see [Architecture](../Architecture.md)).

```csharp
engine.LoadPartSpriteSheet("villager_body", "assets/characters/character_part_body.png", CharacterPartType.Body);
```

### `void LoadPartSpriteSheet(string name, Stream stream, CharacterPartType partType)`

Loads a **part** character spritesheet of layer `partType` from a stream (the
file-system-free entry point). The caller remains the owner of the stream.

```csharp
using var stream = new MemoryStream(await http.GetByteArrayAsync("assets/character_part_body.png"));
engine.LoadPartSpriteSheet("villager_body", stream, CharacterPartType.Body);
```

### `Task LoadSpriteSheetAsync(string name, Stream stream)`

The asynchronous counterpart of `LoadSpriteSheet(string, Stream)` for streams that only support
asynchronous reads (e.g. certain network/browser streams). The caller remains the owner of the
stream.

```csharp
using var stream = new MemoryStream(await http.GetByteArrayAsync("assets/character_full.png"));
await engine.LoadSpriteSheetAsync("hero", stream);
```

### `Task LoadPartSpriteSheetAsync(string name, Stream stream, CharacterPartType partType)`

The asynchronous counterpart of `LoadPartSpriteSheet(string, Stream, CharacterPartType)` for
streams that only support asynchronous reads. The caller remains the owner of the stream.

```csharp
using var stream = new MemoryStream(await http.GetByteArrayAsync("assets/character_part_body.png"));
await engine.LoadPartSpriteSheetAsync("villager_body", stream, CharacterPartType.Body);
```

### `bool SpriteSheetExists(string name)`

Returns whether a spritesheet is registered under `name`. Full sheets (loaded with
`LoadSpriteSheet`) and part sheets (loaded with `LoadPartSpriteSheet`) share one registry, so
both are visible to this check — it is the safe way to test whether a name is already in use
without catching the `KeyNotFoundException` thrown by the render path. The check is
case-sensitive and trims surrounding whitespace from `name` before the lookup, matching how
sheets are registered; a `null` name throws `ArgumentNullException`.

```csharp
// false before loading, true after: full sheets are visible to the check.
engine.LoadSpriteSheet("hero", "assets/characters/character_full.png");
var hasHero = engine.SpriteSheetExists("hero"); // true
var hasVillager = engine.SpriteSheetExists("villager"); // false

// Part sheets are visible too: a "hair" part sheet registers under "hair".
engine.LoadPartSpriteSheet("hair", "assets/characters/character_part_hair1.png", CharacterPartType.Hair1);
var hasHair = engine.SpriteSheetExists("hair"); // true

// Case-sensitive and trimmed: "Hero" is not "hero", but " hero " is.
var hasHeroDifferentCase = engine.SpriteSheetExists("Hero"); // false
var hasHeroTrimmed = engine.SpriteSheetExists(" hero ");     // true

// null throws ArgumentNullException.
engine.SpriteSheetExists(null); // ArgumentNullException
```

## Full example ("hello world")

```csharp
var engine = new GameEngine();
engine.Map = TileMap.Load("assets/map.tmx");
engine.LoadSpriteSheet("hero", "assets/characters/character_full.png");
engine.Player.Position = new Position(6, 6);
engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

engine.LoadPartSpriteSheet("body", "assets/characters/character_part_body.png", CharacterPartType.Body);
var npc = new Character { Position = new Position(3, 4) };
npc.SpriteSheets.Add(new SpriteSheetRef("body", CharacterIndex: 2));
engine.Characters.Add(npc);

engine.Input(Key.D, isPressed: true);
engine.Update(1.0 / 60);
engine.Input(Key.D, isPressed: false);
```
