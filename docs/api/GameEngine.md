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

```csharp
engine.Input(Key.D, isPressed: true);   // key-down
engine.Update(dt);
engine.Input(Key.D, isPressed: false);  // key-up
```

### `void Update(double dt)`

Advances the simulation by `dt` seconds: resolves the movement direction from the currently
pressed keys, moves the player (in tiles), clamps it inside the map, and advances the
walk-cycle animation of the player and every NPC.

```csharp
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
