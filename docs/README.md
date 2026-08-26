# RPG Engine documentation

This folder documents the RPG Engine: a 2D RPG engine written in C# on .NET 10, using SkiaSharp
for rendering and DotTiled for Tiled map/tileset parsing. The engine is framework-agnostic
(WPF, Blazor, Avalonia, …) and targets WebAssembly with hardware-accelerated (GL) rendering.

## Contents

- [Architecture](Architecture.md) — the composition model (Player→Character, engine→managers)
  and the part-composition ordering table.
- [API reference](api/README.md) — one page per public type, mirrored from the XML doc
  comments (CS1591 is enforced, so nothing public is undocumented). Every page includes a
  commented, compilable example.
- [Fixture assets](../assets/README.md) — the committed map, tileset and character sheets used
  by the samples and the end-to-end tests.

## Quick start ("hello world")

The following is the canonical end-to-end example. It creates a `GameEngine`, **asynchronously**
loads a tile map with a `TiledAssetFetcherAsync` and a full character spritesheet with
`LoadSpriteSheetAsync`, adds an NPC built from part sheets, drives one frame with
`Update`/`Render`/`Input` (including **8-direction diagonal input**), and reads the map's custom
**properties** and **object layers**. The exact same scene is what the desktop and WebAssembly
sample hosts render; the `DocsExamplesTests` methods `AsyncMapLoading_WithAsyncFetcher`,
`AsyncSheetLoading_LoadsAndRegisters`, `GameConfig_GetMovementDirection_CombinesHeldKeys` and
`TileMap_CommittedFixture_ReadsPropertiesAndObjectLayers` compile and run these snippets against
the real API (acceptance criterion 6).

```csharp
using RPGEngine;
using RPGEngine.Sprites;
using RPGEngine.Tiled;
using SkiaSharp;

// 1. Create the engine. It starts with a fresh player, an empty NPC list, the default
//    WASD configuration and no map.
var engine = new GameEngine();

// 2. Load assets asynchronously. The map owns its tilesets; a TiledAssetFetcherAsync resolves
//    the external .tsx and its image (e.g. HttpClient.GetByteArrayAsync in a browser host).
using var http = new HttpClient();
using var mapStream = new MemoryStream(await http.GetByteArrayAsync("assets/map.tmx"));
engine.Map = await TileMap.LoadAsync(
    mapStream,
    new Uri("https://example.com/assets/map.tmx"),
    uri => http.GetByteArrayAsync(uri));

using var heroStream = new MemoryStream(await http.GetByteArrayAsync("assets/characters/character_full.png"));
await engine.LoadSpriteSheetAsync("hero", heroStream);

// 3. Place the player (in tiles — the fixture map has 48 px tiles) and give it the "hero"
//    sheet, character slot 1. Position is the player's FEET (the middle-bottom of the sprite,
//    where it stands): the sprite is rendered above and centered on this point.
engine.Player.Position = new Position(6, 6);
engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

// 4. Add an NPC built from part sheets (body + face + hair1, character slot 2). The other part
//    sheets (face, hair1) are loaded the same way with LoadPartSpriteSheetAsync.
using var bodyStream = new MemoryStream(await http.GetByteArrayAsync("assets/characters/character_part_body.png"));
await engine.LoadPartSpriteSheetAsync("body", bodyStream, CharacterPartType.Body);
var npc = new Character { Position = new Position(3, 4) };
npc.SpriteSheets.Add(new SpriteSheetRef("body", CharacterIndex: 2));
engine.Characters.Add(npc);

// 5. Drive the loop with 8-direction input: holding W + D resolves to UpRight (opposite keys
//    cancel, diagonals move at the same speed as cardinal movement).
engine.Input(Key.W, isPressed: true);
engine.Input(Key.D, isPressed: true);
engine.Update(dt: 1.0 / 60);            // moves the player diagonally up-right
engine.Input(Key.W, isPressed: false);
engine.Input(Key.D, isPressed: false);

using var bitmap = new SKBitmap(640, 480);
using (var canvas = new SKCanvas(bitmap))
{
    canvas.Clear(SKColors.Black);
    engine.Render(canvas, dt: 1.0 / 60); // black background, centered map, above_player layers on top
}

// 6. Read the map's custom properties and object layers.
var difficulty = engine.Map?.GetProperty("difficulty");
Console.WriteLine(difficulty?.Value);   // e.g. 3 (an int map property)
foreach (var layer in engine.Map?.ObjectLayers ?? [])
{
    Console.WriteLine(layer.Name);      // e.g. "objects"
    foreach (var obj in layer.Objects)
    {
        Console.WriteLine($"{obj.Name} @ {obj.Position} ({obj.Shape})");
    }
}
```

> The synchronous, file-system based equivalents (`TileMap.Load(path)`, `LoadSpriteSheet(name,
> path)`) are what the desktop sample host uses; both loading styles exercise the same rendering
> pipeline. Before loading, hosts can check whether a name is already registered with
> `SpriteSheetExists(name)` — it covers both full and part sheets and is case-sensitive and
> trimmed.
>
> **Map ownership:** a `TileMap` is `IDisposable` — it prerenders every visible tile layer into
> an `SKImage` on load. The engine owns the assigned map: replacing `engine.Map` disposes the
> previous map, and disposing the engine (`using var engine = ...` or `engine.Dispose()`) disposes
> the current one.
>
> **Collision layers:** a tile layer declaring the Tiled `is_collision` boolean custom property
> set to `true` contains **solid** tiles that block the player (see `docs/Architecture.md`); the
> map edge is solid (characters cannot leave the map), and tiles drawn from non-collision layers
> never block.
>
> **Minimap:** `engine.RenderMinimap(canvas, zoomLevel)` draws the map's prerendered layers plus a
> green dot for the player and a yellow dot for each NPC onto a separate surface. `zoomLevel`
> `1.0` fits the whole map (the default); `> 1` zooms in around the player's dot (clamped to the
> map edges, like the main camera); `0 < zoomLevel < 1` zooms out further. When a map is set it
> clears its canvas to **black** first (like the main camera), so the unused margins are black
> (see `docs/api/GameEngine.md`).
>
> **Click-to-move (optional demo):** after at least one `Render` (so the engine knows the canvas
> size), the host can pass a mouse click to `engine.Click(surfaceX, surfaceY)` and the player
> **auto-walks** along an A* tile path to the clicked tile, stopping centered on it. Clicking a
> solid tile or an unreachable target cancels the walk without moving; a key press cancels it and
> a click mid-walk replaces the destination. `Player.OnMove` fires on every movement-state
> transition (start / stop / direction change) with the current facing direction. See
> `docs/api/GameEngine.md` and `docs/api/Player.md`.

### Reading order

| Page | What it covers |
| --- | --- |
| [Architecture](Architecture.md) | Composition model, camera, spritesheet layout, part ordering, rendering order and the Tiled read model. |
| [api/GameEngine.md](api/GameEngine.md) | Root object: game loop, input (8 directions), **click-to-move auto-walk (`Click`)** and the input-precedence rules, asset loading (sync + async) and `SpriteSheetExists`, camera, black background / map centering, map ownership (`IDisposable`), and the minimap (`RenderMinimap` — fit/zoom semantics, green player + yellow NPC dots). |
| [api/Character.md](api/Character.md) / [api/Player.md](api/Player.md) | In-world state, sprite references, the speed-scaled walk-cycle animation (`AnimationCycleSpeed`), autonomous movement (`StartMoving` / `StopMoving` / `IsMoving`), and the movement-state event (`OnMove` / `PlayerMoveEventArgs` / `Stop()`). |
| [api/SpriteSheet.md](api/SpriteSheet.md) | The 12×8 sheet layout (derived cell size, e.g. 576×384 or 936×864) and the **1..8 character index** semantics. |
| [api/SpriteSheetManager.md](api/SpriteSheetManager.md) | Loading full/part sheets by path or stream, including the async `LoadAsync`/`LoadPartAsync` overloads. |
| [api/TileMap.md](api/TileMap.md) / [api/TileSet.md](api/TileSet.md) | Tiled TMX/TSX loading (sync + async); prerendered layer images, viewport-culled image-blit rendering and `IDisposable`; map custom properties, object layers, the `above_player` flag and collision (`IsSolid`, the `is_collision` layer convention). |
| [api/TileMapLayer.md](api/TileMapLayer.md) | Tile-layer data and the `AbovePlayer` / `IsCollision` flags. |
| [api/MapProperty.md](api/MapProperty.md) / [api/MapPropertyType.md](api/MapPropertyType.md) | Typed map/layer/object custom properties. |
| [api/TileMapObject.md](api/TileMapObject.md) / [api/TileMapObjectShape.md](api/TileMapObjectShape.md) / [api/TileMapObjectLayer.md](api/TileMapObjectLayer.md) | The object-layer read model. |
| [api/TiledAssetFetcher.md](api/TiledAssetFetcher.md) / [api/TiledAssetFetcherAsync.md](api/TiledAssetFetcherAsync.md) | Resolving Tiled assets by URI (sync and async). |
| [api/GameConfig.md](api/GameConfig.md) / [api/Key.md](api/Key.md) | Movement key bindings and host key translation (including `GetMovementDirection`). |
| [api/Position.md](api/Position.md) / [api/Direction.md](api/Direction.md) / [api/DirectionExtensions.md](api/DirectionExtensions.md) | Core primitives and the 8-direction extensions. |

## Building and running

See the repository [README](../README.md) for the build, test and publish commands (the
WebAssembly sample requires the `wasm-tools` workload).
