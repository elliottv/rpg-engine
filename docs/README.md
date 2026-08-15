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

The following is the canonical end-to-end example. It creates a `GameEngine`, loads a tile map
and a full character spritesheet, adds an NPC built from part sheets, and drives one frame with
`Update`, `Render` and `Input`. The exact same scene is what the desktop and WebAssembly sample
hosts render, and the test `DocsExamplesTests.HelloWorld_EndToEnd` compiles and runs it against
the real API (acceptance criterion 6).

```csharp
using RPGEngine;
using RPGEngine.Sprites;
using RPGEngine.Tiled;
using SkiaSharp;

// 1. Create the engine. It starts with a fresh player, an empty NPC list, the default
//    WASD configuration and no map.
var engine = new GameEngine();

// 2. Load assets. The map owns its tilesets; characters reference sheets by name.
engine.Map = TileMap.Load("assets/map.tmx");
engine.LoadSpriteSheet("hero", "assets/characters/character_full.png");

// 3. Place the player and give it the "hero" sheet, character slot 1.
engine.Player.Position = new Position(6 * 48, 6 * 48);
engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

// 4. Add an NPC built from part sheets (body + face + hair1, character slot 2).
engine.LoadPartSpriteSheet("body", "assets/characters/character_part_body.png", CharacterPartType.Body);
engine.LoadPartSpriteSheet("face", "assets/characters/character_part_face.png", CharacterPartType.Face);
engine.LoadPartSpriteSheet("hair1", "assets/characters/character_part_hair1.png", CharacterPartType.Hair1);
var npc = new Character { Position = new Position(3 * 48, 4 * 48) };
npc.SpriteSheets.Add(new SpriteSheetRef("body", CharacterIndex: 2));
npc.SpriteSheets.Add(new SpriteSheetRef("face", CharacterIndex: 2));
npc.SpriteSheets.Add(new SpriteSheetRef("hair1", CharacterIndex: 2));
engine.Characters.Add(npc);

// 5. Drive the loop: forward input, update the simulation, render the frame.
engine.Input(Key.D, isPressed: true);
engine.Update(dt: 1.0 / 60);            // moves the player right for one frame
engine.Input(Key.D, isPressed: false);

using var bitmap = new SKBitmap(640, 480);
using (var canvas = new SKCanvas(bitmap))
{
    canvas.Clear(SKColors.Transparent);
    engine.Render(canvas, dt: 1.0 / 60);
}
```

### Reading order

| Page | What it covers |
| --- | --- |
| [Architecture](Architecture.md) | Composition model, camera, spritesheet layout, part ordering. |
| [api/GameEngine.md](api/GameEngine.md) | Root object: game loop, input, asset loading, camera. |
| [api/Character.md](api/Character.md) / [api/Player.md](api/Player.md) | In-world state and sprite references. |
| [api/SpriteSheet.md](api/SpriteSheet.md) | The 12×8 sheet layout (derived cell size, e.g. 576×384 or 936×864) and the **1..8 character index** semantics. |
| [api/TileMap.md](api/TileMap.md) / [api/TileSet.md](api/TileSet.md) | Tiled TMX/TSX loading and rendering. |
| [api/GameConfig.md](api/GameConfig.md) / [api/Key.md](api/Key.md) | Movement key bindings and host key translation. |
| [api/Position.md](api/Position.md) / [api/Direction.md](api/Direction.md) | Core primitives. |

## Building and running

See the repository [README](../README.md) for the build, test and publish commands (the
WebAssembly sample requires the `wasm-tools` workload).
