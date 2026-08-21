# rpg-engine

A 2D RPG engine written in C# on the latest .NET, using SkiaSharp for rendering and DotTiled
for Tiled map/tileset parsing. The engine is framework-agnostic (WPF, Blazor, Avalonia, …) and
targets WebAssembly with hardware-accelerated (GL) rendering.

## Repository layout

```
RPGEngine.sln
src/RPGEngine/          # The engine class library
src/RPGEngine/Sprites/  # RPG Maker MZ spritesheets (RPGEngine.Sprites namespace)
src/RPGEngine/Tiled/    # Tiled map/tileset types (RPGEngine.Tiled namespace)
samples/                # Sample hosts: Desktop (WPF) and WebAssembly (Blazor)
assets/                 # Committed fixture assets (map, tileset, character sheets)
tests/RPGEngine.Tests/  # xUnit test project (incl. DocsExamplesTests and SampleSceneTests)
docs/                   # Documentation: architecture + API reference with runnable examples
.github/workflows/ci.yml
```

## Documentation

- [docs/README.md](docs/README.md) — overview and "hello world" quick start.
- [docs/Architecture.md](docs/Architecture.md) — composition model and part-composition order.
- [docs/api/README.md](docs/api/README.md) — API reference, one page per public type.
- [assets/README.md](assets/README.md) — the committed fixture assets.

Every public class, property and method is XML-documented (CS1591 enforced) and mirrored in
`docs/api/` with commented, compilable examples (`DocsExamplesTests` runs them against the real
API).

A `TileMap` is `IDisposable` (it prerenders every visible tile layer into an `SKImage` on load
and releases them on dispose). The engine owns the assigned map: replacing `GameEngine.Map`
disposes the previous map, and disposing the engine disposes the current one.

## Samples

- **Desktop** (`samples/RPGEngine.Sample.Desktop`) — a minimal WPF host that renders the
  engine into a SkiaSharp surface, runs the game loop and forwards keyboard input. Run it on
  Windows with `dotnet run --project samples/RPGEngine.Sample.Desktop`.
- **WebAssembly** (`samples/RPGEngine.Sample.Wasm`) — a Blazor WebAssembly host rendering the
  same scene into a GPU-backed `SKGLView`. Publish with
  `dotnet publish samples/RPGEngine.Sample.Wasm -c Release -r browser-wasm` and serve the
  `publish/wwwroot` output.

The samples are **not** referenced by the engine library; they only reference it.

## Build & test

Requires the .NET 10 SDK. The WebAssembly sample also requires the `wasm-tools` workload
(for native GL SkiaSharp linking):

```bash
dotnet workload install wasm-tools
dotnet restore RPGEngine.sln
dotnet build RPGEngine.sln -c Release
dotnet test RPGEngine.sln -c Release --no-build
dotnet publish samples/RPGEngine.Sample.Wasm -c Release -r browser-wasm
```

The CI pipeline (`.github/workflows/ci.yml`) runs these same steps on every push and pull
request to `main`.
