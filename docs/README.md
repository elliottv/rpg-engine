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

// 3. Place the player and give it the "hero" sheet, character slot 1.
engine.Player.Position = new Position(6 * 48, 6 * 48);
engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

// 4. Add an NPC built from part sheets (body + face + hair1, character slot 2). The other part
//    sheets (face, hair1) are loaded the same way with LoadPartSpriteSheetAsync.
using var bodyStream = new MemoryStream(await http.GetByteArrayAsync("assets/characters/character_part_body.png"));
await engine.LoadPartSpriteSheetAsync("body", bodyStream, CharacterPartType.Body);
var npc = new Character { Position = new Position(3 * 48, 4 * 48) };
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
> pipeline.

### Reading order

| Page | What it covers |
| --- | --- |
| [Architecture](Architecture.md) | Composition model, camera, spritesheet layout, part ordering, rendering order and the Tiled read model. |
| [api/GameEngine.md](api/GameEngine.md) | Root object: game loop, input (8 directions), asset loading (sync + async), camera, black background / map centering. |
| [api/Character.md](api/Character.md) / [api/Player.md](api/Player.md) | In-world state, sprite references and the speed-scaled walk-cycle animation (`AnimationCycleSpeed`). |
| [api/SpriteSheet.md](api/SpriteSheet.md) | The 12×8 sheet layout (derived cell size, e.g. 576×384 or 936×864) and the **1..8 character index** semantics. |
| [api/SpriteSheetManager.md](api/SpriteSheetManager.md) | Loading full/part sheets by path or stream, including the async `LoadAsync`/`LoadPartAsync` overloads. |
| [api/TileMap.md](api/TileMap.md) / [api/TileSet.md](api/TileSet.md) | Tiled TMX/TSX loading (sync + async) and rendering; map custom properties, object layers and the `above_player` flag. |
| [api/TileMapLayer.md](api/TileMapLayer.md) | Tile-layer data and the `AbovePlayer` flag. |
| [api/MapProperty.md](api/MapProperty.md) / [api/MapPropertyType.md](api/MapPropertyType.md) | Typed map/layer/object custom properties. |
| [api/TileMapObject.md](api/TileMapObject.md) / [api/TileMapObjectShape.md](api/TileMapObjectShape.md) / [api/TileMapObjectLayer.md](api/TileMapObjectLayer.md) | The object-layer read model. |
| [api/TiledAssetFetcher.md](api/TiledAssetFetcher.md) / [api/TiledAssetFetcherAsync.md](api/TiledAssetFetcherAsync.md) | Resolving Tiled assets by URI (sync and async). |
| [api/GameConfig.md](api/GameConfig.md) / [api/Key.md](api/Key.md) | Movement key bindings and host key translation (including `GetMovementDirection`). |
| [api/Position.md](api/Position.md) / [api/Direction.md](api/Direction.md) / [api/DirectionExtensions.md](api/DirectionExtensions.md) | Core primitives and the 8-direction extensions. |

## Building and running

See the repository [README](../README.md) for the build, test and publish commands (the
WebAssembly sample requires the `wasm-tools` workload).
