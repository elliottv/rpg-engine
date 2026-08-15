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
- The camera is internal: `Render` follows the player and clamps the viewport inside the map.
- Movement input combines every held bound key into a single 8-direction vector: opposite keys
  cancel (`W`+`S` or `A`+`D`), and a diagonal pair combines into a diagonal (`W`+`D` → up-right)
  at the same speed as cardinal movement (see [Architecture](../Architecture.md)).
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
engine.Player.Position = new Position(96, 96);
engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
```

### `IList<Character> Characters`

Gets the mutable list of NPC characters present in the game world. The player is never in this
list (it is rendered separately, on top).

```csharp
var npc = new Character { Position = new Position(144, 192) };
npc.SpriteSheets.Add(new SpriteSheetRef("villager_body", CharacterIndex: 2));
engine.Characters.Add(npc);
```

### `TileMap? Map`

Gets or sets the tile map to be displayed, or `null` when no map is loaded. When changed, the
next `Render` uses the new map immediately.

```csharp
engine.Map = TileMap.Load("assets/map.tmx");
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
pressed keys, moves the player, clamps it inside the map, and advances the walk-cycle animation
of the player and every NPC.

```csharp
engine.Update(dt: 1.0 / 60);
```

### `void Render(SKCanvas canvas, double dt)`

Draws one frame onto the canvas: the visible part of the map (when set), then every NPC, then
the player on top. The camera follows the player and is clamped so the viewport stays inside the
map; the canvas size is read from the canvas clip bounds.

```csharp
using var bitmap = new SKBitmap(640, 480);
using (var canvas = new SKCanvas(bitmap))
{
    canvas.Clear(SKColors.Transparent);
    engine.Render(canvas, dt: 1.0 / 60);
}
```

### `void LoadSpriteSheet(string name, string path)`

Loads a full character spritesheet from a file path and registers it under `name`. Throws when
the name is already loaded, the image cannot be decoded, or its dimensions are not exactly
576×384.

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

## Full example ("hello world")

```csharp
var engine = new GameEngine();
engine.Map = TileMap.Load("assets/map.tmx");
engine.LoadSpriteSheet("hero", "assets/characters/character_full.png");
engine.Player.Position = new Position(6 * 48, 6 * 48);
engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

engine.LoadPartSpriteSheet("body", "assets/characters/character_part_body.png", CharacterPartType.Body);
var npc = new Character { Position = new Position(3 * 48, 4 * 48) };
npc.SpriteSheets.Add(new SpriteSheetRef("body", CharacterIndex: 2));
engine.Characters.Add(npc);

engine.Input(Key.D, isPressed: true);
engine.Update(1.0 / 60);
engine.Input(Key.D, isPressed: false);
```
