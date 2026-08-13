# SpriteSheetManager

Namespace: `RPGEngine.Sprites` — loads, validates and registers RPG Maker MZ spritesheets by
unique name.

## Remarks

- Every loaded sheet is decoded exactly once and kept alive for the lifetime of the sheet;
  `SpriteSheet.GetSprite` crops from that decoded image without re-decoding or re-encoding.
- The sheet kind is never guessed from the file name: `Load` overloads load *full* sheets;
  `LoadPart` overloads load *part* sheets of the given layer.
- Names are case-sensitive and trimmed on registration, lookup and duplicate checks. Loading a
  name that is already registered throws `InvalidOperationException`.

## Methods

### `SpriteSheet Load(string name, string path)`

Loads the full character spritesheet at `path` and registers it under `name`. Throws for a null
name/path, an empty trimmed name, a non-576×384 image, an undecodable image, or a duplicate
name.

```csharp
var manager = new SpriteSheetManager();
var sheet = manager.Load("hero", "assets/characters/character_full.png");
```

### `SpriteSheet Load(string name, Stream stream)`

Loads the full character spritesheet from a stream (file-system-free entry point). The caller
remains the owner of the stream.

```csharp
using var stream = File.OpenRead("assets/characters/character_full.png");
var sheet = manager.Load("hero", stream);
```

### `SpriteSheet LoadPart(string name, string path, CharacterPartType partType)`

Loads the part spritesheet of layer `partType` at `path` and registers it under `name`.

```csharp
var body = manager.LoadPart("body", "assets/characters/character_part_body.png", CharacterPartType.Body);
```

### `SpriteSheet LoadPart(string name, Stream stream, CharacterPartType partType)`

Loads the part spritesheet of layer `partType` from a stream. The caller remains the owner of
the stream.

### `SpriteSheet Get(string name)`

Returns the sheet registered under `name`. Throws `KeyNotFoundException` when no such sheet is
loaded.

```csharp
var sheet = manager.Get("hero");
```

### `bool Contains(string name)`

Returns whether a sheet is registered under `name` (case-sensitive, trimmed).

```csharp
Console.WriteLine(manager.Contains("hero")); // True
```

## Example

```csharp
var manager = new SpriteSheetManager();

// By path (desktop host).
var byPath = manager.Load("hero", "assets/characters/character_full.png");

// By stream (WebAssembly host).
using (var stream = File.OpenRead("assets/characters/character_part_body.png"))
{
    var part = manager.LoadPart("body", stream, CharacterPartType.Body);
    Console.WriteLine(part.Type);     // SpriteSheetType.Part
    Console.WriteLine(part.PartType); // CharacterPartType.Body
}

Console.WriteLine(manager.Contains("hero")); // True
Console.WriteLine(ReferenceEquals(byPath, manager.Get("hero"))); // True
```
