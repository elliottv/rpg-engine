# rpg-engine

A 2D RPG engine written in C# on the latest .NET, using SkiaSharp for rendering and DotTiled for Tiled map/tileset parsing.

## Repository layout

```
RPGEngine.sln
src/RPGEngine/          # The engine class library
src/RPGEngine/Tiled/    # Tiled map/tileset types (RPGEngine.Tiled namespace)
tests/RPGEngine.Tests/  # xUnit test project
.github/workflows/ci.yml
```

## Build & test

Requires the .NET 10 SDK.

```bash
dotnet restore RPGEngine.sln
dotnet build RPGEngine.sln -c Release
dotnet test RPGEngine.sln -c Release --no-build
```

The CI pipeline (`.github/workflows/ci.yml`) runs these same steps on every push and pull request to `main`.
