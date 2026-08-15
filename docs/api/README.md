# RPG Engine API reference

This reference mirrors the XML doc comments of the engine library. CS1591 (missing XML comment
on public member) is enforced as an error, so **every public class, property and method is
documented**. Every page below includes a commented, compilable example; the test project
(`DocsExamplesTests`) compiles and runs those examples against the real API.

> Note: the issue list for this story mentioned a `TileSetManager`. That type was removed during
> story 7: a `TileMap` loads and owns the tilesets its layers reference, so a separate global
> tileset manager is redundant. The pages below document the **actual** public API surface.

## Namespace `RPGEngine`

| Type | Kind | Page |
| --- | --- | --- |
| `GameEngine` | class (root object) | [GameEngine.md](GameEngine.md) |
| `GameConfig` | class | [GameConfig.md](GameConfig.md) |
| `Character` | class | [Character.md](Character.md) |
| `Player` | class | [Player.md](Player.md) |
| `Direction` | enum | [Direction.md](Direction.md) |
| `DirectionExtensions` | static class | [DirectionExtensions.md](DirectionExtensions.md) |
| `Position` | readonly record struct | [Position.md](Position.md) |
| `Vector2` | readonly record struct | [Vector2.md](Vector2.md) |
| `Key` | enum | [Key.md](Key.md) |

## Namespace `RPGEngine.Sprites`

| Type | Kind | Page |
| --- | --- | --- |
| `SpriteSheet` | class | [SpriteSheet.md](SpriteSheet.md) |
| `SpriteSheetRef` | readonly record struct | [SpriteSheetRef.md](SpriteSheetRef.md) |
| `SpriteSheetType` | enum | [SpriteSheetType.md](SpriteSheetType.md) |
| `CharacterPartType` | enum | [CharacterPartType.md](CharacterPartType.md) |
| `SpriteSheetManager` | class | [SpriteSheetManager.md](SpriteSheetManager.md) |

## Namespace `RPGEngine.Tiled`

| Type | Kind | Page |
| --- | --- | --- |
| `TileSet` | class | [TileSet.md](TileSet.md) |
| `TileMap` | class | [TileMap.md](TileMap.md) |
| `TileMapLayer` | class | [TileMapLayer.md](TileMapLayer.md) |
| `TileFlags` | enum (`[Flags]`) | [TileFlags.md](TileFlags.md) |
| `TiledAssetFetcher` | delegate | [TiledAssetFetcher.md](TiledAssetFetcher.md) |
| `TiledAssetFetcherAsync` | delegate (async) | [TiledAssetFetcherAsync.md](TiledAssetFetcherAsync.md) |

## The character index 1..8

The most important convention to understand before using the API: a `SpriteSheetRef(Name,
CharacterIndex)` (and `SpriteSheet.GetSprite(characterIndex, …)`) selects one of the **8
characters** in a sheet whose cells form the normative **12×8** grid (e.g. 576×384 with 48×48
cells, or 936×864 with 78×108 cells). Both full and part sheets use this layout. See
[SpriteSheet.md](SpriteSheet.md) and [SpriteSheetRef.md](SpriteSheetRef.md).
